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
            foreach (var rule in rules.Where(r => r.Enabled))
            {
                // 计算 12 字节归类码
                byte[] codeBytes = new byte[12];
                for (int i = 0; i < 4; i++)
                    codeBytes[i] = (byte)(idBytes[i] & rule.IdMask[i]);
                for (int i = 0; i < 8; i++)
                    codeBytes[4 + i] = (byte)(msgData[i] & rule.DataMask[i]);

                string codeHex = BytesToHex(codeBytes);

                // 查找或创建统计组
                if (!_groupCache.TryGetValue(codeHex, out var group))
                {
                    group = new StatisticsGroup
                    {
                        GroupId = _nextGroupId++,
                        ClassifyCode = codeHex,
                        Count = 0,
                        PreviousTimestampUs = msg.TimestampUs
                    };
                    _groupCache[codeHex] = group;
                }

                // 更新统计
                group.Count++;

                // 计算时间差
                group.TimeDiff = msg.TimestampUs - group.PreviousTimestampUs;
                group.PreviousTimestampUs = msg.TimestampUs;

                // 更新报文上的归类信息
                msg.ClassifyCode = codeBytes;
                msg.ClassifyCodeHex = codeHex;
                msg.GroupId = group.GroupId;

                OnGroupUpdated?.Invoke(group);
                return group;
            }

            // 无规则匹配时，归类码保持默认
            return null;
        }

        /// <summary>
        /// 计算统计组数值
        /// </summary>
        public double CalcGroupValue(StatisticsGroup group, CanMessage msg)
        {
            if (group.CalcRule == 0) return group.Count;

            // 从 Data 中提取数值（根据 calcRule）
            byte[] data = msg.Data ?? new byte[8];
            int sb = 0;

            if (group.CalcRule == 1 && sb + 2 <= data.Length)
                return (short)(data[sb] << 8 | data[sb + 1]);
            if (group.CalcRule == 2 && sb + 2 <= data.Length)
                return data[sb] << 8 | data[sb + 1];
            if (group.CalcRule == 3 && sb + 4 <= data.Length)
                return data[sb] << 24 | data[sb + 1] << 16 | data[sb + 2] << 8 | data[sb + 3];
            if (group.CalcRule == 4 && sb + 4 <= data.Length)
                return (uint)(data[sb] << 24 | data[sb + 1] << 16 | data[sb + 2] << 8 | data[sb + 3]);
            if (group.CalcRule == 5 && sb + 4 <= data.Length)
                return BitConverter.ToSingle(data, sb);

            return 0;
        }

        /// <summary>
        /// 获取所有统计组的快照
        /// </summary>
        public List<StatisticsGroup> GetAllGroups() => _groupCache.Values.ToList();

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
