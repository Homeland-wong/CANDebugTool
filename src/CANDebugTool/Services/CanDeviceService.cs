using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using CANDebugTool.Models;
using CANDebugTool.Native;

namespace CANDebugTool.Services
{
    public class CanDeviceService : IDisposable
    {
        private static CanDeviceService? _instance;
        public static CanDeviceService Instance => _instance ??= new CanDeviceService();

        private CancellationTokenSource? _receiveCts;
        private Task? _receiveTask;

        public bool IsDeviceOpened { get; private set; }
        public bool IsCanStarted { get; private set; }
        public int DeviceIndex { get; private set; }
        public int DeviceTypeId { get; private set; }
        public int CanChannel { get; private set; } = 0;
        public List<DeviceInfo> Devices { get; private set; } = new();

        public event Action<CanMessage>? OnMessageReceived;
        public event Action<string>? OnStatusChanged;
        public event Action<List<DeviceInfo>>? OnDevicesChanged;

        private CanDeviceService() { }

        #region 设备扫描

        public List<DeviceInfo> ScanDevices()
        {
            var devices = new List<DeviceInfo>();
            LogService.LogScanStart();
            devices.AddRange(ScanByOpenDevice());

            LogService.LogScanEnd(devices.Count);
            Devices = devices;
            OnDevicesChanged?.Invoke(devices);

            OnStatusChanged?.Invoke(devices.Count > 0
                ? $"检测到 {devices.Count} 个设备"
                : "未检测到 USB-CAN 设备");

            return devices;
        }

        private static int MatchDeviceType(string hwType)
        {
            string upper = hwType.ToUpper();
            if (upper.Contains("USBCAN-II") || upper.Contains("USBCAN2")) return 4;
            if (upper.Contains("USBCAN-2E-U") || upper.Contains("USBCAN2E")) return 21;
            if (upper.Contains("USBCAN")) return 3;
            return 4;
        }

        private List<DeviceInfo> ScanByOpenDevice()
        {
            var devices = new List<DeviceInfo>();
            var foundSerials = new HashSet<string>();
            int[] deviceTypes = { 4, 21, 3 };

            foreach (int dt in deviceTypes)
            {
                string typeName = Native.DeviceType.GetName(dt);
                LogService.LogScanningType(dt, typeName);

                for (int i = 0; i < 5; i++)
                {
                    try
                    {
                        if (ControlCanApi.VCI_OpenDevice(dt, i, 0) == 1)
                        {
                            string serial = "Unknown", hw = null;
                            var bi = new VCI_BOARD_INFO
                            {
                                str_Serial_Num = new byte[20],
                                str_hw_Type = new byte[40],
                                Reserved = new ushort[4]
                            };
                            int infoResult = ControlCanApi.VCI_ReadBoardInfo(dt, i, ref bi);
                            if (infoResult == 1)
                            {
                                serial = BytesToString(bi.str_Serial_Num);
                                hw = BytesToString(bi.str_hw_Type);
                            }

                            if (foundSerials.Contains(serial))
                            {
                                ControlCanApi.VCI_CloseDevice(dt, i);
                                continue;
                            }
                            foundSerials.Add(serial);

                            string name = !string.IsNullOrWhiteSpace(hw) ? hw : typeName;
                            var device = new DeviceInfo
                            {
                                DeviceIndex = i,
                                DeviceType = dt,
                                Name = name,
                                SerialNumber = serial
                            };

                            devices.Add(device);
                            OnStatusChanged?.Invoke($"发现设备: {device.Name}");
                            ControlCanApi.VCI_CloseDevice(dt, i);
                        }
                    }
                    catch (Exception ex)
                    {
                        LogService.Log($"扫描 {typeName} #{i} 异常: {ex.Message}", LogLevel.Warning);
                    }
                }
            }
            return devices;
        }

        private static string BytesToString(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0) return "Unknown";
            int len = 0;
            while (len < bytes.Length && bytes[len] != 0) len++;
            return System.Text.Encoding.ASCII.GetString(bytes, 0, len);
        }

        #endregion

        #region 设备操作

        public bool OpenDevice(int deviceType, int deviceIndex = 0)
        {
            string typeName = Native.DeviceType.GetName(deviceType);
            LogService.LogConnectStart(deviceType, typeName, deviceIndex);
            OnStatusChanged?.Invoke($"正在打开 {typeName} #{deviceIndex}...");

            try
            {
                int result = ControlCanApi.VCI_OpenDevice(deviceType, deviceIndex, 0);
                LogService.Log($"VCI_OpenDevice 返回: {result}", LogLevel.Info);

                if (result == 1)
                {
                    IsDeviceOpened = true;
                    DeviceTypeId = deviceType;
                    DeviceIndex = deviceIndex;
                    OnStatusChanged?.Invoke($"设备 {typeName} #{deviceIndex} 已打开");
                    return true;
                }

                LogService.Log($"打开设备失败，错误码: {result}", LogLevel.Warning);
                OnStatusChanged?.Invoke($"打开设备失败，错误码: {result}");
                return false;
            }
            catch (Exception ex)
            {
                LogService.LogException(ex, "VCI_OpenDevice");
                OnStatusChanged?.Invoke($"打开设备异常: {ex.Message}");
                return false;
            }
        }

        public bool CloseDevice()
        {
            try
            {
                StopReceive();
                ControlCanApi.VCI_CloseDevice(DeviceTypeId, DeviceIndex);
                IsDeviceOpened = false;
                IsCanStarted = false;
                OnStatusChanged?.Invoke("设备已关闭");
                return true;
            }
            catch (Exception ex)
            {
                LogService.LogException(ex, "VCI_CloseDevice");
                return false;
            }
        }

        #endregion

        #region CAN 通道操作

