# -*- coding: utf-8 -*-
# 代码审查修复：对话框批量勾选失效、扫描枚举异常中断、保留设备名、过期扫描数据混用、控制字符
import io

def patch(path, pairs):
    s = io.open(path, encoding='utf-8').read()
    for old, new in pairs:
        assert s.count(old) == 1, f"{path}: match {s.count(old)} != 1: {old[:70]!r}"
        s = s.replace(old, new)
    io.open(path, 'w', encoding='utf-8', newline='\n').write(s)
    print(path, 'ok')

# ── Bug A：查看组窗口勾选拦截把模组/分隔符也拦了，批量全选功能失效 ──
patch('src/BSGroupGenerator/UI/GroupMembersDialog.cs', [(
'''        _tree.BeforeCheck += (_, e) =>
        {
            if (e.Node?.Tag is string tag && tag.StartsWith("O:", StringComparison.Ordinal))
                return;
            e.Cancel = true; // 只有服装可勾选（用于移出）
        };''',
'''        _tree.BeforeCheck += (_, e) =>
        {
            if (e.Node?.Tag is string tag &&
                (tag.StartsWith("O:", StringComparison.Ordinal) ||
                 tag.StartsWith("M:", StringComparison.Ordinal) ||
                 tag.StartsWith("S:", StringComparison.Ordinal)))
                return;
            e.Cancel = true;
        };''')])

# ── Bug B：扫描的懒枚举在遍历时抛异常（无权限/超长路径）会中断整个扫描 ──
patch('src/BSGroupGenerator/Core/SliderSetScanner.cs', [
(
'''            IEnumerable<string> files;
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
            {''',
'''            List<string> files;
            try
            {
                // 一次遍历所有文件再按扩展名过滤；忽略无权限目录，避免个别目录异常中断整个扫描
                files = Directory.EnumerateFiles(
                        dir, "*",
                        new EnumerationOptions
                        {
                            RecurseSubdirectories = true,
                            IgnoreInaccessible = true,
                        })
                    .Where(f =>
                    {
                        var ext = Path.GetExtension(f);
                        return ext.Equals(".xml", StringComparison.OrdinalIgnoreCase)
                            || ext.Equals(".osp", StringComparison.OrdinalIgnoreCase);
                    })
                    .ToList();
            }
            catch (Exception ex)
            {
                result.Warnings.Add($"枚举 {dir} 失败：{ex.Message}");
                continue;
            }

            foreach (var file in files)
            {'''),
(
'''        try
        {
            return Directory.EnumerateFiles(dir, "*.xml", SearchOption.AllDirectories).Any()
                || Directory.EnumerateFiles(dir, "*.osp", SearchOption.AllDirectories).Any();
        }''',
'''        try
        {
            var options = new EnumerationOptions
            {
                RecurseSubdirectories = true,
                IgnoreInaccessible = true,
            };
            return Directory.EnumerateFiles(dir, "*.xml", options).Any()
                || Directory.EnumerateFiles(dir, "*.osp", options).Any();
        }'''),
])

# ── Bug C：组名撞上 Windows 保留设备名（CON/NUL/COM1…）导致创建文件失败 ──
patch('src/BSGroupGenerator/Core/SliderGroupFile.cs', [
(
'''    public static string FileNameForGroup(string groupName)
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

    public static string FileNameForGroup(string groupName)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var sb = new StringBuilder();
        foreach (var c in groupName.Trim())
            sb.Append(invalid.Contains(c) ? '_' : c);
        var name = sb.ToString().TrimEnd('.', ' ');
        if (ReservedDeviceNames.Contains(name))
            name = "_" + name; // Windows 保留设备名（CON/NUL/COM1…）不能直接做文件名
        return (name.Length == 0 ? "未命名组" : name) + ".xml";
    }''')])

# ── 修复 D：profile 切换瞬间，旧扫描结果会配新 profile 的模组结构（数据错配）──
#     把解析出的模组条目快照进扫描结果，显示永远使用与扫描配套的那一份。
p = 'src/BSGroupGenerator/UI/MainForm.cs'
s = io.open(p, encoding='utf-8').read()

def rep(old, new, count=1):
    global s
    assert s.count(old) == count, f"match {s.count(old)} != {count}: {old[:70]!r}"
    s = s.replace(old, new)

rep(
'''    private sealed record ScanOutcome(
        ProjectPathResolution? Resolution,
        ScanResult? Result,
        List<SliderGroup> ExistingGroups,
        string? TargetDir,
        string TargetDescription,
        List<string> Errors);''',
'''    private sealed record ScanOutcome(
        ProjectPathResolution? Resolution,
        ScanResult? Result,
        List<ModEntry> Entries,
        List<SliderGroup> ExistingGroups,
        string? TargetDir,
        string TargetDescription,
        List<string> Errors);''')

rep(
'''            var modsSnapshot = _mods.ToList();
            var instanceSnapshot = _instance;
            var writeMode = _settings.WriteMode;
            var customDir = _settings.CustomTargetDir;
            var outcome = await Task.Run(() => ComputeScan(bsDir, modsSnapshot, instanceSnapshot, writeMode, customDir));
            ApplyScanOutcome(outcome);''',
'''            var modsSnapshot = _mods.ToList();
            var entriesSnapshot = _entries.ToList();
            var instanceSnapshot = _instance;
            var writeMode = _settings.WriteMode;
            var customDir = _settings.CustomTargetDir;
            var outcome = await Task.Run(() =>
                ComputeScan(bsDir, modsSnapshot, entriesSnapshot, instanceSnapshot, writeMode, customDir));
            ApplyScanOutcome(outcome);''')

