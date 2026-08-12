# 架构说明

测试主流程如下：

```text
MainForm
  └─ TestSequenceService
       ├─ IProjectionController  准备测试画面/可选串口脉冲
       ├─ IMeasurementClient     发送 tN 并等待同编号结果
       ├─ MeasurementEvaluator   按指标键和阈值判定
       └─ IResultExporter        将完成、失败或取消的运行统一落盘
```

## Core

`ZJS2310.Core` 不引用 WinForms 或 Windows API，因此协议与业务可以独立测试。

- `TestCatalog` 是测试代码、名称、画面和结果指标下标的单一来源。
- `ResultPacketParser` 只负责把一条完整文本结果变成数值包。
- `ProtocolMessageFramer` 处理 TCP 没有消息边界的问题。
- `MeasurementEvaluator` 把数值包与配方阈值组合成可追踪的指标判定。
- `TestSequenceService` 是唯一的测试状态机，不使用轮询计时器或共享布尔标记。

## Infrastructure

- `TcpMeasurementClient`：一次只允许一个在途测试请求，非预期编号不会误唤醒当前步骤。
- `ProjectionController`：桌面壁纸、显示拓扑和串口脉冲均为可选能力。
- `NativeSerialLink`：直接使用 Windows 通信 API，因此不需要额外的 `System.IO.Ports` 包。
- `JsonRecipeRepository`：保存稳定的指标键，并能在首次运行时导入旧 INI。
- `CsvResultExporter`：无第三方依赖，失败/取消的测试也保留诊断记录。
- `AuthenticationService`：用户名不区分大小写，密码只存储 PBKDF2 派生值。

## UI

WinForms 只订阅流程事件并呈现状态。设备回调不直接操作控件，所有界面更新都回送到 UI 线程。窗体没有 `.Designer.cs` 依赖，便于代码审查和后续重排。

## 仪器协议变更点

如果仪器返回字段发生变化，只修改 `TestCatalog` 中对应 `MetricDefinition.ValueIndex`；如果消息格式变化，只替换 `ResultPacketParser` / `ProtocolMessageFramer`。测试状态机、配方文件和界面不需要一起修改。
