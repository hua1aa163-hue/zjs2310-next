using System.Globalization;

namespace ZJS2310.Core.Domain;

public sealed record RangeSpec(double? Minimum = null, double? Maximum = null)
{
    public bool IsConfigured => Minimum.HasValue || Maximum.HasValue;

    public bool Contains(double value) =>
        (!Minimum.HasValue || value >= Minimum.Value) &&
        (!Maximum.HasValue || value <= Maximum.Value);

    public static bool TryParseLegacy(string? value, out RangeSpec range)
    {
        range = new RangeSpec();
        if (value is null)
        {
            return true;
        }

        var parts = value.Split('&');
        if (parts.Length != 2 ||
            !TryParseBound(parts[0], out var minimum) ||
            !TryParseBound(parts[1], out var maximum) ||
            minimum.HasValue && maximum.HasValue && minimum > maximum)
        {
            return false;
        }

        range = new RangeSpec(minimum, maximum);
        return true;
    }

    public string ToLegacyString() =>
        $"{Format(Minimum)}&{Format(Maximum)}";

    private static bool TryParseBound(string text, out double? value)
    {
        value = null;
        if (string.IsNullOrWhiteSpace(text))
        {
            return true;
        }

        if (!double.TryParse(text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
        {
            return false;
        }

        value = parsed;
        return true;
    }

    private static string Format(double? value) =>
        value?.ToString("G17", CultureInfo.InvariantCulture) ?? string.Empty;
}
