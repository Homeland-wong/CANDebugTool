using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CANDebugTool.Models;
using CANDebugTool.Native;
using CANDebugTool.Services;

namespace CANDebugTool.ViewModels
{
    public partial class MainViewModel : ObservableObject, IDisposable
    {
        private readonly CanDeviceService _canService;
        private readonly DispatcherTimer _deviceScanTimer;
        private readonly StorageService _storageService;
        private string? _currentWorkPath;   // 当前工作区路径
        private string? _sessionSubFolder;   // 本次捕获子文件夹

        [ObservableProperty]
        private string _statusText = "请先指定工作区";

        [ObservableProperty]
        private bool _isDeviceConnected;

        [ObservableProperty]
        private bool _isCanRunning;

        [ObservableProperty]
        private int _txCount;

        [ObservableProperty]
        private int _rxCount;

        [ObservableProperty]
        private int _deviceIndex;

        [ObservableProperty]
        private int _canChannel;

        [ObservableProperty]
        private CanConfig _selectedBaudrate;

        [ObservableProperty]
        private DeviceInfo? _selectedDevice;

        [ObservableProperty]
        private bool _isAutoScan;

        [ObservableProperty]
        private bool _isScanning;

        /// <summary>处理耗时 (μs)</summary>
        [ObservableProperty]
        private long _processTimeUs;

        /// <summary>捕获状态指示</summary>
        [ObservableProperty]
        private bool _isCapturing;

        /// <summary>暂停接收</summary>
        [ObservableProperty]
        private bool _isPaused;

        public ObservableCollection<CanConfig> BaudrateList { get; } = new(CanConfig.GetPresetConfigs());
        public ObservableCollection<DeviceInfo> DeviceList { get; } = new();
        public ObservableCollection<CanMessage> ReceivedMessages { get; } = new();
        public ObservableCollection<CanMessage> SentMessages { get; } = new();

        private long _uiUpdateStopwatch;
        private readonly List<CanMessage> _pendingMessages = new();
        private const long UiUpdateIntervalUs = 200000; // 200ms 批量更新 UI
        private const int MaxDisplayMessages = 50;         // DataGrid 保留上限
        private int _totalRx;

        public SendViewModel SendVM { get; }
        public ReceiveViewModel ReceiveViewModel { get; }
        public StatisticsViewModel StatisticsVM { get; }
        public CurveViewModel CurveVM { get; }
        public DigitalViewModel DigitalVM { get; }
        public WorkspaceViewModel WorkspaceVM { get; }

        public MainViewModel()
        {
            _canService = CanDeviceService.Instance;
            _storageService = StorageService.Instance;
            _canService.OnStatusChanged += status => StatusText = status;
            _canService.OnMessageReceived += OnMessageReceived;
            _canService.OnDevicesChanged += OnDevicesChanged;

            SelectedBaudrate = BaudrateList.First(b => b.Name == "500Kbps");

            SendVM = new SendViewModel(this);
            ReceiveViewModel = new ReceiveViewModel(this);
            StatisticsVM = new StatisticsViewModel();
            CurveVM = new CurveViewModel();
            DigitalVM = new DigitalViewModel();
            WorkspaceVM = new WorkspaceViewModel();
            WorkspaceVM.OnWorkspaceChanged += OnWorkspaceChanged;

            // 同步掩码规则到曲线 VM
            CurveVM.SyncRules(StatisticsVM.Rules);
            StatisticsVM.Rules.CollectionChanged += (_, _) => CurveVM.SyncRules(StatisticsVM.Rules);

            _deviceScanTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
            _deviceScanTimer.Tick += (s, e) => ScanDevices();

            var defaultDevice = new DeviceInfo
            {
                DeviceIndex = 0, DeviceType = 21, Name = "USBCAN-2E-U #0"
            };
            DeviceList.Add(defaultDevice);
            SelectedDevice = defaultDevice;
            StatusText = "请先指定工作区";
        }

        private void OnWorkspaceChanged(string path)
        {
            _currentWorkPath = path;
            StatisticsVM.WorkspacePath = path;
            StatusText = $"工作区已就绪: {path}";
        }

        partial void OnIsAutoScanChanged(bool value) => UpdateAutoScan();

        private void UpdateAutoScan()
        {
            if (IsAutoScan && !IsDeviceConnected) _deviceScanTimer.Start();
            else _deviceScanTimer.Stop();
        }

