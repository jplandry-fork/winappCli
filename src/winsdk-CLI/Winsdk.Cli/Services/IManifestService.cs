namespace Winsdk.Cli.Services;

internal interface IManifestService
{
    public Task GenerateManifestAsync(
        string directory,
        string? packageName,
        string? publisherName,
        string version,
        string description,
        string? executable,
        bool sparse,
        string? logoPath,
        bool yes,
        bool verbose,
        CancellationToken cancellationToken = default);
}
