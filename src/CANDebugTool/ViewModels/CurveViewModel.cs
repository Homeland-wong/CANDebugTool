using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CANDebugTool.Models;
using CANDebugTool.Services;

namespace CANDebugTool.ViewModels
{
    /// <summary>
    /// 可选规则项（用于 ComboBox 绑定）
    /// </summary>
    public class RuleOption
    {
        public int Value { get; set; }
        public string DisplayText { get; set; } = "";
    }

    public partial class CurveViewModel : ObservableObject
    {
        private readonly ClassificationService _svc;

        /// <summary>曲线配置列表 (最多8条)</summary>
        public ObservableCollection<CurveConfig> Curves { get; } = new();

        /// <summary>可选的掩码规则列表（从 StatisticsVM.Rules 同步）</summary>
        public ObservableCollection<RuleOption> AvailableRuleOptions { get; } = new();

        /// <summary>缓存的规则列表引用</summary>
        private IList<ClassificationRule>? _rules;

        private static readonly string[] CurveColors =
        {
            "#0078D4",  // 曲线1 蓝色
            "#D13438",  // 曲线2 红色
            "#107C10",  // 曲线3 绿色
            "#FF8C00",  // 曲线4 橙色
            "#6A0DAD",  // 曲线5 紫色
            "#00B7C3",  // 曲线6 青色
            "#E74856",  // 曲线7 玫红
            "#8764B8",  // 曲线8 淡紫
        };

        [ObservableProperty]
        private CurveConfig? _selectedCurve;

        [ObservableProperty]
        private bool _isConfigPanelVisible;

        private readonly DispatcherTimer _refreshTimer;

        public CurveViewModel()
        {
            _svc = ClassificationService.Instance;

            for (int i = 0; i < 8; i++)
            {
                Curves.Add(new CurveConfig
                {
                    Id = i,
                    Name = $"曲线{i + 1}",
                    Color = CurveColors[i],
                });
            }

            _refreshTimer = new DispatcherTimer(TimeSpan.FromMilliseconds(200), DispatcherPriority.Normal,
                (s, e) => RefreshAvailableGroups(), Dispatcher.CurrentDispatcher);
            _refreshTimer.Start();
        }

        /// <summary>
        /// 同步掩码规则列表（由 MainViewModel 调用）
        /// </summary>
        public void SyncRules(IList<ClassificationRule> rules)
        {
            _rules = rules;

            var newList = new List<RuleOption>();
            for (int i = 0; i < rules.Count; i++)
            {
                var r = rules[i];
                newList.Add(new RuleOption { Value = i, DisplayText = $"{i} - {r.Name}" });
            }

            bool changed = newList.Count != AvailableRuleOptions.Count;
            if (!changed)
            {
                for (int i = 0; i < newList.Count; i++)
                {
                    if (newList[i].Value != AvailableRuleOptions[i].Value ||
                        newList[i].DisplayText != AvailableRuleOptions[i].DisplayText)
                    {
                        changed = true;
                        break;
                    }
                }
            }

            if (changed)
            {
                AvailableRuleOptions.Clear();
                foreach (var opt in newList)
                    AvailableRuleOptions.Add(opt);

                // 更新每条曲线的可选关注值索引
                foreach (var curve in Curves)
                    UpdateCalcIndices(curve);
            }
        }

        /// <summary>
        /// 刷新可选统计组（运行中动态更新）及每条曲线的关注值索引
        /// </summary>
        public void RefreshAvailableGroups()
        {
            if (_rules == null) return;

            // 确保 AvailableRuleOptions 与规则同步（可能在 SyncRules 之前被 timer 触发）
            if (AvailableRuleOptions.Count != _rules.Count)
                SyncRules(_rules);

            foreach (var curve in Curves)
                UpdateCalcIndices(curve);
        }

        private void UpdateCalcIndices(CurveConfig curve)
        {
            if (_rules == null) return;

            int ruleIdx = curve.SourceGroupId;
            int maxIndex = 0;
            if (ruleIdx >= 0 && ruleIdx < _rules.Count)
                maxIndex = _rules[ruleIdx].CalcConfigs.Count;

            bool needUpdate = curve.AvailableCalcIndices.Count != maxIndex;
            if (!needUpdate && maxIndex > 0)
                needUpdate = curve.AvailableCalcIndices[^1] != maxIndex - 1;

            if (needUpdate)
            {
                curve.AvailableCalcIndices.Clear();
                for (int i = 0; i < maxIndex; i++)
                    curve.AvailableCalcIndices.Add(i);

                if (curve.SourceCalcIndex >= maxIndex)
                    curve.SourceCalcIndex = maxIndex > 0 ? maxIndex - 1 : 0;
            }
        }

        /// <summary>
        /// 从报文接收管线喂入数据 — 由 MainViewModel 调用
        /// 匹配逻辑：曲线选择的规则索引 → 规则名 → 统计组 RuleName
        /// </summary>
        public void FeedData(CanMessage msg)
        {
            if (msg.GroupId < 0 || _rules == null) return;

            long tick = msg.TimestampUs;

            var enabledCurves = Curves.Where(c => c.Enabled).ToList();
            if (enabledCurves.Count == 0) return;

            // 按 classifyCode 查找统计组（一次查找供所有曲线复用）
            var group = _svc.GetGroup(msg.ClassifyCodeHex ?? "");
            if (group == null) return;

            foreach (var curve in enabledCurves)
            {
                int ruleIdx = curve.SourceGroupId;
                if (ruleIdx < 0 || ruleIdx >= _rules.Count) continue;

                // 匹配：曲线选择的规则名与统计组的规则名一致
                if (group.RuleName != _rules[ruleIdx].Name) continue;

                int calcIdx = curve.SourceCalcIndex;
                if (calcIdx < 0 || calcIdx >= group.Results.Count) continue;

                double value = group.Results[calcIdx].CalcValue;
                curve.DataPoints.Enqueue((tick, value));

                while (curve.DataPoints.Count > curve.DisplayWidthPoints)
                    curve.DataPoints.TryDequeue(out _);
            }
        }

        [RelayCommand]
        private void ToggleCurve(CurveConfig curve)
        {
            curve.Enabled = !curve.Enabled;
        }

        [RelayCommand]
        private void AddDataPoint()
        {
            // TODO: 手动测试时可追加模拟数据点
        }

        /// <summary>
        /// 清空所有曲线缓存
        /// </summary>
        public void ClearAll()
        {
            foreach (var c in Curves)
            {
                while (c.DataPoints.TryDequeue(out _)) { }
                c.AvailableCalcIndices.Clear();
            }
            AvailableRuleOptions.Clear();
        }
    }
}
