# CANDebugTool 已知问题清单

> 审查日期: 2026-05-20
> 策略: 记录备查，待测出异常时再处理

---

## 严重

### S1. byte[] setter 绕过 SetProperty，属性变更通知时序异常

- **文件**: `Models/CanClassification.cs:42-43, 69-70, 108-109, 141-142, 395-396`
- **现象**: `IdMaskHex`/`DataMaskHex`/`IdRefHex`/`DataRefHex`/`CalcValueConfig.DataMaskHex` 的 setter 直接赋值 `_idMask = bytes` 绕过 `SetProperty`，导致 WPF 绑定在某些编辑路径下不刷新
- **影响范围**: UI 编辑掩码/参考值时，部分场景下控件可能不更新

### S2. Classify() 竞态条件

- **文件**: `ViewModels/StatisticsViewModel.cs:64-68`
- **现象**: `_activeRulesDirty` 从接收线程读、UI 线程写，无 volatile/Interlocked/锁；`_activeRules` 替换无线程同步
- **影响范围**: 高频接收时可能读到脏数据或空列表

### S3. StatisticsGroup 属性无线程同步

- **文件**: `Services/ClassificationService.cs:96-180`
- **现象**: 接收线程写入 `group.Count`/`group.TimeDiff` 等属性，UI 线程同时读取，存在撕裂读风险
- **影响范围**: 统计数据显示偶发异常值

### S4. FlushGroupUpdates 整体替换对象导致 DataGrid 选中丢失

- **文件**: `ViewModels/StatisticsViewModel.cs:118`
- **现象**: `Groups[idx] = updated` 替换整个对象，每次刷新丢失 DataGrid 选中状态并引起闪烁
- **影响范围**: 统计表格无法稳定选中行

### S5. RxCount 属性变更通知在非 UI 线程触发

- **文件**: `ViewModels/MainViewModel.cs:173`
- **现象**: `RxCount += batch.Count` 在调用线程触发 `OnPropertyChanged`，违反 WPF 线程要求
- **影响范围**: 可能导致 RX 计数显示不更新或抛异常

---

## 中等

### M1. 事件订阅未取消，内存泄漏

- **文件**: `MainViewModel.cs:92-94`, `ReceiveViewModel.cs:31`, `StatisticsViewModel.cs:57`
- **现象**: 订阅了单例事件/CollectionChanged/PropertyChanged 但从未取消
- **影响范围**: 长时间运行后内存缓慢增长

### M2. MainViewModel.Dispose() 不完整

- **文件**: `ViewModels/MainViewModel.cs:281`
- **现象**: 只停了扫描定时器，未取消事件订阅、未释放 StorageService、未停止周期发送
- **影响范围**: 应用退出时可能有资源残留

### M3. WorkspaceViewModel.LoadWorkspace 不加载已有配置

- **文件**: `ViewModels/WorkspaceViewModel.cs:38-50`
- **现象**: 加载工作区时只创建新 `WorkspaceConfig`，不读取磁盘已有的 config.json 或 mask_rules.json
- **影响范围**: 重新打开已有工作区时配置丢失（需手动点"加载规则"）

### M4. ReceiveViewModel.UpdateDisplay 每次添加消息都重建整个列表

- **文件**: `ViewModels/ReceiveViewModel.cs:35-56`
- **现象**: 每条消息触发 Clear + 重新 AddAll，高频接收时性能极差
- **影响范围**: 高波特率下报文过滤面板卡顿

### M5. CanDeviceService.Dispose() 破坏单例

- **文件**: `Services/CanDeviceService.cs:389-391`
- **现象**: `_instance = null` 后再访问 Instance 会创建无事件订阅的新实例，导致静默失败
- **影响范围**: 断开再连接设备后可能收不到回调

### M6. CanMessage._globalSequence 重置与 Interlocked.Increment 竞态

- **文件**: `ViewModels/MainViewModel.cs:233`
- **现象**: 简单赋值 `_globalSequence = 0` 与接收线程的 `Interlocked.Increment` 并发
- **影响范围**: 启动捕获瞬间序号可能不从 0 开始

---

## 低

### L1. WorkspaceConfig List<T> 属性不触发 INotifyPropertyChanged

- **文件**: `Models/WorkspaceConfig.cs:18-27`
- **现象**: 自动 setter 不触发通知

### L2. VCI_CAN_OBJ Data=null! 可能导致 Marshal 异常

- **文件**: `Native/Structs.cs:29`
- **现象**: initData:false 路径 Data 设为 null，与 SizeConst=8 的 MarshalAs 不兼容

### L3. CanMessage.DataHex 不处理 DataLen > Data.Length

- **文件**: `Models/CanMessage.cs:61`
- **现象**: 若 Data 数组短于 DataLen 会越界

### L4. SendViewModel.StopPeriodicSend 不等待任务完成

- **文件**: `ViewModels/SendViewModel.cs:120`
- **现象**: Cancel 后立即置 null，旧任务仍在运行

### L5. 发送/接收消息时间戳计算方式不同

- **文件**: `Models/CanMessage.cs:102`
- **现象**: 发送用 `DateTime.Now.Ticks/10`（绝对），接收用硬件时间戳（相对），不可比较

### L6. LogService 每次调用 File.AppendAllText

- **文件**: `Services/LogService.cs:38`
- **现象**: 高频场景 I/O 开销大

### L7. HexInput_TextChanged 每次按键编译正则

- **文件**: `Views/MainWindow.xaml.cs:42`
- **现象**: 可预编译优化

### L8. CanConfig.Btr0/Btr1 声明但从未使用

- **文件**: `Models/CanConfig.cs:19-23`
- **现象**: ToVciConfig() 总是重新计算，用户手动设置值被忽略
