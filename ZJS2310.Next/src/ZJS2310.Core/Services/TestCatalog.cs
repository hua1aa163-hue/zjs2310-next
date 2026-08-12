using ZJS2310.Core.Domain;

namespace ZJS2310.Core.Services;

public sealed class TestCatalog
{
    private readonly IReadOnlyList<TestDefinition> _definitions;

    public TestCatalog()
    {
        _definitions =
        [
            Define(1, "白画面测试", "t1.bmp",
                Metric("FOV_Diag", "对角 FOV", 14),
                Metric("WhiteAverage", "白场平均亮度", 9),
                Metric("WhiteCenter", "白场中心亮度", 5),
                Metric("WhiteUniformity", "白场亮度均匀性", 10)),
            Define(2, "黑画面测试", "t2.bmp", Metric("BlackWhiteContrast", "黑白对比度", 21)),
            Define(3, "畸变测试", "t3.bmp", Metric("Distortion", "畸变", 21)),
            Define(4, "MTFH 测试", "t4.bmp", Metric("MTFH", "MTFH", 9)),
            Define(5, "MTFV 测试", "t5.bmp", Metric("MTFV", "MTFV", 9)),
            Define(6, "Color 测试", "t6.bmp",
                Metric("ColorX", "中心色坐标 x", 21),
                Metric("ColorY", "中心色坐标 y", 22),
                Metric("ColorUniformity", "色度均匀性", 9)),
            Define(7, "红画面测试", "t7.bmp"),
            Define(8, "绿画面测试", "t8.bmp"),
            Define(9, "蓝画面测试", "t9.bmp", Metric("ColorGamut", "色域", 50)),
            Define(12, "白黑棋盘格测试", "t12.bmp", Metric("WhiteBlackChess", "棋盘格白黑对比度", 18)),
            Define(13, "黑白棋盘格测试", "t13.bmp", Metric("BlackWhiteChess", "棋盘格黑白对比度", 18)),
            Define(14, "鬼像重影测试", "t14.bmp", Metric("Ghost", "鬼影", 18))
        ];
    }

    public IReadOnlyList<TestDefinition> All => _definitions;

    public TestDefinition Get(int code) =>
        _definitions.FirstOrDefault(item => item.Code == code)
        ?? throw new KeyNotFoundException($"未知测试代码 t{code}。");

    public IReadOnlyList<TestDefinition> Resolve(TestRecipe recipe) =>
        _definitions.Where(item => recipe.EnabledTestCodes.Contains(item.Code)).ToArray();

    private static TestDefinition Define(int code, string name, string pattern, params MetricDefinition[] metrics) =>
        new(code, name, pattern, metrics);

    private static MetricDefinition Metric(string key, string name, int index) => new(key, name, index);
}
