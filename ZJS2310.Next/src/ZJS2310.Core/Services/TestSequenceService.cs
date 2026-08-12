using System.Diagnostics;
using ZJS2310.Core.Abstractions;
using ZJS2310.Core.Domain;

namespace ZJS2310.Core.Services;

public sealed record TestSequenceOptions(
    TimeSpan ResultTimeout,
    TimeSpan PatternSettleDelay,
    bool ContinueOnError = false);

public sealed class StepStartedEventArgs(TestDefinition definition, int index, int total) : EventArgs
{
    public TestDefinition Definition { get; } = definition;
    public int Index { get; } = index;
    public int Total { get; } = total;
}

public sealed class StepCompletedEventArgs(TestStepResult result, int index, int total) : EventArgs
{
    public TestStepResult Result { get; } = result;
    public int Index { get; } = index;
    public int Total { get; } = total;
}

public sealed class TestSequenceService
{
    private readonly TestCatalog _catalog;
    private readonly MeasurementEvaluator _evaluator;
    private readonly IMeasurementClient _client;
    private readonly IProjectionController _projection;
    private readonly IResultExporter _exporter;
    private readonly IAppLogger _logger;
    private TestSequenceOptions _options;

    public TestSequenceService(
        TestCatalog catalog,
        MeasurementEvaluator evaluator,
        IMeasurementClient client,
        IProjectionController projection,
        IResultExporter exporter,
        IAppLogger logger,
        TestSequenceOptions options)
    {
        _catalog = catalog;
        _evaluator = evaluator;
        _client = client;
        _projection = projection;
        _exporter = exporter;
        _logger = logger;
        _options = options;
    }

    public event EventHandler<StepStartedEventArgs>? StepStarted;
    public event EventHandler<StepCompletedEventArgs>? StepCompleted;

    public void UpdateOptions(TestSequenceOptions options) => _options = options;

    public async Task<TestRun> RunAsync(string serialNumber, TestRecipe recipe, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serialNumber);
        var definitions = _catalog.Resolve(recipe);
        if (definitions.Count == 0)
        {
            throw new InvalidOperationException("所选配方没有启用任何测试项。");
        }

        if (!_client.IsConnected)
        {
            throw new InvalidOperationException("测量服务尚未连接。");
        }

        var run = new TestRun { SerialNumber = serialNumber.Trim(), RecipeName = recipe.Name };
        run.Start();
        _logger.Info($"开始测试：SN={run.SerialNumber}，配方={recipe.Name}。");
        try
        {
            for (var index = 0; index < definitions.Count; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var definition = definitions[index];
                StepStarted?.Invoke(this, new StepStartedEventArgs(definition, index + 1, definitions.Count));
                var stopwatch = Stopwatch.StartNew();
                TestStepResult step;
                try
                {
                    await _projection.PrepareAsync(definition, cancellationToken).ConfigureAwait(false);
                    await Task.Delay(_options.PatternSettleDelay, cancellationToken).ConfigureAwait(false);
                    var packet = await _client.SendAndWaitAsync(definition.Code, _options.ResultTimeout, cancellationToken)
                        .ConfigureAwait(false);
                    step = _evaluator.Evaluate(definition, recipe, packet, stopwatch.Elapsed);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    _logger.Error($"t{definition.Code} 执行失败。", exception);
                    step = _evaluator.Error(definition, string.Empty, stopwatch.Elapsed, exception.Message);
                }

                run.AddStep(step);
                StepCompleted?.Invoke(this, new StepCompletedEventArgs(step, index + 1, definitions.Count));
                if (step.Verdict == Verdict.Error && !_options.ContinueOnError)
                {
                    run.Finish(RunStatus.Failed, step.Message);
                    break;
                }
            }

            if (run.Status == RunStatus.Running)
            {
                await _client.SendCommandAsync("t32", cancellationToken).ConfigureAwait(false);
                run.Finish(RunStatus.Completed);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            run.Finish(RunStatus.Cancelled, "用户停止测试。");
        }
        catch (Exception exception)
        {
            run.Finish(RunStatus.Failed, exception.Message);
            _logger.Error("测试流程异常终止。", exception);
        }
        finally
        {
            try
            {
                await _exporter.ExportAsync(run, CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                _logger.Error("测试结果导出失败。", exception);
            }
        }

        _logger.Info($"测试结束：SN={run.SerialNumber}，状态={run.Status}，判定={run.Verdict}。");
        return run;
    }
}
