using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CANDebugTool.Models;
using CANDebugTool.Services;

namespace CANDebugTool.ViewModels
{
    public partial class StatisticsViewModel : ObservableObject
    {
        private readonly ClassificationService _svc;

        /// <summary>归类统计组列表（按组号排序）</summary>
        public ObservableCollection<StatisticsGroup> Groups { get; } = new();

        /// <summary>归类掩码规则列表</summary>
        public ObservableCollection<ClassificationRule> Rules { get; } = new();

        [ObservableProperty]
        private ClassificationRule? _selectedRule;

        [ObservableProperty]
        private bool _isRulePanelVisible;

        [ObservableProperty]
        private string _statusText = "待启动";

        /// <summary>当前启用的规则列表（缓存用）</summary>
        private List<ClassificationRule> _activeRules = new();

        public StatisticsViewModel()
        {
            _svc = ClassificationService.Instance;
            _svc.OnGroupUpdated += OnGroupUpdated;
        }

        /// <summary>
        /// 对报文执行归类
        /// </summary>
        public void Classify(CanMessage msg)
        {
            _activeRules = Rules.Where(r => r.IsClassifyMode).ToList();
            if (_activeRules.Count == 0)
            {
                msg.ClassifyCodeHex = "00·00·00·00·00·00·00·00·00·00·00·00";
                msg.GroupId = -1;
                return;
            }

            _svc.Classify(msg, _activeRules);
        }

        /// <summary>
        /// 处理归类服务返回的更新事件
        /// </summary>
        private void OnGroupUpdated(StatisticsGroup updated)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                // 查找现有条目并更新
                var existing = Groups.FirstOrDefault(g => g.GroupId == updated.GroupId);
                if (existing != null)
                {
                    var idx = Groups.IndexOf(existing);
                    Groups[idx] = updated;
                }
                else
                {
                    // 插入并保持组号排序
                    int insertAt = 0;
                    for (int i = 0; i < Groups.Count; i++)
                    {
                        if (Groups[i].GroupId < updated.GroupId)
                            insertAt = i + 1;
                    }
                    Groups.Insert(insertAt, updated);
                }

                // 排序（按组号）
                var sorted = Groups.OrderBy(g => g.GroupId).ToList();
                for (int i = 0; i < sorted.Count; i++)
                {
                    if (Groups[i] != sorted[i])
                    {
                        Groups.Clear();
                        foreach (var g in sorted) Groups.Add(g);
                        break;
                    }
                }

                StatusText = $"{Groups.Count} 组 | 总计 {Groups.Sum(g => g.Count)} 帧";
            });
        }

        [RelayCommand]
        private void ToggleRulePanel()
        {
            IsRulePanelVisible = !IsRulePanelVisible;
        }

        [RelayCommand]
        private void AddRule()
        {
            var rule = new ClassificationRule
            {
                Id = Rules.Count > 0 ? Rules.Max(r => r.Id) + 1 : 0,
                Name = $"规则{Rules.Count + 1}"
            };
            Rules.Add(rule);
            SelectedRule = rule;
        }

        [RelayCommand]
        private void DeleteRule(ClassificationRule rule)
        {
            if (rule != null)
                Rules.Remove(rule);
        }

        [RelayCommand]
        private void ResetRule(ClassificationRule rule)
        {
            if (rule != null)
                _svc.ResetRuleGroups(rule.Name);
        }

        /// <summary>
        /// 清空统计
        /// </summary>
        public void ClearAll()
        {
            Groups.Clear();
            _svc.ClearAll();
            StatusText = "已清空";
        }
    }
}
