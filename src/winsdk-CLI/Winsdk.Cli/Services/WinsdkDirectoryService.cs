namespace Winsdk.Cli.Services;

/// <summary>
/// Service responsible for resolving winsdk directory paths
/// </summary>
internal class WinsdkDirectoryService : IWinsdkDirectoryService
{
    private string? _globalOverride;

    /// <summary>
    /// Method to override the cache directory for testing purposes
    /// </summary>
    /// <param name="cacheDirectory">The directory to use as the winsdk cache</param>
    public void SetCacheDirectoryForTesting(string cacheDirectory)
    {
        _globalOverride = cacheDirectory;
    }

    public string GetGlobalWinsdkDirectory()
    {
        // Instance override takes precedence (for testing)
        if (!string.IsNullOrEmpty(_globalOverride))
        {
            return _globalOverride;
        }

        // Allow override via environment variable (useful for CI/CD)
        var cacheDirectory = Environment.GetEnvironmentVariable("WINSDK_CACHE_DIRECTORY");
        if (!string.IsNullOrEmpty(cacheDirectory))
        {
            return cacheDirectory;
        }

        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var winsdkDir = Path.Combine(userProfile, ".winsdk");
        return winsdkDir;
    }

    public string GetLocalWinsdkDirectory(string? baseDirectoryStr = null)
    {
        if (string.IsNullOrEmpty(baseDirectoryStr))
        {
            baseDirectoryStr = Directory.GetCurrentDirectory();
        }

        var originalBaseDir = new DirectoryInfo(baseDirectoryStr);
        var baseDirectory = originalBaseDir;
        while (baseDirectory != null)
        {
            var winsdkDirectory = Path.Combine(baseDirectory.FullName, ".winsdk");
            if (Directory.Exists(winsdkDirectory))
            {
                return winsdkDirectory;
            }
            baseDirectory = baseDirectory.Parent;
        }

        return Path.Combine(originalBaseDir.FullName, ".winsdk");
    }
}
