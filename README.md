# ZJS2310.Next

此仓库管理 `ZJS2310.Next/` 下的自动测试桌面应用、核心领域逻辑与测试程序。

## 构建与测试

```powershell
dotnet build ZJS2310.Next/ZJS2310.Next.sln -c Release
dotnet run --project ZJS2310.Next/tests/ZJS2310.Core.Tests/ZJS2310.Core.Tests.csproj -c Release
```

架构说明位于 `ZJS2310.Next/docs/ARCHITECTURE.md`，Codex 协作约定见 `AGENTS.md`。
