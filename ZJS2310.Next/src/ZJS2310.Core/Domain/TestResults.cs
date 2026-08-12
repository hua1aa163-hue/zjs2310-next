namespace ZJS2310.Core.Domain;

public sealed record MetricEvaluation(
    string Key,
    string DisplayName,
    double? Value,
    RangeSpec AllowedRange,
    Verdict Verdict,
    string Message);

public sealed record TestStepResult(
    int TestCode,
    string TestName,
    Verdict Verdict,
    IReadOnlyList<MetricEvaluation> Metrics,
    string RawMessage,
    string Message,
    TimeSpan Duration);

public sealed class TestRun
{
    private readonly List<TestStepResult> _steps = [];

    public Guid Id { get; } = Guid.NewGuid();
    public required string SerialNumber { get; init; }
    public required string RecipeName { get; init; }
    public DateTimeOffset StartedAt { get; } = DateTimeOffset.Now;
    public DateTimeOffset? FinishedAt { get; private set; }
    public RunStatus Status { get; private set; } = RunStatus.Created;
    public IReadOnlyList<TestStepResult> Steps => _steps;
    public string? ErrorMessage { get; private set; }

    public Verdict Verdict
    {
        get
        {
            if (Status is RunStatus.Failed or RunStatus.Cancelled || _steps.Any(step => step.Verdict == Verdict.Error))
            {
                return Verdict.Error;
            }

            return _steps.Any(step => step.Verdict == Verdict.Failed)
                ? Verdict.Failed
                : _steps.Count == 0
                    ? Verdict.NotEvaluated
                    : Verdict.Passed;
        }
    }

    public TimeSpan Duration => (FinishedAt ?? DateTimeOffset.Now) - StartedAt;

    public void Start() => Status = RunStatus.Running;

    public void AddStep(TestStepResult result) => _steps.Add(result);

    public void Finish(RunStatus status, string? errorMessage = null)
    {
        Status = status;
        ErrorMessage = errorMessage;
        FinishedAt = DateTimeOffset.Now;
    }
}
