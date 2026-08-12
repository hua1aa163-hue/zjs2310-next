namespace ZJS2310.App.Infrastructure;

public sealed class AppSettings
{
    public string MeasurementHost { get; set; } = "127.0.0.1";
    public int MeasurementPort { get; set; } = 5555;
    public int ResultTimeoutSeconds { get; set; } = 20;
    public int PatternSettleMilliseconds { get; set; } = 1200;
    public bool AutoConnect { get; set; } = true;
    public string PatternDirectory { get; set; } = "Assets\\Patterns";
    public string LegacyRecipePath { get; set; } = string.Empty;
    public bool SetDesktopWallpaper { get; set; }
    public bool EnableSerialProjection { get; set; }
    public bool ExtendDesktopWhenProjecting { get; set; }
    public string SerialPortName { get; set; } = string.Empty;
    public int SerialBaudRate { get; set; } = 921600;
    public int ProjectionPulseMilliseconds { get; set; } = 1000;

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(MeasurementHost))
        {
            throw new InvalidOperationException("测量服务地址不能为空。");
        }

        if (MeasurementPort is < 1 or > 65535)
        {
            throw new InvalidOperationException("测量服务端口必须在 1 到 65535 之间。");
        }

        if (ResultTimeoutSeconds is < 1 or > 600 || PatternSettleMilliseconds is < 0 or > 60000)
        {
            throw new InvalidOperationException("超时或画面稳定时间超出允许范围。");
        }

        if (EnableSerialProjection && string.IsNullOrWhiteSpace(SerialPortName))
        {
            throw new InvalidOperationException("启用串口投影时必须配置串口名称。");
        }
    }
}

public sealed class AppSettingsStore
{
    private readonly JsonFileStore<AppSettings> _store;

    public AppSettingsStore(string path) => _store = new JsonFileStore<AppSettings>(path);

    public AppSettings Load()
    {
        var settings = _store.Load() ?? new AppSettings();
        settings.Validate();
        _store.Save(settings);
        return settings;
    }

    public void Save(AppSettings settings)
    {
        settings.Validate();
        _store.Save(settings);
    }
}