        public bool InitCan(CanConfig config, int canChannel = 0)
        {
            if (!IsDeviceOpened) return false;

            try
            {
                var ic = config.ToVciConfig();
                LogService.LogInitConfig(config.Name, ic.Timing0, ic.Timing1, canChannel);

                int result = ControlCanApi.VCI_InitCAN(DeviceTypeId, DeviceIndex, canChannel, ref ic);
                LogService.Log($"VCI_InitCAN 返回: {result}", LogLevel.Info);

                if (result == 1)
                {
                    CanChannel = canChannel;
                    OnStatusChanged?.Invoke($"CAN{canChannel} 初始化完成，{config.Name}");
                    return true;
                }

                LogService.Log($"初始化 CAN{canChannel} 失败，返回值: {result}", LogLevel.Error);
                OnStatusChanged?.Invoke($"初始化 CAN{canChannel} 失败");
                return false;
            }
            catch (Exception ex)
            {
                LogService.LogException(ex, "VCI_InitCAN");
                OnStatusChanged?.Invoke($"初始化异常: {ex.Message}");
                return false;
            }
        }

        public bool StartCan(int canChannel = 0)
        {
            if (!IsDeviceOpened) return false;

            try
            {
                int result = ControlCanApi.VCI_StartCAN(DeviceTypeId, DeviceIndex, canChannel);
                LogService.Log($"VCI_StartCAN 返回: {result}", LogLevel.Info);

                if (result == 1)
                {
                    IsCanStarted = true;
                    CanChannel = canChannel;
                    StartReceive();
                    OnStatusChanged?.Invoke($"CAN{canChannel} 已启动");
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                LogService.LogException(ex, "VCI_StartCAN");
                return false;
            }
        }

        public bool StopCan(int canChannel = 0)
        {
            LogService.Log($"StopCan called, channel={canChannel}", LogLevel.Info);
            StopReceive();
            IsCanStarted = false;
            OnStatusChanged?.Invoke($"CAN{canChannel} 已停止");
            return true;
        }

        #endregion

        #region 数据收发

        public bool Transmit(CanMessage message)
        {
            if (!IsDeviceOpened) { LogService.Log("Transmit: 设备未打开", LogLevel.Warning); return false; }
            if (!IsCanStarted) { LogService.Log("Transmit: CAN 未启动", LogLevel.Warning); return false; }

            try
            {
                // 确保 Data 数组固定为 8 字节（DLL 要求）
                byte[] paddedData = new byte[8];
                int copyLen = Math.Min(message.Data?.Length ?? 0, 8);
                if (message.Data != null)
                    Array.Copy(message.Data, paddedData, copyLen);

                var obj = new VCI_CAN_OBJ
                {
                    ID = message.Id,
                    SendType = 0,
                    RemoteFlag = (byte)(message.IsRemote ? 1 : 0),
                    ExternFlag = (byte)(message.IsExtended ? 1 : 0),
                    DataLen = message.DataLen,
                    Data = paddedData
                };

                uint result = ControlCanApi.VCI_Transmit(DeviceTypeId, DeviceIndex, CanChannel, ref obj, 1);
                LogService.Log($"VCI_Transmit ID=0x{message.Id:X} 返回: {result}", LogLevel.Info);
                return result == 1;
            }
            catch (Exception ex) { LogService.Log($"Transmit异常: {ex.Message}", LogLevel.Error); return false; }
        }

        private void StartReceive()
        {
            if (_receiveTask != null && !_receiveTask.IsCompleted)
            {
                LogService.Log("Receive task already running", LogLevel.Warning);
                return;
            }
            _receiveCts = new CancellationTokenSource();
            _receiveTask = Task.Run(() => ReceiveLoop(_receiveCts.Token));
            LogService.Log("ReceiveLoop 已启动", LogLevel.Info);
        }

        private void StopReceive()
        {
            _receiveCts?.Cancel();
            try { _receiveTask?.Wait(100); } catch { }
            _receiveCts?.Dispose();
            _receiveCts = null;
            _receiveTask = null;
        }

        private void ReceiveLoop(CancellationToken token)
        {
            var obj = new VCI_CAN_OBJ { Data = new byte[8] };
            int receivedCount = 0;
            long startTicks = DateTime.Now.Ticks;

            LogService.Log("ReceiveLoop 开始运行", LogLevel.Info);

            while (!token.IsCancellationRequested)
            {
                try
                {
                    uint count = ControlCanApi.VCI_Receive(DeviceTypeId, DeviceIndex, CanChannel, ref obj, 1, 100);
                    if (count > 0)
                    {
                        var msg = CanMessage.FromVciObj(obj, false);
                        OnMessageReceived?.Invoke(msg);
                        receivedCount++;
                        if (receivedCount == 1 || receivedCount % 100 == 0)
                            LogService.Log($"已收到 {receivedCount} 帧", LogLevel.Info);
                    }
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex) { LogService.Log($"Receive异常: {ex.Message}", LogLevel.Warning); Thread.Sleep(10); }
            }
            LogService.Log($"ReceiveLoop 结束，共收到 {receivedCount} 帧", LogLevel.Info);
        }

        #endregion

        #region 状态查询

        public int GetReceiveCount(int canChannel = 0)
        {
            if (!IsDeviceOpened) return 0;
            return ControlCanApi.VCI_GetReceiveNum(DeviceTypeId, DeviceIndex, canChannel);
        }

        public bool ClearBuffer(int canChannel = 0)
        {
            if (!IsDeviceOpened) return false;
            return ControlCanApi.VCI_ClearBuffer(DeviceTypeId, DeviceIndex, canChannel) == 1;
        }

        #endregion

        public void Dispose()
        {
            CloseDevice();
            _instance = null;
        }
    }
}
