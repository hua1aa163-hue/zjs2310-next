using ZJS2310.App.Infrastructure;
using ZJS2310.App.UI;
using ZJS2310.Core.Services;

namespace ZJS2310.App;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        FileLogger? logger = null;
        TcpMeasurementClient? client = null;
        ProjectionController? projection = null;
        try
        {
            var paths = DataPaths.Create();
            logger = new FileLogger(paths.LogsDirectory);
            var settingsStore = new AppSettingsStore(paths.SettingsFile);
            var settings = settingsStore.Load();
            var catalog = new TestCatalog();
            var recipes = new JsonRecipeRepository(paths.RecipesFile, catalog, logger, settings.LegacyRecipePath);
            var authentication = new AuthenticationService(paths.UsersFile);
            client = new TcpMeasurementClient(settings, new ResultPacketParser(), logger);
            projection = new ProjectionController(settings, logger);
            var sequence = new TestSequenceService(
                catalog,
                new MeasurementEvaluator(),
                client,
                projection,
                new CsvResultExporter(paths.ExportsDirectory),
                logger,
                new TestSequenceOptions(
                    TimeSpan.FromSeconds(settings.ResultTimeoutSeconds),
                    TimeSpan.FromMilliseconds(settings.PatternSettleMilliseconds)));

            Application.Run(new MainForm(
                paths, settings, settingsStore, catalog, recipes, authentication,
                client, projection, sequence, logger));
        }
        catch (Exception exception)
        {
            logger?.Error("应用程序启动失败。", exception);
            MessageBox.Show($"应用程序启动失败：{exception.Message}", "ZJS2310", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            client?.DisposeAsync().AsTask().GetAwaiter().GetResult();
            projection?.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }
    }
}
