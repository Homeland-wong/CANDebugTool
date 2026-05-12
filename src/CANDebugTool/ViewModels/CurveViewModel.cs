using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CANDebugTool.Models;

namespace CANDebugTool.ViewModels
{
    public partial class CurveViewModel : ObservableObject
    {
        /// <summary>曲线配置列表 (最多8条)</summary>
        public ObservableCollection<CurveConfig> Curves { get; } = new();

        [ObservableProperty]
        private CurveConfig? _selectedCurve;

        [ObservableProperty]
        private bool _isConfigPanelVisible;

        public CurveViewModel()
        {
            for (int i = 0; i < 8; i++)
            {
                Curves.Add(new CurveConfig { Id = i, Name = $"曲线{i + 1}" });
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
            // TODO: 从统计数据源追加数据点
        }

        /// <summary>
        /// 清空所有曲线缓存
        /// </summary>
        public void ClearAll()
        {
            foreach (var c in Curves)
                c.DataPoints.Clear();
        }
    }
}
