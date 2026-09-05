using BSGroupGenerator.Core;

namespace BSGroupGenerator.UI;

/// <summary>
/// 规则归组对话框：按包含/排除关键字批量把服装加入或移出某个组，
/// 支持同时匹配所属模组名、仅处理未分配服装，并实时预览命中数量。
/// 预览计算带 300ms 防抖；所属模组映射在打开时构建一次。
/// </summary>
public class RuleGroupDialog : Form
{
    private readonly ComboBox _cboGroup = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly ComboBox _cboDirection = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly TextBox _txtInclude = MakePaddedBox("多个用分号分隔；留空 = 全部服装");
    private readonly TextBox _txtExclude = MakePaddedBox("命中的服装将被排除");
    private readonly CheckBox _chkOwner = new() { Text = "同时匹配所属模组名", AutoSize = true, Checked = true };
    private readonly CheckBox _chkUnassigned = new() { Text = "仅未分配服装", AutoSize = true, Checked = true };
    private readonly Label _lblPreview = new()
    {
        Text = "将命中 0 个服装：",
        AutoSize = true,
        ForeColor = SystemColors.GrayText,
    };
    private readonly ListBox _lstPreview = new()
    {
        Dock = DockStyle.Fill,
        IntegralHeight = false,
        BorderStyle = BorderStyle.FixedSingle,
    };
    private readonly System.Windows.Forms.Timer _previewDebounce = new() { Interval = 300 };

    private readonly IReadOnlyList<SliderGroup> _groups;
    private readonly Dictionary<string, string> _ownerByOutfit;
    private readonly Func<string, string, bool, bool, List<string>> _preview;

    public string GroupName => (_cboGroup.SelectedItem as SliderGroup)?.Name ?? "";
    public bool Add => _cboDirection.SelectedIndex == 0;
    public string Include => _txtInclude.Text;
    public string Exclude => _txtExclude.Text;
    public bool MatchOwner => _chkOwner.Checked;
    public bool UnassignedOnly => _chkUnassigned.Checked;

    public RuleGroupDialog(IReadOnlyList<SliderGroup> groups, string? preselectGroupName,
        Dictionary<string, string> ownerByOutfit,
        Func<string, string, bool, bool, List<string>> preview)
    {
        _groups = groups;
        _ownerByOutfit = ownerByOutfit;
        _preview = preview;

        Text = "规则归组";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MinimizeBox = false;
        MaximizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(508, 478);

        var table = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 7,
            Padding = new Padding(4),
        };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 96));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (var i = 0; i < 5; i++)
            table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        table.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        table.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        _cboGroup.Width = 364;
        _cboDirection.Width = 364;
        _cboDirection.Items.AddRange(["加入组", "移出组"]);
        _cboDirection.SelectedIndex = 0;
        AddRow(table, 0, "目标组", _cboGroup);
        AddRow(table, 1, "方向", _cboDirection);
        AddRow(table, 2, "包含关键字", PaddedHost(_txtInclude, 364));
        AddRow(table, 3, "排除关键字", PaddedHost(_txtExclude, 364));

        var checks = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            WrapContents = false,
            AutoSize = true,
            Padding = new Padding(0, 4, 0, 0),
        };
        _chkOwner.Margin = new Padding(0, 2, 14, 2);
        _chkUnassigned.Margin = new Padding(0, 2, 0, 2);
        checks.Controls.AddRange([_chkOwner, _chkUnassigned]);
        table.Controls.Add(checks, 1, 4);
        table.Controls.Add(new Label
        {
            Text = "范围",
            TextAlign = ContentAlignment.MiddleRight,
            Dock = DockStyle.Fill,
        }, 0, 4);

        _lblPreview.Margin = new Padding(2, 8, 2, 4);
        table.Controls.Add(_lblPreview, 0, 5);
        table.SetColumnSpan(_lblPreview, 2);
        _lstPreview.Margin = new Padding(2, 0, 2, 6);
        table.Controls.Add(_lstPreview, 0, 6);
        table.SetColumnSpan(_lstPreview, 2);

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 42,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(4, 6, 4, 4),
        };
        var btnOk = new Button { Text = "应用", MinimumSize = new Size(96, 30), DialogResult = DialogResult.OK };
        var btnCancel = new Button { Text = "取消", MinimumSize = new Size(80, 30), DialogResult = DialogResult.Cancel };
        buttons.Controls.Add(btnOk);
        buttons.Controls.Add(btnCancel);

        Controls.Add(table);
        Controls.Add(buttons);
        AcceptButton = btnOk;
        CancelButton = btnCancel;

        _cboGroup.DataSource = null;
        _cboGroup.DataSource = _groups;
        _cboGroup.DisplayMember = nameof(SliderGroup.Name);
        if (preselectGroupName is not null)
            _cboGroup.SelectedItem = _groups.FirstOrDefault(g => g.Name == preselectGroupName);

        EventHandler changed = (_, _) => { _previewDebounce.Stop(); _previewDebounce.Start(); };
        _txtInclude.TextChanged += changed;
        _txtExclude.TextChanged += changed;
        _chkOwner.CheckedChanged += changed;
        _chkUnassigned.CheckedChanged += changed;
        _previewDebounce.Tick += (_, _) =>
        {
            _previewDebounce.Stop();
            UpdatePreview();
        };
        UpdatePreview();
    }

    private static void AddRow(TableLayoutPanel table, int row, string labelText, Control control)
    {
        table.Controls.Add(new Label
        {
            Text = labelText,
            TextAlign = ContentAlignment.MiddleRight,
            Dock = DockStyle.Fill,
        }, 0, row);
        control.Margin = new Padding(0, 3, 0, 3);
        table.Controls.Add(control, 1, row);
    }

    /// <summary>
    /// 白色衬底面板承载无边框文本框：左内边距 7px。
    /// 不用 EM_SETMARGINS 的原因：占位文字自绘不覆盖边距区域，会露出灰色底。
    /// </summary>
    private static Panel PaddedHost(TextBox box, int width)
    {
        var host = new Panel
        {
            Size = new Size(width, 26),
            BackColor = Color.White,
            Margin = new Padding(0, 3, 0, 3),
        };
        host.Paint += (_, e) => e.Graphics.DrawRectangle(
            new Pen(Color.FromArgb(213, 216, 224)), 0, 0, host.Width - 1, host.Height - 1);
        box.BorderStyle = BorderStyle.None;
        box.Location = new Point(7, 2);
        box.Size = new Size(width - 12, 22);
        host.Controls.Add(box);
        return host;
    }

    private static TextBox MakePaddedBox(string placeholder)
    {
        return new TextBox
        {
            BorderStyle = BorderStyle.None,
            BackColor = Color.White,
            PlaceholderText = placeholder,
        };
    }

    private void UpdatePreview()
    {
        var matched = _preview(Include, Exclude, MatchOwner, UnassignedOnly);
        _lblPreview.Text = $"将命中 {matched.Count} 个服装：";
        _lstPreview.BeginUpdate();
        _lstPreview.Items.Clear();
        foreach (var name in matched.Take(30))
            _lstPreview.Items.Add(name);
        if (matched.Count > 30)
            _lstPreview.Items.Add($"… 共 {matched.Count} 个");
        _lstPreview.EndUpdate();
    }
}
