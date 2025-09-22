namespace Winsdk.Cli.Services;

internal sealed class PackageInstallationService
{
    private readonly NugetService _nugetService;
    private readonly ConfigService _configService;

    public PackageInstallationService(ConfigService configService)
    {
        _nugetService = new NugetService();
        _configService = configService;
    }

    /// <summary>
    /// Initialize workspace and ensure required directories exist
    /// </summary>
    /// <param name="rootDirectory">The Root Directory path</param>
    public void InitializeWorkspace(string rootDirectory)
    {
        if (!Directory.Exists(rootDirectory))
        {
            Directory.CreateDirectory(rootDirectory);
        }

        var packagesDir = Path.Combine(rootDirectory, "packages");
        Directory.CreateDirectory(packagesDir);
    }

    /// <summary>
    /// Install a single package if not already present
    /// </summary>
    /// <param name="rootDirectory">The Root Directory path</param>
    /// <param name="packageName">Name of the package to install</param>
    /// <param name="version">Version to install (if null, gets latest)</param>
    /// <param name="includeExperimental">Include experimental/prerelease versions when getting latest</param>
    /// <param name="quiet">Suppress progress messages</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The installed version</returns>
    public async Task<string> InstallPackageAsync(
        string rootDirectory,
        string packageName,
        string? version = null,
        bool includeExperimental = false,
        bool quiet = false,
        CancellationToken cancellationToken = default)
    {
        var packagesDir = Path.Combine(rootDirectory, "packages");

        // Ensure nuget.exe is available
        await _nugetService.EnsureNugetExeAsync(rootDirectory, cancellationToken);

        // Get version if not specified
        if (version == null)
        {
            version = await _nugetService.GetLatestVersionAsync(packageName, includeExperimental, cancellationToken);
        }

        // Check if already installed
        var expectedFolder = Path.Combine(packagesDir, $"{packageName}.{version}");
        if (Directory.Exists(expectedFolder))
        {
            if (!quiet)
            {
                Console.WriteLine($"{UiSymbols.Skip}  {packageName} {version} already present");
            }
            return version;
        }

        // Install the package
        if (!quiet)
        {
            Console.WriteLine($"{UiSymbols.Package} Installing {packageName} {version}...");
        }

        await _nugetService.InstallPackageAsync(rootDirectory, packageName, version, packagesDir, cancellationToken);
        return version;
    }

    /// <summary>
    /// Install multiple packages
    /// </summary>
    /// <param name="rootDirectory">The Root Directory path</param>
    /// <param name="packages">List of packages to install</param>
    /// <param name="includeExperimental">Include experimental/prerelease versions</param>
    /// <param name="ignoreConfig">Ignore configuration file for version management</param>
    /// <param name="quiet">Suppress progress messages</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Dictionary of installed packages and their versions</returns>
    public async Task<Dictionary<string, string>> InstallPackagesAsync(
        string rootDirectory,
        IEnumerable<string> packages,
        bool includeExperimental = false,
        bool ignoreConfig = false,
        bool quiet = false,
        CancellationToken cancellationToken = default)
    {
        var packagesDir = Path.Combine(rootDirectory, "packages");
        var installedVersions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // Ensure nuget.exe is available once for all packages
        if (!quiet)
        {
            Console.WriteLine($"{UiSymbols.Wrench} Ensuring nuget.exe is available...");
        }
        await _nugetService.EnsureNugetExeAsync(rootDirectory, cancellationToken);

        // Load pinned config if available
        WinsdkConfig? pinnedConfig = null;
        if (!ignoreConfig && _configService.Exists())
        {
            pinnedConfig = _configService.Load();
        }

        foreach (var packageName in packages)
        {
            // Resolve version: check pinned config first, then get latest
            string version;
            if (pinnedConfig != null && !ignoreConfig)
            {
                var pinnedVersion = pinnedConfig.GetVersion(packageName);
                if (!string.IsNullOrWhiteSpace(pinnedVersion))
                {
                    version = pinnedVersion!;
                }
                else
                {
                    version = await _nugetService.GetLatestVersionAsync(packageName, includeExperimental, cancellationToken);
                }
            }
            else
            {
                version = await _nugetService.GetLatestVersionAsync(packageName, includeExperimental, cancellationToken);
            }

            // Check if already installed
            var expectedFolder = Path.Combine(packagesDir, $"{packageName}.{version}");
            if (Directory.Exists(expectedFolder))
            {
                if (!quiet)
                {
                    Console.WriteLine($"{UiSymbols.Skip}  {packageName} {version} already present");
                }
                installedVersions[packageName] = version;
                continue;
            }

            // Install the package
            if (!quiet)
            {
                Console.WriteLine($"  {UiSymbols.Bullet} {packageName} {version}");
            }

            await _nugetService.InstallPackageAsync(rootDirectory, packageName, version, packagesDir, cancellationToken);
            installedVersions[packageName] = version;
        }

        return installedVersions;
    }

    /// <summary>
    /// Install a single package and verify it was installed correctly
    /// </summary>
    /// <param name="rootDirectory">The Root Directory path</param>
    /// <param name="packageName">Name of the package to install</param>
    /// <param name="version">Specific version to install (if null, gets latest or uses pinned version from config)</param>
    /// <param name="includeExperimental">Include experimental/prerelease versions</param>
    /// <param name="quiet">Suppress progress messages</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if the package was installed successfully, false otherwise</returns>
    public async Task<bool> EnsurePackageAsync(
        string rootDirectory,
        string packageName,
        string? version = null,
        bool includeExperimental = false,
        bool quiet = false,
        CancellationToken cancellationToken = default)
    {
        try
        {
            InitializeWorkspace(rootDirectory);

            var installedVersion = await InstallPackageAsync(
                rootDirectory,
                packageName,
                version: version,
                includeExperimental,
                quiet,
                cancellationToken);

            return true;
        }
        catch (Exception ex)
        {
            if (!quiet)
            {
                Console.Error.WriteLine($"Failed to install {packageName}: {ex.Message}");
            }
            return false;
        }
    }
}
