using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace CANDebugTool.Native
{
    /// <summary>
    /// ControlCAN.dll API 封装
    /// 基于周立功 USB-CAN 接口函数库
    /// </summary>
    public static class ControlCanApi
    {
        private const string DllName = "ControlCAN.dll";

        #region 设备操作

        /// <summary>
        /// 打开设备
        /// </summary>
        [DllImport(DllName, CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern int VCI_OpenDevice(int DeviceType, int DeviceInd, int Reserved);

        /// <summary>
        /// 关闭设备
        /// </summary>
        [DllImport(DllName, CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern int VCI_CloseDevice(int DeviceType, int DeviceInd);

        /// <summary>
        /// 获取设备信息
        /// </summary>
        [DllImport(DllName, CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall, EntryPoint = "VCI_ReadBoardInfo")]
        public static extern int VCI_ReadBoardInfo(int DeviceType, int DeviceInd, ref VCI_BOARD_INFO pInfo);

        #endregion

        #region CAN 通道操作

        /// <summary>
        /// 初始化 CAN 通道
        /// </summary>
        [DllImport(DllName, CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern int VCI_InitCAN(int DeviceType, int DeviceInd, int CANInd, ref VCI_INIT_CONFIG pInitConfig);

        /// <summary>
        /// 启动 CAN 通道
        /// </summary>
        [DllImport(DllName, CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern int VCI_StartCAN(int DeviceType, int DeviceInd, int CANInd);

        /// <summary>
        /// 重置 CAN 通道
        /// </summary>
        [DllImport(DllName, CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern int VCI_ResetCAN(int DeviceType, int DeviceInd, int CANInd);

        #endregion

        #region 数据收发

        /// <summary>
        /// 发送 CAN 报文
        /// </summary>
        [DllImport(DllName, CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern uint VCI_Transmit(int DeviceType, int DeviceInd, int CANInd, ref VCI_CAN_OBJ pSend, uint Len);

        /// <summary>
        /// 接收 CAN 报文
        /// WaitTime=-1 表示阻塞等待
        /// </summary>
        [DllImport(DllName, CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern uint VCI_Receive(int DeviceType, int DeviceInd, int CANInd, ref VCI_CAN_OBJ pReceive, uint Len, int WaitTime);

        #endregion

        #region 其他操作

        /// <summary>
        /// 获取接收缓冲区中帧数量
        /// </summary>
        [DllImport(DllName, CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern int VCI_GetReceiveNum(int DeviceType, int DeviceInd, int CANInd);

        /// <summary>
        /// 清空接收缓冲区
        /// </summary>
        [DllImport(DllName, CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern int VCI_ClearBuffer(int DeviceType, int DeviceInd, int CANInd);

        #endregion

        #region 波特率计算

        /// <summary>
        /// 波特率计算
        /// SJA1000 16MHz, 根据周立功文档标准值
        /// </summary>
        public static void CalculateBaudrate(uint baudrate, out byte Timing0, out byte Timing1)
        {
            var presetBaudrates = new Dictionary<uint, (byte T0, byte T1)>
            {
                { 10000,  (0x31, 0x1C) },
                { 20000,  (0x18, 0x1C) },
                { 50000,  (0x09, 0x1C) },
                { 100000, (0x04, 0x1C) },
                { 125000, (0x03, 0x1C) },
                { 250000, (0x01, 0x1C) },
                { 500000, (0x00, 0x1C) },
                { 800000, (0x00, 0x16) },
                { 1000000,(0x00, 0x14) }
            };

            if (presetBaudrates.TryGetValue(baudrate, out var preset))
            {
                Timing0 = preset.T0;
                Timing1 = preset.T1;
            }
            else
            {
                // 默认 500Kbps
                Timing0 = 0x00;
                Timing1 = 0x1C;
            }
        }

        #endregion
    }

    #region 常量定义

    /// <summary>
    /// 设备类型定义
    /// </summary>
    public static class DeviceType
    {
        public const int VCI_PCI5121 = 1;
        public const int VCI_PCI9810 = 2;
        public const int VCI_USBCAN = 3;
        public const int VCI_PCI9820 = 4;
        public const int VCI_CANET = 5;
        public const int VCI_DNP9810 = 6;
        public const int VCI_PCI9920 = 7;
        public const int VCI_PCI9950 = 8;
        public const int VCI_PCAN = 9;
        public const int VCI_USBCAN_2 = 20;  // USB-CAN-II
        public const int VCI_USBCAN_E = 21;   // USB-CAN-E
        public const int VCI_USBCAN_2E_U = 22; // USB-CAN-2E-U
        public const int VCI_CANDT = 32;       // CANdian
        public const int VCI_CANDTU = 33;       // CANdian-U

        /// <summary>
        /// 获取设备类型名称
        /// </summary>
        public static string GetName(int deviceType)
        {
            return deviceType switch
            {
                VCI_USBCAN => "USB-CAN",
                VCI_USBCAN_2 => "USBCAN2",
                VCI_USBCAN_E => "USB-CAN-E",
                VCI_USBCAN_2E_U => "USBCAN-2E-U",
                VCI_CANDT => "CANdian",
                VCI_CANDTU => "CANdian-U",
                VCI_PCI5121 => "PCI-5121",
                VCI_PCI9810 => "PCI-9810",
                VCI_PCI9820 => "PCI-9820",
                VCI_PCI9920 => "PCI-9920",
                VCI_PCI9950 => "PCI-9950",
                VCI_PCAN => "PCAN",
                _ => $"Type-{deviceType}"
            };
        }
    }

    #endregion
}
