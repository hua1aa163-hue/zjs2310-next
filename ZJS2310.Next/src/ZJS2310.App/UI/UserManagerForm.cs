using ZJS2310.App.Infrastructure;

namespace ZJS2310.App.UI;

public sealed class UserManagerForm : Form
{
    private readonly AuthenticationService _authentication;
    private readonly ListBox _users = new() { Dock = DockStyle.Fill };
    private readonly TextBox _userName = new() { Dock = DockStyle.Fill };
    private readonly TextBox _password = new() { Dock = DockStyle.Fill, UseSystemPasswordChar = true };
    private readonly ComboBox _role = new() { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };

    public UserManagerForm(AuthenticationService authentication)
    {
        _authentication = authentication;
        Text = "用户管理";
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(650, 390);
        ClientSize = new Size(700, 430);
        _role.Items.AddRange(["管理员", "工程师", "操作员"]);
        _role.SelectedIndex = 2;
        _users.SelectedIndexChanged += UserSelected;

        var split = new SplitContainer { Dock = DockStyle.Fill, SplitterDistance = 220, FixedPanel = FixedPanel.Panel1 };
        split.Panel1.Padding = new Padding(12);
        split.Panel1.Controls.Add(_users);

        var editor = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(16), ColumnCount = 2, RowCount = 5 };
        editor.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90));
        editor.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        editor.RowStyles.Add(new RowStyle(SizeType.Absolute, 45));
        editor.RowStyles.Add(new RowStyle(SizeType.Absolute, 45));
        editor.RowStyles.Add(new RowStyle(SizeType.Absolute, 45));
        editor.RowStyles.Add(new RowStyle(SizeType.Absolute, 55));
        editor.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        editor.Controls.Add(LabelFor("用户名"), 0, 0);
        editor.Controls.Add(_userName, 1, 0);
        editor.Controls.Add(LabelFor("新密码"), 0, 1);
        editor.Controls.Add(_password, 1, 1);
        editor.Controls.Add(LabelFor("角色"), 0, 2);
        editor.Controls.Add(_role, 1, 2);

        var save = new Button { Text = "新增 / 重置密码", AutoSize = true };
        var delete = new Button { Text = "删除用户", AutoSize = true };
        save.Click += SaveClicked;
        delete.Click += DeleteClicked;
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill };
        buttons.Controls.Add(save);
        buttons.Controls.Add(delete);
        editor.Controls.Add(buttons, 0, 3);
        editor.SetColumnSpan(buttons, 2);
        editor.Controls.Add(new Label
        {
            Text = "说明：保存同名用户会重置其密码。内置 admin 管理员不可删除。首次运行默认密码为 1234，请及时修改。",
            Dock = DockStyle.Fill,
            ForeColor = Color.DimGray
        }, 0, 4);
        editor.SetColumnSpan(editor.GetControlFromPosition(0, 4)!, 2);
        split.Panel2.Controls.Add(editor);
        Controls.Add(split);
        RefreshUsers();
    }

    private static Label LabelFor(string text) => new() { Text = text, AutoSize = true, Anchor = AnchorStyles.Left };

    private void RefreshUsers()
    {
        _users.Items.Clear();
        foreach (var user in _authentication.GetUsers())
        {
            _users.Items.Add(user);
        }

        _users.DisplayMember = nameof(UserSummary.UserName);
    }

    private void UserSelected(object? sender, EventArgs e)
    {
        if (_users.SelectedItem is not UserSummary user)
        {
            return;
        }

        _userName.Text = user.UserName;
        _role.SelectedItem = user.Role;
        if (_role.SelectedIndex < 0)
        {
            _role.SelectedIndex = 2;
        }
        _password.Clear();
    }

    private void SaveClicked(object? sender, EventArgs e)
    {
        try
        {
            _authentication.Upsert(_userName.Text, _password.Text, _role.Text);
            RefreshUsers();
            _password.Clear();
            MessageBox.Show(this, "用户已保存。", "用户管理", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception exception)
        {
            MessageBox.Show(this, exception.Message, "无法保存", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void DeleteClicked(object? sender, EventArgs e)
    {
        if (_users.SelectedItem is not UserSummary user ||
            MessageBox.Show(this, $"确认删除用户 {user.UserName}？", "删除确认", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
        {
            return;
        }

        try
        {
            _authentication.Delete(user.UserName);
            RefreshUsers();
        }
        catch (Exception exception)
        {
            MessageBox.Show(this, exception.Message, "无法删除", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }
}
