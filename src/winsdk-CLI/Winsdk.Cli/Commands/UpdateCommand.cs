using System.CommandLine;
using Winsdk.Cli.Services;

namespace Winsdk.Cli.Commands;

internal class UpdateCommand : Command
{
    public UpdateCommand() : base("update", "Update packages in winsdk.yaml and install/update build tools in cache")
    {
        var prereleaseOption = new Option<bool>("--prerelease")
        {
            Description = "Include prerelease versions when checking for updates"
        };

        Options.Add(prereleaseOption);
        Options.Add(Program.VerboseOption);

        SetAction(async (parseResult, ct) =>
        {
            var prerelease = parseResult.GetValue(prereleaseOption);
            var verbose = parseResult.GetValue(Program.VerboseOption);

            try
            {
                var configService = new ConfigService(Directory.GetCurrentDirectory());
                var buildToolsService = new BuildToolsService(configService);
                var packageService = new PackageInstallationService(configService);
                var nugetService = new NugetService();

                var winsdkDir = BuildToolsService.GetGlobalWinsdkDirectory();

                // Step 1: Find yaml config file
                if (verbose)
                {
                    Console.WriteLine($"{UiSymbols.Note} Checking for winsdk.yaml configuration...");
                }

                if (configService.Exists())
                {
                    // Step 1.1: Update packages in yaml config
                    var config = configService.Load();
                    
                    if (config.Packages.Count == 0)
                    {
                        if (verbose)
                        {
                            Console.WriteLine($"{UiSymbols.Note} winsdk.yaml found but contains no packages");
                        }
                    }
                    else
                    {
                        Console.WriteLine($"{UiSymbols.Package} Found winsdk.yaml with {config.Packages.Count} packages, checking for updates...");
                        
                        var updatedConfig = new WinsdkConfig();
                        bool hasUpdates = false;

                        foreach (var package in config.Packages)
                        {
                            if (verbose)
                            {
                                Console.WriteLine($"  {UiSymbols.Bullet} Checking {package.Name} (current: {package.Version})");
                            }

                            try
                            {
                                var latestVersion = await nugetService.GetLatestVersionAsync(package.Name, prerelease, ct);
                                
                                if (latestVersion != package.Version)
                                {
                                    Console.WriteLine($"  {UiSymbols.Rocket} {package.Name}: {package.Version} → {latestVersion}");
                                    updatedConfig.SetVersion(package.Name, latestVersion);
                                    hasUpdates = true;
                                }
                                else
                                {
                                    if (verbose)
                                    {
                                        Console.WriteLine($"  {UiSymbols.Check} {package.Name}: already latest ({latestVersion})");
                                    }
                                    updatedConfig.SetVersion(package.Name, package.Version);
                                }
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"  ⚠️  Failed to check {package.Name}: {ex.Message}");
                                // Keep current version on error
                                updatedConfig.SetVersion(package.Name, package.Version);
                            }
                        }

                        if (hasUpdates)
                        {
                            configService.Save(updatedConfig);
                            Console.WriteLine($"{UiSymbols.Save} Updated winsdk.yaml with latest versions");
                            
                            // Install the updated packages
                            Console.WriteLine($"{UiSymbols.Package} Installing updated packages...");
                            var packageNames = updatedConfig.Packages.Select(p => p.Name).ToArray();
                            
                            var installedVersions = await packageService.InstallPackagesAsync(
                                winsdkDir,
                                packageNames,
                                includeExperimental: prerelease,
                                ignoreConfig: false, // Use the updated config
                                quiet: !verbose,
                                cancellationToken: ct
                            );
                            
                            Console.WriteLine($"{UiSymbols.Check} Package installation completed");
                        }
                        else
                        {
                            Console.WriteLine($"{UiSymbols.Check} All packages are already up to date");
                        }
                    }
                }
                else
                {
                    if (verbose)
                    {
                        Console.WriteLine($"{UiSymbols.Note} No winsdk.yaml found");
                    }
                }

                // Step 2: Ensure build tools are installed/updated in cache
                if (verbose)
                {
                    Console.WriteLine($"{UiSymbols.Wrench} Checking build tools in cache...");
                }

                var buildToolsPath = await buildToolsService.EnsureBuildToolsAsync(quiet: !verbose, forceLatest: true, cancellationToken: ct);
                
                if (buildToolsPath != null)
                {
                    if (verbose)
                    {
                        Console.WriteLine($"{UiSymbols.Check} Build tools are available at: {buildToolsPath}");
                    }
                    else
                    {
                        Console.WriteLine($"{UiSymbols.Check} Build tools are up to date");
                    }
                }
                else
                {
                    Console.Error.WriteLine($"❌ Failed to install/update build tools");
                    return 1;
                }

                Console.WriteLine($"{UiSymbols.Party} Update completed successfully!");
                return 0;
            }
            catch (Exception error)
            {
                Console.Error.WriteLine($"❌ Update failed: {error.Message}");
                if (verbose)
                {
                    Console.Error.WriteLine(error.StackTrace);
                }
                return 1;
            }
        });
    }
}
