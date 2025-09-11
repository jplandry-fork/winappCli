using System.CommandLine;
using System.Runtime.InteropServices;
using Winsdk.Cli.Services;

namespace Winsdk.Cli.Commands;

internal class SetupCommand : Command
{
    enum Architecture
    {
        x64,
        arm64
    }

    public SetupCommand() : base("setup", "Download and setup Windows SDKs")
    {
        var baseDirectoryArgument = new Argument<string>("base-directory")
        {
            Description = "Base/root directory for the winsdk workspace, for consumption or installation.",
            Arity = ArgumentArity.ZeroOrOne
        };
        var configDirOption = new Option<string>("--config-dir")
        {
            Description = "Directory to read/store configuration (default: current directory)",
            DefaultValueFactory = (argumentResult) => Directory.GetCurrentDirectory()
        };
        var experimentalOption = new Option<bool>("--experimental")
        {
            Description = "Include experimental/prerelease packages from NuGet"
        };
        var ignoreConfigOption = new Option<bool>("--ignore-config", "--no-config")
        {
            Description = "Don't use configuration file for version management"
        };
        var noGitignoreOption = new Option<bool>("--no-gitignore")
        {
            Description = "Don't update .gitignore file"
        };
        var quietOption = new Option<bool>("--quiet", "-q")
        {
            Description = "Suppress progress messages"
        };
        var yesOption = new Option<bool>("--yes")
        {
            Description = "Assume yes to all prompts"
        };
        var archOption = new Option<Architecture>("--arch")
        {
            Description = "Target architecture",
            DefaultValueFactory = (argumentResult) =>
            {
                // Default to host architecture
                return RuntimeInformation.ProcessArchitecture == System.Runtime.InteropServices.Architecture.Arm64 ? Architecture.arm64 : Architecture.x64;
            }
        };

        Arguments.Add(baseDirectoryArgument);
        Options.Add(configDirOption);
        Options.Add(experimentalOption);
        Options.Add(ignoreConfigOption);
        Options.Add(noGitignoreOption);
        Options.Add(quietOption);
        Options.Add(yesOption);
        Options.Add(archOption);
        Options.Add(Program.VerboseOption);

        SetAction(async (parseResult, ct) =>
        {
            var baseDirectory = parseResult.GetValue(baseDirectoryArgument);
            var configDir = parseResult.GetRequiredValue(configDirOption);
            var experimental = parseResult.GetValue(experimentalOption);
            var ignoreConfig = parseResult.GetValue(ignoreConfigOption);
            var noGitignore = parseResult.GetValue(noGitignoreOption);
            var quiet = parseResult.GetValue(quietOption);
            var assumeYes = parseResult.GetValue(yesOption);
            var arch = parseResult.GetRequiredValue(archOption);
            var verbose = parseResult.GetValue(Program.VerboseOption);

            var winsdkDir = BuildToolsService.FindWinsdkDirectory(baseDirectory);

            if (!Directory.Exists(winsdkDir))
            {
                Directory.CreateDirectory(winsdkDir);
            }

            var pkgsDir = Path.Combine(winsdkDir, "packages");
            var includeOut = Path.Combine(winsdkDir, "include");
            var libOut = Path.Combine(winsdkDir, "lib");
            var binOut = Path.Combine(winsdkDir, "bin");
            Directory.CreateDirectory(pkgsDir);
            Directory.CreateDirectory(includeOut);
            Directory.CreateDirectory(libOut);
            Directory.CreateDirectory(binOut);
            
            var config = new ConfigService(configDir);

            Console.WriteLine($"{UiSymbols.Rocket} using config → {config.ConfigPath}");
            Console.WriteLine($"{UiSymbols.Rocket} winsdk init starting in {baseDirectory}");
            Console.WriteLine($"{UiSymbols.Folder} Workspace → {winsdkDir}");

            if (experimental)
            {
                Console.WriteLine($"{UiSymbols.Wrench} Experimental/prerelease packages will be included");
            }

            var nuget = new NugetService();
            var cppwinrt = new CppWinrtService();
            var usedVersions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            WinsdkConfig pinned = new();

            if (quiet && verbose)
            {
                Console.Error.WriteLine($"Cannot specify both --quiet and --verbose options together.");
                return 1;
            }

            Console.WriteLine($"{UiSymbols.Note} Checking for winsdk.yaml...");
            var hadConfig = config.Exists();
            if (hadConfig)
            {
                pinned = config.Load();
                if (!quiet)
                {
                    Console.WriteLine($"{UiSymbols.Note} Found winsdk.yaml; using pinned versions unless overridden.");
                }
                if (!ignoreConfig && pinned.Packages.Count > 0)
                {
                    var overwrite = assumeYes || Program.PromptYesNo("winsdk.yaml exists with pinned versions. Overwrite with latest versions? [y/N]: ");
                    if (overwrite) { ignoreConfig = true; }
                }
            }
            else
            {
                if (!quiet)
                {
                    Console.WriteLine($"{UiSymbols.New} No winsdk.yaml found; will generate one after init.");
                }
            }

            Console.WriteLine($"{UiSymbols.Wrench} Ensuring nuget.exe is available...");
            if (!quiet)
            {
                Console.WriteLine($"{UiSymbols.Package} Installing SDK packages → {pkgsDir}");
            }
            await nuget.EnsureNugetExeAsync(winsdkDir, ct);

            foreach (var pkg in NugetService.SDK_PACKAGES)
            {
                string version;
                if (!ignoreConfig && config.Exists())
                {
                    var v = pinned.GetVersion(pkg);
                    if (string.IsNullOrWhiteSpace(v))
                    {
                        version = await nuget.GetLatestVersionAsync(pkg, experimental, ct);
                    }
                    else
                    {
                        version = v!;
                    }
                }
                else
                {
                    version = await nuget.GetLatestVersionAsync(pkg, experimental, ct);
                }

                Console.WriteLine($"  {UiSymbols.Bullet} {pkg} {version}");
                await nuget.InstallPackageAsync(winsdkDir, pkg, version, pkgsDir, ct);
                usedVersions[pkg] = version;
            }

            // Prepare consolidated layout and run cppwinrt
            var cppWinrtExe = cppwinrt.FindCppWinrtExe(pkgsDir, usedVersions);
            if (cppWinrtExe is null)
            {
                Console.Error.WriteLine("cppwinrt.exe not found in installed packages.");
                return 2;
            }
            Console.WriteLine($"{UiSymbols.Tools} Using cppwinrt tool → {cppWinrtExe}");

            // Copy headers, libs, runtimes
            var layout = new PackageLayoutService();
            if (!quiet)
            {
                Console.WriteLine($"{UiSymbols.Files} Copying headers → {includeOut}");
            }
            layout.CopyIncludesFromPackages(pkgsDir, includeOut);
            Console.WriteLine($"{UiSymbols.Check} Headers ready → {includeOut}");

            var libRoot = Path.Combine(winsdkDir, "lib");
            if (!quiet)
            {
                Console.WriteLine($"{UiSymbols.Books} Copying import libs by arch → {libRoot}");
            }
            layout.CopyLibsAllArch(pkgsDir, libRoot);
            var libArchs = Directory.Exists(libRoot) ? string.Join(", ", Directory.EnumerateDirectories(libRoot).Select(Path.GetFileName)) : "(none)";
            Console.WriteLine($"{UiSymbols.Books} Import libs ready for archs: {libArchs}");

            var binRoot = Path.Combine(winsdkDir, "bin");
            if (!quiet)
            {
                Console.WriteLine($"{UiSymbols.Gear} Copying runtime binaries by arch → {binRoot}");
            }
            layout.CopyRuntimesAllArch(pkgsDir, binRoot);
            var binArchs = Directory.Exists(binRoot) ? string.Join(", ", Directory.EnumerateDirectories(binRoot).Select(Path.GetFileName)) : "(none)";
            Console.WriteLine($"{UiSymbols.Gear} Runtime binaries ready for archs: {binArchs}");

            // Copy Windows App SDK license into the workspace share so downstream consumers can include the license in their packages.
            try
            {
                if (usedVersions.TryGetValue("Microsoft.WindowsAppSDK", out var wasdkVersion))
                {
                    var pkgDir = Path.Combine(pkgsDir, $"Microsoft.WindowsAppSDK.{wasdkVersion}");
                    var licenseSrc = Path.Combine(pkgDir, "license.txt");
                    if (File.Exists(licenseSrc))
                    {
                        var shareDir = Path.Combine(winsdkDir, "share", "Microsoft.WindowsAppSDK");
                        Directory.CreateDirectory(shareDir);
                        var licenseDst = Path.Combine(shareDir, "copyright");
                        File.Copy(licenseSrc, licenseDst, overwrite: true);
                        Console.WriteLine($"{UiSymbols.Check} License copied → {licenseDst}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"{UiSymbols.Note} Failed to copy license: {ex.Message}");
            }

            // Collect winmd inputs
            if (!quiet)
            {
                Console.WriteLine($"{UiSymbols.Search} Searching for .winmd metadata...");
            }
            var winmds = layout.FindWinmds(pkgsDir).ToList();
            if (!quiet)
            {
                Console.WriteLine($"{UiSymbols.Search} Found {winmds.Count} .winmd");
            }
            if (winmds.Count == 0)
            {
                Console.Error.WriteLine("No .winmd files found for C++/WinRT projection.");
                return 2;
            }

            // Run cppwinrt inline with -input sdk+ and explicit winmds
            if (!quiet)
            {
                Console.WriteLine($"{UiSymbols.Gear} Generating C++/WinRT projections...");
            }
            await CppWinrtRunner.RunWithRspAsync(cppWinrtExe, winmds, includeOut, winsdkDir, verbose: !quiet, cancellationToken: ct);
            Console.WriteLine($"{UiSymbols.Check} C++/WinRT headers generated → {includeOut}");

            // Save winsdk.yaml with used versions
            var finalConfig = new WinsdkConfig();
            foreach (var kvp in usedVersions)
            {
                finalConfig.SetVersion(kvp.Key, kvp.Value);
            }
            config.Save(finalConfig);
            Console.WriteLine($"{UiSymbols.Save} Wrote config → {config.ConfigPath}");

            // Update .gitignore to exclude .winsdk folder (unless --no-gitignore is specified)
            if (!noGitignore)
            {
                var path = new DirectoryInfo(winsdkDir);
                if (path.Parent != null)
                {
                    GitignoreService.UpdateGitignore(path.Parent.FullName, verbose: !quiet);
                }
            }

            Console.WriteLine($"{UiSymbols.Party} winsdk init completed.");

            return 0;
        });
    }
}
