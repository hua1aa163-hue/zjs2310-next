using System.Globalization;
using System.Text;
using ZJS2310.Core.Abstractions;
using ZJS2310.Core.Domain;

namespace ZJS2310.App.Infrastructure;

public sealed class CsvResultExporter : IResultExporter
{
    private const string Header = "开始时间,SN,配方,运行状态,运行判定,测试代码,测试名称,测试判定,耗时毫秒,指标,原始结果";
    private readonly string _directory;
    private readonly SemaphoreSlim _sync = new(1, 1);

    public CsvResultExporter(string directory) => _directory = directory;

    public async Task ExportAsync(TestRun run, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(_directory);
        var path = Path.Combine(_directory, $"测试数据_{run.StartedAt:yyyy-MM-dd}.csv");
        await _sync.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var isNew = !File.Exists(path);
            await using var writer = new StreamWriter(path, append: true, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
            if (isNew)
            {
                await writer.WriteLineAsync(Header).ConfigureAwait(false);
            }

            if (run.Steps.Count == 0)
            {
                await writer.WriteLineAsync(BuildLine(run, null)).ConfigureAwait(false);
            }
            else
            {
                foreach (var step in run.Steps)
                {
                    await writer.WriteLineAsync(BuildLine(run, step)).ConfigureAwait(false);
                }
            }
        }
        finally
        {
            _sync.Release();
        }
    }

    private static string BuildLine(TestRun run, TestStepResult? step)
    {
        var metrics = step is null
            ? string.Empty
            : string.Join("; ", step.Metrics.Select(metric =>
                $"{metric.DisplayName}={metric.Value?.ToString("G17", CultureInfo.InvariantCulture) ?? "缺失"}[{metric.Verdict}]"));
        return string.Join(',', new[]
        {
            Escape(run.StartedAt.ToString("yyyy-MM-dd HH:mm:ss.fff zzz", CultureInfo.InvariantCulture)),
            Escape(run.SerialNumber),
            Escape(run.RecipeName),
            Escape(run.Status.ToString()),
            Escape(run.Verdict.ToString()),
            Escape(step is null ? string.Empty : $"t{step.TestCode}"),
            Escape(step?.TestName ?? string.Empty),
            Escape(step?.Verdict.ToString() ?? string.Empty),
            Escape(step?.Duration.TotalMilliseconds.ToString("F0", CultureInfo.InvariantCulture) ?? string.Empty),
            Escape(metrics),
            Escape(step?.RawMessage ?? run.ErrorMessage ?? string.Empty)
        });
    }

    private static string Escape(string value) => $"\"{value.Replace("\"", "\"\"")}\"";
}
