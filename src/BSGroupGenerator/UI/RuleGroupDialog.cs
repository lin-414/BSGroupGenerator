using BSGroupGenerator.Core;

namespace BSGroupGenerator.UI;

/// <summary>
/// 规则归组对话框：按包含/排除关键字批量把服装加入或移出某个组，
/// 支持同时匹配所属模组名、仅处理未分配服装，并实时预览命中数量。
/// </summary>
public class RuleGroupDialog : Form
{
    private readonly ComboBox _cboGroup = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly ComboBox _cboDirection = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly TextBox _txtInclude = new() { PlaceholderText = "多个用分号分隔；留空 = 全部服装" };
    private readonly TextBox _txtExclude = new() { PlaceholderText = "命中的服装将被排除" };
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
        IntegralHeight = false,
        BorderStyle = BorderStyle.FixedSingle,
    };

    private readonly IReadOnlyList<SliderGroup> _groups;
    private readonly Func<string, string, bool, bool, List<string>> _preview;

    public string GroupName => (_cboGroup.SelectedItem as SliderGroup)?.Name ?? "";
    public bool Add => _cboDirection.SelectedIndex == 0;
    public string Include => _txtInclude.Text;
    public string Exclude => _txtExclude.Text;
    public bool MatchOwner => _chkOwner.Checked;
    public bool UnassignedOnly => _chkUnassigned.Checked;

    public RuleGroupDialog(IReadOnlyList<SliderGroup> groups,
        Func<string, string, bool, bool, List<string>> preview)
    {
        _groups = groups;
        _preview = preview;

        Text = "规则归组";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MinimizeBox = false;
        MaximizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(508, 478);

        // 布局（绝对坐标，列：标签 16-106，控件 110-470）
        _cboDirection.Items.AddRange(["加入组", "移出组"]);
        _cboDirection.SelectedIndex = 0;
        AddLabeledRow(16, "目标组", _cboGroup, 364);
        AddLabeledRow(56, "方向", _cboDirection, 364);
        AddLabeledRow(96, "包含关键字", _txtInclude, 364);
        AddLabeledRow(136, "排除关键字", _txtExclude, 364);

        var lblScope = new Label
        {
            Text = "范围",
            Location = new Point(16, 180),
            AutoSize = true,
            ForeColor = SystemColors.GrayText,
        };
        _chkOwner.Location = new Point(110, 176);
        _chkUnassigned.Location = new Point(110, 202);

        _lblPreview.Location = new Point(16, 242);
        _lstPreview.Location = new Point(16, 266);
        _lstPreview.Size = new Size(458, 132);

        var btnOk = new Button
        {
            Text = "应用",
            DialogResult = DialogResult.OK,
            Location = new Point(ClientSize.Width - 188, ClientSize.Height - 44),
            Size = new Size(90, 30),
            Anchor = AnchorStyles.Bottom | AnchorStyles.Right,
        };
        var btnCancel = new Button
        {
            Text = "取消",
            DialogResult = DialogResult.Cancel,
            Location = new Point(ClientSize.Width - 90, ClientSize.Height - 44),
            Size = new Size(80, 30),
            Anchor = AnchorStyles.Bottom | AnchorStyles.Right,
        };

        Controls.AddRange([_cboGroup, _cboDirection, _txtInclude, _txtExclude, lblScope, _chkOwner,
            _chkUnassigned, _lblPreview, _lstPreview, btnOk, btnCancel]);
        AcceptButton = btnOk;
        CancelButton = btnCancel;

        _cboGroup.DataSource = null;
        _cboGroup.DataSource = _groups;
        _cboGroup.DisplayMember = nameof(SliderGroup.Name);

        EventHandler changed = (_, _) => UpdatePreview();
        _txtInclude.TextChanged += changed;
        _txtExclude.TextChanged += changed;
        _chkOwner.CheckedChanged += changed;
        _chkUnassigned.CheckedChanged += changed;
        UpdatePreview();
    }

    private void AddLabeledRow(int y, string labelText, Control control, int controlWidth)
    {
        var label = new Label
        {
            Text = labelText,
            AutoSize = true,
            ForeColor = SystemColors.GrayText,
        };
        control.Location = new Point(110, y);
        control.Size = new Size(controlWidth, control.Height);
        Controls.Add(control);
        Controls.Add(label);
        // 标签与控件垂直居中（控件含边框，文字居其中线）
        label.Location = new Point(16, control.Location.Y + (control.Height - label.Height) / 2 + 1);
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
