using ZJS2310.Core.Domain;

namespace ZJS2310.Core.Services;

public sealed class MeasurementEvaluator
{
    public TestStepResult Evaluate(
        TestDefinition definition,
        TestRecipe recipe,
        ResultPacket packet,
        TimeSpan duration)
    {
        if (packet.TestCode != definition.Code)
        {
            return Error(definition, packet.RawMessage, duration,
                $"期望 t{definition.Code}，实际收到 t{packet.TestCode}。");
        }

        var metrics = new List<MetricEvaluation>(definition.Metrics.Count);
        foreach (var metric in definition.Metrics)
        {
            recipe.Thresholds.TryGetValue(metric.Key, out var configuredRange);
            var range = configuredRange ?? new RangeSpec();
            if (metric.ValueIndex < 0 || metric.ValueIndex >= packet.Values.Count)
            {
                metrics.Add(new MetricEvaluation(metric.Key, metric.DisplayName, null, range, Verdict.Error,
                    $"结果只有 {packet.Values.Count} 个值，缺少下标 {metric.ValueIndex}。"));
                continue;
            }

            var value = packet.Values[metric.ValueIndex];
            var verdict = !range.IsConfigured
                ? Verdict.NotEvaluated
                : range.Contains(value) ? Verdict.Passed : Verdict.Failed;
            var message = verdict switch
            {
                Verdict.NotEvaluated => "未配置阈值",
                Verdict.Passed => "在阈值范围内",
                _ => "超出阈值范围"
            };
            metrics.Add(new MetricEvaluation(metric.Key, metric.DisplayName, value, range, verdict, message));
        }

        var stepVerdict = metrics.Any(item => item.Verdict == Verdict.Error)
            ? Verdict.Error
            : metrics.Any(item => item.Verdict == Verdict.Failed)
                ? Verdict.Failed
                : metrics.Any(item => item.Verdict == Verdict.Passed)
                    ? Verdict.Passed
                    : Verdict.NotEvaluated;

        return new TestStepResult(definition.Code, definition.Name, stepVerdict, metrics,
            packet.RawMessage, stepVerdict == Verdict.NotEvaluated ? "结果已接收，未配置判定阈值。" : "结果解析完成。", duration);
    }

    public TestStepResult Error(TestDefinition definition, string raw, TimeSpan duration, string message) =>
        new(definition.Code, definition.Name, Verdict.Error, [], raw, message, duration);
}
