using BSGroupGenerator.Core;

namespace BSGroupGenerator.Wpf.ViewModels;

public partial class MainViewModel
{
    public string BuildDiagnostics()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("=== MO2 实例 ===");
        foreach (var instance in Instances)
        {
            sb.AppendLine($"[{instance.DisplayName}]");
            sb.AppendLine($"  实例目录: {instance.InstanceDir}");
            sb.AppendLine($"  mods:     {instance.ModsDirectory} ({(Directory.Exists(instance.ModsDirectory) ? "存在" : "不存在")})");
            sb.AppendLine($"  profiles: {instance.ProfilesDirectory}");
            sb.AppendLine($"  gameName: {instance.GameName}");
            sb.AppendLine($"  gamePath: {instance.GamePath}");
        }
        if (Instances.Count == 0)
            sb.AppendLine("（无）");

        sb.AppendLine();
        sb.AppendLine("=== 当前 Profile ===");
        sb.AppendLine($"{SelectedProfile ?? "（无）"} — 启用模组 {Mods.Count} 个");
        foreach (var (entry, dir) in Mods.Take(200))
            sb.AppendLine($"  #{entry.Priority} {entry.Name} → {(Directory.Exists(dir) ? "存在" : "缺失")}");

        sb.AppendLine();
        sb.AppendLine("=== BodySlide ===");
        sb.AppendLine($"目录: {_bsAppDir ?? "（未选择）"}");
        if (Resolution is not null)
        {
            sb.AppendLine($"Kind: {Resolution.Kind}");
            sb.AppendLine($"有效项目路径: {Resolution.EffectivePath}");
            sb.AppendLine($"GameDataPath: {Resolution.GameDataPath}（来自 MO2: {Resolution.GameDataPathFromMo2}）");
            sb.AppendLine("解析步骤:");
            foreach (var step in Resolution.Steps)
                sb.AppendLine($"  - {step}");
        }

        sb.AppendLine();
        sb.AppendLine("=== 扫描结果 ===");
        if (Scan is null)
        {
            sb.AppendLine("（未扫描）");
        }
        else
        {
            foreach (var note in Scan.LayerNotes)
                sb.AppendLine(note);
            sb.AppendLine($"服装总数: {Scan.Outfits.Count}（同名冲突 {Scan.Outfits.Count(o => o.HasConflict)}）");
            sb.AppendLine($"输出目录: {ResolveWriteTarget()?.Dir ?? "（未定）"}");
        }
        return sb.ToString();
    }
}
