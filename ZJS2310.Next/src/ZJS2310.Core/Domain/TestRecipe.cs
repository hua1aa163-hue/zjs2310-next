namespace ZJS2310.Core.Domain;

public sealed class TestRecipe
{
    public string Name { get; set; } = string.Empty;
    public HashSet<int> EnabledTestCodes { get; set; } = [];
    public Dictionary<string, RangeSpec> Thresholds { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public TestRecipe Clone() => new()
    {
        Name = Name,
        EnabledTestCodes = [.. EnabledTestCodes],
        Thresholds = Thresholds.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase)
    };
}
