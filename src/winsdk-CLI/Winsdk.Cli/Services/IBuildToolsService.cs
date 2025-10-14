namespace Winsdk.Cli.Services;

internal interface IBuildToolsService
{
    /// <summary>
    /// Get the path to a build tool if it exists in the current installation.
    /// This method does NOT install BuildTools if they are missing.
    /// Use EnsureBuildToolAvailableAsync if you want automatic installation.
    /// </summary>
    /// <param name="toolName">Name of the tool (e.g., 'mt.exe', 'signtool.exe')</param>
    /// <returns>Full path to the executable if found, null otherwise</returns>
    string? GetBuildToolPath(string toolName);

    /// <summary>
    /// Ensures a build tool is available by finding it or installing BuildTools if necessary.
    /// This method guarantees a tool path will be returned or an exception will be thrown.
    /// </summary>
    /// <param name="toolName">Name of the tool (e.g., 'mt.exe', 'signtool.exe')</param>  
    /// <param name="quiet">Suppress progress messages during auto-installation</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Full path to the executable</returns>
    /// <exception cref="FileNotFoundException">Tool not found even after installation</exception>
    /// <exception cref="InvalidOperationException">BuildTools installation failed</exception>
    Task<string> EnsureBuildToolAvailableAsync(string toolName, bool quiet = false, CancellationToken cancellationToken = default);

    Task<string?> EnsureBuildToolsAsync(bool quiet = false, bool forceLatest = false, CancellationToken cancellationToken = default);
    Task<(string stdout, string stderr)> RunBuildToolAsync(string toolName, string arguments, bool verbose = false, bool quiet = false, CancellationToken cancellationToken = default);
}
