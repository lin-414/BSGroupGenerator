using Xunit;
using BSGroupGenerator.Core;

namespace BSGroupGenerator.Tests;

/// <summary>实例发现：全局实例（根目录下含 ModOrganizer.ini 的子目录）+ 用户指定目录。
/// 用 <see cref="Mo2Discovery.GlobalRootOverride"/> 指到临时目录，结果不依赖本机装了什么。</summary>
public class Mo2DiscoveryTests
{
    private static string IniOf(string dir) => System.IO.Path.Combine(dir, "ModOrganizer.ini");

    [Fact]
    public void DiscoversGlobalAndUserSpecifiedInstances()
    {
        using var temp = new TempDir();
        var globalRoot = temp.Sub("ModOrganizer");
        var globalInstance = temp.Sub("ModOrganizer", "MyInstance");
        System.IO.File.WriteAllText(IniOf(globalInstance), "[General]\ngameName=Skyrim Special Edition\n");
        var portable = temp.Sub("PortableMO2");
        System.IO.File.WriteAllText(IniOf(portable), "[General]\ngameName=Fallout 4\n");

        var previous = Mo2Discovery.GlobalRootOverride;
        try
        {
            Mo2Discovery.GlobalRootOverride = globalRoot;
            var found = Mo2Discovery.Discover(new[] { portable });

            Assert.Contains(found, i => i.InstanceDir == globalInstance && i.Kind == Mo2InstanceKind.Global);
            Assert.Contains(found, i => i.InstanceDir == portable && i.Kind == Mo2InstanceKind.Manual);
        }
        finally
        {
            Mo2Discovery.GlobalRootOverride = previous;
        }
    }

    /// <summary>只有 ModOrganizer.exe、没有 ModOrganizer.ini 的目录不登记为实例
    ///（判定依据只有 ini；可执行文件本身不构成实例）。</summary>
    [Fact]
    public void ExeWithoutIniIsNotAnInstance()
    {
        using var temp = new TempDir();
        var exeOnly = temp.Sub("ExeOnly");
        System.IO.File.WriteAllText(System.IO.Path.Combine(exeOnly, "ModOrganizer.exe"), "");

        var previous = Mo2Discovery.GlobalRootOverride;
        try
        {
            Mo2Discovery.GlobalRootOverride = temp.Sub("NoGlobalInstances"); // 空根目录
            var found = Mo2Discovery.Discover(new[] { exeOnly });
            Assert.DoesNotContain(found, i => i.InstanceDir == exeOnly);
        }
        finally
        {
            Mo2Discovery.GlobalRootOverride = previous;
        }
    }
}
