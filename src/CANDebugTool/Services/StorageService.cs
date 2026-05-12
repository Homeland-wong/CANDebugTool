using System;
using System.IO;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;

namespace CANDebugTool.Services
{
    /// <summary>
    /// 存储服务 (CSV)
    /// </summary>
    public partial class StorageService : ObservableObject
    {
        private static StorageService? _instance;
        public static StorageService Instance => _instance ??= new StorageService();

        private StreamWriter? _writer;
        private string? _currentPath;

        [ObservableProperty]
        private bool _isRecording;

        public event Action<string>? OnError;

        private StorageService() { }

        public bool StartRecording(string filePath)
        {
            try
            {
                var dir = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                _writer = new StreamWriter(filePath, false, Encoding.UTF8);
                _writer.WriteLine("序号,μs时间戳,时间,ID,方向,类型,DLC,数据,归类码,组号");
                _currentPath = filePath;
                IsRecording = true;
                return true;
            }
            catch (Exception ex)
            {
                OnError?.Invoke($"打开存储文件失败: {ex.Message}");
                return false;
            }
        }

        public void WriteMessage(Models.CanMessage msg)
        {
            if (_writer == null) return;
            try
            {
                _writer.WriteLine(
                    $"{msg.SequenceNumber},{msg.TimestampUs},{msg.Timestamp:HH:mm:ss.fff}," +
                    $"{msg.IdDisplay},{msg.DirectionCn},{msg.FrameType},{msg.DataLen}," +
                    $"{msg.DataHex},{msg.ClassifyCodeHex},{msg.GroupId}");
            }
            catch (Exception ex)
            {
                OnError?.Invoke($"写入存储失败: {ex.Message}");
            }
        }

        public void Flush() => _writer?.Flush();

        public void StopRecording()
        {
            if (_writer != null)
            {
                _writer.Flush();
                _writer.Close();
                _writer.Dispose();
                _writer = null;
            }
            _currentPath = null;
            IsRecording = false;
        }
    }
}
