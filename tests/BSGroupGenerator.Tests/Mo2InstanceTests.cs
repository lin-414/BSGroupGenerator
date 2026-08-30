using Xunit;
using BSGroupGenerator.Core;

namespace BSGroupGenerator.Tests;

public class Mo2InstanceTests
{
    private static Mo2Instance Create(string iniContent, out TempDir temp)
    {
        temp = new TempDir();
        var instanceDir = temp.Sub("MyInstance");
        var iniPath = System.IO.Path.Combine(instanceDir, "ModOrganizer.ini");
        System.IO.File.WriteAllText(iniPath, iniContent);
        return new Mo2Instance(instanceDir, iniPath, Mo2InstanceKind.Global);
    }

    [Fact]
    public void ReadsExplicitPathsAndExpandsBaseDirVariable()
    {
        var instance = Create("""
            [General]
            gameName=Skyrim Special Edition
            gamePath=D:\Games\Skyrim Special Edition
            selected_profile=MyProfile

            [Settings]
            base_directory=D:\MO2
            mod_directory=%BASE_DIR%\mymods

            """, out var temp);
        temp.Dispose();

        Assert.Equal("Skyrim Special Edition", instance.GameName);
        Assert.Equal(@"D:\Games\Skyrim Special Edition", instance.GamePath);
        Assert.Equal("MyProfile", instance.SelectedProfile);
        Assert.Equal(@"D:\MO2\mymods", instance.ModsDirectory);
        Assert.Equal(@"D:\MO2\profiles", instance.ProfilesDirectory);
    }

    [Fact]
    public void DefaultsToInstanceDirWhenPathsMissing()
    {
        var instance = Create("[General]\ngameName=Fallout 4\n", out var temp);
        var instanceDir = instance.InstanceDir;
        temp.Dispose();

        Assert.Equal(System.IO.Path.Combine(instanceDir, "mods"), instance.ModsDirectory);
        Assert.Equal(System.IO.Path.Combine(instanceDir, "profiles"), instance.ProfilesDirectory);
    }

    [Fact]
    public void GetProfilesReturnsFoldersWithModlist()
    {
        using var temp = new TempDir();
        var instanceDir = temp.Sub("Inst");
        var iniPath = System.IO.Path.Combine(instanceDir, "ModOrganizer.ini");
        System.IO.File.WriteAllText(iniPath, "[General]\nselected_profile=vanilla\n");

        var profilesDir = temp.Sub("Inst", "profiles");
        Directory.CreateDirectory(System.IO.Path.Combine(profilesDir, "vanilla"));
        Directory.CreateDirectory(System.IO.Path.Combine(profilesDir, "modded"));
        Directory.CreateDirectory(System.IO.Path.Combine(profilesDir, "empty")); // 没有 modlist.txt
        System.IO.File.WriteAllText(System.IO.Path.Combine(profilesDir, "vanilla", "modlist.txt"), "+CBBE\n");
        System.IO.File.WriteAllText(System.IO.Path.Combine(profilesDir, "modded", "modlist.txt"), "+SkyUI\n");

        var instance = new Mo2Instance(instanceDir, iniPath, Mo2InstanceKind.Global);
        var profiles = instance.GetProfiles();

        Assert.Equal(new[] { "modded", "vanilla" }, profiles);
        Assert.Equal("vanilla", instance.SelectedProfile);
    }
}
