using CommunityToolkit.Mvvm.ComponentModel;

namespace CANDebugTool.Models
{
    /// <summary>
    /// 设备信息模型
    /// </summary>
    public partial class DeviceInfo : ObservableObject
    {
        [ObservableProperty]
        private int _deviceType = 3;  // USB-CAN

        [ObservableProperty]
        private int _deviceIndex;

        [ObservableProperty]
        private int _canChannel;

        [ObservableProperty]
        private string _name = "";

        [ObservableProperty]
        private string _serialNumber = "";

        [ObservableProperty]
        private bool _isConnected;

        [ObservableProperty]
        private bool _isRunning;

        [ObservableProperty]
        private string _statusText = "未连接";

        /// <summary>
        /// 显示名称
        /// </summary>
        public string DisplayName => $"USB-CAN #{DeviceIndex} (CH{CanChannel})";


    }
}
