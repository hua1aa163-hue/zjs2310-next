using ZJS2310.App.Infrastructure;
using ZJS2310.Core.Abstractions;
using ZJS2310.Core.Domain;
using ZJS2310.Core.Services;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace ZJS2310.Core.Tests;

internal static class Program
{
    private static async Task<int> Main()
    {
        var tests = new (string Name, Func<Task> Run)[]
        {
            ("RangeSpec 支持开区间边界", RangeSpecSupportsOpenBounds),
            ("结果解析使用固定文化并拒绝脏数据", ResultParserIsStrict),
            ("TCP 消息分帧支持拆包和粘包", FramerSupportsSplitAndCombinedPackets),
            ("指标判定按键名和定义下标执行", EvaluatorUsesDefinitions),
            ("测试状态机按顺序发送并最终保存", SequenceRunsInOrder),
            ("用户凭据可以安全持久化后重新读取", AuthenticationPersists),
            ("旧 INI 配方按键名导入", LegacyRecipeImportsByKey),
            ("设置、配方和 CSV 可以持久化", StoresPersist),
            ("TCP 客户端可以接收真实拆包结果", TcpClientHandlesFragmentedResult)
        };

        var failed = 0;
        foreach (var test in tests)
        {
            try
            {
                await test.Run();
                Console.WriteLine($"PASS  {test.Name}");
            }
            catch (Exception exception)
            {
                failed++;
                Console.Error.WriteLine($"FAIL  {test.Name}: {exception.Message}");
            }
        }

        Console.WriteLine($"共 {tests.Length} 项，失败 {failed} 项。");
        return failed == 0 ? 0 : 1;
    }

    private static Task RangeSpecSupportsOpenBounds()
    {
        Assert(RangeSpec.TryParseLegacy("1.5&", out var lower), "应解析仅下限范围");
        Assert(lower.Contains(1.5) && lower.Contains(100) && !lower.Contains(1.49), "下限判定错误");
        Assert(RangeSpec.TryParseLegacy("&2.5", out var upper), "应解析仅上限范围");
        Assert(upper.Contains(-100) && upper.Contains(2.5) && !upper.Contains(2.51), "上限判定错误");
        Assert(!RangeSpec.TryParseLegacy("5&2", out _), "反向范围必须被拒绝");
        return Task.CompletedTask;
    }

    private static Task ResultParserIsStrict()
    {
        var parser = new ResultPacketParser();
        Assert(parser.TryParse("t12_Result: 1, 2.5 -3 ,%", out var packet, out _), "合法结果解析失败");
        Assert(packet!.TestCode == 12 && packet.Values.SequenceEqual(new[] { 1d, 2.5d, -3d }), "结果内容错误");
        Assert(!parser.TryParse("t1_Result: 1 abc ,%", out _, out var error) && error.Contains("非数字"), "脏数据必须失败");
        return Task.CompletedTask;
    }

    private static Task FramerSupportsSplitAndCombinedPackets()
    {
        var framer = new ProtocolMessageFramer();
        Assert(framer.Append("t1_Result:1 2").Count == 0, "半包不能提前输出");
        var messages = framer.Append(",%t2_Result:3 4,%\r\n");
        Assert(messages.Count == 2, "粘包应拆成两条消息");
        Assert(messages[0].StartsWith("t1_Result") && messages[1].StartsWith("t2_Result"), "消息顺序错误");
        return Task.CompletedTask;
    }

    private static Task EvaluatorUsesDefinitions()
    {
        var catalog = new TestCatalog();
        var recipe = new TestRecipe { Name = "test", EnabledTestCodes = [1] };
        recipe.Thresholds["FOV_Diag"] = new RangeSpec(10, 20);
        var values = Enumerable.Repeat(0d, 23).ToArray();
        values[14] = 15;
        var result = new MeasurementEvaluator().Evaluate(
            catalog.Get(1), recipe, new ResultPacket(1, values, "raw"), TimeSpan.Zero);
        Assert(result.Metrics.Single(item => item.Key == "FOV_Diag").Verdict == Verdict.Passed, "阈值内应通过");
        Assert(result.Verdict == Verdict.Passed, "测试项应通过");
        return Task.CompletedTask;
    }

    private static async Task SequenceRunsInOrder()
    {
        var catalog = new TestCatalog();
        var recipe = new TestRecipe { Name = "flow", EnabledTestCodes = [1, 2] };
        var client = new FakeMeasurementClient();
        var exporter = new FakeExporter();
        var sequence = new TestSequenceService(
            catalog, new MeasurementEvaluator(), client, new FakeProjection(), exporter,
            new FakeLogger(), new TestSequenceOptions(TimeSpan.FromSeconds(1), TimeSpan.Zero));
        var run = await sequence.RunAsync("SN001", recipe, CancellationToken.None);
        Assert(client.Commands.SequenceEqual(new[] { "t1", "t2", "t32" }), "指令顺序错误");
        Assert(run.Status == RunStatus.Completed && run.Steps.Count == 2, "运行状态错误");
        Assert(exporter.ExportCount == 1, "结果应只导出一次");
    }

