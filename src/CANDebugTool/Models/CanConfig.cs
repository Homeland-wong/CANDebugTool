using System;
using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;

namespace CANDebugTool.Models
{
    /// <summary>
    /// CAN 配置模型
    /// </summary>
    public partial class CanConfig : ObservableObject
    {
        [ObservableProperty]
        private string _name = "500Kbps";

        [ObservableProperty]
        private uint _baudrate = 500000;

        [ObservableProperty]
        private byte _btr0 = 0x00;

        [ObservableProperty]
        private byte _btr1 = 0x1C;

        [ObservableProperty]
        private bool _listenOnlyMode = false;

        [ObservableProperty]
        private bool _selfTestMode = false;

        [ObservableProperty]
        private uint _acceptCode = 0x00000000;

        [ObservableProperty]
        private uint _acceptMask = 0xFFFFFFFF;

        /// <summary>
        /// 转换为 VCI_INIT_CONFIG
        /// </summary>
        public Native.VCI_INIT_CONFIG ToVciConfig()
        {
            // 计算波特率寄存器值
            Native.ControlCanApi.CalculateBaudrate(Baudrate, out byte btr0, out byte btr1);

            byte mode = 0x00;
            if (ListenOnlyMode) mode |= 0x01;  // 只听模式
            if (SelfTestMode) mode |= 0x02;    // 自测试模式

            return new Native.VCI_INIT_CONFIG
            {
                AccCode = AcceptCode,
                AccMask = AcceptMask,
                Timing0 = btr0,
                Timing1 = btr1,
                Mode = mode
            };
        }

        /// <summary>
        /// 预定义的常用波特率
        /// </summary>
        public static List<CanConfig> GetPresetConfigs()
        {
            return new List<CanConfig>
            {
                new() { Name = "10Kbps", Baudrate = 10000 },
                new() { Name = "20Kbps", Baudrate = 20000 },
                new() { Name = "50Kbps", Baudrate = 50000 },
                new() { Name = "100Kbps", Baudrate = 100000 },
                new() { Name = "125Kbps", Baudrate = 125000 },
                new() { Name = "250Kbps", Baudrate = 250000 },
                new() { Name = "500Kbps", Baudrate = 500000 },
                new() { Name = "800Kbps", Baudrate = 800000 },
                new() { Name = "1Mbps", Baudrate = 1000000 }
            };
        }
    }
}
