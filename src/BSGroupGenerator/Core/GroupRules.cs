namespace BSGroupGenerator.Core;

/// <summary>
/// 规则归组的匹配逻辑：包含关键字任一命中即命中（留空 = 全部服装），
/// 排除关键字任一命中则排除；可选同时匹配所属模组名。全部不区分大小写。
/// </summary>
public static class GroupRules
{
    public static List<string> SplitKeywords(string input) =>
        (input ?? "").Split([';', '；', ',', '，'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();

    /// <summary>
    /// 按服装名/模组名双关键字语义匹配：
    /// 服装关键字任一命中（留空 = 全部）；排除关键字任一命中则排除；
    /// 模组关键字非空时，所属模组名也必须命中（把范围缩小到目标模组）。
    /// </summary>
    public static bool MatchesOutfit(string outfitName, string ownerName,
        IReadOnlyList<string> outfitKeywords, IReadOnlyList<string> outfitExcludeKeywords,
        IReadOnlyList<string> modKeywords, bool unassignedOnly, bool isInAnyGroup)
    {
        if (unassignedOnly && isInAnyGroup)
            return false;
        if (outfitExcludeKeywords.Any(k => outfitName.Contains(k, StringComparison.OrdinalIgnoreCase)))
            return false;
        if (modKeywords.Count > 0 && !modKeywords.Any(k => ownerName.Contains(k, StringComparison.OrdinalIgnoreCase)))
            return false;
        if (outfitKeywords.Count == 0)
            return true;
        return outfitKeywords.Any(k => outfitName.Contains(k, StringComparison.OrdinalIgnoreCase));
    }

    public static bool Matches(string name, string owner, IReadOnlyList<string> include,
        IReadOnlyList<string> exclude, bool matchOwner)
    {
        if (exclude.Any(k => Hit(name, owner, k, matchOwner)))
            return false;
        if (include.Count == 0)
            return true;
        return include.Any(k => Hit(name, owner, k, matchOwner));
    }

    private static bool Hit(string name, string owner, string keyword, bool matchOwner) =>
        name.Contains(keyword, StringComparison.OrdinalIgnoreCase)
        || (matchOwner && owner.Contains(keyword, StringComparison.OrdinalIgnoreCase));
}