rep(
'''    private static ScanOutcome ComputeScan(
        string bsDir, List<(ModEntry Entry, string Dir)> mods, Mo2Instance? instance, WriteMode writeMode, string? customDir)
    {
        var errors = new List<string>();
        var configPath = Path.Combine(bsDir, "Config.xml");
        if (!File.Exists(configPath))
            return new ScanOutcome(null, null, new List<SliderGroup>(), null, "",
                new List<string> { $"找不到 {configPath}" });''',
'''    private static ScanOutcome ComputeScan(
        string bsDir, List<(ModEntry Entry, string Dir)> mods, List<ModEntry> entries,
        Mo2Instance? instance, WriteMode writeMode, string? customDir)
    {
        var errors = new List<string>();
        var configPath = Path.Combine(bsDir, "Config.xml");
        if (!File.Exists(configPath))
            return new ScanOutcome(null, null, new List<ModEntry>(), new List<SliderGroup>(), null, "",
                new List<string> { $"找不到 {configPath}" });''')

rep(
'''        return new ScanOutcome(resolution, scan, existing, target?.Dir, target?.Description ?? "", errors);''',
'''        return new ScanOutcome(resolution, scan, entries, existing, target?.Dir, target?.Description ?? "", errors);''')

rep(
'''    private ProjectPathResolution? _resolution;
    private ScanResult? _scan;
    private HashSet<string>? _conflictNames;''',
'''    private ProjectPathResolution? _resolution;
    private ScanResult? _scan;
    private List<ModEntry> _scanEntries = new();
    private HashSet<string>? _conflictNames;''')

rep(
'''        _resolution = outcome.Resolution;
        _scan = outcome.Result;
        _conflictNames = outcome.Result is null''',
'''        _resolution = outcome.Resolution;
        _scan = outcome.Result;
        _scanEntries = outcome.Entries;
        _conflictNames = outcome.Result is null''')

# 显示层改用与扫描配套的快照
rep(
'''    private bool IsVirtualScan =>
        _resolution is not null
        && _resolution.Kind is ProjectPathKind.GameDataCalienteTools or ProjectPathKind.GameDataTools
        && _entries.Count > 0;''',
'''    private bool IsVirtualScan =>
        _resolution is not null
        && _resolution.Kind is ProjectPathKind.GameDataCalienteTools or ProjectPathKind.GameDataTools
        && _scanEntries.Count > 0;''')

rep(
'''        for (var i = _entries.Count - 1; i >= 0; i--)
        {
            var entry = _entries[i];
            if (entry.IsForeign)
                continue;

            if (entry.IsSeparator)
            {''',
'''        for (var i = _scanEntries.Count - 1; i >= 0; i--)
        {
            var entry = _scanEntries[i];
            if (entry.IsForeign)
                continue;

            if (entry.IsSeparator)
            {''')

rep(
'''            for (var i = _entries.Count - 1; i >= 0; i--)
            {
                var entry = _entries[i];
                if (entry.IsForeign || entry.IsSeparator || !entry.Enabled)
                    continue;''',
'''            for (var i = _scanEntries.Count - 1; i >= 0; i--)
            {
                var entry = _scanEntries[i];
                if (entry.IsForeign || entry.IsSeparator || !entry.Enabled)
                    continue;''')

# ── 修复 E：组名里的控制字符会导致 XML 写出失败 ──
patch('src/BSGroupGenerator/UI/InputDialog.cs', [(
'''        return form.ShowDialog(owner) == DialogResult.OK ? txt.Text.Trim() : null;''',
'''        if (form.ShowDialog(owner) != DialogResult.OK)
            return null;
        // 去掉控制字符（无法写入 XML），避免保存时报错
        return new string(txt.Text.Where(c => !char.IsControl(c)).ToArray()).Trim();''')])

# ── 改进：扫描耗时日志 ──
rep(
'''        _scanning = true;
        try
        {
            var modsSnapshot = _mods.ToList();''',
'''        _scanning = true;
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            var modsSnapshot = _mods.ToList();''')
rep(
'''            ApplyScanOutcome(outcome);
        }
        catch (Exception ex)
        {
            Log($"扫描失败：{ex}");
        }''',
'''            ApplyScanOutcome(outcome);
            Log($"扫描耗时 {stopwatch.Elapsed.TotalSeconds:0.0} 秒。");
        }
        catch (Exception ex)
        {
            Log($"扫描失败：{ex}");
        }''')

io.open(p, 'w', encoding='utf-8', newline='\n').write(s)
print(p, 'ok')

# ── 测试：保留设备名用例 ──
p = 'tests/BSGroupGenerator.Tests/SliderGroupFileTests.cs'
s = io.open(p, encoding='utf-8').read()
old = '''    [InlineData("a:b<c>d", "a_b_c_d.xml")]'''
new = '''    [InlineData("a:b<c>d", "a_b_c_d.xml")]
    [InlineData("CON", "_CON.xml")]
    [InlineData("nul", "_nul.xml")]'''
assert s.count(old) == 1
s = s.replace(old, new)
io.open(p, 'w', encoding='utf-8', newline='\n').write(s)
print(p, 'ok')
