# CAN 调试工具 - 项目规划

## 1. 项目概述

**项目名称**: CANDebugTool
**技术栈**: C# + WPF + .NET 8.0
**目标**: 开发一个运行在 Win11 上的 CAN 总线调试工具

### 核心功能
- 设备连接/断开管理
- CAN 通道配置（波特率、滤波、模式）
- CAN 报文发送
- CAN 报文接收与显示
- 报文数据解析
- 发送历史记录
- 帧过滤与搜索

---

## 2. 项目结构

```
CANDebugTool/
├── CANDebugTool.sln                 # 解决方案文件
│
├── src/
│   └── CANDebugTool/
│       ├── CANDebugTool.csproj      # 项目文件
│       │
│       ├── App.xaml                 # 应用程序入口
│       ├── App.xaml.cs
│       │
│       ├── Views/                   # 视图层
│       │   ├── MainWindow.xaml      # 主窗口
│       │   ├── MainWindow.xaml.cs
│       │   ├── DevicePanel.xaml     # 设备连接面板
│       │   ├── CanConfigPanel.xaml  # CAN配置面板
│       │   ├── SendPanel.xaml       # 发送面板
│       │   └── ReceivePanel.xaml    # 接收面板
│       │
│       ├── ViewModels/              # 视图模型层 (MVVM)
│       │   ├── MainViewModel.cs
│       │   ├── DeviceViewModel.cs
│       │   ├── CanConfigViewModel.cs
│       │   ├── SendViewModel.cs
│       │   └── ReceiveViewModel.cs
│       │
│       ├── Models/                  # 数据模型层
│       │   ├── CanMessage.cs        # CAN 报文模型
│       │   ├── CanConfig.cs         # CAN 配置模型
│       │   └── DeviceInfo.cs        # 设备信息模型
│       │
│       ├── Services/                # 服务层
│       │   ├── CanDeviceService.cs  # CAN 设备服务（封装 DLL 调用）
│       │   ├── MessageParser.cs     # 报文解析服务
│       │   └── LogService.cs        # 日志服务
│       │
│       ├── Native/                  # 原生互操作层
│       │   ├── ControlCanApi.cs     # ControlCAN.dll API 声明
│       │   ├── NativeMethods.cs     # DllImport 封装
│       │   └── Structs.cs          # 结构体定义
│       │
│       ├── Converters/             # 值转换器
│       │   └── ByteArrayToHexConverter.cs
│       │
│       └── Resources/               # 资源文件
│           ├── Styles.xaml          # 样式定义
│           └── Icons/               # 图标资源
│
├── lib/                             # 第三方库
│   └── ControlCAN.dll              # 周立功 CAN 接口库
│
└── docs/                            # 文档
    └── API_reference.md             # API 参考文档
```

---

## 3. 核心模块设计

### 3.1 Native 层（底层封装）

基于接口函数库文档，封装 ControlCAN.dll：

```csharp
// 设备类型定义
public const int VCI_USBCAN = 3;  // USB-CAN 适配器

// 核心 API
VCI_OpenDevice(int DeviceType, int DeviceInd, int Reserved);
VCI_CloseDevice(int DeviceType, int DeviceInd);
VCI_InitCAN(int DeviceType, int DeviceInd, int CANInd, ref VCI_INIT_CONFIG pInitConfig);
VCI_StartCAN(int DeviceType, int DeviceInd, int CANInd);
VCI_ResetCAN(int DeviceType, int DeviceInd, int CANInd);
VCI_Transmit(int DeviceType, int DeviceInd, int CANInd, ref VCI_CAN_OBJ pSend, int Len);
VCI_Receive(int DeviceType, int DeviceInd, int CANInd, ref VCI_CAN_OBJ pReceive, int Len, int WaitTime);
```

### 3.2 Services 层（业务逻辑）

| 服务类 | 职责 |
|--------|------|
| CanDeviceService | 设备连接、初始化、数据收发 |
| MessageParser | 报文格式转换（Hex/ASCII/DEC） |
| LogService | 操作日志、报文存储 |

### 3.3 ViewModels 层（MVVM）

| ViewModel | 绑定视图 | 职责 |
|-----------|----------|------|
| MainViewModel | MainWindow | 主窗口状态协调 |
| DeviceViewModel | DevicePanel | 设备列表、连接状态 |
| CanConfigViewModel | CanConfigPanel | 波特率、滤波配置 |
| SendViewModel | SendPanel | 发送队列、发送历史 |
| ReceiveViewModel | ReceivePanel | 接收显示、过滤、搜索 |

### 3.4 Views 层（UI）

