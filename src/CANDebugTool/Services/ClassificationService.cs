using System;
using System.Collections.Generic;
using CANDebugTool.Models;

namespace CANDebugTool.Services
{
    /// <summary>
    /// 归类/统计/计算服务（后续实现完整逻辑）
    /// </summary>
    public class ClassificationService
    {
        private static ClassificationService? _instance;
        public static ClassificationService Instance => _instance ??= new ClassificationService();

        private readonly Dictionary<string, StatisticsGroup> _groupCache = new();

        public event Action<StatisticsGroup>? OnGroupUpdated;

        private ClassificationService() { }

        /// <summary>
        /// 对报文执行归类（占位实现）
        /// </summary>
        public StatisticsGroup? Classify(CanMessage msg, List<ClassificationRule> rules)
        {
            // Apply mask, compute classify code, update group
            // TODO: full implementation
            return null;
        }

        /// <summary>
        /// 计算统计组数值
        /// </summary>
        public double CalcValue(StatisticsGroup group, CanMessage msg)
        {
            // TODO: 根据计算规则从msg中提取数值
            return 0;
        }
    }
}
