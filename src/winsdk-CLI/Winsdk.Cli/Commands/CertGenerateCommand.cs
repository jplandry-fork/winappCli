using System.CommandLine;
using Winsdk.Cli.Services;

namespace Winsdk.Cli.Commands;

internal class CertGenerateCommand : Command
{
    private readonly CertificateServices _certificateService;
    private readonly MsixService _msixService;

    public CertGenerateCommand()
        : base("generate", "Generate a new development certificate")
    {
        var configService = new ConfigService(Directory.GetCurrentDirectory());
        var buildToolsService = new BuildToolsService(configService);
        _certificateService = new CertificateServices(buildToolsService);
        _msixService = new MsixService(buildToolsService);
        
        var publisherOption = new Option<string>("--publisher")
        {
            Description = "Publisher name for the generated certificate. If not specified, will be inferred from manifest."
        };
        var manifestOption = new Option<string>("--manifest")
        {
            Description = "Path to appxmanifest.xml file to extract publisher information from"
        };
        manifestOption.AcceptLegalFilePathsOnly();
        var outputOption = new Option<string>("--output")
        {
            Description = "Output path for the generated PFX file",
            DefaultValueFactory = (argumentResult) => "devcert.pfx"
        };
        outputOption.AcceptLegalFileNamesOnly();
        var passwordOption = new Option<string>("--password")
        {
            Description = "Password for the generated PFX file",
            DefaultValueFactory = (argumentResult) => "password",
        };
        var validDaysOption = new Option<int>("--valid-days")
        {
            Description = "Number of days the certificate is valid",
            DefaultValueFactory = (argumentResult) => 365,
        };
        var installOption = new Option<bool>("--install")
        {
            Description = "Install the certificate to the local machine store after generation",
            DefaultValueFactory = (argumentResult) => false,
        };

        Options.Add(publisherOption);
        Options.Add(manifestOption);
        Options.Add(outputOption);
        Options.Add(passwordOption);
        Options.Add(validDaysOption);
        Options.Add(installOption);
        Options.Add(Program.VerboseOption);

        SetAction(async (parseResult, ct) =>
        {
            var publisher = parseResult.GetValue(publisherOption);
            var manifestPath = parseResult.GetValue(manifestOption);
            var output = parseResult.GetRequiredValue(outputOption);
            var password = parseResult.GetRequiredValue(passwordOption);
            var validDays = parseResult.GetRequiredValue(validDaysOption);
            var install = parseResult.GetRequiredValue(installOption);
            var verbose = parseResult.GetValue(Program.VerboseOption);

            try
            {
                // Check if certificate file already exists
                if (File.Exists(output))
                {
                    Console.Error.WriteLine($"❌ Certificate file already exists: {output}");
                    Console.Error.WriteLine("Please specify a different output path or remove the existing file.");
                    return 1;
                }

                // Infer publisher using the hierarchy specified
                var finalPublisher = await InferPublisherAsync(publisher, manifestPath, verbose, ct);
                
                var result = await _certificateService.GenerateDevCertificateAsync(finalPublisher, output, password, validDays, verbose, ct);

                Console.WriteLine("✅ Certificate generated successfully!");
                Console.WriteLine($"🔐 Certificate: {result.CertificatePath}");

                // Add certificate to .gitignore
                var certFileName = Path.GetFileName(result.CertificatePath);
                var projectDirectory = Directory.GetCurrentDirectory();
                GitignoreService.AddCertificateToGitignore(projectDirectory, certFileName, verbose);

                // Install certificate if requested
                if (install)
                {
                    if (verbose)
                    {
                        Console.WriteLine("Installing certificate...");
                    }
                    
                    var installResult = await _certificateService.InstallCertificateAsync(result.CertificatePath, password, false, verbose, ct);
                    if (installResult)
                    {
                        Console.WriteLine("✅ Certificate installed successfully!");
                    }
                    else
                    {
                        Console.WriteLine("ℹ️ Certificate was already installed");
                    }
                }

                return 0;
            }
            catch (Exception error)
            {
                Console.Error.WriteLine($"❌ Failed to generate certificate: {error.Message}");
                return 1;
            }
        });
    }

    /// <summary>
    /// Infers the publisher name using the specified hierarchy:
    /// 1. If publisher is explicitly provided, use that
    /// 2. If manifest path is provided, extract publisher from that manifest
    /// 3. If appxmanifest.xml is found in project (.winsdk directory), use that
    /// 4. Otherwise, fail
    /// </summary>
    private async Task<string> InferPublisherAsync(string? publisher, string? manifestPath, bool verbose, CancellationToken cancellationToken)
    {
        // 1. If --publisher is passed, use that
        if (!string.IsNullOrWhiteSpace(publisher))
        {
            if (verbose)
            {
                Console.WriteLine($"Using publisher from command line: {publisher}");
            }
            return publisher;
        }

        // 2. If --manifest path is passed, use the publisher from the appxmanifest
        if (!string.IsNullOrWhiteSpace(manifestPath))
        {
            if (verbose)
            {
                Console.WriteLine($"Extracting publisher from manifest: {manifestPath}");
            }
            
            var identityInfo = await _msixService.ParseAppxManifestAsync(manifestPath, cancellationToken);
            return identityInfo.Publisher;
        }

        // 3. If appxmanifest.xml is found in the current project, use that
        var projectManifestPath = MsixService.FindProjectManifest();
        if (projectManifestPath != null)
        {
            if (verbose)
            {
                Console.WriteLine($"Found project manifest: {projectManifestPath}");
            }
            
            var identityInfo = await _msixService.ParseAppxManifestAsync(projectManifestPath, cancellationToken);
            return identityInfo.Publisher;
        }

        // 4. Fail if no publisher can be determined
        throw new InvalidOperationException(
            "Publisher could not be determined. Please specify --publisher, --manifest, or ensure appxmanifest.xml exists in your project.");
    }
}
