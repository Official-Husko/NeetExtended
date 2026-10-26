using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace NeetLoader.Mods;

/// <summary>
/// Loads a mod's DLLs. Hot-reload mods are read into memory first so the files stay unlocked
/// and each reload gets a fresh copy of the code.
/// </summary>
internal static class AssemblyLoading
{
    public static List<Assembly> Load(ModInfo mod)
    {
        var result = new List<Assembly>();
        foreach (string path in ModDiscovery.AssemblyPaths(mod))
        {
            var assembly = mod.Manifest.HotReload ? FromBytes(path) : Assembly.LoadFrom(path);
            AssemblyResolver.SetCurrent(assembly);
            result.Add(assembly);
        }
        return result;
    }

    /// <summary>Newest write time across the mod's DLLs, used to spot rebuilds.</summary>
    public static System.DateTime LastWrite(ModInfo mod)
    {
        var newest = System.DateTime.MinValue;
        foreach (string path in ModDiscovery.AssemblyPaths(mod))
        {
            var t = File.GetLastWriteTimeUtc(path);
            if (t > newest)
                newest = t;
        }
        return newest;
    }

    private static Assembly FromBytes(string path)
    {
        byte[] image = File.ReadAllBytes(path);
        string pdb = Path.ChangeExtension(path, ".pdb");
        return File.Exists(pdb)
            ? Assembly.Load(image, File.ReadAllBytes(pdb))
            : Assembly.Load(image);
    }
}