```
┌─────────────────────────────────────────────────────────────┐
│  CAN 调试工具                                      [_][□][X] │
├─────────────────────────────────────────────────────────────┤
│ ┌─────────────┐ ┌─────────────┐ ┌─────────────────────────┐│
│ │ 设备连接     │ │ CAN 配置     │ │ [连接] [断开] [启动] [停止]││
│ └─────────────┘ └─────────────┘ └─────────────────────────┘│
├─────────────────────────────────────────────────────────────┤
│ ┌───────────────────────────┐ ┌───────────────────────────┐│
│ │       发送面板            │ │       接收面板              ││
│ │ ┌─────────────────────┐  │ │ │ ID      │ 数据        │ 时戳 ││
│ │ │ ID: [________] DLC:[_]│  │ │ │ 0x123   │ 01 02 03 04│ 100  ││
│ │ │ 数据: [_____________] │  │ │ │ 0x456   │ 05 06      │ 105  ││
│ │ │ [标准帧] [扩展帧]      │  │ │ │ ...     │ ...        │ ...  ││
│ │ │ [发送] [周期发送]      │  │ │ └───────────────────────────┘│
│ │ └─────────────────────┘  │ │ │ [清空] [保存] [过滤:______] ││
│ └───────────────────────────┘ └───────────────────────────┘│
├─────────────────────────────────────────────────────────────┤
│ 状态栏: 设备: 已连接 | 通道1: 运行中 | 接收: 1234 发送: 567  │
└─────────────────────────────────────────────────────────────┘
```

---

## 4. 依赖项配置

```xml
<!-- CANDebugTool.csproj -->
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net8.0-windows</TargetFramework>
    <Nullable>enable</Nullable>
    <UseWPF>true</UseWPF>
    <ApplicationIcon>Resources\app.ico</ApplicationIcon>
    <AssemblyName>CANDebugTool</AssemblyName>
    <Version>1.0.0</Version>
  </PropertyGroup>

  <ItemGroup>
    <!-- CommunityToolkit.Mvvm: MVVM 框架 -->
    <PackageReference Include="CommunityToolkit.Mvvm" Version="8.2.2" />
  </ItemGroup>

  <ItemGroup>
    <!-- 复制 DLL 到输出目录 -->
    <None Include="..\lib\ControlCAN.dll" Link="ControlCAN.dll">
      <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
    </None>
  </ItemGroup>
</Project>
```

---

## 5. 波特率配置对照表

| 波特率 | BTR0:BTR1 (十六进制) |
|--------|---------------------|
| 5Kbps  | 0xBF:0xFF |
| 10Kbps | 0xBF:0x7F |
| 20Kbps | 0xBF:0x3F |
| 50Kbps | 0xBF:0x1F |
| 100Kbps | 0x01:0x1C |
| 125Kbps | 0x01:0x14 |
| 250Kbps | 0x01:0x0C |
| 500Kbps | 0x01:0x06 |
| 800Kbps | 0x00:0xD6 |
| 1Mbps   | 0x00:0x14 |

---

## 6. 开发阶段划分

| 阶段 | 内容 | 优先级 |
|------|------|--------|
| **Phase 1** | 项目搭建、DLL 封装、设备连接/断开 | P0 |
| **Phase 2** | CAN 配置、通道初始化、启动/停止 | P0 |
| **Phase 3** | 报文发送功能（单帧、周期） | P0 |
| **Phase 4** | 报文接收、实时显示 | P0 |
| **Phase 5** | UI 美化、数据过滤、历史记录 | P1 |
| **Phase 6** | 报文解析（协议解析）、数据导出 | P2 |

---

## 7. 技术要点

### 7.1 DllImport 声明
```csharp
[DllImport("ControlCAN.dll", CharSet = CharSet.Ansi)]
public static extern int VCI_OpenDevice(int DeviceType, int DeviceInd, int Reserved);
```

### 7.2 结构体对齐
使用 `StructLayout` 确保与 C 语言结构体对齐：
```csharp
[StructLayout(LayoutKind.Sequential)]
public struct VCI_CAN_OBJ
{
    public uint ID;      // 帧 ID
    public uint TimeStamp; // 时间戳
    public byte TimeFlag; // 是否使用时间戳
    public byte SendType; // 发送类型
    public byte RemoteFlag; // 远程帧
    public byte ExternFlag; // 扩展帧
    public byte DataLen;  // 数据长度
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
    public byte[] Data;   // 数据
}
```

### 7.3 线程安全
- 接收使用独立线程
- UI 更新使用 `Dispatcher.Invoke`
- 使用 `BlockingCollection` 缓存报文

---

## 8. 后续扩展方向

- [ ] 多设备同时连接
- [ ] CANopen 协议解析
- [ ] J1939 协议解析
- [ ] 报文回放功能
- [ ] 自动化测试脚本
- [ ] 数据统计分析
