using CommunityToolkit.Mvvm.ComponentModel;

namespace CANDebugTool.Models
{
    /// <summary>
    /// 数字显示单元
    /// </summary>
    public partial class DigitalDisplay : ObservableObject
    {
        [ObservableProperty]
        private int _id;

        [ObservableProperty]
        private string _name = "";

        /// <summary>数据来源：统计组号</summary>
        [ObservableProperty]
        private int _sourceGroupId;

        /// <summary>数据来源类型: count, calcValue, dataDiff, timeDiff</summary>
        [ObservableProperty]
        private string _sourceType = "calcValue";

        /// <summary>当前值（实时刷新）</summary>
        [ObservableProperty]
        private string _value = "---";

        /// <summary>格式化字符串</summary>
        [ObservableProperty]
        private string _format = "F2";
    }
}
