# ZJS2310.Next

这是根据旧版 `ZJS2310` 重新设计的 C# / WinForms 光学测试程序。新程序使用 .NET 8，不依赖旧项目的 SunnyUI、NPOI、HslCommunication 或本机 NuGet `packages` 目录。

## 快速开始

1. 使用 Visual Studio 2022 打开 `ZJS2310.Next.sln`，或在当前目录执行：

   ```powershell
   dotnet build .\ZJS2310.Next.sln -c Release
   dotnet run --project .\src\ZJS2310.App\ZJS2310.App.csproj
   ```

2. 测量服务默认地址为 `127.0.0.1:5555`。在“系统 → 连接与设备设置”中修改。
3. 默认管理员是 `admin`，初始密码为 `1234`。首次使用后请在“管理 → 用户管理”中重置。
4. 测试画面放入 `Assets\Patterns`。默认关闭桌面壁纸切换和串口控制，因此没有接硬件时也能安全启动程序。
5. 测试记录以 UTF-8 CSV 写入 `Data\Exports`，可直接用 Excel 打开；日志写入 `Data\Logs`。

## 旧配方迁移

在“连接与设备设置”中选择旧版 `TestType.ini` 并保存，程序会立即导入其中的配方；同名配方以导入内容更新。导入按指标键名匹配，不依赖 INI 条目的物理顺序；非法范围（例如最小值大于最大值）会跳过并记入日志。首次启动时若配置文件中已经指定了旧 INI，也会自动导入。

## 项目结构

- `src/ZJS2310.Core`：领域模型、结果协议、阈值判定、测试状态机和抽象接口。
- `src/ZJS2310.App/Infrastructure`：TCP、串口、投影、JSON 配置、用户认证、CSV 与日志。
- `src/ZJS2310.App/UI`：完全由 C# 构建的 WinForms 操作界面和管理对话框。
- `tests/ZJS2310.Core.Tests`：不依赖第三方测试框架的核心回归测试。
- `docs`：缺陷梳理和架构说明。

## 注意

真实仪器联调前，需要确认测量服务仍以 `,%` 作为结果结束标记，并核对 `TestCatalog.cs` 中各项结果下标与当前仪器协议版本一致。旧代码对 t1、t3、t14 的展示下标与判定下标互相矛盾；新程序统一采用旧判定逻辑所使用的下标，并在缺少数据时明确报错，不再用 `0` 静默代替。
