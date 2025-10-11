using System.Text.RegularExpressions;
using Winsdk.Cli.Helpers;

namespace Winsdk.Cli.Services;

internal class ManifestService : IManifestService
{
    public async Task GenerateManifestAsync(
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
        CancellationToken cancellationToken = default)
    {
        if (verbose)
        {
            Console.WriteLine($"Generating manifest in directory: {directory}");
        }

        // Check if manifest already exists
        var manifestPath = MsixService.FindProjectManifest(directory);
        if (File.Exists(manifestPath))
        {
            throw new InvalidOperationException($"Manifest already exists at: {manifestPath}");
        }

        // Interactive mode if not --yes (get defaults for prompts)
        packageName ??= SystemDefaultsHelper.GetDefaultPackageName(directory);
        publisherName ??= SystemDefaultsHelper.GetDefaultPublisherCN();
        executable ??= $"{packageName}.exe";

        // Interactive mode if not --yes
        if (!yes)
        {
            packageName = PromptForValue("Package name", packageName);
            publisherName = PromptForValue("Publisher name", publisherName);
            version = PromptForValue("Version", version);
            description = PromptForValue("Description", description);
            executable = PromptForValue("Executable", executable);
        }

        if (verbose)
        {
            Console.WriteLine($"Logo path: {logoPath ?? "None"}");
        }

        packageName = CleanPackageName(packageName);

        // Generate complete manifest using shared service
        await ManifestTemplateService.GenerateCompleteManifestAsync(
            directory,
            packageName,
            publisherName,
            version,
            executable,
            sparse,
            description,
            verbose,
            cancellationToken);

        // If logo path is provided, copy it as additional asset
        if (!string.IsNullOrEmpty(logoPath) && File.Exists(logoPath))
        {
            await CopyLogoAsAdditionalAssetAsync(directory, logoPath, verbose, cancellationToken);
        }
    }

    /// <summary>
    /// Cleans and sanitizes a package name to meet MSIX AppxManifest schema requirements.
    /// Based on ST_PackageName type which restricts ST_AsciiIdentifier.
    /// </summary>
    /// <param name="packageName">The package name to clean</param>
    /// <returns>A cleaned package name that meets MSIX schema requirements</returns>
    internal static string CleanPackageName(string packageName)
    {
        if (string.IsNullOrWhiteSpace(packageName))
        {
            return "DefaultPackage";
        }

        // Trim whitespace
        var cleaned = packageName.Trim();

        // Remove invalid characters (keep only letters, numbers, hyphens, underscores, periods, and spaces)
        // ST_AllowedAsciiCharSet pattern="[-_. A-Za-z0-9]+"
        cleaned = Regex.Replace(cleaned, @"[^A-Za-z0-9\-_. ]", "");

        // Check if it starts with underscore BEFORE removing them
        bool startsWithUnderscore = cleaned.StartsWith('_');

        // Remove leading underscores (ST_AsciiIdentifier restriction)
        cleaned = cleaned.TrimStart('_');

        // If still empty or whitespace after cleaning, use default
        if (string.IsNullOrWhiteSpace(cleaned))
        {
            cleaned = "DefaultPackage";
        }

        // If originally started with underscore, prepend "App"
        if (startsWithUnderscore)
        {
            cleaned = "App" + cleaned;
        }

        // Ensure minimum length of 3 characters
        if (cleaned.Length < 3)
        {
            cleaned = cleaned.PadRight(3, '1'); // Pad with '1' to reach minimum length
        }

        // Truncate to maximum length of 50 characters
        if (cleaned.Length > 50)
        {
            cleaned = cleaned.Substring(0, 50).TrimEnd(); // Trim end in case we cut off mid-word
        }

        return cleaned;
    }

    private async Task CopyLogoAsAdditionalAssetAsync(string directory, string logoPath, bool verbose, CancellationToken cancellationToken = default)
    {
        var assetsDir = Path.Combine(directory, "Assets");
        Directory.CreateDirectory(assetsDir);

        var logoFileName = Path.GetFileName(logoPath);
        var destinationPath = Path.Combine(assetsDir, logoFileName);

        using var sourceStream = new FileStream(logoPath, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 4096, useAsync: true);
        using var destinationStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize: 4096, useAsync: true);
        
        await sourceStream.CopyToAsync(destinationStream, cancellationToken);

        if (verbose)
        {
            Console.WriteLine($"Logo copied to: {destinationPath}");
        }
    }

    private string PromptForValue(string prompt, string defaultValue)
    {
        if (!string.IsNullOrEmpty(defaultValue))
        {
            return defaultValue;
        }
        
        Console.Write($"{prompt} ({defaultValue}): ");
        var input = Console.ReadLine();
        return string.IsNullOrWhiteSpace(input) ? defaultValue : input.Trim();
    }
}
