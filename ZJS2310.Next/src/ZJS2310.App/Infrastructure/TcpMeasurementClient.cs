using System.Net.Sockets;
using System.Text;
using ZJS2310.Core.Abstractions;
using ZJS2310.Core.Domain;
using ZJS2310.Core.Services;

namespace ZJS2310.App.Infrastructure;

public sealed class TcpMeasurementClient : IMeasurementClient
{
    private readonly AppSettings _settings;
    private readonly ResultPacketParser _parser;
    private readonly IAppLogger _logger;
    private readonly SemaphoreSlim _sendLock = new(1, 1);
    private readonly SemaphoreSlim _requestLock = new(1, 1);
    private readonly object _pendingSync = new();
    private TcpClient? _client;
    private NetworkStream? _stream;
    private CancellationTokenSource? _receiveCancellation;
    private Task? _receiveTask;
    private PendingRequest? _pending;
    private bool _isConnected;

    public TcpMeasurementClient(AppSettings settings, ResultPacketParser parser, IAppLogger logger)
    {
        _settings = settings;
        _parser = parser;
        _logger = logger;
    }

    public bool IsConnected => _isConnected && _client?.Connected == true;

    public event EventHandler<bool>? ConnectionStateChanged;
    public event EventHandler<string>? RawMessageReceived;

    public async Task ConnectAsync(CancellationToken cancellationToken)
    {
        await DisconnectAsync().ConfigureAwait(false);
        _settings.Validate();
        var client = new TcpClient { NoDelay = true };
        try
        {
            await client.ConnectAsync(_settings.MeasurementHost, _settings.MeasurementPort, cancellationToken)
                .ConfigureAwait(false);
            _client = client;
            _stream = client.GetStream();
            _receiveCancellation = new CancellationTokenSource();
            SetConnected(true);
            _receiveTask = Task.Run(() => ReceiveLoopAsync(_receiveCancellation.Token), CancellationToken.None);
            _logger.Info($"已连接测量服务 {_settings.MeasurementHost}:{_settings.MeasurementPort}。");
        }
        catch
        {
            client.Dispose();
            SetConnected(false);
            throw;
        }
    }

    public async Task DisconnectAsync()
    {
        var receiveTask = _receiveTask;
        _receiveTask = null;
        _receiveCancellation?.Cancel();
        _client?.Dispose();
        if (receiveTask is not null && receiveTask.Id != Task.CurrentId)
        {
            try
            {
                await receiveTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
            catch (IOException)
            {
            }
        }

        _receiveCancellation?.Dispose();
        _receiveCancellation = null;
        _stream = null;
        _client = null;
        FailPending(new IOException("测量服务连接已断开。"));
        SetConnected(false);
    }

    public async Task<ResultPacket> SendAndWaitAsync(
        int testCode,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        await _requestLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            EnsureConnected();
            var completion = new TaskCompletionSource<ResultPacket>(TaskCreationOptions.RunContinuationsAsynchronously);
            lock (_pendingSync)
            {
                _pending = new PendingRequest(testCode, completion);
            }

            try
            {
                await SendCommandAsync($"t{testCode}", cancellationToken).ConfigureAwait(false);
                return await completion.Task.WaitAsync(timeout, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                lock (_pendingSync)
                {
                    if (_pending?.Completion == completion)
                    {
                        _pending = null;
                    }
                }
            }
        }
        finally
        {
            _requestLock.Release();
        }
    }

    public async Task SendCommandAsync(string command, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(command);
        EnsureConnected();
        var data = Encoding.UTF8.GetBytes(command);
        await _sendLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await _stream!.WriteAsync(data, cancellationToken).ConfigureAwait(false);
            await _stream.FlushAsync(cancellationToken).ConfigureAwait(false);
            _logger.Info($"发送指令：{command}");
        }
        finally
        {
            _sendLock.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        await DisconnectAsync().ConfigureAwait(false);
        _sendLock.Dispose();
        _requestLock.Dispose();
    }

    private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
    {
        var framer = new ProtocolMessageFramer();
        var buffer = new char[2048];
        try
        {
            using var reader = new StreamReader(_stream!, Encoding.UTF8, true, 2048, leaveOpen: true);
            while (!cancellationToken.IsCancellationRequested)
            {
                var count = await reader.ReadAsync(buffer.AsMemory(), cancellationToken).ConfigureAwait(false);
                if (count == 0)
                {
                    throw new IOException("测量服务已关闭连接。");
                }

                foreach (var message in framer.Append(new string(buffer, 0, count)))
                {
                    RawMessageReceived?.Invoke(this, message);
                    _logger.Info($"收到数据：{message}");
                    HandleMessage(message);
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            _logger.Error("测量服务接收循环终止。", exception);
            FailPending(exception);
        }
        finally
        {
            SetConnected(false);
        }
    }

    private void HandleMessage(string message)
    {
        if (!_parser.TryParse(message, out var packet, out var error))
        {
            _logger.Error($"无法解析测量结果：{error} 原文：{message}");
            return;
        }

        lock (_pendingSync)
        {
            if (_pending is { } pending && packet!.TestCode == pending.TestCode)
            {
                _pending = null;
                pending.Completion.TrySetResult(packet);
            }
            else
            {
                _logger.Error($"收到非预期结果 t{packet!.TestCode}，当前没有对应的等待请求。");
            }
        }
    }

    private void FailPending(Exception exception)
    {
        lock (_pendingSync)
        {
            _pending?.Completion.TrySetException(exception);
            _pending = null;
        }
    }

    private void EnsureConnected()
    {
        if (!IsConnected || _stream is null)
        {
            throw new InvalidOperationException("测量服务尚未连接。");
        }
    }

    private void SetConnected(bool connected)
    {
        if (_isConnected == connected)
        {
            return;
        }

        _isConnected = connected;
        ConnectionStateChanged?.Invoke(this, connected);
    }

    private sealed record PendingRequest(int TestCode, TaskCompletionSource<ResultPacket> Completion);
}
