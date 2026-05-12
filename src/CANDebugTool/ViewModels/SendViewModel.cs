using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CANDebugTool.Models;

namespace CANDebugTool.ViewModels
{
    /// <summary>
    /// 发送面板 ViewModel
    /// </summary>
    public partial class SendViewModel : ObservableObject
    {
        private readonly MainViewModel _mainVM;
        private CancellationTokenSource? _periodicCts;
        private Task? _periodicTask;

        [ObservableProperty]
        private string _canId = "123";

        [ObservableProperty]
        private string _canData = "";

        [ObservableProperty]
        private bool _isExtendedFrame;

        partial void OnIsExtendedFrameChanged(bool value)
        {
            OnPropertyChanged(nameof(FrameTypeButtonText));
        }

        [ObservableProperty]
        private bool _isRemoteFrame;

        [ObservableProperty]
        private bool _isPeriodicSend;

        [ObservableProperty]
        private int _periodicInterval = 100;  // ms

        [ObservableProperty]
        private string _statusMessage = "";

        /// <summary>帧类型切换按钮文本</summary>
        public string FrameTypeButtonText => IsExtendedFrame ? "扩展" : "标准";

        [RelayCommand]
        private void ToggleFrameType()
        {
            IsExtendedFrame = !IsExtendedFrame;
        }

        public SendViewModel(MainViewModel mainVM)
        {
            _mainVM = mainVM;
        }

        [RelayCommand]
        private void Send()
        {
            if (!ParseAndSend())
            {
                StatusMessage = "发送失败，请检查数据格式";
            }
            else
            {
                StatusMessage = "发送成功";
            }
        }

        [RelayCommand]
        private void StartPeriodicSend()
        {
            if (IsPeriodicSend) return;
            if (!ValidateInput()) return;

            IsPeriodicSend = true;
            _periodicCts = new CancellationTokenSource();

            _periodicTask = Task.Run(async () =>
            {
                try
                {
                    while (!_periodicCts.Token.IsCancellationRequested)
                    {
                        System.Windows.Application.Current.Dispatcher.Invoke(() =>
                        {
                            ParseAndSend();
                        });
                        await Task.Delay(PeriodicInterval, _periodicCts.Token);
                    }
                }
                catch (OperationCanceledException)
                {
                    // 正常取消
                }
                finally
                {
                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        IsPeriodicSend = false;
                    });
                }
            });
        }

        [RelayCommand]
        private void StopPeriodicSend()
        {
            _periodicCts?.Cancel();
            _periodicTask = null;
        }

        private bool ParseAndSend()
        {
            if (!ValidateInput()) return false;

            uint id = Convert.ToUInt32(CanId.Replace("·", ""), 16);
            byte[]? data = ParseHexData(CanData);

            var message = new CanMessage
            {
                Id = id,
                IsExtended = IsExtendedFrame,
                IsRemote = IsRemoteFrame,
                DataLen = (byte)(data?.Length ?? 0),
                Data = data ?? new byte[8],
                IsTransmit = true,
                Timestamp = DateTime.Now
            };

            return _mainVM.SendMessage(message);
        }

        private bool ValidateInput()
        {
            if (string.IsNullOrWhiteSpace(CanId))
            {
                StatusMessage = "请输入 CAN ID";
                return false;
            }

            try
            {
                uint id = Convert.ToUInt32(CanId.Replace("·", ""), 16);
                if (IsExtendedFrame && id > 0x1FFFFFFF)
                {
                    StatusMessage = "扩展帧 ID 不能超过 0x1FFFFFFF";
                    return false;
                }
                if (!IsExtendedFrame && id > 0x7FF)
                {
                    StatusMessage = "标准帧 ID 不能超过 0x7FF";
                    return false;
                }
            }
            catch
            {
                StatusMessage = "CAN ID 格式错误";
                return false;
            }

            return true;
        }

        private static byte[]? ParseHexData(string hex)
        {
            if (string.IsNullOrWhiteSpace(hex)) return new byte[0];

            hex = hex.Replace("·", "").Replace(" ", "").Replace(",", "");
            if (hex.Length % 2 != 0) return null;

            var bytes = new byte[hex.Length / 2];
            for (int i = 0; i < bytes.Length; i++)
            {
                try
                {
                    bytes[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
                }
                catch
                {
                    return null;
                }
            }
            return bytes;
        }
    }
}
