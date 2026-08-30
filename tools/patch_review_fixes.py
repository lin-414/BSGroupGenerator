# -*- coding: utf-8 -*-
# 全量修复：关闭保护/组复活/绿色判定一致性/全局异常兜底 + 展开状态恢复/模组名过滤/扫描排序/异步检测/日志上限/保留名
import io

def patch(path, pairs):
    s = io.open(path, encoding='utf-8').read()
    for old, new in pairs:
        assert s.count(old) == 1, f"{path}: match {s.count(old)} != 1: {old[:80]!r}"
        s = s.replace(old, new)
    io.open(path, 'w', encoding='utf-8', newline='\n').write(s)
    print(path, 'ok')

# ── Program.cs：全局异常兜底（整文件重写）──
io.open('src/BSGroupGenerator/Program.cs', 'w', encoding='utf-8', newline='\n').write('''namespace BSGroupGenerator;

internal static class Program
{
    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.ThreadException += (_, e) => ReportCrash(e.Exception, isFatal: false);
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            ReportCrash(e.ExceptionObject as Exception, isFatal: e.IsTerminating);
        Application.Run(new UI.MainForm());
    }

    private static void ReportCrash(Exception? ex, bool isFatal)
    {
        try
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "BSGroupGenerator");
            Directory.CreateDirectory(dir);
            File.AppendAllText(Path.Combine(dir, "crash.log"),
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {(isFatal ? "致命" : "UI")}异常：\\n{ex}\\n\\n");
        }
        catch
        {
            // 日志失败不影响提示
        }

        MessageBox.Show(
            isFatal
                ? $"发生未处理的错误，程序即将退出。\\n详细信息已写入 %APPDATA%\\\\BSGroupGenerator\\\\crash.log\\n\\n{ex?.Message}"
                : $"发生了一个错误，已忽略（详情见 %APPDATA%\\\\BSGroupGenerator\\\\crash.log）。\\n\\n{ex?.Message}",
            isFatal ? "错误" : "提示",
            MessageBoxButtons.OK, MessageBoxIcon.Error);
    }
}
''')
print('Program.cs ok')

# ── Core/SliderGroupFile.cs：Windows 保留名特判 ──
patch('src/BSGroupGenerator/Core/SliderGroupFile.cs', [(
'''    /// <summary>组名转安全文件名（非法字符替换为下划线，自动补 .xml）。</summary>
    public static string FileNameForGroup(string groupName)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var sb = new StringBuilder();
        foreach (var c in groupName.Trim())
            sb.Append(invalid.Contains(c) ? '_' : c);
        var name = sb.ToString().TrimEnd('.', ' ');
        return (name.Length == 0 ? "未命名组" : name) + ".xml";
    }''',
'''    private static readonly HashSet<string> ReservedDeviceNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9",
    };

    /// <summary>组名转安全文件名（非法字符替换为下划线、Windows 保留名加前缀、自动补 .xml）。</summary>
    public static string FileNameForGroup(string groupName)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var sb = new StringBuilder();
        foreach (var c in groupName.Trim())
            sb.Append(invalid.Contains(c) ? '_' : c);
        var name = sb.ToString().TrimEnd('.', ' ');
        if (name.Length == 0)
            name = "未命名组";
        var stem = name.Contains('.') ? name[..name.IndexOf('.')] : name;
        if (ReservedDeviceNames.Contains(stem))
            name = "_" + name;
        return name + ".xml";
    }''')])

