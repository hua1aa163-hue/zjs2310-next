using ZJS2310.App.Infrastructure;

namespace ZJS2310.App.UI;

public sealed class ConnectionSettingsForm : Form
{
    private readonly AppSettings _settings;
    private readonly AppSettingsStore _store;
    private readonly TextBox _host = new();
    private readonly NumericUpDown _port = new() { Minimum = 1, Maximum = 65535 };
    private readonly NumericUpDown _timeout = new() { Minimum = 1, Maximum = 600 };
    private readonly NumericUpDown _settle = new() { Minimum = 0, Maximum = 60000, Increment = 100 };
    private readonly CheckBox _autoConnect = new() { Text = "启动后自动连接" };
    private readonly TextBox _patternDirectory = new();
    private readonly TextBox _legacyIni = new();
    private readonly CheckBox _wallpaper = new() { Text = "切换 Windows 桌面壁纸" };
    private readonly CheckBox _serialEnabled = new() { Text = "启用串口投影脉冲" };
    private readonly TextBox _serialPort = new();
    private readonly NumericUpDown _baud = new() { Minimum = 1200, Maximum = 4_000_000, Increment = 1200 };
    private readonly CheckBox _extend = new() { Text = "投影时切换为扩展桌面" };

    public ConnectionSettingsForm(AppSettings settings, AppSettingsStore store)
    {
        _settings = settings;
        _store = store;
        Text = "连接与设备设置";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ClientSize = new Size(650, 565);

        var table = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(16), ColumnCount = 3, RowCount = 13 };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 165));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 82));
        for (var i = 0; i < 12; i++) table.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        table.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        AddRow(table, 0, "测量服务地址", _host);
        AddRow(table, 1, "测量服务端口", _port);
        AddRow(table, 2, "结果超时（秒）", _timeout);
        AddRow(table, 3, "画面稳定（毫秒）", _settle);
        table.Controls.Add(_autoConnect, 1, 4);
        AddRow(table, 5, "测试画面目录", _patternDirectory, BrowseFolder(_patternDirectory));
        AddRow(table, 6, "旧版配方 INI", _legacyIni, BrowseFile(_legacyIni));
        table.Controls.Add(_wallpaper, 1, 7);
        table.Controls.Add(_serialEnabled, 1, 8);
        AddRow(table, 9, "串口名称（如 COM6）", _serialPort);
        AddRow(table, 10, "串口波特率", _baud);
        table.Controls.Add(_extend, 1, 11);

        var save = new Button { Text = "保存", AutoSize = true };
        var cancel = new Button { Text = "取消", AutoSize = true, DialogResult = DialogResult.Cancel };
        save.Click += SaveClicked;
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
        buttons.Controls.Add(cancel);
        buttons.Controls.Add(save);
        table.Controls.Add(buttons, 0, 12);
        table.SetColumnSpan(buttons, 3);
        Controls.Add(table);
        AcceptButton = save;
        CancelButton = cancel;
        LoadValues();
    }

    private static void AddRow(TableLayoutPanel table, int row, string label, Control control, Control? action = null)
    {
        control.Dock = DockStyle.Fill;
        table.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left }, 0, row);
        table.Controls.Add(control, 1, row);
        if (action is not null) table.Controls.Add(action, 2, row);
    }

    private Button BrowseFolder(TextBox target)
    {
        var button = new Button { Text = "浏览…", Dock = DockStyle.Fill };
        button.Click += (_, _) =>
        {
            using var dialog = new FolderBrowserDialog { SelectedPath = target.Text };
            if (dialog.ShowDialog(this) == DialogResult.OK) target.Text = dialog.SelectedPath;
        };
        return button;
    }

    private Button BrowseFile(TextBox target)
    {
        var button = new Button { Text = "浏览…", Dock = DockStyle.Fill };
        button.Click += (_, _) =>
        {
            using var dialog = new OpenFileDialog { Filter = "INI 配方|*.ini|所有文件|*.*", FileName = target.Text };
            if (dialog.ShowDialog(this) == DialogResult.OK) target.Text = dialog.FileName;
        };
        return button;
    }

    private void LoadValues()
    {
        _host.Text = _settings.MeasurementHost;
        _port.Value = _settings.MeasurementPort;
        _timeout.Value = _settings.ResultTimeoutSeconds;
        _settle.Value = _settings.PatternSettleMilliseconds;
        _autoConnect.Checked = _settings.AutoConnect;
        _patternDirectory.Text = _settings.PatternDirectory;
        _legacyIni.Text = _settings.LegacyRecipePath;
        _wallpaper.Checked = _settings.SetDesktopWallpaper;
        _serialEnabled.Checked = _settings.EnableSerialProjection;
        _serialPort.Text = _settings.SerialPortName;
        _baud.Value = Math.Clamp(_settings.SerialBaudRate, (int)_baud.Minimum, (int)_baud.Maximum);
        _extend.Checked = _settings.ExtendDesktopWhenProjecting;
    }

    private void SaveClicked(object? sender, EventArgs e)
    {
        try
        {
            _settings.MeasurementHost = _host.Text.Trim();
            _settings.MeasurementPort = (int)_port.Value;
            _settings.ResultTimeoutSeconds = (int)_timeout.Value;
            _settings.PatternSettleMilliseconds = (int)_settle.Value;
            _settings.AutoConnect = _autoConnect.Checked;
            _settings.PatternDirectory = _patternDirectory.Text.Trim();
            _settings.LegacyRecipePath = _legacyIni.Text.Trim();
            _settings.SetDesktopWallpaper = _wallpaper.Checked;
            _settings.EnableSerialProjection = _serialEnabled.Checked;
            _settings.SerialPortName = _serialPort.Text.Trim();
            _settings.SerialBaudRate = (int)_baud.Value;
            _settings.ExtendDesktopWhenProjecting = _extend.Checked;
            _store.Save(_settings);
            DialogResult = DialogResult.OK;
            Close();
        }
        catch (Exception exception)
        {
            MessageBox.Show(this, exception.Message, "设置无效", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }
}