        private void OnDevicesChanged(List<DeviceInfo> devices)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                var prev = SelectedDevice?.DeviceIndex;
                DeviceList.Clear();
                foreach (var d in devices) DeviceList.Add(d);
                if (prev.HasValue) SelectedDevice = DeviceList.FirstOrDefault(x => x.DeviceIndex == prev.Value);
                if (DeviceList.Count > 0 && SelectedDevice == null) SelectedDevice = DeviceList[0];
            });
        }

        private void OnMessageReceived(CanMessage msg)
        {
            if (IsPaused) return;

            StatisticsVM.Classify(msg);
            CurveVM.FeedData(msg);
            _storageService.WriteMessage(msg);

            _totalRx++;

            lock (_pendingMessages)
            {
                _pendingMessages.Add(msg);
            }

            long now = msg.TimestampUs > 0 ? msg.TimestampUs : DateTime.Now.Ticks / 10;
            if (now - _uiUpdateStopwatch < UiUpdateIntervalUs)
                return;
            _uiUpdateStopwatch = now;

            List<CanMessage> batch;
            lock (_pendingMessages)
            {
                if (_pendingMessages.Count == 0) return;
                batch = new List<CanMessage>(_pendingMessages);
                _pendingMessages.Clear();
            }

            // RxCount 立即更新，不靠 DataGrid 刷新
            RxCount += batch.Count;

            // 最低优先级：仅在空闲时刷新 DataGrid，不影响统计/曲线/按键
            System.Windows.Application.Current.Dispatcher.BeginInvoke(() =>
            {
                foreach (var m in batch)
                    ReceivedMessages.Add(m);

                int excess = ReceivedMessages.Count - MaxDisplayMessages;
                if (excess > 0)
                {
                    for (int i = 0; i < excess; i++)
                        ReceivedMessages.RemoveAt(0);
                }
            }, System.Windows.Threading.DispatcherPriority.ContextIdle);
        }

        [RelayCommand]
        private void ScanDevices()
        {
            if (IsScanning || IsDeviceConnected) return;
            IsScanning = true;
            try { _canService.ScanDevices(); } finally { IsScanning = false; }
        }

        [RelayCommand]
        private void Connect()
        {
            _deviceScanTimer.Stop();
            int dt = SelectedDevice?.DeviceType ?? 21;
            int idx = SelectedDevice?.DeviceIndex ?? 0;
            if (_canService.OpenDevice(dt, idx))
            {
                IsDeviceConnected = true; DeviceIndex = idx;
                if (_canService.InitCan(SelectedBaudrate, CanChannel))
                    StatusText = "连接成功，请点击启动";
                else { _canService.CloseDevice(); IsDeviceConnected = false; StatusText = "初始化失败"; }
                IsAutoScan = false;
            }
        }

        [RelayCommand]
        private void Disconnect()
        {
            _canService.CloseDevice();
            IsDeviceConnected = false; IsCanRunning = false; IsCapturing = false; IsAutoScan = true;
        }

        [RelayCommand]
        private void StartCan()
        {
            if (!IsDeviceConnected) { StatusText = "请先连接设备"; return; }
            if (string.IsNullOrEmpty(_currentWorkPath)) { StatusText = "请先指定工作区"; return; }

            // 创建工作区子文件夹: 工作区路径\CAN_Capture_20260512_185200
            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            _sessionSubFolder = Path.Combine(_currentWorkPath, $"CAN_Capture_{timestamp}");
            Directory.CreateDirectory(_sessionSubFolder);

            // 重置所有数据
            CanMessage._globalSequence = 0;
            ReceivedMessages.Clear();
            SentMessages.Clear();
            TxCount = 0;
            RxCount = 0;
            StatisticsVM.ClearAll();
            ClassificationService.Instance.ClearAll();
            CurveVM.ClearAll();
            DigitalVM.ClearAll();
            ReceiveViewModel.DisplayMessages.Clear();

            // 自动开始 CSV 存储
            string csvPath = Path.Combine(_sessionSubFolder, $"data.csv");
            _storageService.StartRecording(csvPath);

            if (_canService.StartCan(CanChannel))
            {
                IsCanRunning = true; IsCapturing = true;
                StatusText = $"正在捕获 → {_sessionSubFolder}";
            }
        }

        [RelayCommand]
        private void StopCan()
        {
            _canService.StopCan(CanChannel);
            IsCanRunning = false; IsCapturing = false;

            // 停止 CSV 存储
            _storageService.StopRecording();
            StatusText = "已停止";
        }

        public bool SendMessage(CanMessage msg)
        {
            msg.TimestampUs = DateTime.Now.Ticks / 10;
            _canService.Transmit(msg);
            _storageService.WriteMessage(msg);

            Application.Current.Dispatcher.Invoke(() =>
            {
                ReceivedMessages.Add(msg);
                TxCount++;
                while (ReceivedMessages.Count > 2000) ReceivedMessages.RemoveAt(0);
            });
            return true;
        }

        public void Dispose() => _deviceScanTimer.Stop();
    }
}
