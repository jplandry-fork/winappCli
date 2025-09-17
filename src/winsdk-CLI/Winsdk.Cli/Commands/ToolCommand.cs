using System.CommandLine;

namespace Winsdk.Cli.Commands;

internal class ToolCommand : Command
{
    private readonly BuildToolsService _buildToolsService;

    public ToolCommand() : base("tool", "Run a build tool command with Windows SDK paths")
    {
        var configService = new ConfigService(Directory.GetCurrentDirectory());
        _buildToolsService = new BuildToolsService(configService);
        Aliases.Add("run-buildtool");
        this.TreatUnmatchedTokensAsErrors = false;

        var quietOption = new Option<bool>("--quiet", "-q")
        {
            Description = "Suppress progress messages during auto-installation"
        };
        Options.Add(quietOption);

        SetAction(async (parseResult, ct) =>
        {
            var args = parseResult.UnmatchedTokens.ToArray();
            var quiet = parseResult.GetValue(quietOption);
            
            if (args.Length == 0)
            {
                Console.Error.WriteLine("No build tool command specified.");
                Console.Error.WriteLine($"Usage: winsdk tool [--quiet] <command> [args...]");
                Console.Error.WriteLine($"Example: winsdk tool makeappx.exe pack /o /d \"./msix\" /nv /p \"./dist/app.msix\"");
                return 1;
            }
            
            var toolName = args[0];
            var toolArgs = args.Skip(1).ToArray();
            
            // First, try to find the tool in existing installation
            var toolPath = _buildToolsService.GetBuildToolPath(toolName);
            if (toolPath == null && !toolName.EndsWith(".exe"))
            {
                toolPath = _buildToolsService.GetBuildToolPath(toolName + ".exe");
            }
            
            // If tool not found, ensure BuildTools are installed
            if (toolPath == null)
            {
                var binPath = await _buildToolsService.EnsureBuildToolsAsync(quiet: quiet, cancellationToken: ct);
                if (binPath == null)
                {
                    Console.Error.WriteLine($"Could not install or find Windows SDK Build Tools.");
                    return 1;
                }
                
                // Try again after installation
                toolPath = _buildToolsService.GetBuildToolPath(toolName);
                if (toolPath == null && !toolName.EndsWith(".exe"))
                {
                    toolPath = _buildToolsService.GetBuildToolPath(toolName + ".exe");
                }
            }
            
            if (toolPath == null)
            {
                Console.Error.WriteLine($"Could not find '{toolName}' in the Windows SDK Build Tools.");
                Console.Error.WriteLine($"Usage: winsdk tool [--quiet] <command> [args...]");
                Console.Error.WriteLine($"Example: winsdk tool makeappx.exe pack /o /d \"./msix\" /nv /p \"./dist/app.msix\"");
                return 1;
            }
            var processStartInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = toolPath,
                Arguments = string.Join(" ", toolArgs.Select(a => a.Contains(' ') ? $"\"{a}\"" : a)),
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            try
            {
                using var process = System.Diagnostics.Process.Start(processStartInfo);
                if (process == null)
                {
                    Console.Error.WriteLine($"Failed to start process for '{toolName}'.");
                    return 1;
                }
                process.OutputDataReceived += (sender, e) =>
                {
                    if (e.Data != null)
                        Console.Out.WriteLine(e.Data);
                };
                process.ErrorDataReceived += (sender, e) =>
                {
                    if (e.Data != null)
                        Console.Error.WriteLine(e.Data);
                };
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                await process.WaitForExitAsync(ct);
                return process.ExitCode;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error executing '{toolName}': {ex.Message}");
                return 1;
            }
        });
    }
}