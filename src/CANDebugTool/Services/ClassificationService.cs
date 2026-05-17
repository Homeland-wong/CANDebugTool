using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using CANDebugTool.Models;

namespace CANDebugTool.Services
{
    /// <summary>
    /// 归类/统计/计算服务
    /// </summary>
    public class ClassificationService
    {
        private static ClassificationService? _instance;
        public static ClassificationService Instance => _instance ??= new ClassificationService();

        private readonly ConcurrentDictionary<string, StatisticsGroup> _groupCache = new();
        private int _nextGroupId;

        public event Action<StatisticsGroup>? OnGroupUpdated;

        private ClassificationService() { }

        /// <summary>
        /// 清空所有统计组
        /// </summary>
        public void ClearAll()
        {
            _groupCache.Clear();
            _nextGroupId = 0;
        }

        /// <summary>
        /// 对报文执行归类
        // 预置常量，避免热路径重复分配
        private static readonly string ZeroCodeHex = "00·00·00·00·00·00·00·00·00·00·00·00";
        private static readonly char[] HexLookup = { '0','1','2','3','4','5','6','7','8','9','A','B','C','D','E','F' };

        /// <summary>
        /// 对报文执行归类（热路径优化：stackalloc 避免堆分配）
        /// </summary>
        public StatisticsGroup? Classify(CanMessage msg, List<ClassificationRule> rules)
        {
            // stackalloc 替代堆分配（4 字节 ID + 8 字节 Data）
            Span<byte> idBytes = stackalloc byte[4];
            uint id = msg.Id;
            idBytes[0] = (byte)((id >> 24) & 0xFF);
            idBytes[1] = (byte)((id >> 16) & 0xFF);
            idBytes[2] = (byte)((id >> 8) & 0xFF);
            idBytes[3] = (byte)(id & 0xFF);

            Span<byte> msgData = stackalloc byte[8];
            var rawData = msg.Data;
            if (rawData != null)
            {
                int copyLen = rawData.Length < 8 ? rawData.Length : 8;
                for (int i = 0; i < copyLen; i++)
                    msgData[i] = rawData[i];
            }

            foreach (var rule in rules)
            {
                // ── ID 匹配 (stackalloc) ──
                Span<byte> in1 = stackalloc byte[4];
                var idMask = rule.IdMask;
                var idRef = rule.IdRef;
                for (int i = 0; i < 4; i++)
                    in1[i] = (byte)(idBytes[i] & idMask[i]);

                bool idMatch = rule.IdOpEquals
                    ? SpanEqual(in1, idRef)
                    : !SpanEqual(in1, idRef);
                if (!idMatch) continue;

                // ── Data 匹配 (stackalloc) ──
                Span<byte> in2 = stackalloc byte[8];
                var dataMask = rule.DataMask;
                var dataRef = rule.DataRef;
                for (int i = 0; i < 8; i++)
                    in2[i] = (byte)(msgData[i] & dataMask[i]);

                bool dataMatch = rule.DataOpEquals
                    ? SpanEqual(in2, dataRef)
                    : !SpanEqual(in2, dataRef);
                if (!dataMatch) continue;

                // 生成 12 字节归类码
                Span<byte> codeBytes = stackalloc byte[12];
                for (int i = 0; i < 4; i++) codeBytes[i] = in1[i];
                for (int i = 0; i < 8; i++) codeBytes[4 + i] = in2[i];

                string codeHex = BytesToHex12(codeBytes);

                // 查找或创建统计组（线程安全，初始化 Results 时同步规则配置数）
                var group = _groupCache.GetOrAdd(codeHex, key =>
                {
                    var g = new StatisticsGroup
                    {
                        GroupId = Interlocked.Increment(ref _nextGroupId) - 1,
                        RuleName = rule.Name,
                        ClassifyCode = key,
                        Count = 0,
                        FocusMode = "无"
                    };
                    for (int ci = 0; ci < rule.CalcConfigs.Count; ci++)
                        g.Results.Add(new CalcResult());
                    return g;
                });

                group.Count++;

                if (group.Count > 1)
                {
                    group.TimeDiff = msg.TimestampUs - group.PreviousTimestampUs;
                    if (group.TimeDiffMin == null)
                    {
                        group.TimeDiffMin = group.TimeDiff;
                        group.TimeDiffMax = group.TimeDiff;
                    }
                    else
                    {
                        if (group.TimeDiff < group.TimeDiffMin) group.TimeDiffMin = group.TimeDiff;
                        if (group.TimeDiff > group.TimeDiffMax) group.TimeDiffMax = group.TimeDiff;
                    }
                }
                group.PreviousTimestampUs = msg.TimestampUs;

                // ── 关注值计算（支持多条配置）──
                int calcCount = group.Results.Count < rule.CalcConfigs.Count ? group.Results.Count : rule.CalcConfigs.Count;
                for (int ci = 0; ci < calcCount; ci++)
                {
                    var calcCfg = rule.CalcConfigs[ci];
                    var result = group.Results[ci];
                    result.ConfigIndex = ci;
                    result.PropertyType = calcCfg.PropertyType;

                    if (calcCfg.FfCount > 0)
                    {
                        double val = ExtractFocusedValue(msg, calcCfg);
                        result.CalcValue = val;
                        result.FocusMode = calcCfg.FocusMode;

                        if (calcCfg.FocusMode == "增量")
                        {
                            if (group.Count > 1)
                                result.FocusedDelta = val - result.PreviousCalcValue;
                            result.PreviousCalcValue = val;
                        }
                        else if (calcCfg.FocusMode == "跳动")
                        {
                            if (result.FocusedMin == null)
                            {
                                result.FocusedMin = val;
                                result.FocusedMax = val;
                            }
                            else
                            {
                                if (val < result.FocusedMin) result.FocusedMin = val;
                                if (val > result.FocusedMax) result.FocusedMax = val;
                            }
                        }
                    }
                    else
                    {
                        result.FocusMode = "无";
                    }
                }

                group.SyncFromResults();

                // 存储归类码字节（需拷贝到堆数组）
                byte[] codeCopy = new byte[12];
                for (int i = 0; i < 12; i++) codeCopy[i] = codeBytes[i];
                msg.ClassifyCode = codeCopy;
                msg.ClassifyCodeHex = codeHex;
                msg.GroupId = group.GroupId;

                OnGroupUpdated?.Invoke(group);
                return group;
            }

            // 无规则匹配
            msg.ClassifyCodeHex = ZeroCodeHex;
            msg.GroupId = -1;
            return null;
        }

