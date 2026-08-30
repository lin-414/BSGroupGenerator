using BSGroupGenerator.Core;

namespace BSGroupGenerator.UI;

/// <summary>程序内使用说明：分章节的详细帮助（RichTextBox 排版，可滚动、可复制）。</summary>
public class HelpDialog : Form
{
    private static readonly Color TitleColor = Color.FromArgb(30, 30, 36);

    public HelpDialog()
    {
        Text = "使用说明 — BS Group Generator";
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(880, 700);
        MinimumSize = new Size(700, 500);
        try
        {
            Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
        }
        catch
        {
            // 提取失败时使用默认图标
        }

        var rtb = new RichTextBox
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            BackColor = Color.White,
            BorderStyle = BorderStyle.None,
            Font = new Font("Microsoft YaHei UI", 9.5f),
            WordWrap = true,
        };

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 44,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(6, 6, 6, 6),
        };
        var btnClose = new Button { Text = "关闭", MinimumSize = new Size(90, 30), DialogResult = DialogResult.Cancel };
        buttons.Controls.Add(btnClose);

        Controls.Add(rtb);
        Controls.Add(buttons);
        CancelButton = btnClose;

        BuildContent(rtb);
    }

    private static void Append(RichTextBox rtb, string text, bool bold = false, bool heading = false)
    {
        rtb.SelectionStart = rtb.TextLength;
        rtb.SelectionColor = Color.Black;
        rtb.SelectionFont = new Font("Microsoft YaHei UI", heading ? 11.5f : 9.5f,
            heading ? FontStyle.Bold : bold ? FontStyle.Bold : FontStyle.Regular);
        rtb.AppendText(text);
        rtb.SelectionColor = rtb.ForeColor;
    }

    private static void H(RichTextBox rtb, string t) => Append(rtb, "\n" + t + "\n", heading: true);
    private static void P(RichTextBox rtb, string t) => Append(rtb, t + "\n");
    private static void B(RichTextBox rtb, string t) => Append(rtb, "  · " + t + "\n");

    private static void BuildContent(RichTextBox rtb)
    {
        P(rtb, "本工具读取 Mod Organizer 2（MO2）安装的模组，让你把服装（BodySlide 滑块组）批量划进分组，并按组生成 BodySlide 原生格式的分组文件。");

        H(rtb, "一、快速上手（4 步）");
        P(rtb, "1. 选择 MO2 实例与配置（Profile）——程序自动发现全局实例与便携安装；Profile 会默认带上次使用的。");
        P(rtb, "2. 确认 BodySlide 目录——通常自动检测成功。卡片下方的灰字\"有效项目路径\"就是 BodySlide 实际读取服装与分组的位置，请留意它。");
        P(rtb, "3. 建组与勾选——右侧「新建组」创建并选中一个组；左侧树勾选分隔符（=全选其下模组）或展开后勾选单个服装；点「加入当前组」才真正写入，绿色 ✔ 表示已在该组，「移出当前组」用于撤销勾选内容。");
        P(rtb, "4. 保存——点「保存分组文件」（Ctrl+S）。每个组生成一个文件，文件名即组名（如 UBE.xml）。完成后重启 BodySlide（通过 MO2 启动的建议连 MO2 一起重启）。");

        H(rtb, "二、界面各区域说明");
        P(rtb, "【顶部设置区】");
        B(rtb, "MO2 实例：自动发现 %LOCALAPPDATA%\\ModOrganizer 下的全局实例与便携安装；「添加 MO2 目录…」可手动指定实例目录；「刷新」重新扫描实例。");
        B(rtb, "配置 Profile：列出该实例所有可用的 Profile。启用模组列表读取自该 Profile 的 modlist.txt，顺序与 MO2 左侧栏一致。");
        B(rtb, "BodySlide：自动在启用模组中查找含 BodySlide*.exe 与 Config.xml 的目录；找不到时用「浏览…」手动指定。");
        B(rtb, "输出位置：决定分组文件保存到哪里（详见第五章）。状态栏右下角会显示当前输出目录，悬停可看完整路径。");
        P(rtb, "【左侧模组树】");
        B(rtb, "层级为 分隔符（灰色）→ 模组（加粗，可折叠）→ 服装；顺序与 MO2 左侧栏一致，只显示含 BodySlide 服装的启用模组。");
        B(rtb, "顶部过滤框：同时匹配服装名与模组名（连续子串、不区分大小写），模组名命中会显示该模组全部服装；「仅看未分配」只显示还没进任何组的服装；「展开全部模组」一键展开。");
        B(rtb, "勾选框只是\"选中\"待操作的内容——点「加入当前组」/「移出当前组」才写入。勾选模组或分隔符会自动勾选其下所有服装。");
        B(rtb, "绿色 ✔ 前缀 = 该服装已在当前选中的组里；模组标题后的 [组内 x/总数] 表示组内命中情况。");
        P(rtb, "【右侧组面板】");
        B(rtb, "组列表：单击选中（左侧勾选状态与标注随之切换且不打断浏览位置），双击或「查看组」打开成员预览（可过滤、可勾选批量移出）。");
        B(rtb, "「规则归组」：按关键字批量操作，详见第四章。");
        B(rtb, "「撤销」（Ctrl+Z）：回退最近 30 步分组操作，详见第五章。");
        B(rtb, "「导入现有组文件…」：把其他分组 XML 合并进当前列表继续编辑。");
        P(rtb, "【状态栏】");
        B(rtb, "左侧：统计（模组/服装/已分配/未分配）、「诊断」（查看实例发现与路径解析细节）、「日志」（展开运行日志）。");
        B(rtb, "右侧：当前输出目录描述，悬停显示完整路径。");

        H(rtb, "三、保存与文件布局");
        P(rtb, "保存时每个组生成一个独立文件，文件名即组名（如 UBE.xml、护甲.xml），内容为 BodySlide 原生 <SliderGroups> 格式（UTF-8 带 BOM），与 BodySlide 完全兼容。");
        P(rtb, "重复保存会覆盖同名组文件，并按内部清单自动清理改名/删除组后遗留的旧文件。未保存就关闭程序会弹出提示。");
        P(rtb, "注意：自定义输出目录如果不在 BodySlide 能读取的范围内（有效项目路径的 SliderGroups 或模组的 SliderGroups），BodySlide 不会加载这些组。");

        H(rtb, "四、规则归组（批量分组）");
        P(rtb, "服装数量大时，用关键字一次圈定一批：");
        P(rtb, "1. 右侧先选中目标组；点「规则归组」。");
        P(rtb, "2. 填\"包含关键字\"（分号分隔多个，任一命中即命中，留空 = 全部服装）与\"排除关键字\"（命中的排除）。关键字不区分大小写，是连续子串匹配。");
        P(rtb, "3. 勾选\"同时匹配所属模组名\"后，模组名命中等于该模组全部服装命中；勾选\"仅未分配服装\"则已入组的不动。");
        P(rtb, "4. 对话框实时显示\"将命中 N 个服装\"和前 30 个样例，确认后点「应用」。结果可用 Ctrl+Z 撤销。");
        P(rtb, "例：包含 ube、排除 汉化、仅未分配 → 一键把所有 UBE 服装中未分组的收入目标组。");

        H(rtb, "五、撤销与输出位置");
        P(rtb, "「撤销」（Ctrl+Z 或右侧按钮）可逐步回退最近 30 步分组操作：加入/移出、规则归组、新建/重命名/删除组、导入、查看组中的移出都可撤销。保存到文件不会清空撤销记录，但重启程序会。");
        P(rtb, "输出位置五种模式：");
        B(rtb, "自动（推荐）：BodySlide 有效项目路径是真实目录时写它的 SliderGroups；是 MO2 虚拟 Data 下的 CalienteTools\\BodySlide（最常见）时，写入 MO2 专用模组\"BS Group Generator\"。");
        B(rtb, "MO2 专用模组：mods\\BS Group Generator\\CalienteTools\\BodySlide\\SliderGroups\\ —— 在 MO2 里可见、可按 Profile 开关、重装 BodySlide 不丢。");
        B(rtb, "BodySlide 程序目录 / 游戏真实 Data / 自定义：分别写到对应位置的 SliderGroups。");

        H(rtb, "六、常见问题");
        P(rtb, "· 勾选后为什么没进组？勾选只是\"选中\"，要点「加入当前组」。");
        P(rtb, "· BodySlide 里看不到新组？1) 重启 BodySlide（经 MO2 启动的连 MO2 重启）；2) 打开「工具→诊断信息」，核对\"有效项目路径\"与\"输出目录\"是否一致——BodySlide 只从有效项目路径的 SliderGroups 读取分组。");
        P(rtb, "· 为什么有些模组不显示？树里只显示含 BodySlide 服装的启用模组；未启用或不含服装的模组不会出现。");
        P(rtb, "· 服装名后的（同名冲突）？该服装名在多个模组中都出现过，归属显示为优先级最高的模组；分组按名称匹配，不受影响。");
        P(rtb, "· 输出目录里多了个 BSGroupGenerator.files.txt？那是本工具的清单文件，用于自动清理旧文件，BodySlide 不会读取它。");
        P(rtb, "· 程序出错了？异常会写入 %APPDATA%\\BSGroupGenerator\\crash.log，可把该文件发给开发者。");

        rtb.SelectionStart = 0;
    }
}
