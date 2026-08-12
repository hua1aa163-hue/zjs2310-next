using ZJS2310.Core.Abstractions;
using ZJS2310.Core.Domain;
using ZJS2310.Core.Services;

namespace ZJS2310.App.Infrastructure;

public sealed class JsonRecipeRepository : IRecipeRepository
{
    private readonly JsonFileStore<List<TestRecipe>> _store;
    private readonly TestCatalog _catalog;
    private readonly IAppLogger _logger;
    private List<TestRecipe> _recipes;

    public JsonRecipeRepository(string path, TestCatalog catalog, IAppLogger logger, string? legacyIniPath)
    {
        _store = new JsonFileStore<List<TestRecipe>>(path);
        _catalog = catalog;
        _logger = logger;
        _recipes = _store.Load() ?? LoadInitialRecipes(legacyIniPath);
        Normalize(_recipes);
        _store.Save(_recipes);
    }

    public IReadOnlyList<TestRecipe> GetAll() => _recipes.Select(recipe => recipe.Clone()).ToArray();

    public void SaveAll(IEnumerable<TestRecipe> recipes)
    {
        var materialized = recipes.Select(recipe => recipe.Clone()).ToList();
        if (materialized.Count == 0)
        {
            throw new InvalidOperationException("至少需要保留一个测试配方。");
        }

        if (materialized.Any(recipe => string.IsNullOrWhiteSpace(recipe.Name)) ||
            materialized.GroupBy(recipe => recipe.Name.Trim(), StringComparer.OrdinalIgnoreCase).Any(group => group.Count() > 1))
        {
            throw new InvalidOperationException("配方名称不能为空或重复。");
        }

        Normalize(materialized);
        _recipes = materialized;
        _store.Save(_recipes);
    }

    private List<TestRecipe> LoadInitialRecipes(string? legacyIniPath)
    {
        if (!string.IsNullOrWhiteSpace(legacyIniPath) && File.Exists(legacyIniPath))
        {
            var imported = new LegacyIniRecipeImporter(_logger).Import(legacyIniPath).ToList();
            if (imported.Count > 0)
            {
                _logger.Info($"已从旧 INI 导入 {imported.Count} 个测试配方。");
                return imported;
            }
        }

        var allTests = _catalog.All.Select(item => item.Code).ToHashSet();
        return
        [
            Create("T1-测试配方", allTests),
            Create("T2-测试配方", allTests.Where(code => code != 3)),
            Create("T3-测试配方", allTests.Where(code => code != 3))
        ];
    }

    private void Normalize(List<TestRecipe> recipes)
    {
        var validCodes = _catalog.All.Select(item => item.Code).ToHashSet();
        var metrics = _catalog.All.SelectMany(item => item.Metrics).Select(item => item.Key).Distinct(StringComparer.OrdinalIgnoreCase);
        foreach (var recipe in recipes)
        {
            recipe.Name = recipe.Name.Trim();
            recipe.EnabledTestCodes.RemoveWhere(code => !validCodes.Contains(code));
            foreach (var metric in metrics)
            {
                recipe.Thresholds.TryAdd(metric, new RangeSpec());
            }
        }
    }

    private static TestRecipe Create(string name, IEnumerable<int> codes) => new()
    {
        Name = name,
        EnabledTestCodes = [.. codes]
    };
}
