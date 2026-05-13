using System;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CANDebugTool.Models;

namespace CANDebugTool.ViewModels
{
    /// <summary>
    /// 接收面板 ViewModel
    /// </summary>
    public partial class ReceiveViewModel : ObservableObject
    {
        private readonly MainViewModel _mainVM;

        [ObservableProperty]
        private string _filterId = "";

        [ObservableProperty]
        private bool _filterById;

        [ObservableProperty]
        private string _statusMessage = "";

        public ObservableCollection<CanMessage> DisplayMessages { get; } = new();

        public ReceiveViewModel(MainViewModel mainVM)
        {
            _mainVM = mainVM;
            _mainVM.ReceivedMessages.CollectionChanged += (s, e) => UpdateDisplay();
        }

        private void UpdateDisplay()
        {
            DisplayMessages.Clear();
            var messages = _mainVM.ReceivedMessages.AsEnumerable();

            if (FilterById && !string.IsNullOrWhiteSpace(FilterId))
            {
                try
                {
                    uint filter = Convert.ToUInt32(FilterId, 16);
                    messages = messages.Where(m => m.Id == filter);
                }
                catch
                {
                    StatusMessage = "过滤 ID 格式错误";
                }
            }

            foreach (var msg in messages.Take(500))
            {
                DisplayMessages.Add(msg);
            }
        }

        partial void OnFilterIdChanged(string value) => UpdateDisplay();
        partial void OnFilterByIdChanged(bool value) => UpdateDisplay();

        [RelayCommand]
        private void Clear()
        {
            _mainVM.ReceivedMessages.Clear();
            DisplayMessages.Clear();
            StatusMessage = "已清空";
        }

        [RelayCommand]
        private void SaveToFile()
        {
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "CSV 文件 (*.csv)|*.csv|文本文件 (*.txt)|*.txt",
                FileName = $"CAN_Log_{DateTime.Now:yyyyMMdd_HHmmss}.csv"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    using var writer = new System.IO.StreamWriter(dialog.FileName);

                    // 功能需求第2条：序号、μs时间戳、归类码、统计组号
                    writer.WriteLine("序号,μs时间戳,ID,方向,帧类型,DLC,字节1,字节2,字节3,字节4,字节5,字节6,字节7,字节8,归类码,组号");

                    foreach (var msg in _mainVM.ReceivedMessages)
                    {
                        var d = msg.Data;
                        writer.WriteLine(
                            $"{msg.SequenceNumber}," +
                            $"{msg.TimestampUs}," +
                            $"{msg.IdDisplay}," +
                            $"{msg.DirectionCn}," +
                            $"{msg.FrameTypeCn}," +
                            $"{msg.DataLen}," +
                            $"{d[0]:X2},{d[1]:X2},{d[2]:X2},{d[3]:X2},{d[4]:X2},{d[5]:X2},{d[6]:X2},{d[7]:X2}," +
                            $"{msg.ClassifyCodeHex}," +
                            $"{msg.GroupId}");
                    }

                    StatusMessage = $"已保存 {dialog.FileName}";
                }
                catch (Exception ex)
                {
                    StatusMessage = $"保存失败: {ex.Message}";
                }
            }
        }
    }
}
