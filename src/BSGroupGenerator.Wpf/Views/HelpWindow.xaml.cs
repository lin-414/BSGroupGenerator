using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using BSGroupGenerator.Wpf.Services;

namespace BSGroupGenerator.Wpf.Views;

/// <summary>程序内使用说明：分章节的详细帮助（FlowDocument 排版，可滚动、可复制）。
/// 文案取自语言资源（L.Help_01…），中英各一份。</summary>
public partial class HelpWindow : Window
{
    public HelpWindow()
    {
        InitializeComponent();
        BuildContent(Doc);
    }

    private static Paragraph P(string text, bool bold = false, bool heading = false)
    {
        var para = new Paragraph(new Run(text)
        {
            FontWeight = heading || bold ? FontWeights.Bold : FontWeights.Normal,
        });
        if (heading)
        {
            para.FontSize = 15;
            para.Margin = new Thickness(0, 14, 0, 4);
        }
        else
        {
            para.Margin = new Thickness(0, 2, 0, 2);
        }
        return para;
    }

    private static Paragraph B(string text) => P("  · " + text);

    private static void BuildContent(FlowDocument doc)
    {
        doc.Blocks.Add(P(L10n.Tr("L.Help_01")));

        doc.Blocks.Add(P(L10n.Tr("L.Help_H1"), heading: true));
        doc.Blocks.Add(P(L10n.Tr("L.Help_02")));
        doc.Blocks.Add(P(L10n.Tr("L.Help_03")));
        doc.Blocks.Add(P(L10n.Tr("L.Help_04")));
        doc.Blocks.Add(P(L10n.Tr("L.Help_05")));

        doc.Blocks.Add(P(L10n.Tr("L.Help_H2"), heading: true));
        doc.Blocks.Add(P(L10n.Tr("L.Help_S2_Top")));
        doc.Blocks.Add(B(L10n.Tr("L.Help_06")));
        doc.Blocks.Add(B(L10n.Tr("L.Help_07")));
        doc.Blocks.Add(B(L10n.Tr("L.Help_08")));
        doc.Blocks.Add(B(L10n.Tr("L.Help_09")));
        doc.Blocks.Add(P(L10n.Tr("L.Help_S2_Tree")));
        doc.Blocks.Add(B(L10n.Tr("L.Help_10")));
        doc.Blocks.Add(B(L10n.Tr("L.Help_11")));
        doc.Blocks.Add(B(L10n.Tr("L.Help_12")));
        doc.Blocks.Add(B(L10n.Tr("L.Help_13")));
        doc.Blocks.Add(P(L10n.Tr("L.Help_S2_Groups")));
        doc.Blocks.Add(B(L10n.Tr("L.Help_14")));
        doc.Blocks.Add(B(L10n.Tr("L.Help_15")));
        doc.Blocks.Add(B(L10n.Tr("L.Help_16")));
        doc.Blocks.Add(B(L10n.Tr("L.Help_17")));
        doc.Blocks.Add(B(L10n.Tr("L.Help_18")));
        doc.Blocks.Add(P(L10n.Tr("L.Help_S2_Status")));
        doc.Blocks.Add(B(L10n.Tr("L.Help_19")));
        doc.Blocks.Add(B(L10n.Tr("L.Help_20")));

        doc.Blocks.Add(P(L10n.Tr("L.Help_H3"), heading: true));
        doc.Blocks.Add(P(L10n.Tr("L.Help_21")));
        doc.Blocks.Add(P(L10n.Tr("L.Help_22")));
        doc.Blocks.Add(P(L10n.Tr("L.Help_23")));

        doc.Blocks.Add(P(L10n.Tr("L.Help_H4"), heading: true));
        doc.Blocks.Add(P(L10n.Tr("L.Help_24")));
        doc.Blocks.Add(P(L10n.Tr("L.Help_25")));
        doc.Blocks.Add(P(L10n.Tr("L.Help_26")));
        doc.Blocks.Add(B(L10n.Tr("L.Help_27")));
        doc.Blocks.Add(B(L10n.Tr("L.Help_28")));
        doc.Blocks.Add(B(L10n.Tr("L.Help_29")));
        doc.Blocks.Add(P(L10n.Tr("L.Help_30")));
        doc.Blocks.Add(P(L10n.Tr("L.Help_31")));

        doc.Blocks.Add(P(L10n.Tr("L.Help_H5"), heading: true));
        doc.Blocks.Add(P(L10n.Tr("L.Help_32")));
        doc.Blocks.Add(P(L10n.Tr("L.Help_33")));
        doc.Blocks.Add(B(L10n.Tr("L.Help_34")));
        doc.Blocks.Add(B(L10n.Tr("L.Help_35")));
        doc.Blocks.Add(B(L10n.Tr("L.Help_36")));

        doc.Blocks.Add(P(L10n.Tr("L.Help_H6"), heading: true));
        doc.Blocks.Add(P(L10n.Tr("L.Help_37")));
        doc.Blocks.Add(P(L10n.Tr("L.Help_38")));
        doc.Blocks.Add(P(L10n.Tr("L.Help_39")));
        doc.Blocks.Add(P(L10n.Tr("L.Help_40")));
        doc.Blocks.Add(P(L10n.Tr("L.Help_41")));
        doc.Blocks.Add(P(L10n.Tr("L.Help_42")));
    }
}
