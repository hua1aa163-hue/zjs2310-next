using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace ZJS2310.App.Infrastructure;

internal sealed class NativeSerialLink : IAsyncDisposable
{
    private SafeFileHandle? _handle;
    private FileStream? _stream;
    private string? _portName;
    private int _baudRate;

    public void EnsureOpen(string portName, int baudRate)
    {
        if (_stream is not null && string.Equals(_portName, portName, StringComparison.OrdinalIgnoreCase) && _baudRate == baudRate)
        {
            return;
        }

        Close();
        var normalized = portName.StartsWith("\\\\.\\", StringComparison.Ordinal) ? portName : $"\\\\.\\{portName}";
        var handle = CreateFile(normalized, GenericRead | GenericWrite, 0, IntPtr.Zero, OpenExisting, 0, IntPtr.Zero);
        if (handle.IsInvalid)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), $"无法打开串口 {portName}。");
        }

        var dcb = new Dcb { Length = (uint)Marshal.SizeOf<Dcb>() };
        if (!BuildCommDCB($"baud={baudRate} parity=n data=8 stop=1", ref dcb) || !SetCommState(handle, ref dcb))
        {
            var error = Marshal.GetLastWin32Error();
            handle.Dispose();
            throw new Win32Exception(error, $"无法配置串口 {portName}。");
        }

        if (!SetupComm(handle, 4096, 4096))
        {
            var error = Marshal.GetLastWin32Error();
            handle.Dispose();
            throw new Win32Exception(error, $"无法设置串口 {portName} 缓冲区。");
        }

        PurgeComm(handle, PurgeRxClear | PurgeTxClear);
        _handle = handle;
        _stream = new FileStream(handle, FileAccess.ReadWrite, 4096, isAsync: false);
        _portName = portName;
        _baudRate = baudRate;
    }

    public async Task WriteAsync(ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken)
    {
        if (_stream is null)
        {
            throw new InvalidOperationException("串口尚未打开。");
        }

        await _stream.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
        await _stream.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    public ValueTask DisposeAsync()
    {
        Close();
        return ValueTask.CompletedTask;
    }

    public void Close()
    {
        _stream?.Dispose();
        _stream = null;
        _handle?.Dispose();
        _handle = null;
        _portName = null;
    }

    private const uint GenericRead = 0x80000000;
    private const uint GenericWrite = 0x40000000;
    private const uint OpenExisting = 3;
    private const uint PurgeTxClear = 0x0004;
    private const uint PurgeRxClear = 0x0008;

    [StructLayout(LayoutKind.Sequential)]
    private struct Dcb
    {
        public uint Length;
        public uint BaudRate;
        public uint Flags;
        public ushort Reserved;
        public ushort XonLimit;
        public ushort XoffLimit;
        public byte ByteSize;
        public byte Parity;
        public byte StopBits;
        public byte XonChar;
        public byte XoffChar;
        public byte ErrorChar;
        public byte EofChar;
        public byte EventChar;
        public ushort Reserved1;
    }

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern SafeFileHandle CreateFile(
        string fileName, uint desiredAccess, uint shareMode, IntPtr securityAttributes,
        uint creationDisposition, uint flagsAndAttributes, IntPtr templateFile);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Ansi)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool BuildCommDCB(string definition, ref Dcb dcb);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetCommState(SafeFileHandle handle, ref Dcb dcb);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetupComm(SafeFileHandle handle, uint inputQueueSize, uint outputQueueSize);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PurgeComm(SafeFileHandle handle, uint flags);
}
