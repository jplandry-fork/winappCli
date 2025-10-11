namespace Winsdk.Cli.Services;

internal sealed class PackageLayoutService : IPackageLayoutService
{
    public void CopyIncludesFromPackages(string pkgsDir, string includeOut)
    {
        EnsureDir(includeOut);
        foreach (var includeDir in SafeEnumDirs(pkgsDir, "include", SearchOption.AllDirectories))
        {
            foreach (var file in SafeEnumFiles(includeDir, "*.*", SearchOption.TopDirectoryOnly))
            {
                var target = Path.Combine(includeOut, Path.GetFileName(file));
                TryCopy(file, target);
            }
        }
    }

    public void CopyLibs(string pkgsDir, string libOut, string arch)
    {
        EnsureDir(libOut);
        foreach (var libDir in SafeEnumDirs(pkgsDir, "lib", SearchOption.AllDirectories))
        {
            var archDir = Path.Combine(libDir, arch);
            var nativeArchDir = Path.Combine(libDir, "native", arch);
            var winArchDir = Path.Combine(libDir, $"win-{arch}");
            var win10ArchDir = Path.Combine(libDir, $"win10-{arch}");
            var nativeWin10ArchDir = Path.Combine(libDir, "native", $"win10-{arch}");
            
            CopyTopFiles(archDir, "*.lib", libOut);
            CopyTopFiles(nativeArchDir, "*.lib", libOut);
            CopyTopFiles(winArchDir, "*.lib", libOut);
            CopyTopFiles(win10ArchDir, "*.lib", libOut);
            CopyTopFiles(nativeWin10ArchDir, "*.lib", libOut);
        }
    }

    public void CopyRuntimes(string pkgsDir, string binOut, string arch)
    {
        EnsureDir(binOut);
        foreach (var rtDir in SafeEnumDirs(pkgsDir, "runtimes", SearchOption.AllDirectories))
        {
            var native = Path.Combine(rtDir, $"win-{arch}", "native");
            CopyTopFiles(native, "*.*", binOut);
        }
    }

    public IEnumerable<string> FindWinmds(string pkgsDir, Dictionary<string, string> usedVersions)
    {
        var results = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Only search in package directories that were actually used
        foreach (var (packageName, version) in usedVersions)
        {
            var packageDir = Path.Combine(pkgsDir, $"{packageName}.{version}");
            if (!Directory.Exists(packageDir))
                continue;

            // Search for metadata directories within this specific package
            foreach (var metadataDir in SafeEnumDirs(packageDir, "metadata", SearchOption.AllDirectories))
            {
                foreach (var f in SafeEnumFiles(metadataDir, "*.winmd", SearchOption.TopDirectoryOnly))
                    results.Add(Path.GetFullPath(f));

                var v18362 = Path.Combine(metadataDir, "10.0.18362.0");
                foreach (var f in SafeEnumFiles(v18362, "*.winmd", SearchOption.TopDirectoryOnly))
                    results.Add(Path.GetFullPath(f));
            }

            // Search for lib directories within this specific package
            foreach (var libDir in SafeEnumDirs(packageDir, "lib", SearchOption.AllDirectories))
            {
                foreach (var f in SafeEnumFiles(libDir, "*.winmd", SearchOption.TopDirectoryOnly))
                    results.Add(Path.GetFullPath(f));

                var uap10 = Path.Combine(libDir, "uap10.0");
                foreach (var f in SafeEnumFiles(uap10, "*.winmd", SearchOption.TopDirectoryOnly))
                    results.Add(Path.GetFullPath(f));

                var uap18362 = Path.Combine(libDir, "uap10.0.18362");
                foreach (var f in SafeEnumFiles(uap18362, "*.winmd", SearchOption.TopDirectoryOnly))
                    results.Add(Path.GetFullPath(f));
            }

            // Search for References directories within this specific package
            foreach (var refDir in SafeEnumDirs(packageDir, "References", SearchOption.AllDirectories))
            {
                foreach (var f in SafeEnumFiles(refDir, "*.winmd", SearchOption.AllDirectories))
                    results.Add(Path.GetFullPath(f));
            }
        }

        return results;
    }

    private static IEnumerable<string> SafeEnumDirs(string root, string searchPattern, SearchOption option)
    {
        try { return Directory.EnumerateDirectories(root, searchPattern, option); }
        catch { return Array.Empty<string>(); }
    }

