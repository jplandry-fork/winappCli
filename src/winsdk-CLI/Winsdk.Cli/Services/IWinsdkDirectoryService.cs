namespace Winsdk.Cli.Services;

/// <summary>
/// Interface for resolving winsdk directory paths
/// </summary>
internal interface IWinsdkDirectoryService
{
    string GetGlobalWinsdkDirectory();
    string GetLocalWinsdkDirectory(string? baseDirectoryStr = null);
    void SetCacheDirectoryForTesting(string cacheDirectory);
}
