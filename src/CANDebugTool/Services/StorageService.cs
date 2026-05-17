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
                _writer.WriteLine("序号,μs时间戳,时间,ID,方向,类型,DLC,字节1,字节2,字节3,字节4,字节5,字节6,字节7,字节8,归类码,组号,关注值_1,增量_1,最小_1,最大_1,关注值_2,增量_2,最小_2,最大_2,关注值_3,增量_3,最小_3,最大_3,关注值_4,增量_4,最小_4,最大_4,关注值_5,增量_5,最小_5,最大_5,关注值_6,增量_6,最小_6,最大_6,关注值_7,增量_7,最小_7,最大_7,关注值_8,增量_8,最小_8,最大_8");
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
                var sb = new StringBuilder(256);

                sb.Append(msg.SequenceNumber);
                sb.Append(',');
                sb.Append(msg.TimestampUs);
                sb.Append(',');
                sb.AppendFormat("{0:HH:mm:ss.fff}", msg.Timestamp);
                sb.Append(',');
                sb.Append(msg.IdDisplay);
                sb.Append(',');
                sb.Append(msg.DirectionCn);
                sb.Append(',');
                sb.Append(msg.FrameType);
                sb.Append(',');
                sb.Append(msg.DataLen);
                sb.Append(',');
                for (int i = 0; i < 8; i++)
                {
                    if (d != null && i < d.Length)
                        sb.AppendFormat("{0:X2}", d[i]);
                    sb.Append(',');
                }
                sb.Append(msg.ClassifyCodeHex);
                sb.Append(',');
                sb.Append(msg.GroupId);

                // 写入关注值结果列
                if (msg.GroupId >= 0 && !string.IsNullOrEmpty(msg.ClassifyCodeHex))
                {
                    var group = ClassificationService.Instance.GetGroup(msg.ClassifyCodeHex);
                    if (group != null)
                    {
                        for (int i = 0; i < 8; i++)
                        {
                            if (i < group.Results.Count)
                            {
                                var r = group.Results[i];
                                if (r.FocusMode == "无") sb.Append(",-,,-,-");
                                else
                                {
                                    sb.Append(',');
                                    sb.Append(r.CalcValue.ToString("F2"));
                                    sb.Append(',');
                                    sb.Append(r.FocusMode == "增量" ? r.FocusedDelta.ToString("F2") : "-");
                                    sb.Append(',');
                                    sb.Append(r.FocusMode == "跳动" && r.FocusedMin.HasValue ? r.FocusedMin.Value.ToString("F2") : "-");
                                    sb.Append(',');
                                    sb.Append(r.FocusMode == "跳动" && r.FocusedMax.HasValue ? r.FocusedMax.Value.ToString("F2") : "-");
                                }
                            }
                            else
                            {
                                sb.Append(",,,,");
                            }
                        }
                    }
                    else
                    {
                        sb.Append(new string(',', 32));
                    }
                }
                else
                {
                    sb.Append(new string(',', 32));
                }

                _writeQueue.TryAdd(sb.ToString());
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
