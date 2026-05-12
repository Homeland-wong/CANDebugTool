using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CANDebugTool.Models;

namespace CANDebugTool.ViewModels
{
    public partial class DigitalViewModel : ObservableObject
    {
        /// <summary>数字显示列表 (最多16个)</summary>
        public ObservableCollection<DigitalDisplay> Displays { get; } = new();

        [ObservableProperty]
        private DigitalDisplay? _selectedDisplay;

        public DigitalViewModel()
        {
            for (int i = 0; i < 16; i++)
            {
                Displays.Add(new DigitalDisplay
                {
                    Id = i,
                    Name = $"数字{i + 1}",
                    SourceGroupId = -1,
                    Value = "---"
                });
            }
        }

        /// <summary>
        /// 刷新所有数字显示（占位）
        /// </summary>
        public void RefreshAll()
        {
            // TODO: 从统计数据源更新各数字的值
        }
    }
}
