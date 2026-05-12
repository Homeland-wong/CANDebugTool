using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CANDebugTool.Models;

namespace CANDebugTool.ViewModels
{
    public partial class StatisticsViewModel : ObservableObject
    {
        /// <summary>归类统计组列表</summary>
        public ObservableCollection<StatisticsGroup> Groups { get; } = new();

        /// <summary>归类掩码规则列表</summary>
        public ObservableCollection<ClassificationRule> Rules { get; } = new();

        [ObservableProperty]
        private ClassificationRule? _selectedRule;

        [ObservableProperty]
        private bool _isRulePanelVisible;

        [ObservableProperty]
        private string _statusText = "待启动";

        [RelayCommand]
        private void ToggleRulePanel()
        {
            IsRulePanelVisible = !IsRulePanelVisible;
        }

        [RelayCommand]
        private void AddRule()
        {
            Rules.Add(new ClassificationRule
            {
                Id = Rules.Count,
                Name = $"规则{Rules.Count + 1}"
            });
        }

        [RelayCommand]
        private void DeleteRule()
        {
            if (SelectedRule != null)
                Rules.Remove(SelectedRule);
        }

        /// <summary>
        /// 对报文执行归类（占位，后续实现）
        /// </summary>
        public void Classify(CanMessage msg)
        {
            // TODO: 应用掩码规则计算归类码
            // 更新统计组计数
        }
    }
}
