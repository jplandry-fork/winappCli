namespace Winsdk.Cli.Services;

internal interface IPackageCacheService
{
    Task<PackageCache> LoadAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(PackageCache cache, bool quiet = false, CancellationToken cancellationToken = default);
    Task UpdatePackageAsync(string packageName, string version, Dictionary<string, string> installedPackages, bool quiet = false, CancellationToken cancellationToken = default);
    Task<Dictionary<string, string>> GetCachedPackageAsync(string packageName, string version, CancellationToken cancellationToken = default);
}