# ── Core/SliderSetScanner.cs：单层内按相对路径排序，保证确定性 ──
patch('src/BSGroupGenerator/Core/SliderSetScanner.cs', [(
'''            foreach (var file in files)
            {
                var rel = BodySlideLocator.GetSuffixUnder(file, dir);
                if (rel is null)
                    continue;
                layerFileCount++;
                if (winnerByRel.TryAdd(rel, label))
                    winners.Add((rel, label, file));
            }''',
'''            var layerFiles = new List<(string RelPath, string FullPath)>();
            foreach (var file in files)
            {
                var rel = BodySlideLocator.GetSuffixUnder(file, dir);
                if (rel is null)
                    continue;
                layerFiles.Add((rel, file));
            }

            // 显式排序：同名滑块组"先见者胜"的归属不依赖文件系统枚举顺序
            layerFiles.Sort(static (a, b) =>
                string.Compare(a.RelPath, b.RelPath, StringComparison.OrdinalIgnoreCase));

            foreach (var (rel, file) in layerFiles)
            {
                layerFileCount++;
                if (winnerByRel.TryAdd(rel, label))
                    winners.Add((rel, label, file));
            }''')])

# ── UI/MainForm.cs ──
pairs = []

# 字段：关闭标记
pairs.append((
'''    private bool _scanQueued;''',
'''    private bool _scanQueued;
    private bool _closed;'''))

# 过滤框占位文本支持模组名
pairs.append((
'''    private readonly TextBox _txtFilter = new() { Width = 180, PlaceholderText = "过滤服装名…" };''',
'''    private readonly TextBox _txtFilter = new() { Width = 180, PlaceholderText = "过滤服装 / 模组…" };'''))

# 退出时停防抖计时器并置关闭标记
pairs.append((
'''    private void OnFormClosingGuard(object? sender, FormClosingEventArgs e)
    {
        if (_dirty)
        {''',
'''    private void OnFormClosingGuard(object? sender, FormClosingEventArgs e)
    {
        _filterDebounce.Stop();
        if (_dirty)
        {'''))
pairs.append((
'''                return;
            }
        }
        _settings.Save();
    }''',
'''                return;
            }
        }
        _closed = true;
        _settings.Save();
    }'''))

# Log：关闭保护 + 行数上限
pairs.append((
'''    private void Log(string message) =>
        _txtLog.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");''',
'''    private void Log(string message)
    {
        if (_closed || _txtLog.IsDisposed)
            return;
        if (_txtLog.TextLength > 131072)
            _txtLog.Text = _txtLog.Text[65536..];
        _txtLog.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
    }'''))

# RefreshTree：关闭保护 + 展开/滚动恢复（整方法替换）
pairs.append((
'''    private void RefreshTree()
    {
        _updatingChecks = true;
        _tree.BeginUpdate();
        try
        {
            _tree.Nodes.Clear();
            if (_scan is null)
            {
                UpdateCounts();
                return;
            }

            var filter = _txtFilter.Text.Trim();
            var group = CurrentGroup;
            var outfitsByOwner = new Dictionary<string, List<OutfitEntry>>();
            foreach (var outfit in _scan.Outfits)
            {
                if (!outfitsByOwner.TryGetValue(outfit.OwnerLabel, out var list))
                    outfitsByOwner[outfit.OwnerLabel] = list = new List<OutfitEntry>();
                list.Add(outfit);
            }

            if (IsVirtualScan)
                BuildStructuredTree(outfitsByOwner, filter, group);
            else
                BuildFlatTree(outfitsByOwner, filter, group);

            if (_tree.Nodes.Count == 0)
                _tree.Nodes.Add(new TreeNode("（没有符合过滤条件的模组）") { ForeColor = SystemColors.GrayText });
        }
        finally
        {
            _tree.EndUpdate();
            _updatingChecks = false;
        }
        UpdateCounts();
    }''',
'''    private void RefreshTree()
    {
        if (_closed || IsDisposed)
            return;
        _updatingChecks = true;
        _tree.BeginUpdate();
        HashSet<string> expanded = new();
        string? topChain = null;
        try
        {
            // 记录展开状态与滚动位置，重建后恢复
            foreach (var node in Walk(_tree.Nodes))
                if (node.IsExpanded)
                    expanded.Add(Chain(node));
            if (_tree.TopNode is { } top)
                topChain = Chain(top);

            _tree.Nodes.Clear();
            if (_scan is null)
            {
                UpdateCounts();
                return;
            }

            var filter = _txtFilter.Text.Trim();
            var group = CurrentGroup;
            var outfitsByOwner = new Dictionary<string, List<OutfitEntry>>();
            foreach (var outfit in _scan.Outfits)
            {
                if (!outfitsByOwner.TryGetValue(outfit.OwnerLabel, out var list))
                    outfitsByOwner[outfit.OwnerLabel] = list = new List<OutfitEntry>();
                list.Add(outfit);
            }

            if (IsVirtualScan)
                BuildStructuredTree(outfitsByOwner, filter, group);
            else
                BuildFlatTree(outfitsByOwner, filter, group);

            if (_tree.Nodes.Count == 0)
                _tree.Nodes.Add(new TreeNode("（没有符合过滤条件的模组）") { ForeColor = SystemColors.GrayText });

            foreach (var node in Walk(_tree.Nodes))
                if (expanded.Contains(Chain(node)))
                    node.Expand();
        }
        finally
        {
            _tree.EndUpdate();
            _updatingChecks = false;
        }

        if (topChain is not null)
        {
            foreach (var node in Walk(_tree.Nodes))
            {
                if (Chain(node) != topChain)
                    continue;
                try { _tree.TopNode = node; } catch { /* 结构变化时无法恢复则忽略 */ }
                break;
            }
        }
        UpdateCounts();
    }'''))

