namespace ZJS2310.Core.Domain;

public enum Verdict
{
    NotEvaluated,
    Passed,
    Failed,
    Error
}

public enum RunStatus
{
    Created,
    Running,
    Completed,
    Failed,
    Cancelled
}
