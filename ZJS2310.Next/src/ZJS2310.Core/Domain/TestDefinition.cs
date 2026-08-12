namespace ZJS2310.Core.Domain;

public sealed record MetricDefinition(string Key, string DisplayName, int ValueIndex);

public sealed record TestDefinition(
    int Code,
    string Name,
    string PatternFileName,
    IReadOnlyList<MetricDefinition> Metrics);
