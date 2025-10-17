// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Text.Json.Serialization;
using Winsdk.Cli.Helpers;

namespace Winsdk.Cli.Services;

[JsonSerializable(typeof(PackageCache))]
[JsonSerializable(typeof(Dictionary<string, Dictionary<string, string>>))]
[JsonSerializable(typeof(Dictionary<string, string>))]
[JsonSourceGenerationOptions(
    WriteIndented = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
internal partial class PackageCacheJsonContext : JsonSerializerContext
{
}

/// <summary>
/// Manages a JSON cache of installed packages in the .winsdk/packages folder
/// </summary>
internal sealed class PackageCacheService : IPackageCacheService
{
    private const string CacheFileName = "package-cache.json";
    private readonly string _cacheFilePath;
    private readonly ILogger<PackageCacheService> _logger;

    public PackageCacheService(IWinsdkDirectoryService directoryService, ILogger<PackageCacheService> logger)
    {
        var globalWinsdkDirectory = directoryService.GetGlobalWinsdkDirectory();
        var packagesDir = Path.Combine(globalWinsdkDirectory, "packages");
        _cacheFilePath = Path.Combine(packagesDir, CacheFileName);
        _logger = logger;
    }

    /// <summary>
    /// Load the package cache from disk
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The cached package information</returns>
    public async Task<PackageCache> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_cacheFilePath))
        {
            return new PackageCache();
        }

        try
        {
            using var fileStream = new FileStream(_cacheFilePath, FileMode.Open, FileAccess.Read);
            return await JsonSerializer.DeserializeAsync(fileStream, PackageCacheJsonContext.Default.PackageCache, cancellationToken) ?? new PackageCache();
        }
        catch (Exception ex)
        {
            _logger.LogError("Warning: Failed to load package cache: {ErrorMessage}", ex.Message);
            return new PackageCache();
        }
    }

    /// <summary>
    /// Save the package cache to disk
    /// </summary>
    /// <param name="cache">The cache to save</param>
    /// <param name="cancellationToken">Cancellation token</param>
    public async Task SaveAsync(PackageCache cache, CancellationToken cancellationToken = default)
    {
        try
        {
            // Ensure the packages directory exists
            var packagesDir = Path.GetDirectoryName(_cacheFilePath);
            if (!string.IsNullOrEmpty(packagesDir))
            {
                Directory.CreateDirectory(packagesDir);
            }

            using var stream = new FileStream(_cacheFilePath, FileMode.Create, FileAccess.Write);
            await JsonSerializer.SerializeAsync(stream, cache, PackageCacheJsonContext.Default.PackageCache, cancellationToken);

            _logger.LogInformation("{UISymbol} Package cache updated", UiSymbols.Save);
        }
        catch (Exception ex)
        {
            _logger.LogError("Warning: Failed to save package cache: {ErrorMessage}", ex.Message);
        }
    }

    /// <summary>
    /// Update the cache with a package installation
    /// </summary>
    /// <param name="packageName">The main package name that was requested</param>
    /// <param name="version">The main package version that was requested</param>
    /// <param name="installedPackages">Dictionary of all packages that were installed (including dependencies)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    public async Task UpdatePackageAsync(string packageName, string version, Dictionary<string, string> installedPackages, CancellationToken cancellationToken = default)
    {
        var cache = await LoadAsync(cancellationToken);
        var packageKey = $"{packageName}.{version}";

        // Filter out the main package from the installed packages to avoid self-reference
        var filteredPackages = installedPackages
            .Where(kvp => !kvp.Key.Equals(packageName, StringComparison.OrdinalIgnoreCase))
            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value, StringComparer.OrdinalIgnoreCase);

        // Store only the dependencies/related packages, not the main package itself
        cache.InstalledPackages[packageKey] = filteredPackages;

        await SaveAsync(cache, cancellationToken);
    }

    /// <summary>
    /// Get cached package installation info
    /// </summary>
    /// <param name="packageName">Name of the package</param>
    /// <param name="version">Version of the package</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Dictionary of installed packages if cached, throws if not found</returns>
    public async Task<Dictionary<string, string>> GetCachedPackageAsync(string packageName, string version, CancellationToken cancellationToken = default)
    {
        var cache = await LoadAsync(cancellationToken);
        var packageKey = $"{packageName}.{version}";
        if (cache.InstalledPackages.TryGetValue(packageKey, out var cachedPackages))
        {
            return cachedPackages;
        }
        
        throw new KeyNotFoundException($"Package {packageName} version {version} not found in cache");
    }
}

/// <summary>
/// Represents the overall package cache structure
/// </summary>
internal sealed class PackageCache
{
    public Dictionary<string, Dictionary<string, string>> InstalledPackages { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

