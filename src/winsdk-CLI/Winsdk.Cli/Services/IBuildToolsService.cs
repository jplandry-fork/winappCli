namespace Winsdk.Cli.Services;

internal interface IBuildToolsService
{
    string? GetBuildToolPath(string toolName);
    Task<string?> EnsureBuildToolsAsync(bool quiet = false, bool forceLatest = false, CancellationToken cancellationToken = default);
    Task<(string stdout, string stderr)> RunBuildToolAsync(string toolName, string arguments, bool verbose = false, CancellationToken cancellationToken = default);
}
