using System.Diagnostics;

namespace Winsdk.Cli.Services;

/// <summary>
/// Service for executing PowerShell commands
/// </summary>
internal class PowerShellService
{
    /// <summary>
    /// Runs a PowerShell command and returns the exit code and output
    /// </summary>
    /// <param name="command">The PowerShell command to run</param>
    /// <param name="elevated">Whether to run with elevated privileges (UAC prompt)</param>
    /// <param name="environmentVariables">Optional dictionary of environment variables to set/override</param>
    /// <param name="verbose">Enable verbose logging</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Tuple containing (exitCode, stdout)</returns>
    public async Task<(int exitCode, string output)> RunCommandAsync(
        string command,
        bool elevated = false,
        Dictionary<string, string>? environmentVariables = null,
        bool verbose = false,
        CancellationToken cancellationToken = default)
    {
        if (verbose)
        {
            var elevatedText = elevated ? "elevated " : "";
            Console.WriteLine($"Running {elevatedText}PowerShell: {command}");
            if (elevated)
            {
                Console.WriteLine("UAC prompt may appear...");
            }
        }

        var psi = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            Arguments = $"-Command \"{command}\"",
            UseShellExecute = elevated, // Required for elevation
            RedirectStandardOutput = !elevated, // Always redirect when not elevated so we can capture output
            RedirectStandardError = !elevated,
            CreateNoWindow = !elevated
        };
        
        // Apply custom environment variables if provided
        if (environmentVariables != null)
        {
            foreach (var kvp in environmentVariables)
            {
                psi.Environment[kvp.Key] = kvp.Value;
            }
        }
        
        // Always clear PSModulePath to prevent PowerShell Core module conflicts when calling Windows PowerShell
        // This fixes the issue where calling powershell.exe from PowerShell Core causes module loading errors
        if (!psi.Environment.ContainsKey("PSModulePath"))
        {
            psi.Environment["PSModulePath"] = "";
        }

        if (elevated)
        {
            psi.Verb = "runas"; // This triggers UAC elevation
            psi.WindowStyle = ProcessWindowStyle.Normal; // Show window for elevated commands
        }

        using var process = Process.Start(psi);
        if (process == null)
        {
            return (-1, "Failed to start PowerShell process");
        }

        string stdout = "";
        string stderr = "";

        if (!elevated)
        {
            stdout = await process.StandardOutput.ReadToEndAsync(cancellationToken);
            stderr = await process.StandardError.ReadToEndAsync(cancellationToken);
        }

        await process.WaitForExitAsync(cancellationToken);

        if (verbose)
        {
            if (process.ExitCode != 0 && !string.IsNullOrWhiteSpace(stderr))
            {
                Console.WriteLine($"PowerShell error: {stderr}");
            }
            else if (!string.IsNullOrWhiteSpace(stdout))
            {
                Console.WriteLine($"PowerShell output: {stdout.Trim()}");
            }
        }

        // For elevated commands, exit codes may not be reliable, so we return 0 if no exception occurred
        var exitCode = elevated ? 0 : process.ExitCode;
        
        return (exitCode, stdout);
    }
}