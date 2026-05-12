using System;
using System.IO;
using System.Text;

namespace CANDebugTool.Services
{
    /// <summary>
    /// 日志服务
    /// </summary>
    public static class LogService
    {
        private static readonly object _lock = new();
        private static readonly string _logPath;

        static LogService()
        {
            // 日志文件保存在程序目录下
            string exeDir = AppDomain.CurrentDomain.BaseDirectory;
            _logPath = Path.Combine(exeDir, $"CANDebugTool_{DateTime.Now:yyyyMMdd_HHmmss}.log");
        }

        /// <summary>
        /// 获取日志文件路径
        /// </summary>
        public static string LogPath => _logPath;

        /// <summary>
        /// 写入日志
        /// </summary>
        public static void Log(string message, LogLevel level = LogLevel.Info)
        {
            string logEntry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [{level}] {message}";

            lock (_lock)
            {
                try
                {
                    File.AppendAllText(_logPath, logEntry + Environment.NewLine, Encoding.UTF8);
                }
                catch
                {
                    // 忽略写入错误
                }
            }

            // 同时输出到控制台（调试用）
            Console.WriteLine(logEntry);
        }

        /// <summary>
        /// 写入日志（带参数）
        /// </summary>
        public static void Log(string format, LogLevel level, params object[] args)
        {
            Log(string.Format(format, args), level);
        }

        /// <summary>
        /// 记录扫描开始
        /// </summary>
        public static void LogScanStart()
        {
            Log("========== 设备扫描开始 ==========", LogLevel.Info);
        }

        /// <summary>
        /// 记录扫描设备类型
        /// </summary>
        public static void LogScanningType(int deviceType, string typeName)
        {
            Log($"正在扫描设备类型: {deviceType} ({typeName})", LogLevel.Info);
        }

        /// <summary>
        /// 记录打开设备结果
        /// </summary>
        public static void LogOpenDevice(int deviceType, int deviceIndex, int result)
        {
            string status = result == 1 ? "成功" : $"失败 (返回 {result})";
            Log($"  VCI_OpenDevice({deviceType}, {deviceIndex}, 0) = {result} [{status}]", LogLevel.Info);
        }

        /// <summary>
        /// 记录设备发现
        /// </summary>
        public static void LogDeviceFound(int deviceType, string typeName, int deviceIndex, string serialNumber)
        {
            Log($"  >>> 发现设备: {typeName} #{deviceIndex}, SN: {serialNumber}", LogLevel.Info);
        }

        /// <summary>
        /// 记录扫描结束
        /// </summary>
        public static void LogScanEnd(int deviceCount)
        {
            Log($"========== 设备扫描结束，发现 {deviceCount} 个设备 ==========", LogLevel.Info);
        }

        /// <summary>
        /// 记录连接开始
        /// </summary>
        public static void LogConnectStart(int deviceType, string typeName, int deviceIndex)
        {
            Log("========== 连接开始 ==========", LogLevel.Info);
            Log($"设备类型: {deviceType} ({typeName}), 索引: {deviceIndex}", LogLevel.Info);
        }

        /// <summary>
        /// 记录初始化配置
        /// </summary>
        public static void LogInitConfig(string baudrate, byte btr0, byte btr1, int canChannel)
        {
            Log($"CAN配置 - 波特率: {baudrate}, BTR0: 0x{btr0:X2}, BTR1: 0x{btr1:X2}, 通道: {canChannel}", LogLevel.Info);
        }

        /// <summary>
        /// 记录异常
        /// </summary>
        public static void LogException(Exception ex, string context)
        {
            Log($"异常 [{context}]: {ex.GetType().Name}: {ex.Message}", LogLevel.Error);
            Log($"StackTrace: {ex.StackTrace}", LogLevel.Error);
        }
    }

    public enum LogLevel
    {
        Debug,
        Info,
        Warning,
        Error
    }
}
