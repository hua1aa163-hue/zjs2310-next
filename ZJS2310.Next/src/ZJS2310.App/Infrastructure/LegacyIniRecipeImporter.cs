using ZJS2310.Core.Abstractions;
using ZJS2310.Core.Domain;
using ZJS2310.Core.Services;

namespace ZJS2310.App.Infrastructure;

public sealed class LegacyIniRecipeImporter
{
    private static readonly Dictionary<string, int> TestCodes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["白画面测试"] = 1,
        ["黑画面测试"] = 2,
        ["畸变测试"] = 3,
        ["MTFH测试"] = 4,
        ["MTFV测试"] = 5,
        ["color测试"] = 6,
        ["红画面测试"] = 7,
        ["绿画面测试"] = 8,
        ["蓝画面测试"] = 9,
        ["白黑棋盘格测试"] = 12,
        ["黑白棋盘格测试"] = 13,
        ["鬼像重影测试"] = 14
    };

    private static readonly Dictionary<string, string> MetricKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        ["FOV_Diag"] = "FOV_Diag",
        ["白场平均亮度"] = "WhiteAverage",
        ["白场中心亮度"] = "WhiteCenter",
        ["白场亮度均匀性"] = "WhiteUniformity",
        ["黑白对比度"] = "BlackWhiteContrast",
        ["畸变"] = "Distortion",
        ["MTFH"] = "MTFH",
        ["MTFV"] = "MTFV",
        ["中心色坐标x"] = "ColorX",
        ["中心色坐标y"] = "ColorY",
        ["色度均匀性"] = "ColorUniformity",
        ["色域"] = "ColorGamut",
        ["棋盘格白黑对比度"] = "WhiteBlackChess",
        ["棋盘格黑白对比度"] = "BlackWhiteChess",
        ["鬼影"] = "Ghost"
    };

    private readonly IAppLogger _logger;

    public LegacyIniRecipeImporter(IAppLogger logger) => _logger = logger;

    public IReadOnlyList<TestRecipe> Import(string path)
    {
        var sections = Parse(path);
        var recipes = new List<TestRecipe>();
        foreach (var pair in sections.Where(item => item.Key.EndsWith("-测试配方", StringComparison.OrdinalIgnoreCase)))
        {
            var recipe = new TestRecipe { Name = pair.Key };
            foreach (var test in pair.Value)
            {
                if (TestCodes.TryGetValue(test.Key, out var code) && bool.TryParse(test.Value, out var enabled) && enabled)
                {
                    recipe.EnabledTestCodes.Add(code);
                }
            }

            var intervalName = pair.Key.Replace("测试配方", "interval", StringComparison.OrdinalIgnoreCase);
            if (sections.TryGetValue(intervalName, out var intervals))
            {
                foreach (var interval in intervals)
                {
                    if (!MetricKeys.TryGetValue(interval.Key, out var metricKey))
                    {
                        continue;
                    }

                    if (RangeSpec.TryParseLegacy(interval.Value, out var range))
                    {
                        recipe.Thresholds[metricKey] = range;
                    }
                    else
                    {
                        _logger.Error($"忽略配方 {recipe.Name} 中无效的阈值：{interval.Key}={interval.Value}");
                    }
                }
            }

            recipes.Add(recipe);
        }

        return recipes;
    }

    private static Dictionary<string, Dictionary<string, string>> Parse(string path)
    {
        var result = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, string>? current = null;
        foreach (var sourceLine in File.ReadLines(path))
        {
            var line = sourceLine.Trim();
            if (line.Length == 0 || line.StartsWith(';') || line.StartsWith('#'))
            {
                continue;
            }

            if (line.StartsWith('[') && line.EndsWith(']'))
            {
                var name = line[1..^1].Trim();
                current = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                result[name] = current;
                continue;
            }

            var equals = line.IndexOf('=');
            if (current is not null && equals > 0)
            {
                current[line[..equals].Trim()] = line[(equals + 1)..].Trim();
            }
        }

        return result;
    }
}
