# Codex 协作说明

## 项目入口

- 代码位于 `ZJS2310.Next/`。
- 解决方案：`ZJS2310.Next/ZJS2310.Next.sln`
- 构建：`dotnet build ZJS2310.Next/ZJS2310.Next.sln -c Release`
- 测试：`dotnet run --project ZJS2310.Next/tests/ZJS2310.Core.Tests/ZJS2310.Core.Tests.csproj -c Release`

## 修改约定

- `main` 保持可构建；日常修改默认使用 `codex/<任务名>` 分支。
- 领域与流程逻辑优先放在 `ZJS2310.Core`，WinForms 与设备适配放在 `ZJS2310.App`。
- 不提交 `Data` 下的用户、设置、日志和导出结果，也不提交构建物、本机配置或密钥。
- 提交前运行 Release 构建和 Core Tests；串口、网络、投影及真实仪器需单独说明验证范围。

