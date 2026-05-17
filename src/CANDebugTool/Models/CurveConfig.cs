using CommunityToolkit.Mvvm.ComponentModel;

namespace CANDebugTool.Models
{
    /// <summary>
    /// 曲线配置
    /// </summary>
    public partial class CurveConfig : ObservableObject
    {
        [ObservableProperty]
        private int _id;

        [ObservableProperty]
        private string _name = $"曲线1";

        [ObservableProperty]
        private bool _enabled;

        /// <summary>数据来源：统计组号</summary>
        [ObservableProperty]
        private int _sourceGroupId;

        /// <summary>关注值配置索引（-1=使用主关注值）</summary>
        [ObservableProperty]
        private int _sourceCalcIndex = -1;

        /// <summary>数据来源类型: count, calcValue, dataDiff, timeDiff</summary>
        [ObservableProperty]
        private string _sourceType = "calcValue";

        [ObservableProperty]
        private double _upperLimit = 100;

        [ObservableProperty]
        private double _lowerLimit;

        /// <summary>曲线颜色 (ARGB)</summary>
        [ObservableProperty]
        private string _color = "#0078D4";

        /// <summary>显示宽度 (点数，即多少条报文组成曲线)</summary>
        [ObservableProperty]
        private int _displayWidthPoints = 1000;

        /// <summary>数据点列表 (tick, value)</summary>
        public System.Collections.Concurrent.ConcurrentQueue<(long Tick, double Value)> DataPoints { get; } = new();

        /// <summary>最大缓存点数</summary>
        public int MaxCachePoints => DisplayWidthPoints + 200;
    }
}
