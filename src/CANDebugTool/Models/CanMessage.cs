using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace CANDebugTool.Models
{
    public partial class CanMessage : ObservableObject
    {
        [ObservableProperty]
        private DateTime _timestamp = DateTime.Now;

        [ObservableProperty]
        private uint _id;

        [ObservableProperty]
        private bool _isExtended;

        [ObservableProperty]
        private bool _isRemote;

        [ObservableProperty]
        private byte _dataLen;

        [ObservableProperty]
        private byte[] _data = new byte[8];

        [ObservableProperty]
        private uint _timeStampValue;

        [ObservableProperty]
        private bool _isTransmit;

        // === 高级功能扩展字段 ===

        /// <summary>序号</summary>
        [ObservableProperty]
        private long _sequenceNumber;

        /// <summary>微秒级时间戳</summary>
        [ObservableProperty]
        private long _timestampUs;

        /// <summary>归类码 (12字节: ID 4 + Data 8)</summary>
        [ObservableProperty]
        private byte[] _classifyCode = new byte[12] { 0xFF,0xFF,0xFF,0xFF,0xFF,0xFF,0xFF,0xFF,0xFF,0xFF,0xFF,0xFF };

        /// <summary>归类码 Hex 显示</summary>
        [ObservableProperty]
        private string _classifyCodeHex = "FF·FF·FF·FF·FF·FF·FF·FF·FF·FF·FF·FF";

        /// <summary>统计组号</summary>
        [ObservableProperty]
        private int _groupId = -1;

        public string IdDisplay => IsExtended ? $"{Id:X8}" : $"{Id:X3}";

        public string DataHex
        {
            get
            {
                var sb = new System.Text.StringBuilder();
                for (int i = 0; i < DataLen; i++)
                    sb.Append($"{Data[i]:X2} ");
                return sb.ToString().TrimEnd();
            }
        }

        public string FrameType => IsRemote ? "RTR" : "DATA";
        public string Direction => IsTransmit ? "TX" : "RX";
        public string TimeDisplay => $"{(double)TimeStampValue / 1000.0:F3}";

        /// <summary>方向显示（中文）</summary>
        public string DirectionCn => IsTransmit ? "发送" : "接收";

        /// <summary>帧类型显示（中文）</summary>
        public string FrameTypeCn => IsExtended ? "扩展" : "标准";

        public static long _globalSequence = 0;

        public static CanMessage FromVciObj(Native.VCI_CAN_OBJ obj, bool isTransmit = false)
        {
            // VCI_CAN_OBJ.TimeStamp 是 0.1ms 单位，转换为 μs: *100
            long hwTimestampUs = (long)obj.TimeStamp * 100;

            return new CanMessage
            {
                Id = obj.ID,
                IsExtended = obj.ExternFlag == 1,
                IsRemote = obj.RemoteFlag == 1,
                DataLen = obj.DataLen,
                Data = (byte[])obj.Data.Clone(),
                TimeStampValue = obj.TimeStamp,
                IsTransmit = isTransmit,
                Timestamp = DateTime.Now,
                TimestampUs = hwTimestampUs,
                SequenceNumber = System.Threading.Interlocked.Increment(ref _globalSequence)
            };
        }

        public static CanMessage CreateTransmit(uint id, byte[] data, bool isExtended = false)
        {
            // 发送报文没有硬件时间戳，使用系统时间（μs）
            long nowUs = DateTime.Now.Ticks / 10;

            return new CanMessage
            {
                Id = id,
                IsExtended = isExtended,
                IsRemote = false,
                DataLen = (byte)(data?.Length ?? 0),
                Data = data ?? new byte[8],
                IsTransmit = true,
                Timestamp = DateTime.Now,
                TimestampUs = nowUs,
                SequenceNumber = System.Threading.Interlocked.Increment(ref _globalSequence)
            };
        }
    }
}
