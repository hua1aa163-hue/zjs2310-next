using ZJS2310.Core.Domain;

namespace ZJS2310.Core.Abstractions;

public interface IMeasurementClient : IAsyncDisposable
{
    bool IsConnected { get; }
    event EventHandler<bool>? ConnectionStateChanged;
    event EventHandler<string>? RawMessageReceived;
    Task ConnectAsync(CancellationToken cancellationToken);
    Task DisconnectAsync();
    Task<ResultPacket> SendAndWaitAsync(int testCode, TimeSpan timeout, CancellationToken cancellationToken);
    Task SendCommandAsync(string command, CancellationToken cancellationToken);
}

public interface IProjectionController : IAsyncDisposable
{
    Task PrepareAsync(TestDefinition definition, CancellationToken cancellationToken);
}

public interface IResultExporter
{
    Task ExportAsync(TestRun run, CancellationToken cancellationToken);
}

public interface IAppLogger
{
    void Info(string message);
    void Error(string message, Exception? exception = null);
}

public interface IRecipeRepository
{
    IReadOnlyList<TestRecipe> GetAll();
    void SaveAll(IEnumerable<TestRecipe> recipes);
}
