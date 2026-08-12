using System.Diagnostics;
using ZJS2310.App.Infrastructure;
using ZJS2310.Core.Abstractions;
using ZJS2310.Core.Domain;
using ZJS2310.Core.Services;

namespace ZJS2310.App.UI;

public sealed class MainForm : Form
{
    private readonly DataPaths _paths;
    private readonly AppSettings _settings;
    private readonly AppSettingsStore _settingsStore;
    private readonly TestCatalog _catalog;
    private readonly IRecipeRepository _recipes;
    private readonly AuthenticationService _authentication;
    private readonly TcpMeasurementClient _client;
    private readonly ProjectionController _projection;
    private readonly TestSequenceService _sequence;
    private readonly FileLogger _logger;
    private readonly ComboBox _recipe = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 210 };
    private readonly TextBox _serialNumber = new() { Width = 240 };
    private readonly Button _connect = new() { Text = "连接服务", AutoSize = true };
    private readonly Button _start = new() { Text = "开始测试", AutoSize = true };
    private readonly Button _stop = new() { Text = "停止", AutoSize = true, Enabled = false };
    private readonly Button _closeProjection = new() { Text = "关闭投影", AutoSize = true };
    private readonly Label _connectionState = StatusLabel("未连接", Color.Firebrick);
    private readonly Label _runState = StatusLabel("就绪", Color.DimGray);
    private readonly Label _okCount = StatisticLabel("通过 0");
    private readonly Label _ngCount = StatisticLabel("不通过 0");
    private readonly Label _totalCount = StatisticLabel("总数 0");
    private readonly Label _yield = StatisticLabel("良率 0.00%");
    private readonly DataGridView _currentSteps = Grid();
    private readonly DataGridView _history = Grid();
    private readonly RichTextBox _log = new() { Dock = DockStyle.Fill, ReadOnly = true, BackColor = Color.FromArgb(25, 25, 25), ForeColor = Color.Gainsboro };
    private readonly PictureBox _preview = new() { Dock = DockStyle.Fill, SizeMode = PictureBoxSizeMode.Zoom, BackColor = Color.Black };
    private readonly ToolStripMenuItem _recipeManagement = new("配方管理") { Enabled = false };
    private readonly ToolStripMenuItem _userManagement = new("用户管理") { Enabled = false };
    private readonly ToolStripMenuItem _logout = new("退出登录") { Enabled = false };
    private readonly ToolStripStatusLabel _userStatus = new("未登录");
    private readonly List<TestRun> _runs = [];
    private CancellationTokenSource? _runCancellation;
    private UserSummary? _currentUser;

    public MainForm(
        DataPaths paths,
        AppSettings settings,
        AppSettingsStore settingsStore,
        TestCatalog catalog,
        IRecipeRepository recipes,
        AuthenticationService authentication,
        TcpMeasurementClient client,
        ProjectionController projection,
        TestSequenceService sequence,
        FileLogger logger)
    {
        _paths = paths;
        _settings = settings;
        _settingsStore = settingsStore;
        _catalog = catalog;
        _recipes = recipes;
        _authentication = authentication;
        _client = client;
        _projection = projection;
        _sequence = sequence;
        _logger = logger;

        Text = "ZJS2310 光学测试系统 · 重构版";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(1100, 720);
        WindowState = FormWindowState.Maximized;
        Font = new Font("Microsoft YaHei UI", 9F);

        BuildInterface();
        WireEvents();
        RefreshRecipes();
    }

    private void BuildInterface()
    {
        var menu = BuildMenu();
        MainMenuStrip = menu;
        Controls.Add(menu);

        var status = new StatusStrip();
        status.Items.Add(new ToolStripStatusLabel("当前用户："));
        status.Items.Add(_userStatus);
        status.Items.Add(new ToolStripStatusLabel { Spring = true });
        status.Items.Add(new ToolStripStatusLabel($"数据目录：{_paths.DataDirectory}"));
        Controls.Add(status);

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(12), RowCount = 4, ColumnCount = 1 };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 76));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 72));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 210));
        root.Controls.Add(BuildOperationBar(), 0, 0);
        root.Controls.Add(BuildStatistics(), 0, 1);
        root.Controls.Add(BuildCurrentArea(), 0, 2);
        root.Controls.Add(BuildHistory(), 0, 3);
        Controls.Add(root);
        root.BringToFront();

        ConfigureCurrentGrid();
        ConfigureHistoryGrid();
    }

    private MenuStrip BuildMenu()
    {
        var menu = new MenuStrip();
        var system = new ToolStripMenuItem("系统");
        var connectionSettings = new ToolStripMenuItem("连接与设备设置");
        var openData = new ToolStripMenuItem("打开数据目录");
        var exit = new ToolStripMenuItem("退出");
        connectionSettings.Click += SettingsClicked;
        openData.Click += (_, _) => Process.Start(new ProcessStartInfo(_paths.DataDirectory) { UseShellExecute = true });
        exit.Click += (_, _) => Close();
        system.DropDownItems.AddRange([connectionSettings, openData, new ToolStripSeparator(), exit]);

        var management = new ToolStripMenuItem("管理");
        var login = new ToolStripMenuItem("用户登录");
        login.Click += LoginClicked;
        _logout.Click += (_, _) => SetCurrentUser(null);
        _recipeManagement.Click += RecipeManagementClicked;
        _userManagement.Click += UserManagementClicked;
        management.DropDownItems.AddRange([login, _logout, new ToolStripSeparator(), _recipeManagement, _userManagement]);
        menu.Items.AddRange([system, management]);
        return menu;
    }

    private Control BuildOperationBar()
    {
        var group = new GroupBox { Text = "测试操作", Dock = DockStyle.Fill };
        var flow = new FlowLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(10, 8, 10, 4), WrapContents = false };
        flow.Controls.Add(FlowLabel("SN"));
        flow.Controls.Add(_serialNumber);
        flow.Controls.Add(FlowLabel("测试配方", 18));
        flow.Controls.Add(_recipe);
        flow.Controls.Add(_connect);
        flow.Controls.Add(_start);
        flow.Controls.Add(_stop);
        flow.Controls.Add(_closeProjection);
        flow.Controls.Add(FlowLabel("连接：", 24));
        flow.Controls.Add(_connectionState);
        flow.Controls.Add(FlowLabel("流程：", 18));
        flow.Controls.Add(_runState);
        group.Controls.Add(flow);
        return group;
    }

    private Control BuildStatistics()
    {
        var panel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, Padding = new Padding(0, 8, 0, 8) };
        for (var i = 0; i < 4; i++) panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
        panel.Controls.Add(StatisticCard(_okCount, Color.SeaGreen), 0, 0);
        panel.Controls.Add(StatisticCard(_ngCount, Color.Firebrick), 1, 0);
        panel.Controls.Add(StatisticCard(_totalCount, Color.SteelBlue), 2, 0);
        panel.Controls.Add(StatisticCard(_yield, Color.DarkOrange), 3, 0);
        return panel;
    }

    private Control BuildCurrentArea()
    {
        var split = new SplitContainer { Dock = DockStyle.Fill, SplitterDistance = 690 };
        var current = new GroupBox { Text = "本次测试明细", Dock = DockStyle.Fill, Padding = new Padding(8) };
        current.Controls.Add(_currentSteps);
        split.Panel1.Controls.Add(current);

        var tabs = new TabControl { Dock = DockStyle.Fill };
        var previewTab = new TabPage("测试画面预览");
        var logTab = new TabPage("运行日志");
        previewTab.Controls.Add(_preview);
        logTab.Controls.Add(_log);
        tabs.TabPages.Add(previewTab);
        tabs.TabPages.Add(logTab);
        split.Panel2.Controls.Add(tabs);
        return split;
    }

    private Control BuildHistory()
    {
        var group = new GroupBox { Text = "本次启动期间的测试记录", Dock = DockStyle.Fill, Padding = new Padding(8) };
        group.Controls.Add(_history);
        return group;
    }

    private void ConfigureCurrentGrid()
    {
        _currentSteps.Columns.Add("Code", "指令");
        _currentSteps.Columns.Add("Name", "测试项");
        _currentSteps.Columns.Add("Status", "状态");
        _currentSteps.Columns.Add("Metrics", "测量值 / 判定");
        _currentSteps.Columns.Add("Duration", "耗时");
        _currentSteps.Columns[0].Width = 65;
        _currentSteps.Columns[1].Width = 150;
        _currentSteps.Columns[2].Width = 85;
        _currentSteps.Columns[3].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
        _currentSteps.Columns[4].Width = 85;
    }

    private void ConfigureHistoryGrid()
    {
        _history.Columns.Add("Time", "开始时间");
        _history.Columns.Add("SN", "SN");
        _history.Columns.Add("Recipe", "配方");
        _history.Columns.Add("Status", "运行状态");
        _history.Columns.Add("Verdict", "判定");
        _history.Columns.Add("Duration", "耗时");
        _history.Columns[0].Width = 170;
        _history.Columns[1].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
        _history.Columns[2].Width = 170;
        _history.Columns[3].Width = 100;
        _history.Columns[4].Width = 100;
        _history.Columns[5].Width = 100;
    }

    private void WireEvents()
    {
        Load += MainFormLoaded;
        FormClosing += (_, _) => _runCancellation?.Cancel();
        _connect.Click += ConnectClicked;
        _start.Click += StartClicked;
        _stop.Click += (_, _) => _runCancellation?.Cancel();
        _closeProjection.Click += CloseProjectionClicked;
        _client.ConnectionStateChanged += (_, connected) => Post(() => ShowConnectionState(connected));
        _sequence.StepStarted += (_, args) => Post(() => ShowStepStarted(args));
        _sequence.StepCompleted += (_, args) => Post(() => ShowStepCompleted(args));
        _logger.EntryWritten += (_, entry) => Post(() => AppendLog(entry));
    }

    private async void MainFormLoaded(object? sender, EventArgs e)
    {
        AppendLog(new LogEntry(DateTimeOffset.Now, "INFO", "程序已启动。默认管理员为 admin / 1234，请登录后修改密码。"));
        if (_settings.AutoConnect)
        {
            await ConnectSafelyAsync();
        }
    }

    private async void ConnectClicked(object? sender, EventArgs e)
    {
        if (_client.IsConnected)
        {
            await _client.DisconnectAsync();
        }
        else
        {
            await ConnectSafelyAsync();
        }
    }

    private async Task ConnectSafelyAsync()
    {
        _connect.Enabled = false;
        _connectionState.Text = "连接中…";
        try
        {
            await _client.ConnectAsync(CancellationToken.None);
        }
        catch (Exception exception)
        {
            _logger.Error("连接测量服务失败。", exception);
            MessageBox.Show(this, $"连接失败：{exception.Message}", "测量服务", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        finally
        {
            _connect.Enabled = true;
            ShowConnectionState(_client.IsConnected);
        }
    }

    private async void StartClicked(object? sender, EventArgs e)
    {
        if (_recipe.SelectedItem is not TestRecipe recipe)
        {
            MessageBox.Show(this, "请选择测试配方。", "无法开始", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (string.IsNullOrWhiteSpace(_serialNumber.Text))
        {
            MessageBox.Show(this, "请输入 SN。", "无法开始", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            _serialNumber.Focus();
            return;
        }

        if (!_client.IsConnected)
        {
            MessageBox.Show(this, "请先连接测量服务。", "无法开始", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        SetBusy(true);
        _currentSteps.Rows.Clear();
        _runCancellation = new CancellationTokenSource();
        try
        {
            var run = await _sequence.RunAsync(_serialNumber.Text, recipe.Clone(), _runCancellation.Token);
            AddRun(run);
            _runState.Text = RunText(run);
            _runState.ForeColor = run.Verdict == Verdict.Passed ? Color.SeaGreen : Color.Firebrick;
            if (run.Status is RunStatus.Failed or RunStatus.Cancelled)
            {
                MessageBox.Show(this, run.ErrorMessage ?? "测试未完成。", "测试结束", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
        catch (Exception exception)
        {
            _logger.Error("无法启动测试。", exception);
            MessageBox.Show(this, exception.Message, "测试失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            _runCancellation.Dispose();
            _runCancellation = null;
            SetBusy(false);
        }
    }

    private async void CloseProjectionClicked(object? sender, EventArgs e)
    {
        _closeProjection.Enabled = false;
        try
        {
            await _projection.ShutdownAsync(CancellationToken.None);
            _logger.Info("投影输出已关闭。");
        }
        catch (Exception exception)
        {
            _logger.Error("关闭投影失败。", exception);
            MessageBox.Show(this, exception.Message, "关闭投影失败", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        finally
        {
            _closeProjection.Enabled = true;
        }
    }

    private void ShowStepStarted(StepStartedEventArgs args)
    {
        _runState.Text = $"{args.Index}/{args.Total}  {args.Definition.Name}";
        _runState.ForeColor = Color.SteelBlue;
        var row = _currentSteps.Rows.Add($"t{args.Definition.Code}", args.Definition.Name, "测试中", string.Empty, string.Empty);
        _currentSteps.Rows[row].DefaultCellStyle.BackColor = Color.LightCyan;
        ShowPattern(args.Definition);
    }

    private void ShowStepCompleted(StepCompletedEventArgs args)
    {
        var row = _currentSteps.Rows.Cast<DataGridViewRow>()
            .LastOrDefault(item => string.Equals(Convert.ToString(item.Cells[0].Value), $"t{args.Result.TestCode}", StringComparison.OrdinalIgnoreCase));
        if (row is null) return;
        row.Cells[2].Value = VerdictText(args.Result.Verdict);
        row.Cells[3].Value = args.Result.Metrics.Count == 0
            ? args.Result.Message
            : string.Join("；", args.Result.Metrics.Select(metric => $"{metric.DisplayName}={metric.Value:G6}({VerdictText(metric.Verdict)})"));
        row.Cells[4].Value = $"{args.Result.Duration.TotalSeconds:F2}s";
        row.DefaultCellStyle.BackColor = args.Result.Verdict switch
        {
            Verdict.Failed or Verdict.Error => Color.MistyRose,
            Verdict.Passed => Color.Honeydew,
            _ => Color.LightYellow
        };
    }

    private void ShowPattern(TestDefinition definition)
    {
        Image next;
        var path = _projection.ResolvePatternPath(definition.PatternFileName);
        try
        {
            if (File.Exists(path))
            {
                using var source = Image.FromFile(path);
                next = new Bitmap(source);
            }
            else
            {
                next = CreatePlaceholder(definition);
            }
        }
        catch (Exception exception)
        {
            _logger.Error($"无法加载画面预览 {path}。", exception);
            next = CreatePlaceholder(definition);
        }

        var previous = _preview.Image;
        _preview.Image = next;
        previous?.Dispose();
    }

    private static Bitmap CreatePlaceholder(TestDefinition definition)
    {
        var bitmap = new Bitmap(900, 520);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.Clear(Color.FromArgb(22, 26, 32));
        using var title = new Font("Microsoft YaHei UI", 34, FontStyle.Bold);
        using var subtitle = new Font("Consolas", 22, FontStyle.Regular);
        using var brush = new SolidBrush(Color.WhiteSmoke);
        using var secondary = new SolidBrush(Color.DeepSkyBlue);
        graphics.DrawString(definition.Name, title, brush, new PointF(55, 180));
        graphics.DrawString($"t{definition.Code} · {definition.PatternFileName}", subtitle, secondary, new PointF(58, 255));
        return bitmap;
    }

    private void AddRun(TestRun run)
    {
        _runs.Add(run);
        var index = _history.Rows.Add(
            run.StartedAt.ToString("yyyy-MM-dd HH:mm:ss"), run.SerialNumber, run.RecipeName,
            run.Status.ToString(), VerdictText(run.Verdict), $"{run.Duration.TotalSeconds:F1}s");
        _history.Rows[index].DefaultCellStyle.BackColor = run.Verdict == Verdict.Passed ? Color.Honeydew : Color.MistyRose;

        var completed = _runs.Where(item => item.Status == RunStatus.Completed).ToArray();
        var ok = completed.Count(item => item.Verdict == Verdict.Passed);
        var ng = completed.Length - ok;
        var rate = completed.Length == 0 ? 0d : 100d * ok / completed.Length;
        _okCount.Text = $"通过 {ok}";
        _ngCount.Text = $"不通过 {ng}";
        _totalCount.Text = $"总数 {completed.Length}";
        _yield.Text = $"良率 {rate:F2}%";
    }

    private void SettingsClicked(object? sender, EventArgs e)
    {
        var previousLegacyPath = _settings.LegacyRecipePath;
        using var dialog = new ConnectionSettingsForm(_settings, _settingsStore);
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        _sequence.UpdateOptions(new TestSequenceOptions(
            TimeSpan.FromSeconds(_settings.ResultTimeoutSeconds),
            TimeSpan.FromMilliseconds(_settings.PatternSettleMilliseconds)));
        _ = _client.DisconnectAsync();
        var importedCount = 0;
        if (!string.Equals(previousLegacyPath, _settings.LegacyRecipePath, StringComparison.OrdinalIgnoreCase) &&
            File.Exists(_settings.LegacyRecipePath))
        {
            var imported = new LegacyIniRecipeImporter(_logger).Import(_settings.LegacyRecipePath);
            if (imported.Count > 0)
            {
                var merged = _recipes.GetAll().Select(item => item.Clone()).ToList();
                foreach (var recipe in imported)
                {
                    merged.RemoveAll(item => string.Equals(item.Name, recipe.Name, StringComparison.OrdinalIgnoreCase));
                    merged.Add(recipe.Clone());
                }
                _recipes.SaveAll(merged);
                importedCount = imported.Count;
                RefreshRecipes();
            }
        }

        var importMessage = importedCount == 0 ? string.Empty : $" 已导入 {importedCount} 个旧版配方。";
        MessageBox.Show(this, $"设置已保存。{importMessage}测量服务连接已断开，请按新设置重新连接。", "设置", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void LoginClicked(object? sender, EventArgs e)
    {
        using var dialog = new LoginDialog(_authentication);
        if (dialog.ShowDialog(this) == DialogResult.OK) SetCurrentUser(dialog.AuthenticatedUser);
    }

    private void SetCurrentUser(UserSummary? user)
    {
        _currentUser = user;
        _userStatus.Text = user is null ? "未登录" : $"{user.UserName}（{user.Role}）";
        _logout.Enabled = user is not null;
        _recipeManagement.Enabled = user is not null;
        _userManagement.Enabled = string.Equals(user?.Role, "管理员", StringComparison.Ordinal);
    }

    private void RecipeManagementClicked(object? sender, EventArgs e)
    {
        using var dialog = new RecipeEditorForm(_recipes, _catalog);
        dialog.ShowDialog(this);
        RefreshRecipes();
    }

    private void UserManagementClicked(object? sender, EventArgs e)
    {
        using var dialog = new UserManagerForm(_authentication);
        dialog.ShowDialog(this);
    }

    private void RefreshRecipes()
    {
        var selectedName = (_recipe.SelectedItem as TestRecipe)?.Name;
        _recipe.Items.Clear();
        foreach (var recipe in _recipes.GetAll()) _recipe.Items.Add(recipe);
        _recipe.DisplayMember = nameof(TestRecipe.Name);
        var index = _recipe.Items.Cast<TestRecipe>().ToList()
            .FindIndex(item => string.Equals(item.Name, selectedName, StringComparison.OrdinalIgnoreCase));
        if (_recipe.Items.Count > 0) _recipe.SelectedIndex = index >= 0 ? index : 0;
    }

    private void ShowConnectionState(bool connected)
    {
        _connectionState.Text = connected ? "已连接" : "未连接";
        _connectionState.ForeColor = connected ? Color.SeaGreen : Color.Firebrick;
        _connect.Text = connected ? "断开服务" : "连接服务";
    }

    private void SetBusy(bool busy)
    {
        _start.Enabled = !busy;
        _stop.Enabled = busy;
        _connect.Enabled = !busy;
        _recipe.Enabled = !busy;
        _serialNumber.ReadOnly = busy;
        if (!busy && _runCancellation is null && _runState.Text.Contains('/')) _runState.Text = "就绪";
    }

    private void AppendLog(LogEntry entry)
    {
        _log.AppendText($"{entry.Timestamp:HH:mm:ss.fff} [{entry.Level}] {entry.Message}{Environment.NewLine}");
        _log.SelectionStart = _log.TextLength;
        _log.ScrollToCaret();
    }

    private void Post(Action action)
    {
        if (IsDisposed || Disposing || !IsHandleCreated) return;
        if (InvokeRequired) BeginInvoke(action); else action();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _runCancellation?.Dispose();
            _preview.Image?.Dispose();
        }
        base.Dispose(disposing);
    }

    private static DataGridView Grid() => new()
    {
        Dock = DockStyle.Fill,
        ReadOnly = true,
        AllowUserToAddRows = false,
        AllowUserToDeleteRows = false,
        MultiSelect = false,
        SelectionMode = DataGridViewSelectionMode.FullRowSelect,
        RowHeadersVisible = false,
        AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells
    };

    private static Label StatusLabel(string text, Color color) => new()
    {
        Text = text,
        ForeColor = color,
        AutoSize = true,
        Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Bold),
        Margin = new Padding(0, 8, 0, 0)
    };

    private static Label StatisticLabel(string text) => new()
    {
        Text = text,
        Dock = DockStyle.Fill,
        TextAlign = ContentAlignment.MiddleCenter,
        Font = new Font("Microsoft YaHei UI", 16F, FontStyle.Bold)
    };

    private static Control StatisticCard(Label content, Color color)
    {
        var panel = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(245, 247, 250), Margin = new Padding(6) };
        content.ForeColor = color;
        panel.Controls.Add(content);
        return panel;
    }

    private static Label FlowLabel(string text, int leftMargin = 0) => new()
    {
        Text = text,
        AutoSize = true,
        Margin = new Padding(leftMargin, 8, 5, 0)
    };

    private static string VerdictText(Verdict verdict) => verdict switch
    {
        Verdict.Passed => "通过",
        Verdict.Failed => "不通过",
        Verdict.Error => "错误",
        _ => "未判定"
    };

    private static string RunText(TestRun run) => run.Status switch
    {
        RunStatus.Cancelled => "已停止",
        RunStatus.Failed => "流程失败",
        _ => VerdictText(run.Verdict)
    };
}
