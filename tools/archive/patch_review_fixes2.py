# -*- coding: utf-8 -*-
# MainForm 全量修复 + 保留名测试补充
import io

def patch(path, pairs):
    s = io.open(path, encoding='utf-8').read()
    for old, new in pairs:
        assert s.count(old) == 1, f"{path}: match {s.count(old)} != 1: {old[:80]!r}"
        s = s.replace(old, new)
    io.open(path, 'w', encoding='utf-8', newline='\n').write(s)
    print(path, 'ok')

pairs = []

pairs.append((
'''    private bool _scanQueued;''',
'''    private bool _scanQueued;
    private bool _closed;'''))

pairs.append((
'''    private readonly TextBox _txtFilter = new() { Width = 180, PlaceholderText = "过滤服装名…" };''',
'''    private readonly TextBox _txtFilter = new() { Width = 180, PlaceholderText = "过滤服装 / 模组…" };'''))

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

pairs.append((
'''                    node.ForeColor = inGroup > 0 ? MemberGreen : SystemColors.WindowText;''',
'''                    node.ForeColor = visible.Count > 0 && inGroup == visible.Count
                        ? MemberGreen
                        : SystemColors.WindowText;'''))

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

    private async Task DetectBodySlideAsync()''',
'''        _ = DetectBodySlideAsync();
    }

    private async Task DetectBodySlideAsync()'''))

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

# 测试补充：Windows 保留名
p = 'tests/BSGroupGenerator.Tests/SliderGroupFileTests.cs'
s = io.open(p, encoding='utf-8').read()
old = '''    [Theory]
    [InlineData("UBE", "UBE.xml")]'''
new = '''    [Theory]
    [InlineData("CON", "_CON.xml")]
    [InlineData("NUL", "_NUL.xml")]
    [InlineData("CON.x", "_CON.x.xml")]
    [Theory]
    [InlineData("UBE", "UBE.xml")]'''
assert s.count(old) == 1
s = s.replace(old, new)
io.open(p, 'w', encoding='utf-8', newline='\n').write(s)
print('tests ok')
