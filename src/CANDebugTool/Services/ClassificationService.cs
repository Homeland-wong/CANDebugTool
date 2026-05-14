using System;
using System.Collections.Generic;
using System.Linq;
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

        private readonly Dictionary<string, StatisticsGroup> _groupCache = new();
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
        /// </summary>
        /// <param name="msg">报文</param>
        /// <param name="rules">启用的掩码规则列表</param>
        /// <returns>匹配的统计组，无规则返回 null</returns>
        public StatisticsGroup? Classify(CanMessage msg, List<ClassificationRule> rules)
        {
            // 获取 ID 的 4 字节表示
            byte[] idBytes = new byte[4];
            idBytes[0] = (byte)((msg.Id >> 24) & 0xFF);
            idBytes[1] = (byte)((msg.Id >> 16) & 0xFF);
            idBytes[2] = (byte)((msg.Id >> 8) & 0xFF);
            idBytes[3] = (byte)(msg.Id & 0xFF);

            // 获取 Data（确保8字节）
            byte[] msgData = new byte[8];
            if (msg.Data != null)
            {
                int copyLen = Math.Min(msg.Data.Length, 8);
                Array.Copy(msg.Data, msgData, copyLen);
            }

            // 遍历所有启用的规则，取第一条匹配的
            foreach (var rule in rules)
            {
                // ── ID 匹配 ──
                // in1 = id & mask,  then  in1 op ref1
                byte[] in1 = new byte[4];
                for (int i = 0; i < 4; i++)
                    in1[i] = (byte)(idBytes[i] & rule.IdMask[i]);

                bool idMatch = rule.IdOpEquals
                    ? BytesEqual(in1, rule.IdRef)
                    : !BytesEqual(in1, rule.IdRef);

                if (!idMatch) continue;

                // ── Data 匹配 ──
                // in2 = data & mask,  then  in2 op ref2
                byte[] in2 = new byte[8];
                for (int i = 0; i < 8; i++)
                    in2[i] = (byte)(msgData[i] & rule.DataMask[i]);

                bool dataMatch = rule.DataOpEquals
                    ? BytesEqual(in2, rule.DataRef)
                    : !BytesEqual(in2, rule.DataRef);

                if (!dataMatch) continue;

                // 计算 12 字节归类码（复用已算好的 in1/in2）
                byte[] codeBytes = new byte[12];
                for (int i = 0; i < 4; i++)
                    codeBytes[i] = in1[i];
                for (int i = 0; i < 8; i++)
                    codeBytes[4 + i] = in2[i];

                string codeHex = BytesToHex(codeBytes);

                // 查找或创建统计组
                if (!_groupCache.TryGetValue(codeHex, out var group))
                {
                    group = new StatisticsGroup
                    {
                        GroupId = _nextGroupId++,
                        RuleName = rule.Name,
                        ClassifyCode = codeHex,
                        Count = 0
                    };
                    _groupCache[codeHex] = group;
                }

                // 更新统计
                group.Count++;

                // 计算时间差（首帧跳过，Count>1 才有上一条可比较）
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

                // ── 关注值计算 ──
                var calcCfg = rule.CalcConfigs.FirstOrDefault();
                if (calcCfg != null && calcCfg.FfCount > 0)
                {
                    double val = ExtractFocusedValue(msg, calcCfg);

                    if (calcCfg.FocusMode == "增量")
                    {
                        group.CalcValue = val;
                        if (group.Count > 1)
                        {
                            group.FocusedDelta = val - group.PreviousCalcValue;
                        }
                        group.PreviousCalcValue = val;
                    }
                    else if (calcCfg.FocusMode == "跳动")
                    {
                        group.CalcValue = val;
                        if (group.FocusedMin == null)
                        {
                            group.FocusedMin = val;
                            group.FocusedMax = val;
                        }
                        else
                        {
                            if (val < group.FocusedMin) group.FocusedMin = val;
                            if (val > group.FocusedMax) group.FocusedMax = val;
                        }
                    }
                }

                // 更新报文上的归类信息
                msg.ClassifyCode = codeBytes;
                msg.ClassifyCodeHex = codeHex;
                msg.GroupId = group.GroupId;

                OnGroupUpdated?.Invoke(group);
                return group;
            }

            // 无规则匹配时，归类码置为全 0
            for (int i = 0; i < 12; i++)
                msg.ClassifyCode[i] = 0;
            msg.ClassifyCodeHex = "00·00·00·00·00·00·00·00·00·00·00·00";
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

            byte[] slice = new byte[ffLen];
            Array.Copy(data, ffStart, slice, 0, ffLen);

            if (!cfg.IsBigEndian) Array.Reverse(slice);

            int ff = ffLen;
            string pt = cfg.PropertyType;

            if (pt == "hex") return 0;
            if (ff == 1) return pt == "8S" ? (sbyte)slice[0] : slice[0];
            if (ff == 2)
            {
                ushort u16 = (ushort)(slice[0] << 8 | slice[1]);
                return pt == "16S" ? (short)u16 : u16;
            }
            if (ff == 4)
            {
                if (pt == "flt") return BitConverter.ToSingle(slice, 0);
                uint u32 = (uint)(slice[0] << 24 | slice[1] << 16 | slice[2] << 8 | slice[3]);
                return pt == "32S" ? (int)u32 : u32;
            }
            if (ff == 8)
            {
                if (pt == "dbl") return BitConverter.ToDouble(slice, 0);
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
                g.FocusedDelta = 0;
                g.FocusedMin = null;
                g.FocusedMax = null;
                g.PreviousTimestampUs = 0;
                g.PreviousCalcValue = 0;
                OnGroupUpdated?.Invoke(g);
            }
        }

        private static bool BytesEqual(byte[] a, byte[] b)
        {
            for (int i = 0; i < a.Length; i++)
                if (a[i] != b[i]) return false;
            return true;
        }

        private static string BytesToHex(byte[] bytes)
        {
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < bytes.Length; i++)
            {
                if (i > 0) sb.Append('·');
                sb.Append($"{bytes[i]:X2}");
            }
            return sb.ToString();
        }
    }
}