# 过滤支持模组名：OutfitVisible 带 owner
pairs.append((
'''    private bool OutfitVisible(OutfitEntry outfit)
    {
        if (_chkUnassigned.Checked && IsInAnyGroup(outfit.Name))
            return false;
        return TextFilter.Matches(outfit.Name, _txtFilter.Text);
    }''',
'''    private bool OutfitVisible(OutfitEntry outfit, string owner)
    {
        if (_chkUnassigned.Checked && IsInAnyGroup(outfit.Name))
            return false;
        var filter = _txtFilter.Text.Trim();
        if (filter.Length == 0)
            return true;
        return owner.Contains(filter, StringComparison.OrdinalIgnoreCase)
               || TextFilter.Matches(outfit.Name, filter);
    }'''))

pairs.append((
'''            if (!ModVisible(outfits.Count, outfits.Count(OutfitVisible), filter))
                continue;
            _tree.Nodes.Add(BuildOutfitModNode(owner, outfits, outfits.Where(OutfitVisible).ToList(), filter, group));''',
'''            if (!ModVisible(outfits.Count, outfits.Count(o => OutfitVisible(o, owner)), filter))
                continue;
            _tree.Nodes.Add(BuildOutfitModNode(owner, outfits, outfits.Where(o => OutfitVisible(o, owner)).ToList(), filter, group));'''))

pairs.append((
'''            var visibleOutfits = outfits.Where(OutfitVisible).ToList();
            if (!ModVisible(outfits.Count, visibleOutfits.Count, filter))
                continue;
            AddUnder(separator, BuildOutfitModNode(entry.Name, outfits, visibleOutfits, filter, group));''',
'''            var visibleOutfits = outfits.Where(o => OutfitVisible(o, entry.Name)).ToList();
            if (!ModVisible(outfits.Count, visibleOutfits.Count, filter))
                continue;
            AddUnder(separator, BuildOutfitModNode(entry.Name, outfits, visibleOutfits, filter, group));'''))

pairs.append((
'''            var visibleOutfits = outfits.Where(OutfitVisible).ToList();
            if ((_chkUnassigned.Checked || filter.Length > 0) && visibleOutfits.Count == 0)
                continue;
            _tree.Nodes.Add(BuildOutfitModNode(owner, outfits, visibleOutfits, filter, group));''',
'''            var visibleOutfits = outfits.Where(o => OutfitVisible(o, owner)).ToList();
            if ((_chkUnassigned.Checked || filter.Length > 0) && visibleOutfits.Count == 0)
                continue;
            _tree.Nodes.Add(BuildOutfitModNode(owner, outfits, visibleOutfits, filter, group));'''))

