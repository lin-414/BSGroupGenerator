# -*- coding: utf-8 -*-
# 一次性修补：UI 卡顿修复（成员标注内联到建树、过滤防抖、扫描排队、单次遍历）
import io

p = 'src/BSGroupGenerator/UI/MainForm.cs'
s = io.open(p, encoding='utf-8').read()
orig_len = len(s)

def rep(old, new, count=1):
    global s
    assert s.count(old) == count, f"match {s.count(old)} != {count}: {old[:60]!r}"
    s = s.replace(old, new)

# R3 fields
rep("""    private ScanResult? _scan;
    private HashSet<string>? _conflictNames;""",
"""    private ScanResult? _scan;
    private HashSet<string>? _conflictNames;
    private bool _scanQueued;
    private readonly System.Windows.Forms.Timer _filterDebounce = new() { Interval = 350 };""")

# R1 filter debounce
rep("""        _txtFilter.TextChanged += (_, _) => RefreshTree();""",
"""        _txtFilter.TextChanged += (_, _) => { _filterDebounce.Stop(); _filterDebounce.Start(); };
        _filterDebounce.Tick += (_, _) => { _filterDebounce.Stop(); RefreshTree(); };""")

# R2 group switch rebuilds tree
rep("""        _lstGroups.SelectedIndexChanged += (_, _) => { if (!_loadingUi) ApplyMembershipDisplay(); };""",
"""        _lstGroups.SelectedIndexChanged += (_, _) => { if (!_loadingUi) RefreshTree(); };""")

# R5 capture group in RefreshTree
rep("""            var filter = _txtFilter.Text.Trim();
            var outfitsByOwner = new Dictionary<string, List<OutfitEntry>>();""",
"""            var filter = _txtFilter.Text.Trim();
            var group = CurrentGroup;
            var outfitsByOwner = new Dictionary<string, List<OutfitEntry>>();""")

# R4 RefreshTree tail
rep("""        finally
        {
            _tree.EndUpdate();
            _updatingChecks = false;
        }
        ApplyMembershipDisplay();
        UpdateCounts();
    }""",
"""        finally
        {
            _tree.EndUpdate();
            _updatingChecks = false;
        }
        UpdateCounts();
    }""")

# R6/R7/R8 pass group to node builder
rep("""            _tree.Nodes.Add(BuildOutfitModNode(owner, outfits, outfits.Where(OutfitVisible).ToList(), filter));""",
"""            _tree.Nodes.Add(BuildOutfitModNode(owner, outfits, outfits.Where(OutfitVisible).ToList(), filter, group));""")
rep("""            AddUnder(separator, BuildOutfitModNode(entry.Name, outfits, visibleOutfits, filter));""",
"""            AddUnder(separator, BuildOutfitModNode(entry.Name, outfits, visibleOutfits, filter, group));""")
rep("""            _tree.Nodes.Add(BuildOutfitModNode(owner, outfits, visibleOutfits, filter));""",
"""            _tree.Nodes.Add(BuildOutfitModNode(owner, outfits, visibleOutfits, filter, group));""")

# R9 node builder with inline membership marks
rep("""    private TreeNode BuildOutfitModNode(string owner, List<OutfitEntry> outfits, List<OutfitEntry> visibleOutfits, string filter)
    {
        // 过滤时标明是"该模组有几个服装命中"：模组名本身可能不含关键词（如 "UB EBodyslide"），
        // 命中的是它内部的某件服装，避免看起来像按单字母匹配
        var header = filter.Length > 0 && visibleOutfits.Count < outfits.Count
            ? $"{owner}　(匹配 {visibleOutfits.Count}/{outfits.Count})"
            : $"{owner}　({outfits.Count})";
        var modNode = new TreeNode(header)
        {
            Name = header,
            Tag = "M:" + owner,
            NodeFont = BoldFont,
        };
        foreach (var outfit in visibleOutfits)
        {
            var baseText = outfit.HasConflict ? $"{outfit.Name}（同名冲突）" : outfit.Name;
            modNode.Nodes.Add(new TreeNode(baseText)
            {
                Name = baseText,
                Tag = "O:" + outfit.Name,
                ForeColor = outfit.HasConflict ? SystemColors.HotTrack : SystemColors.WindowText,
            });
        }
        if (filter.Length > 0)
            modNode.Expand();
        return modNode;
    }""",
"""    private TreeNode BuildOutfitModNode(string owner, List<OutfitEntry> outfits, List<OutfitEntry> visibleOutfits,
        string filter, SliderGroup? group)
    {
        // 过滤时标明是"该模组有几个服装命中"：模组名本身可能不含关键词（如 "UB EBodyslide"），
        // 命中的是它内部的某件服装，避免看起来像按单字母匹配
        var header = filter.Length > 0 && visibleOutfits.Count < outfits.Count
            ? $"{owner}　(匹配 {visibleOutfits.Count}/{outfits.Count})"
            : $"{owner}　({outfits.Count})";
        var modNode = new TreeNode(header)
        {
            Name = header,
            Tag = "M:" + owner,
            NodeFont = BoldFont,
        };

        var inGroup = 0;
        foreach (var outfit in visibleOutfits)
        {
            var member = group is not null && group.Members.Contains(outfit.Name, StringComparer.Ordinal);
            if (member)
                inGroup++;
            var baseText = outfit.HasConflict ? $"{outfit.Name}（同名冲突）" : outfit.Name;
            modNode.Nodes.Add(new TreeNode((member ? "✔ " : "") + baseText)
            {
                Name = baseText,
                Tag = "O:" + outfit.Name,
                ForeColor = member ? MemberGreen : outfit.HasConflict ? SystemColors.HotTrack : SystemColors.WindowText,
            });
        }

        if (inGroup > 0)
        {
            modNode.Text = $"{header}　[组内 {inGroup}/{visibleOutfits.Count}]";
            modNode.Name = modNode.Text;
            modNode.ForeColor = inGroup == visibleOutfits.Count ? MemberGreen : SystemColors.WindowText;
        }

        if (filter.Length > 0)
            modNode.Expand();
        return modNode;
    }""")

