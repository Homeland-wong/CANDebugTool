using System.Runtime.InteropServices;

namespace CANDebugTool.Native
{
    #region VCI_CAN_OBJ - CAN 报文结构体

    [StructLayout(LayoutKind.Sequential)]
    public struct VCI_CAN_OBJ
    {
        public uint ID;
        public uint TimeStamp;
        public byte TimeFlag;
        public byte SendType;
        public byte RemoteFlag;
        public byte ExternFlag;
        public byte DataLen;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
        public byte[] Data;

        public VCI_CAN_OBJ(bool initData = true)
        {
            ID = 0;
            TimeStamp = 0;
            TimeFlag = 0;
            SendType = 0;
            RemoteFlag = 0;
            ExternFlag = 0;
            DataLen = 8;
            Data = initData ? new byte[8] : null!;
        }
    }

    #endregion

    #region VCI_INIT_CONFIG - CAN 初始化配置结构体

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi, Pack = 1)]
    public struct VCI_INIT_CONFIG
    {
        public uint AccCode;       // 验收码
        public uint AccMask;      // 验收屏蔽码
        public uint Reserved;      // 保留 (DWORD)
        public byte Filter;        // 滤波方式
        public byte Timing0;      // BTR0
        public byte Timing1;      // BTR1
        public byte Mode;          // 模式

        public VCI_INIT_CONFIG(bool init = true)
        {
            AccCode = 0x00000000;
            AccMask = 0xFFFFFFFF;
            Reserved = 0;
            Filter = 0;
            Timing0 = 0x00;   // 500Kbps default
            Timing1 = 0x1C;
            Mode = 0x00;
        }
    }

    #endregion

    #region VCI_BOARD_INFO - 设备信息结构体

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi, Pack = 1)]
    public struct VCI_BOARD_INFO
    {
        public ushort hw_Version;
        public ushort fw_Version;
        public ushort dr_Version;
        public ushort in_Version;
        public ushort irq_Num;
        public byte can_Num;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 20)]
        public byte[] str_Serial_Num;   // 用 byte[] 替代 string，避免封送问题
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 40)]
        public byte[] str_hw_Type;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
        public ushort[] Reserved;       // 必须！DLL写入会越界
    }

    #endregion
}
