using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
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

        public ObservableCollection<StatisticsGroup> Groups { get; } = new();
        public ObservableCollection<ClassificationRule> Rules { get; } = new();

        [ObservableProperty]
        private ClassificationRule? _selectedRule;

        [ObservableProperty]
        private bool _isRulePanelVisible;

        [ObservableProperty]
        private string _statusText = "待启动";

        private List<ClassificationRule> _activeRules = new();
        private bool _activeRulesDirty = true;

        // 批量刷新：收集最新 group 状态，定时投递到 UI
        private readonly ConcurrentDictionary<int, StatisticsGroup> _pendingGroupUpdates = new();
        private long _groupUpdateStopwatch;
        private const long GroupUpdateIntervalTicks = 100 * 10000L; // 100ms

        public StatisticsViewModel()
        {
            _svc = ClassificationService.Instance;
            _svc.OnGroupUpdated += OnGroupUpdated;

            // 定时兜底刷盘：确保统计组即使在消息低速/停止时也能刷新
            var flushTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(500)
            };
            flushTimer.Tick += (s, e) => FlushGroupUpdates();
            flushTimer.Start();

            // 监听到规则变更时标记缓存失效
            Rules.CollectionChanged += (s, e) =>
            {
                _activeRulesDirty = true;
                if (e.NewItems != null)
                    foreach (ClassificationRule r in e.NewItems)
                        r.PropertyChanged += (_, _) => _activeRulesDirty = true;
            };
        }

        public void Classify(CanMessage msg)
        {
            if (_activeRulesDirty)
            {
                _activeRules = Rules.Where(r => r.IsClassifyMode).ToList();
                _activeRulesDirty = false;
            }
            if (_activeRules.Count == 0)
            {
                msg.ClassifyCodeHex = "FF·FF·FF·FF·FF·FF·FF·FF·FF·FF·FF·FF";
                msg.GroupId = -1;
                return;
            }

            _svc.Classify(msg, _activeRules);
        }

        /// <summary>标记规则缓存失效（IsClassifyMode 切换时调用）</summary>
        public void InvalidateRuleCache() => _activeRulesDirty = true;

        /// <summary>
        /// 接收线程回调 — 只更新缓存，不直接操作 UI
        /// </summary>
        private void OnGroupUpdated(StatisticsGroup updated)
        {
            // 覆盖式存储最新状态
            _pendingGroupUpdates[updated.GroupId] = updated;

            // 每 50ms 批量刷新到 UI
            long now = Stopwatch.GetTimestamp();
            if (now - _groupUpdateStopwatch < GroupUpdateIntervalTicks)
                return;
            _groupUpdateStopwatch = now;

            FlushGroupUpdates();
        }

        private void FlushGroupUpdates()
        {
            if (_pendingGroupUpdates.IsEmpty) return;

            // 提取快照
            var snapshot = _pendingGroupUpdates.Values.ToList();
            _pendingGroupUpdates.Clear();

            int count = snapshot.Count;
            Application.Current.Dispatcher.BeginInvoke(() =>
            {
                foreach (var updated in snapshot)
                {
                    var existing = Groups.FirstOrDefault(g => g.GroupId == updated.GroupId);
                    if (existing != null)
                    {
                        var idx = Groups.IndexOf(existing);
                        Groups[idx] = updated;
                    }
                    else
                    {
                        int insertAt = 0;
                        for (int i = 0; i < Groups.Count; i++)
                            if (Groups[i].GroupId < updated.GroupId) insertAt = i + 1;
                        Groups.Insert(insertAt, updated);
                    }
                }

                var sorted = Groups.OrderBy(g => g.GroupId).ToList();
                for (int i = 0; i < sorted.Count; i++)
                    if (Groups[i] != sorted[i])
                    { Groups.Clear(); foreach (var g in sorted) Groups.Add(g); break; }

                StatusText = $"{Groups.Count} 组 | 总计 {Groups.Sum(g => g.Count)} 帧";
            }); // Normal 优先级，优先于报文监控
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

        [RelayCommand]
        private void AddCalcConfig(ClassificationRule rule)
        {
            if (rule != null && rule.CalcConfigs.Count < 8)
            {
                rule.CalcConfigs.Add(new CalcValueConfig());
                _svc.SyncGroupsResultCount(rule.Name, rule.CalcConfigs.Count);
            }
        }

        [RelayCommand]
        private void RemoveCalcConfig(CalcValueConfig config)
        {
            if (config == null) return;
            foreach (var rule in Rules)
            {
                if (rule.CalcConfigs.Remove(config))
                {
                    _svc.SyncGroupsResultCount(rule.Name, rule.CalcConfigs.Count);
                    return;
                }
            }
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