        /// <summary>
        /// 从报文 Data 中按 CalcValueConfig 提取关注值
        /// </summary>
        private static double ExtractFocusedValue(CanMessage msg, CalcValueConfig cfg)
        {
            byte[] data = msg.Data ?? new byte[8];

            int ffStart = -1;
            int ffLen = cfg.FfCount;
            for (int i = 0; i < 8; i++)
                if (cfg.DataMask[i] == 0xFF && ffStart < 0) { ffStart = i; break; }
            if (ffStart < 0 || ffLen == 0) return 0;
            if (ffStart + ffLen > 8) ffLen = 8 - ffStart;

            string pt = cfg.PropertyType;

            // 浮点数：BitConverter 在 x86 上需要 LSB 优先，大端需反转
            if (ffLen == 4 && pt == "flt")
            {
                Span<byte> raw = stackalloc byte[4];
                for (int i = 0; i < 4; i++) raw[i] = data[ffStart + i];
                if (cfg.IsBigEndian) raw.Reverse();
                return BitConverter.ToSingle(raw);
            }
            if (ffLen == 8 && pt == "dbl")
            {
                Span<byte> raw = stackalloc byte[8];
                for (int i = 0; i < 8; i++) raw[i] = data[ffStart + i];
                if (cfg.IsBigEndian) raw.Reverse();
                return BitConverter.ToDouble(raw);
            }

            // 整数类型：手动移位需要 MSB 优先，小端需反转
            Span<byte> slice = stackalloc byte[ffLen];
            for (int i = 0; i < ffLen; i++) slice[i] = data[ffStart + i];
            if (!cfg.IsBigEndian) slice.Reverse();

            if (pt == "hex") return 0;
            if (ffLen == 1) return pt == "8S" ? (sbyte)slice[0] : slice[0];
            if (ffLen == 2)
            {
                ushort u16 = (ushort)(slice[0] << 8 | slice[1]);
                return pt == "16S" ? (short)u16 : u16;
            }
            if (ffLen == 4)
            {
                uint u32 = (uint)(slice[0] << 24 | slice[1] << 16 | slice[2] << 8 | slice[3]);
                return pt == "32S" ? (int)u32 : u32;
            }
            if (ffLen == 8)
            {
                long u64 = (long)((ulong)slice[0] << 56 | (ulong)slice[1] << 48 | (ulong)slice[2] << 40 | (ulong)slice[3] << 32 | (ulong)slice[4] << 24 | (ulong)slice[5] << 16 | (ulong)slice[6] << 8 | slice[7]);
                return pt == "64S" ? u64 : (double)(ulong)u64;
            }
            return 0;
        }

