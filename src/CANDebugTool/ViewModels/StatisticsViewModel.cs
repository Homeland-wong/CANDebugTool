using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Windows;
using Microsoft.Win32;
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

        [ObservableProperty]
        private string _workspacePath = "";

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

        [RelayCommand]
        private void SaveRules()
        {
            if (string.IsNullOrEmpty(WorkspacePath))
            {
                MessageBox.Show("请先指定工作区", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (Rules.Count == 0)
            {
                MessageBox.Show("没有可保存的掩码规则", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            var dialog = new SaveFileDialog
            {
                Filter = "掩码规则文件 (*.json)|*.json",
                DefaultExt = ".json",
                FileName = "mask_rules",
                InitialDirectory = WorkspacePath,
                Title = "保存掩码规则"
            };
            if (dialog.ShowDialog() != true) return;
            try
            {
                var jsonArray = new JsonArray();
                foreach (var rule in Rules)
                {
                    var ruleObj = new JsonObject
                    {
                        ["Id"] = rule.Id,
                        ["Name"] = rule.Name,
                        ["IdMaskHex"] = rule.IdMaskHex,
                        ["DataMaskHex"] = rule.DataMaskHex,
                        ["IdRefHex"] = rule.IdRefHex,
                        ["DataRefHex"] = rule.DataRefHex,
                        ["IdOpEquals"] = rule.IdOpEquals,
                        ["DataOpEquals"] = rule.DataOpEquals,
                        ["IsClassifyMode"] = rule.IsClassifyMode,
                    };
                    var calcArray = new JsonArray();
                    foreach (var calc in rule.CalcConfigs)
                    {
                        calcArray.Add(new JsonObject
                        {
                            ["DataMaskHex"] = calc.DataMaskHex,
                            ["IsBigEndian"] = calc.IsBigEndian,
                            ["PropertyType"] = calc.PropertyType,
                            ["FocusMode"] = calc.FocusMode,
                        });
                    }
                    ruleObj["CalcConfigs"] = calcArray;
                    jsonArray.Add(ruleObj);
                }
                var json = jsonArray.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(dialog.FileName, json);
                StatusText = $"规则已保存 → {Path.GetFileName(dialog.FileName)} ({Rules.Count} 条)";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"保存失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private void LoadRules()
        {
            if (string.IsNullOrEmpty(WorkspacePath))
            {
                MessageBox.Show("请先指定工作区", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            var dialog = new OpenFileDialog
            {
                Filter = "掩码规则文件 (*.json)|*.json",
                DefaultExt = ".json",
                InitialDirectory = WorkspacePath,
                Title = "加载掩码规则"
            };
            if (dialog.ShowDialog() != true) return;
            try
            {
                var json = File.ReadAllText(dialog.FileName);
                var jsonArray = JsonNode.Parse(json)?.AsArray();
                if (jsonArray == null)
                {
                    MessageBox.Show("规则文件格式无效", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                Rules.Clear();
                foreach (var node in jsonArray)
                {
                    var rule = new ClassificationRule
                    {
                        Id = (int)(node?["Id"]?.GetValue<int>() ?? 0),
                        Name = (string?)node?["Name"] ?? "",
                        IdOpEquals = node?["IdOpEquals"]?.GetValue<bool>() ?? true,
                        DataOpEquals = node?["DataOpEquals"]?.GetValue<bool>() ?? true,
                        IsClassifyMode = node?["IsClassifyMode"]?.GetValue<bool>() ?? false,
                    };
                    rule.IdMaskHex = (string?)node?["IdMaskHex"] ?? "00000000";
                    rule.DataMaskHex = (string?)node?["DataMaskHex"] ?? "0000000000000000";
                    rule.IdRefHex = (string?)node?["IdRefHex"] ?? "00000000";
                    rule.DataRefHex = (string?)node?["DataRefHex"] ?? "0000000000000000";

                    var calcArray = node?["CalcConfigs"]?.AsArray();
                    if (calcArray != null)
                    {
                        foreach (var calcNode in calcArray)
                        {
                            var calc = new CalcValueConfig
                            {
                                IsBigEndian = calcNode?["IsBigEndian"]?.GetValue<bool>() ?? true,
                                PropertyType = (string?)calcNode?["PropertyType"] ?? "hex",
                                FocusMode = (string?)calcNode?["FocusMode"] ?? "无",
                            };
                            calc.DataMaskHex = (string?)calcNode?["DataMaskHex"] ?? "0000000000000000";
                            rule.CalcConfigs.Add(calc);
                        }
                    }
                    Rules.Add(rule);
                }
                StatusText = $"已加载 {Path.GetFileName(dialog.FileName)} ({Rules.Count} 条规则)";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"加载失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
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
