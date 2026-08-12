using System.Globalization;
using ZJS2310.Core.Abstractions;
using ZJS2310.Core.Domain;
using ZJS2310.Core.Services;

namespace ZJS2310.App.UI;

public sealed class RecipeEditorForm : Form
{
    private readonly IRecipeRepository _repository;
    private readonly TestCatalog _catalog;
    private readonly ComboBox _recipeSelector = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 230 };
    private readonly TextBox _recipeName = new() { Width = 230 };
    private readonly CheckedListBox _tests = new() { Dock = DockStyle.Fill, CheckOnClick = true };
    private readonly DataGridView _thresholds = new()
    {
        Dock = DockStyle.Fill,
        AllowUserToAddRows = false,
        AllowUserToDeleteRows = false,
        AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
        RowHeadersVisible = false
    };
    private List<TestRecipe> _recipes;

    public RecipeEditorForm(IRecipeRepository repository, TestCatalog catalog)
    {
        _repository = repository;
        _catalog = catalog;
        _recipes = repository.GetAll().Select(item => item.Clone()).ToList();
        Text = "测试配方管理";
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(920, 600);
        ClientSize = new Size(1050, 700);

        _thresholds.Columns.Add(new DataGridViewTextBoxColumn { Name = "Key", Visible = false });
        _thresholds.Columns.Add(new DataGridViewTextBoxColumn { Name = "Metric", HeaderText = "指标", ReadOnly = true, FillWeight = 160 });
        _thresholds.Columns.Add(new DataGridViewTextBoxColumn { Name = "Minimum", HeaderText = "最小值（可空）" });
        _thresholds.Columns.Add(new DataGridViewTextBoxColumn { Name = "Maximum", HeaderText = "最大值（可空）" });
        foreach (var definition in _catalog.All) _tests.Items.Add(definition);
        _tests.DisplayMember = nameof(TestDefinition.Name);
        _recipeSelector.SelectedIndexChanged += (_, _) => LoadSelectedRecipe();

        var header = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 52, Padding = new Padding(10), WrapContents = false };
        header.Controls.Add(new Label { Text = "配方", AutoSize = true, Margin = new Padding(0, 8, 6, 0) });
        header.Controls.Add(_recipeSelector);
        header.Controls.Add(new Label { Text = "名称", AutoSize = true, Margin = new Padding(18, 8, 6, 0) });
        header.Controls.Add(_recipeName);

        var split = new SplitContainer { Dock = DockStyle.Fill, SplitterDistance = 300, FixedPanel = FixedPanel.Panel1 };
        var testsGroup = new GroupBox { Text = "启用的测试项", Dock = DockStyle.Fill, Padding = new Padding(10) };
        testsGroup.Controls.Add(_tests);
        var thresholdGroup = new GroupBox { Text = "判定阈值（空白表示只采集、不判定）", Dock = DockStyle.Fill, Padding = new Padding(10) };
        thresholdGroup.Controls.Add(_thresholds);
        split.Panel1.Padding = new Padding(10);
        split.Panel2.Padding = new Padding(10);
        split.Panel1.Controls.Add(testsGroup);
        split.Panel2.Controls.Add(thresholdGroup);

        var footer = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 58,
            Padding = new Padding(10),
            FlowDirection = FlowDirection.RightToLeft
        };
        var close = new Button { Text = "关闭", AutoSize = true, DialogResult = DialogResult.OK };
        var delete = new Button { Text = "删除", AutoSize = true };
        var save = new Button { Text = "保存", AutoSize = true };
        var create = new Button { Text = "新建", AutoSize = true };
        delete.Click += DeleteClicked;
        save.Click += SaveClicked;
        create.Click += CreateClicked;
        footer.Controls.Add(close);
        footer.Controls.Add(delete);
        footer.Controls.Add(save);
        footer.Controls.Add(create);
        Controls.Add(split);
        Controls.Add(header);
        Controls.Add(footer);
        RefreshSelector(0);
    }

    private void RefreshSelector(int selectedIndex)
    {
        _recipeSelector.Items.Clear();
        foreach (var recipe in _recipes) _recipeSelector.Items.Add(recipe.Name);
        if (_recipes.Count > 0) _recipeSelector.SelectedIndex = Math.Clamp(selectedIndex, 0, _recipes.Count - 1);
    }

    private void LoadSelectedRecipe()
    {
        if (_recipeSelector.SelectedIndex < 0) return;
        var recipe = _recipes[_recipeSelector.SelectedIndex];
        _recipeName.Text = recipe.Name;
        for (var index = 0; index < _tests.Items.Count; index++)
        {
            var definition = (TestDefinition)_tests.Items[index]!;
            _tests.SetItemChecked(index, recipe.EnabledTestCodes.Contains(definition.Code));
        }

        _thresholds.Rows.Clear();
        foreach (var metric in _catalog.All.SelectMany(item => item.Metrics).DistinctBy(item => item.Key))
        {
            recipe.Thresholds.TryGetValue(metric.Key, out var range);
            _thresholds.Rows.Add(
                metric.Key,
                metric.DisplayName,
                Format(range?.Minimum),
                Format(range?.Maximum));
        }
    }

    private void SaveClicked(object? sender, EventArgs e)
    {
        if (_recipeSelector.SelectedIndex < 0) return;
        try
        {
            var recipe = _recipes[_recipeSelector.SelectedIndex];
            recipe.Name = _recipeName.Text.Trim();
            recipe.EnabledTestCodes.Clear();
            foreach (var checkedItem in _tests.CheckedItems.Cast<TestDefinition>())
            {
                recipe.EnabledTestCodes.Add(checkedItem.Code);
            }

            recipe.Thresholds.Clear();
            foreach (DataGridViewRow row in _thresholds.Rows)
            {
                var key = Convert.ToString(row.Cells["Key"].Value, CultureInfo.InvariantCulture)!;
                var minimumText = Convert.ToString(row.Cells["Minimum"].Value, CultureInfo.CurrentCulture);
                var maximumText = Convert.ToString(row.Cells["Maximum"].Value, CultureInfo.CurrentCulture);
                var minimum = ParseNullable(minimumText, row.Index, "最小值");
                var maximum = ParseNullable(maximumText, row.Index, "最大值");
                if (minimum.HasValue && maximum.HasValue && minimum > maximum)
                {
                    throw new InvalidOperationException($"第 {row.Index + 1} 行最小值不能大于最大值。");
                }

                recipe.Thresholds[key] = new RangeSpec(minimum, maximum);
            }

            _repository.SaveAll(_recipes);
            _recipes = _repository.GetAll().Select(item => item.Clone()).ToList();
            RefreshSelector(_recipeSelector.SelectedIndex);
            MessageBox.Show(this, "配方已保存。", "配方管理", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception exception)
        {
            MessageBox.Show(this, exception.Message, "无法保存配方", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void CreateClicked(object? sender, EventArgs e)
    {
        var number = 1;
        string name;
        do name = $"新配方{number++}";
        while (_recipes.Any(item => string.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase)));
        _recipes.Add(new TestRecipe { Name = name });
        RefreshSelector(_recipes.Count - 1);
    }

    private void DeleteClicked(object? sender, EventArgs e)
    {
        if (_recipeSelector.SelectedIndex < 0) return;
        if (_recipes.Count == 1)
        {
            MessageBox.Show(this, "至少需要保留一个配方。", "无法删除", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var index = _recipeSelector.SelectedIndex;
        var name = _recipes[index].Name;
        if (MessageBox.Show(this, $"确认删除配方“{name}”？", "删除确认", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
        {
            return;
        }

        _recipes.RemoveAt(index);
        _repository.SaveAll(_recipes);
        RefreshSelector(Math.Max(0, index - 1));
    }

    private static double? ParseNullable(string? text, int rowIndex, string columnName)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        if (double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out var value) ||
            double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
        {
            return value;
        }

        throw new InvalidOperationException($"第 {rowIndex + 1} 行{columnName}不是有效数字。");
    }

    private static string Format(double? value) => value?.ToString("G17", CultureInfo.CurrentCulture) ?? string.Empty;
}
