using ZJS2310.App.Infrastructure;

namespace ZJS2310.App.UI;

public sealed class LoginDialog : Form
{
    private readonly AuthenticationService _authentication;
    private readonly TextBox _userName = new() { Dock = DockStyle.Fill };
    private readonly TextBox _password = new() { Dock = DockStyle.Fill, UseSystemPasswordChar = true };

    public LoginDialog(AuthenticationService authentication)
    {
        _authentication = authentication;
        Text = "用户登录";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ClientSize = new Size(380, 175);

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(16),
            ColumnCount = 2,
            RowCount = 3
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 82));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.Controls.Add(new Label { Text = "用户名", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 0);
        layout.Controls.Add(_userName, 1, 0);
        layout.Controls.Add(new Label { Text = "密码", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 1);
        layout.Controls.Add(_password, 1, 1);

        var login = new Button { Text = "登录", AutoSize = true };
        var cancel = new Button { Text = "取消", AutoSize = true, DialogResult = DialogResult.Cancel };
        login.Click += LoginClicked;
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
        buttons.Controls.Add(cancel);
        buttons.Controls.Add(login);
        layout.Controls.Add(buttons, 0, 2);
        layout.SetColumnSpan(buttons, 2);
        Controls.Add(layout);
        AcceptButton = login;
        CancelButton = cancel;
        Shown += (_, _) => _userName.Focus();
    }

    public UserSummary? AuthenticatedUser { get; private set; }

    private void LoginClicked(object? sender, EventArgs e)
    {
        if (_authentication.Authenticate(_userName.Text, _password.Text, out var user))
        {
            AuthenticatedUser = user;
            DialogResult = DialogResult.OK;
            Close();
            return;
        }

        MessageBox.Show(this, "用户名或密码错误。", "登录失败", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        _password.SelectAll();
        _password.Focus();
    }
}
