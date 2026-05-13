using System;
using System.Collections.Concurrent;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;

namespace CANDebugTool.Services
{
    /// <summary>
    /// 存储服务 (CSV) — 异步缓冲写入，避免磁盘 I/O 阻塞接收线程
    /// </summary>
    public partial class StorageService : ObservableObject, IDisposable
    {
        private static StorageService? _instance;
        public static StorageService Instance => _instance ??= new StorageService();

        private StreamWriter? _writer;
        private string? _currentPath;
        private readonly BlockingCollection<string> _writeQueue = new(boundedCapacity: 8192);
        private CancellationTokenSource? _writeCts;
        private Task? _writeTask;

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

                _writer = new StreamWriter(filePath, false, Encoding.UTF8, bufferSize: 65536);
                _writer.WriteLine("序号,μs时间戳,时间,ID,方向,类型,DLC,字节1,字节2,字节3,字节4,字节5,字节6,字节7,字节8,归类码,组号");
                _currentPath = filePath;

                _writeCts = new CancellationTokenSource();
                _writeTask = Task.Run(() => WriteLoop(_writeCts.Token));

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
            if (!IsRecording) return;
            try
            {
                var d = msg.Data;
                string line =
                    $"{msg.SequenceNumber},{msg.TimestampUs},{msg.Timestamp:HH:mm:ss.fff}," +
                    $"{msg.IdDisplay},{msg.DirectionCn},{msg.FrameType},{msg.DataLen}," +
                    $"{d[0]:X2},{d[1]:X2},{d[2]:X2},{d[3]:X2},{d[4]:X2},{d[5]:X2},{d[6]:X2},{d[7]:X2}," +
                    $"{msg.ClassifyCodeHex},{msg.GroupId}";

                // 非阻塞入队；队列满时丢弃而非阻塞
                _writeQueue.TryAdd(line);
            }
            catch (Exception ex)
            {
                OnError?.Invoke($"写入存储失败: {ex.Message}");
            }
        }

        private void WriteLoop(CancellationToken token)
        {
            var batch = new System.Collections.Generic.List<string>(256);
            try
            {
                while (!token.IsCancellationRequested)
                {
                    batch.Clear();
                    if (!_writeQueue.TryTake(out var first, 100, token))
                        continue;
                    batch.Add(first);

                    // 尽可能多取不阻塞
                    while (batch.Count < 256 && _writeQueue.TryTake(out var next))
                        batch.Add(next);

                    foreach (var line in batch)
                        _writer?.WriteLine(line);

                    int qDepth = _writeQueue.Count;
                    if (qDepth > 1024)
                        LogService.Log($"存储队列积压: {qDepth} 条", LogLevel.Warning);

                    if (batch.Count >= 256)
                        _writer?.Flush();
                }
            }
            catch (OperationCanceledException) { }
            try
            {
                int remain = 0;
                while (_writeQueue.TryTake(out var line)) { _writer?.WriteLine(line); remain++; }
                if (remain > 0) LogService.Log($"存储关闭: 写入剩余 {remain} 条", LogLevel.Info);
                _writer?.Flush();
            }
            catch { }
        }

        public void Flush() => _writer?.Flush();

        public void StopRecording()
        {
            _writeCts?.Cancel();
            try { _writeTask?.Wait(500); } catch { }
            _writeCts?.Dispose();
            _writeCts = null;
            _writeTask = null;

            if (_writer != null)
            {
                try { _writer.Flush(); } catch { }
                try { _writer.Close(); } catch { }
                try { _writer.Dispose(); } catch { }
                _writer = null;
            }
            _currentPath = null;
            IsRecording = false;
        }

        public void Dispose() => StopRecording();
    }
}
