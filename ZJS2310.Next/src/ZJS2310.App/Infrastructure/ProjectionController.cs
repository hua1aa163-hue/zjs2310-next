using System.Runtime.InteropServices;
using ZJS2310.Core.Abstractions;
using ZJS2310.Core.Domain;

namespace ZJS2310.App.Infrastructure;

public sealed class ProjectionController : IProjectionController
{
    private const int SetDesktopWallpaperAction = 20;
    private const int UpdateIniFile = 0x01;
    private const uint Apply = 0x00000080;
    private const uint Internal = 0x00000001;
    private const uint Extend = 0x00000004;
    private readonly AppSettings _settings;
    private readonly IAppLogger _logger;
    private readonly NativeSerialLink _serial = new();

    public ProjectionController(AppSettings settings, IAppLogger logger)
    {
        _settings = settings;
        _logger = logger;
    }

    public async Task PrepareAsync(TestDefinition definition, CancellationToken cancellationToken)
    {
        var imagePath = ResolvePatternPath(definition.PatternFileName);
        if (_settings.SetDesktopWallpaper)
        {
            if (!File.Exists(imagePath))
            {
                throw new FileNotFoundException($"找不到测试画面 {definition.PatternFileName}。", imagePath);
            }

            if (!SystemParametersInfo(SetDesktopWallpaperAction, 0, imagePath, UpdateIniFile))
            {
                throw new InvalidOperationException($"切换桌面测试画面失败，Win32 错误 {Marshal.GetLastWin32Error()}。");
            }
        }

        if (_settings.EnableSerialProjection)
        {
            _serial.EnsureOpen(_settings.SerialPortName, _settings.SerialBaudRate);
            var command = new byte[] { 0xc4, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x4a, 0x42, 0x44 };
            await _serial.WriteAsync(command, cancellationToken).ConfigureAwait(false);
            try
            {
                if (_settings.ExtendDesktopWhenProjecting)
                {
                    var result = SetDisplayConfig(0, IntPtr.Zero, 0, IntPtr.Zero, Apply | Extend);
                    if (result != 0)
                    {
                        _logger.Error($"切换到扩展桌面失败，错误代码 {result}。");
                    }
                }

                await Task.Delay(_settings.ProjectionPulseMilliseconds, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                command[1] = 0x00;
                await _serial.WriteAsync(command, CancellationToken.None).ConfigureAwait(false);
            }
        }
    }

    public async Task ShutdownAsync(CancellationToken cancellationToken)
    {
        if (_settings.EnableSerialProjection)
        {
            _serial.EnsureOpen(_settings.SerialPortName, _settings.SerialBaudRate);
            var command = new byte[] { 0xc4, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x4a, 0x42, 0x44 };
            await _serial.WriteAsync(command, cancellationToken).ConfigureAwait(false);
            try
            {
                await Task.Delay(_settings.ProjectionPulseMilliseconds, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                command[1] = 0x00;
                await _serial.WriteAsync(command, CancellationToken.None).ConfigureAwait(false);
                _serial.Close();
            }
        }

        if (_settings.ExtendDesktopWhenProjecting)
        {
            var result = SetDisplayConfig(0, IntPtr.Zero, 0, IntPtr.Zero, Apply | Internal);
            if (result != 0)
            {
                _logger.Error($"恢复主屏显示失败，错误代码 {result}。");
            }
        }
    }

    public ValueTask DisposeAsync() => _serial.DisposeAsync();

    public string ResolvePatternPath(string fileName)
    {
        var directory = Path.IsPathRooted(_settings.PatternDirectory)
            ? _settings.PatternDirectory
            : Path.Combine(AppContext.BaseDirectory, _settings.PatternDirectory);
        return Path.Combine(directory, fileName);
    }

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SystemParametersInfo(int action, int parameter, string value, int flags);

    [DllImport("user32.dll")]
    private static extern int SetDisplayConfig(
        uint pathCount, IntPtr paths, uint modeCount, IntPtr modes, uint flags);
}
