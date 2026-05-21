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
    /// 可选统计组（用于 ComboBox 绑定）
    /// </summary>
    public class GroupOption
    {
        public int GroupId { get; set; }
        public string DisplayText { get; set; } = "";
    }

    public partial class CurveViewModel : ObservableObject
    {
        private readonly ClassificationService _svc;

        /// <summary>曲线配置列表 (最多8条)</summary>
        public ObservableCollection<CurveConfig> Curves { get; } = new();

        /// <summary>可选的统计组列表（从当前分类结果动态刷新）</summary>
        public ObservableCollection<GroupOption> AvailableGroups { get; } = new();

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
                Curves.Add(new CurveConfig { Id = i, Name = $"曲线{i + 1}" });
            }

            _refreshTimer = new DispatcherTimer(TimeSpan.FromMilliseconds(100), DispatcherPriority.Normal,
                (s, e) => RefreshAvailableGroups(), Dispatcher.CurrentDispatcher);
            _refreshTimer.Start();
        }

        /// <summary>
        /// 刷新可选统计组列表，并更新每条曲线的可选关注值索引
        /// </summary>
        public void RefreshAvailableGroups()
        {
            var groups = _svc.GetAllGroups();
            var newList = groups
                .Select(g => new GroupOption { GroupId = g.GroupId, DisplayText = $"组{g.GroupId} - {g.RuleName}" })
                .ToList();

            // 只在实际变化时更新，避免频繁触发 UI 绑定
            bool changed = newList.Count != AvailableGroups.Count;
            if (!changed)
            {
                for (int i = 0; i < newList.Count; i++)
                {
                    if (newList[i].GroupId != AvailableGroups[i].GroupId ||
                        newList[i].DisplayText != AvailableGroups[i].DisplayText)
                    {
                        changed = true;
                        break;
                    }
                }
            }

            if (changed)
            {
                AvailableGroups.Clear();
                foreach (var opt in newList)
                    AvailableGroups.Add(opt);
            }

            // 更新每条曲线的可选关注值索引
            foreach (var curve in Curves)
            {
                var group = groups.FirstOrDefault(g => g.GroupId == curve.SourceGroupId);
                int maxIndex = group?.Results.Count ?? 0;

                if (maxIndex != curve.AvailableCalcIndices.Count ||
                    (maxIndex > 0 && (curve.AvailableCalcIndices.Count == 0 ||
                     curve.AvailableCalcIndices[^1] != maxIndex - 1)))
                {
                    curve.AvailableCalcIndices.Clear();
                    for (int i = 0; i < maxIndex; i++)
                        curve.AvailableCalcIndices.Add(i);

                    // clamp SourceCalcIndex
                    if (curve.SourceCalcIndex >= maxIndex)
                        curve.SourceCalcIndex = maxIndex > 0 ? maxIndex - 1 : 0;
                }
            }
        }

        /// <summary>
        /// 从报文接收管线喂入数据 — 由 MainViewModel 调用
        /// </summary>
        public void FeedData(CanMessage msg)
        {
            if (msg.GroupId < 0) return;

            long tick = msg.TimestampUs;
            var enabledCurves = Curves.Where(c => c.Enabled && c.SourceGroupId == msg.GroupId).ToList();
            if (enabledCurves.Count == 0) return;

            var group = _svc.GetGroup(msg.ClassifyCodeHex ?? "");
            if (group == null) return;

            foreach (var curve in enabledCurves)
            {
                int idx = curve.SourceCalcIndex;
                if (idx < 0 || idx >= group.Results.Count) continue;

                double value = group.Results[idx].CalcValue;
                curve.DataPoints.Enqueue((tick, value));

                // 裁剪到 DisplayWidthPoints
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
            AvailableGroups.Clear();
        }
    }
}
