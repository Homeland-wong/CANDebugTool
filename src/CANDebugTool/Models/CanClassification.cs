using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;

namespace CANDebugTool.Models
{
    /// <summary>
    /// 归类掩码规则
    /// ID段4字节 + Data段8字节 = 12字节掩码
    /// </summary>
    public partial class ClassificationRule : ObservableObject
    {
        [ObservableProperty]
        private int _id = -1;

        [ObservableProperty]
        private string _name = "";

        /// <summary>ID段掩码 (4字节)</summary>
        [ObservableProperty]
        private byte[] _idMask = new byte[4] { 0xFF, 0xFF, 0xFF, 0xFF };

        /// <summary>Data段掩码 (8字节)</summary>
        [ObservableProperty]
        private byte[] _dataMask = new byte[8] { 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF };

        /// <summary>是否启用此规则</summary>
        [ObservableProperty]
        private bool _enabled = true;
    }

    /// <summary>
    /// 归类码 (12字节: ID 4 + Data 8)
    /// </summary>
    public partial class ClassifyCode : ObservableObject
    {
        [ObservableProperty]
        private byte[] _code = new byte[12];

        public string DisplayHex
        {
            get
            {
                var sb = new System.Text.StringBuilder();
                foreach (var b in Code)
                    sb.Append($"{b:X2} ");
                return sb.ToString().TrimEnd();
            }
        }

        public override bool Equals(object? obj)
        {
            if (obj is ClassifyCode other)
            {
                for (int i = 0; i < 12; i++)
                    if (Code[i] != other.Code[i]) return false;
                return true;
            }
            return false;
        }

        public override int GetHashCode()
        {
            int hash = 17;
            for (int i = 0; i < 12; i++)
                hash = hash * 31 + Code[i];
            return hash;
        }
    }

    /// <summary>
    /// 归类统计组
    /// </summary>
    public partial class StatisticsGroup : ObservableObject
    {
        [ObservableProperty]
        private int _groupId;

        [ObservableProperty]
        private string _classifyCode = "";

        [ObservableProperty]
        private long _count;

        /// <summary>计算规则: 0=无, 1=16位有符号, 2=16位无符号, 3=32位有符号, 4=32位无符号, 5=浮点数</summary>
        [ObservableProperty]
        private int _calcRule;

        /// <summary>计算值</summary>
        [ObservableProperty]
        private double _calcValue;

        /// <summary>前一条报文的计算值（用于差值）</summary>
        public double PreviousCalcValue;

        /// <summary>前一条报文的时间戳（用于时间差）</summary>
        public long PreviousTimestampUs;

        /// <summary>时间戳差值</summary>
        [ObservableProperty]
        private long _timeDiff;

        /// <summary>数据差值</summary>
        [ObservableProperty]
        private double _dataDiff;
    }

    /// <summary>
    /// 计算规则配置（绑定到 Data 字节位置）
    /// </summary>
    public partial class CalcRuleConfig : ObservableObject
    {
        /// <summary>起始字节号 (0-11, ID从0-3, Data从4-11)</summary>
        [ObservableProperty]
        private int _startByte;

        /// <summary>字节长度 (1, 2, 4)</summary>
        [ObservableProperty]
        private int _byteLength = 2;

        /// <summary>数据类型: int16, uint16, int32, uint32, float32</summary>
        [ObservableProperty]
        private string _dataType = "uint16";

        /// <summary>是否计算差值</summary>
        [ObservableProperty]
        private bool _enableDiff;
    }
}