# R10 delete ApplyMembershipDisplay
start = s.find('    /// <summary>把"当前组的成员关系"渲染到树上')
end = s.find('    /// <summary>把左侧勾选的内容')
assert start != -1 and end != -1 and start < end, (start, end)
s = s[:start] + s[end:]

# R11 apply tail
rep("""        foreach (var name in names)
            ApplyMembership(group, name, add);
        Log($"已把 {names.Count} 个服装{(add ? "加入" : "移出")}组 \\"{group.Name}\\"（现成员 {group.Members.Count} 个）。");

        // 应用后清空勾选，方便继续为其他组挑选
        _updatingChecks = true;
        try
        {
            foreach (var node in Walk(_tree.Nodes))
                node.Checked = false;
        }
        finally
        {
            _updatingChecks = false;
        }
        ApplyMembershipDisplay();
        UpdateCounts();
    }""",
"""        foreach (var name in names)
            ApplyMembership(group, name, add);
        Log($"已把 {names.Count} 个服装{(add ? "加入" : "移出")}组 \\"{group.Name}\\"（现成员 {group.Members.Count} 个）。");

        // 重建树：显示新的成员关系，勾选自然清空
        RefreshTree();
    }""")

# R12 delete/import tails
old2 = """        ApplyMembershipDisplay();
        UpdateCounts();"""
assert s.count(old2) == 2, s.count(old2)
s = s.replace(old2, """        RefreshTree();""")

# R13 scan queue
rep("""    private async Task RunScanAsync()
    {
        if (_scanning)
            return;""",
"""    private async Task RunScanAsync()
    {
        if (_scanning)
        {
            _scanQueued = true; // 扫描进行中又收到新请求：完成后补一次，避免丢扫描
            return;
        }""")

# R14 catch + finally
rep("""        catch (Exception ex)
        {
            Log($"扫描失败：{ex.Message}");
        }
        finally
        {
            _scanning = false;
        }
    }""",
"""        catch (Exception ex)
        {
            Log($"扫描失败：{ex}");
        }
        finally
        {
            _scanning = false;
            if (_scanQueued)
            {
                _scanQueued = false;
                _ = RunScanAsync();
            }
        }
    }""")

io.open(p, 'w', encoding='utf-8', newline='\n').write(s)
print('MainForm ok', orig_len, '->', len(s))

# Scanner: single walk
p2 = 'src/BSGroupGenerator/Core/SliderSetScanner.cs'
s2 = io.open(p2, encoding='utf-8').read()
old_mask_loop = '''            foreach (var mask in Masks)
            {
                IEnumerable<string> files;
                try
                {
                    files = Directory.EnumerateFiles(dir, mask, SearchOption.AllDirectories);
                }
                catch (Exception ex)
                {
                    result.Warnings.Add($"枚举 {dir} 失败：{ex.Message}");
                    continue;
                }

                foreach (var file in files)
                {
                    var rel = BodySlideLocator.GetSuffixUnder(file, dir);
                    if (rel is null)
                        continue;
                    layerFileCount++;
                    if (winnerByRel.TryAdd(rel, label))
                        winners.Add((rel, label, file));
                }
            }'''
new_mask_loop = '''            IEnumerable<string> files;
            try
            {
                // 一次遍历所有文件再按扩展名过滤，比按两个掩码各走一遍快一倍
                files = Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories)
                    .Where(f =>
                    {
                        var ext = Path.GetExtension(f);
                        return ext.Equals(".xml", StringComparison.OrdinalIgnoreCase)
                            || ext.Equals(".osp", StringComparison.OrdinalIgnoreCase);
                    });
            }
            catch (Exception ex)
            {
                result.Warnings.Add($"枚举 {dir} 失败：{ex.Message}");
                continue;
            }

            foreach (var file in files)
            {
                var rel = BodySlideLocator.GetSuffixUnder(file, dir);
                if (rel is null)
                    continue;
                layerFileCount++;
                if (winnerByRel.TryAdd(rel, label))
                    winners.Add((rel, label, file));
            }'''
assert s2.count(old_mask_loop) == 1, s2.count(old_mask_loop)
s2 = s2.replace(old_mask_loop, new_mask_loop)
s2 = s2.replace('''    private static readonly string[] Masks = { "*.xml", "*.osp" };

''', '')
io.open(p2, 'w', encoding='utf-8', newline='\n').write(s2)
print('Scanner ok')