# 就地更新的模组绿色判定与建树保持一致（全部成员在组内才标绿）
pairs.append((
'''                    node.ForeColor = inGroup > 0 ? MemberGreen : SystemColors.WindowText;''',
'''                    node.ForeColor = visible.Count > 0 && inGroup == visible.Count
                        ? MemberGreen
                        : SystemColors.WindowText;'''))

# ApplyScanOutcome：关闭保护 + 不复活已删除的组
pairs.append((
'''    private void ApplyScanOutcome(ScanOutcome outcome)
    {
        foreach (var error in outcome.Errors)''',
'''    private void ApplyScanOutcome(ScanOutcome outcome)
    {
        if (_closed || IsDisposed)
            return;
        foreach (var error in outcome.Errors)'''))
pairs.append((
'''        if (_groups.Count == 0 && outcome.ExistingGroups.Count > 0)''',
'''        if (!_dirty && _groups.Count == 0 && outcome.ExistingGroups.Count > 0)'''))

# DetectBodySlide 异步化
pairs.append((
'''    private void DetectBodySlide()
    {
        _loadingUi = true;
        try
        {
            var previous = _bsAppDir;
            var candidates = BodySlideLocator.FindCandidates(_mods, _instance?.GamePath, previous);
            _cboBodySlide.DataSource = null;''',
'''    private async Task DetectBodySlideAsync()
    {
        _loadingUi = true;
        List<BodySlideCandidate> candidates;
        try
        {
            var previous = _bsAppDir;
            var modsSnapshot = _mods;
            var gamePath = _instance?.GamePath;
            candidates = await Task.Run(() => BodySlideLocator.FindCandidates(modsSnapshot, gamePath, previous));
            _cboBodySlide.DataSource = null;'''))
pairs.append((
'''            if (candidates.Count == 0)
            {
                _bsAppDir = null;
                Log("未自动找到 BodySlide：请点\\"浏览…\\"选择含 BodySlide.exe 和 Config.xml 的目录（通常是 MO2 里 BodySlide 模组所在的目录）。");
            }
        }
        finally
        {
            _loadingUi = false;
        }
        OnBodySlideChanged();
    }''',
'''            if (candidates.Count == 0)
            {
                _bsAppDir = null;
                Log("未自动找到 BodySlide：请点\\"浏览…\\"选择含 BodySlide.exe 和 Config.xml 的目录（通常是 MO2 里 BodySlide 模组所在的目录）。");
            }
        }
        catch (Exception ex)
        {
            Log($"查找 BodySlide 失败：{ex.Message}");
        }
        finally
        {
            _loadingUi = false;
        }
        OnBodySlideChanged();
    }'''))
pairs.append((
'''        _btnDetectBs.Click += (_, _) => DetectBodySlide();''',
'''        _btnDetectBs.Click += async (_, _) => await DetectBodySlideAsync();'''))
pairs.append((
'''        DetectBodySlide();
    }

    private void OnBrowseBodySlide(object? sender, EventArgs e)''',
'''        _ = DetectBodySlideAsync();
    }

    private void OnBrowseBodySlide(object? sender, EventArgs e)'''))

# Chain 辅助方法（展开/滚动恢复用）
pairs.append((
'''    private static IEnumerable<TreeNode> Walk(TreeNodeCollection nodes)
    {
        foreach (TreeNode node in nodes)
        {
            yield return node;
            foreach (var child in Walk(node.Nodes))
                yield return child;
        }
    }''',
'''    private static IEnumerable<TreeNode> Walk(TreeNodeCollection nodes)
    {
        foreach (TreeNode node in nodes)
        {
            yield return node;
            foreach (var child in Walk(node.Nodes))
                yield return child;
        }
    }

    /// <summary>节点在树中的稳定标识（父链 + 标签 + 序号），用于重建后恢复展开/滚动。</summary>
    private static string Chain(TreeNode node)
    {
        var parent = node.Parent;
        var tag = node.Tag as string ?? "";
        return (parent is null ? "" : Chain(parent)) + $"{tag}{node.Index}/";
    }'''))

patch('src/BSGroupGenerator/UI/MainForm.cs', pairs)