        /// <summary>
        /// 获取所有统计组的快照
        /// </summary>
        public List<StatisticsGroup> GetAllGroups() => _groupCache.Values.ToList();

        /// <summary>
        /// 根据归类码查找统计组（用于 CSV 写入关联关注值）
        /// </summary>
        public StatisticsGroup? GetGroup(string classifyCodeHex)
            => _groupCache.TryGetValue(classifyCodeHex, out var group) ? group : null;

        /// <summary>
        /// 重置指定规则的所有统计组
        /// </summary>
        public void ResetRuleGroups(string ruleName)
        {
            var groups = _groupCache.Values.Where(g => g.RuleName == ruleName).ToList();
            foreach (var g in groups)
            {
                g.Count = 0;
                g.CalcValue = 0;
                g.TimeDiff = 0;
                g.TimeDiffMin = null;
                g.TimeDiffMax = null;
                g.FocusMode = "无";
                g.FocusedDelta = 0;
                g.FocusedMin = null;
                g.FocusedMax = null;
                g.PreviousTimestampUs = 0;
                // 重置每条关注值结果
                foreach (var r in g.Results)
                {
                    r.CalcValue = 0;
                    r.FocusMode = "无";
                    r.FocusedDelta = 0;
                    r.FocusedMin = null;
                    r.FocusedMax = null;
                    r.PreviousCalcValue = 0;
                }
                OnGroupUpdated?.Invoke(g);
            }
        }

        /// <summary>
        /// 同步所有匹配规则的统计组的 Results 数量（UI 线程调用，因 ObservableCollection 限制）
        /// </summary>
        public void SyncGroupsResultCount(string ruleName, int targetCount)
        {
            var groups = _groupCache.Values.Where(g => g.RuleName == ruleName).ToList();
            foreach (var g in groups)
            {
                while (g.Results.Count < targetCount)
                    g.Results.Add(new CalcResult());
                while (g.Results.Count > targetCount)
                    g.Results.RemoveAt(g.Results.Count - 1);
            }
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private static bool SpanEqual(ReadOnlySpan<byte> a, byte[] b)
        {
            for (int i = 0; i < a.Length; i++)
                if (a[i] != b[i]) return false;
            return true;
        }

        /// <summary>12 字节 → "AB·CD·EF·01·23·45·67·89·AB·CD·EF·01" (35 chars)</summary>
        private static string BytesToHex12(ReadOnlySpan<byte> bytes)
        {
            Span<char> chars = stackalloc char[35];
            for (int i = 0; i < 12; i++)
            {
                byte b = bytes[i];
                int pos = i * 3;
                chars[pos] = HexLookup[b >> 4];
                chars[pos + 1] = HexLookup[b & 0x0F];
                if (i < 11) chars[pos + 2] = '·';
            }
            return new string(chars);
        }
    }
}
