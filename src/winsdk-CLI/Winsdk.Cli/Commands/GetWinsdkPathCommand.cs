using System.CommandLine;

namespace Winsdk.Cli.Commands;

internal class GetWinsdkPathCommand : Command
{
    public GetWinsdkPathCommand() : base("get-winsdk-path", "Get the path to the .winsdk directory (local by default, global with --global)")
    {
        var globalOption = new Option<bool>("--global")
        {
            Description = "Get the global .winsdk directory instead of local"
        };
        
        Options.Add(globalOption);
        Options.Add(Program.VerboseOption);

        SetAction((parseResult) =>
        {
            var verbose = parseResult.GetValue(Program.VerboseOption);
            var global = parseResult.GetValue(globalOption);

            try
            {
                string winsdkDir;
                string directoryType;
                
                if (global)
                {
                    // Get the global .winsdk directory
                    winsdkDir = BuildToolsService.GetGlobalWinsdkDirectory();
                    directoryType = "Global";
                }
                else
                {
                    // Get the local .winsdk directory
                    var buildToolsService = new BuildToolsService(new ConfigService(Directory.GetCurrentDirectory()));
                    winsdkDir = buildToolsService.GetLocalWinsdkDirectory(Directory.GetCurrentDirectory());
                    directoryType = "Local";
                }
                
                if (string.IsNullOrEmpty(winsdkDir))
                {
                    if (verbose)
                    {
                        Console.Error.WriteLine($"❌ {directoryType} .winsdk directory path could not be determined");
                    }
                    return 1;
                }

                // For global directories, check if they exist
                if (global && !Directory.Exists(winsdkDir))
                {
                    if (verbose)
                    {
                        Console.Error.WriteLine($"❌ {directoryType} .winsdk directory not found: {winsdkDir}");
                        Console.Error.WriteLine($"   Make sure to run 'winsdk init' first");
                    }
                    return 1;
                }

                // Output just the path for easy consumption by scripts
                Console.WriteLine(winsdkDir);
                
                if (verbose)
                {
                    var exists = Directory.Exists(winsdkDir);
                    var status = exists ? "exists" : "does not exist";
                    Console.Error.WriteLine($"📂 {directoryType} .winsdk directory: {winsdkDir} ({status})");
                }
                
                return 0;
            }
            catch (Exception ex)
            {
                if (verbose)
                {
                    Console.Error.WriteLine($"❌ Error getting {(global ? "global" : "local")} winsdk directory: {ex.Message}");
                }
                else
                {
                    Console.Error.WriteLine($"{(global ? "Global" : "Local")} winsdk directory not found");
                }
                return 1;
            }
        });
    }
}