    private static IEnumerable<string> SafeEnumFiles(string root, string searchPattern, SearchOption option)
    {
        try { return Directory.Exists(root) ? Directory.EnumerateFiles(root, searchPattern, option) : Array.Empty<string>(); }
        catch { return Array.Empty<string>(); }
    }

    private static void CopyTopFiles(string fromDir, string pattern, string toDir)
    {
        if (!Directory.Exists(fromDir)) return;
        EnsureDir(toDir);
        foreach (var f in Directory.EnumerateFiles(fromDir, pattern, SearchOption.TopDirectoryOnly))
        {
            var target = Path.Combine(toDir, Path.GetFileName(f));
            TryCopy(f, target);
        }
    }

    private static void EnsureDir(string dir) => Directory.CreateDirectory(dir);

    private static void TryCopy(string src, string dst)
    {
        try
        {
            File.Copy(src, dst, overwrite: true);
        }
        catch (IOException)
        {
            // Ignore to keep resilient.
        }
        catch (UnauthorizedAccessException)
        {
            // Ignore and continue.
        }
    }

    private static IEnumerable<string> SafeEnumSubdirs(string root)
    {
        try { return Directory.Exists(root) ? Directory.EnumerateDirectories(root) : Array.Empty<string>(); }
        catch { return Array.Empty<string>(); }
    }

    public void CopyLibsAllArch(string pkgsDir, string libRoot)
    {
        EnsureDir(libRoot);
        foreach (var libDir in SafeEnumDirs(pkgsDir, "lib", SearchOption.AllDirectories))
        {
            foreach (var sub in SafeEnumSubdirs(libDir))
            {
                var name = Path.GetFileName(sub);
                if (name.StartsWith("win-", StringComparison.OrdinalIgnoreCase))
                {
                    var arch = name.Substring("win-".Length);
                    var outDir = Path.Combine(libRoot, arch);
                    CopyTopFiles(sub, "*.lib", outDir);
                }
                else if (name.StartsWith("win10-", StringComparison.OrdinalIgnoreCase))
                {
                    var arch = name.Substring("win10-".Length);
                    var outDir = Path.Combine(libRoot, arch);
                    CopyTopFiles(sub, "*.lib", outDir);
                }
                else if (string.Equals(name, "native", StringComparison.OrdinalIgnoreCase))
                {
                    foreach (var d in SafeEnumSubdirs(sub))
                    {
                        var dn = Path.GetFileName(d);
                        if (dn.StartsWith("win10-", StringComparison.OrdinalIgnoreCase))
                        {
                            var arch = dn.Substring("win10-".Length);
                            var outDir = Path.Combine(libRoot, arch);
                            CopyTopFiles(d, "*.lib", outDir);
                        }
                    }
                    
                    // Also check for direct arch folders under native
                    foreach (var d in SafeEnumSubdirs(sub))
                    {
                        var dn = Path.GetFileName(d);
                        // Check for direct architecture names (x86, x64, arm, arm64)
                        if (IsValidArchitecture(dn))
                        {
                            var outDir = Path.Combine(libRoot, dn);
                            CopyTopFiles(d, "*.lib", outDir);
                        }
                    }
                }
                
                // Handle direct architecture folders
                if (IsValidArchitecture(name))
                {
                    var outDir = Path.Combine(libRoot, name);
                    CopyTopFiles(sub, "*.lib", outDir);
                }
            }
        }
    }

    public void CopyRuntimesAllArch(string pkgsDir, string binRoot)
    {
        EnsureDir(binRoot);
        foreach (var rtDir in SafeEnumDirs(pkgsDir, "runtimes", SearchOption.AllDirectories))
        {
            foreach (var plat in SafeEnumSubdirs(rtDir))
            {
                var name = Path.GetFileName(plat);
                if (name.StartsWith("win-", StringComparison.OrdinalIgnoreCase))
                {
                    var arch = name.Substring("win-".Length);
                    var native = Path.Combine(plat, "native");
                    var outDir = Path.Combine(binRoot, arch);
                    CopyTopFiles(native, "*.*", outDir);
                }
            }
        }
    }

    private static bool IsValidArchitecture(string name)
    {
        return string.Equals(name, "x86", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(name, "x64", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(name, "arm", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(name, "arm64", StringComparison.OrdinalIgnoreCase);
    }
}
