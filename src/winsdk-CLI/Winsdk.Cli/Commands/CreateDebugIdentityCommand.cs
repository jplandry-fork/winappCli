// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Extensions.Logging;
using System.CommandLine;
using System.CommandLine.Invocation;
using Winsdk.Cli.Services;

namespace Winsdk.Cli.Commands;

internal class CreateDebugIdentityCommand : Command
{
    public static Argument<string> ExecutableArgument { get; }
    public static Option<string> ManifestOption { get; }
    public static Option<bool> NoInstallOption { get; }
    public static Option<string> LocationOption { get; }

    static CreateDebugIdentityCommand()
    {
        ExecutableArgument = new Argument<string>("executable")
        {
            Description = "Path to the .exe that will need to run with identity"
        };
        ManifestOption = new Option<string>("--manifest")
        {
            Description = "Path to the appxmanifest.xml",
            DefaultValueFactory = (argumentResult) => ".\\appxmanifest.xml"
        };
        NoInstallOption = new Option<bool>("--no-install")
        {
            Description = "Do not install the package after creation."
        };
        LocationOption = new Option<string>("--location")
        {
            Description = "Root path of the application. Default is parent directory of the executable."
        };
    }

    public CreateDebugIdentityCommand() : base("create-debug-identity", "Create and install a temporary package for debugging. Must be called every time the appxmanifest.xml is modified for changes to take effect.")
    {
        Arguments.Add(ExecutableArgument);
        Options.Add(ManifestOption);
        Options.Add(NoInstallOption);
        Options.Add(LocationOption);
    }

    public class Handler(IMsixService msixService, ILogger<CreateDebugIdentityCommand> logger) : AsynchronousCommandLineAction
    {
        public override async Task<int> InvokeAsync(ParseResult parseResult, CancellationToken cancellationToken = default)
        {
            var executablePath = parseResult.GetRequiredValue(ExecutableArgument);
            var manifest = parseResult.GetRequiredValue(ManifestOption);
            var noInstall = parseResult.GetValue(NoInstallOption);
            var location = parseResult.GetValue(LocationOption);

            if (!File.Exists(executablePath))
            {
                logger.LogError("Executable not found: {ExecutablePath}", executablePath);
                return 1;
            }

            try
            {
                var result = await msixService.AddMsixIdentityToExeAsync(executablePath, manifest, noInstall, location, cancellationToken);

                logger.LogInformation("✅ MSIX identity added successfully!");
                logger.LogInformation("📦 Package: {PackageName}", result.PackageName);
                logger.LogInformation("👤 Publisher: {Publisher}", result.Publisher);
                logger.LogInformation("🆔 App ID: {ApplicationId}", result.ApplicationId);
            }
            catch (Exception error)
            {
                logger.LogError("❌ Failed to add MSIX identity: {ErrorMessage}", error.Message);
                return 1;
            }

            return 0;
        }
    }
}
