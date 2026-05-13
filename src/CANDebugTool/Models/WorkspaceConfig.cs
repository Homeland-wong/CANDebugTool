using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;

namespace CANDebugTool.Models
{
    /// <summary>
    /// 工作区配置
    /// </summary>
    public partial class WorkspaceConfig : ObservableObject
    {
        [ObservableProperty]
        private string _workspacePath = "";

        [ObservableProperty]
        private string _name = "未命名工作区";

        /// <summary>归类掩码规则列表 (序列化保存)</summary>
        public List<ClassificationRule> ClassificationRules { get; set; } = new();

        /// <summary>曲线配置列表 (序列化保存)</summary>
        public List<CurveConfig> CurveConfigs { get; set; } = new();

        /// <summary>数字显示配置列表 (序列化保存)</summary>
        public List<DigitalDisplay> DigitalDisplays { get; set; } = new();

        /// <summary>计算值配置列表 (序列化保存)</summary>
        public List<CalcValueConfig> CalcValueConfigs { get; set; } = new();

        /// <summary>CSV存储路径</summary>
        public string StorePath => System.IO.Path.Combine(WorkspacePath, "data.csv");

        /// <summary>配置JSON路径</summary>
        public string ConfigPath => System.IO.Path.Combine(WorkspacePath, "config.json");
    }
}