    private static Task AuthenticationPersists()
    {
        var directory = CreateTemporaryDirectory();
        try
        {
            var path = Path.Combine(directory, "users.json");
            var first = new AuthenticationService(path);
            first.Upsert("operator", "pass123", "操作员");
            var second = new AuthenticationService(path);
            Assert(second.Authenticate("OPERATOR", "pass123", out var user), "持久化后的账号无法认证");
            Assert(user?.Role == "操作员", "用户角色没有持久化");
            Assert(!File.ReadAllText(path).Contains("pass123", StringComparison.Ordinal), "文件中不能保存明文密码");
            return Task.CompletedTask;
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static Task LegacyRecipeImportsByKey()
    {
        var directory = CreateTemporaryDirectory();
        try
        {
            var path = Path.Combine(directory, "TestType.ini");
            File.WriteAllText(path,
                "[A-测试配方]\n畸变测试=False\n白画面测试=True\n" +
                "[A-interval]\n白场平均亮度=10&20\nFOV_Diag=5&2\n");
            var recipes = new LegacyIniRecipeImporter(new FakeLogger()).Import(path);
            var recipe = recipes.Single();
            Assert(recipe.EnabledTestCodes.SetEquals([1]), "测试开关导入错误");
            Assert(recipe.Thresholds["WhiteAverage"] == new RangeSpec(10, 20), "阈值没有按键名导入");
            Assert(!recipe.Thresholds.ContainsKey("FOV_Diag"), "非法阈值应被跳过");
            return Task.CompletedTask;
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static async Task StoresPersist()
    {
        var directory = CreateTemporaryDirectory();
        try
        {
            var settingsPath = Path.Combine(directory, "settings.json");
            var settingsStore = new AppSettingsStore(settingsPath);
            var settings = settingsStore.Load();
            settings.MeasurementPort = 6001;
            settingsStore.Save(settings);
            Assert(new AppSettingsStore(settingsPath).Load().MeasurementPort == 6001, "设置没有持久化");

            var recipePath = Path.Combine(directory, "recipes.json");
            var catalog = new TestCatalog();
            var firstRepository = new JsonRecipeRepository(recipePath, catalog, new FakeLogger(), null);
            var recipes = firstRepository.GetAll().Select(item => item.Clone()).ToList();
            recipes[0].Thresholds["MTFH"] = new RangeSpec(1, 2);
            firstRepository.SaveAll(recipes);
            var secondRepository = new JsonRecipeRepository(recipePath, catalog, new FakeLogger(), null);
            Assert(secondRepository.GetAll()[0].Thresholds["MTFH"] == new RangeSpec(1, 2), "配方没有持久化");

            var run = new TestRun { SerialNumber = "SN,001", RecipeName = "T1" };
            run.Start();
            run.Finish(RunStatus.Completed);
            await new CsvResultExporter(directory).ExportAsync(run, CancellationToken.None);
            var csv = Directory.GetFiles(directory, "测试数据_*.csv").Single();
            Assert(File.ReadAllText(csv).Contains("\"SN,001\"", StringComparison.Ordinal), "CSV 没有正确转义 SN");
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static async Task TcpClientHandlesFragmentedResult()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var settings = new AppSettings { MeasurementHost = "127.0.0.1", MeasurementPort = port };
        await using var client = new TcpMeasurementClient(settings, new ResultPacketParser(), new FakeLogger());
        var acceptTask = listener.AcceptTcpClientAsync();
        await client.ConnectAsync(CancellationToken.None);
        using var server = await acceptTask;
        var serverTask = Task.Run(async () =>
        {
            var commandBuffer = new byte[16];
            var count = await server.GetStream().ReadAsync(commandBuffer);
            Assert(Encoding.UTF8.GetString(commandBuffer, 0, count) == "t1", "服务端收到的指令错误");
            var values = string.Join(' ', Enumerable.Range(0, 60));
            var first = Encoding.UTF8.GetBytes("t1_Result:" + values[..20]);
            var second = Encoding.UTF8.GetBytes(values[20..] + ",%");
            await server.GetStream().WriteAsync(first);
            await Task.Delay(10);
            await server.GetStream().WriteAsync(second);
        });
        var packet = await client.SendAndWaitAsync(1, TimeSpan.FromSeconds(2), CancellationToken.None);
        await serverTask;
        Assert(packet.TestCode == 1 && packet.Values.Count == 60, "拆包结果内容错误");
        await client.DisconnectAsync();
    }

    private static string CreateTemporaryDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), $"zjs2310-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private sealed class FakeMeasurementClient : IMeasurementClient
    {
        public bool IsConnected => true;
        public List<string> Commands { get; } = [];
        public event EventHandler<bool>? ConnectionStateChanged;
        public event EventHandler<string>? RawMessageReceived;
        public Task ConnectAsync(CancellationToken cancellationToken)
        {
            ConnectionStateChanged?.Invoke(this, true);
            return Task.CompletedTask;
        }
        public Task DisconnectAsync() => Task.CompletedTask;
        public Task<ResultPacket> SendAndWaitAsync(int testCode, TimeSpan timeout, CancellationToken cancellationToken)
        {
            Commands.Add($"t{testCode}");
            RawMessageReceived?.Invoke(this, $"t{testCode}_Result");
            return Task.FromResult(new ResultPacket(testCode, Enumerable.Repeat(0d, 60).ToArray(), $"t{testCode}_Result"));
        }
        public Task SendCommandAsync(string command, CancellationToken cancellationToken)
        {
            Commands.Add(command);
            return Task.CompletedTask;
        }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class FakeProjection : IProjectionController
    {
        public Task PrepareAsync(TestDefinition definition, CancellationToken cancellationToken) => Task.CompletedTask;
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class FakeExporter : IResultExporter
    {
        public int ExportCount { get; private set; }
        public Task ExportAsync(TestRun run, CancellationToken cancellationToken)
        {
            ExportCount++;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeLogger : IAppLogger
    {
        public void Info(string message) { }
        public void Error(string message, Exception? exception = null) { }
    }
}
