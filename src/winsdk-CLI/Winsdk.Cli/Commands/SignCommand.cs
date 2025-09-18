using System.CommandLine;
using Winsdk.Cli.Services;

namespace Winsdk.Cli.Commands;

internal class SignCommand : Command
{
    private readonly CertificateServices _certificateService;

    public SignCommand() : base("sign", "Sign a file/package with a certificate")
    {
        var configService = new ConfigService(Directory.GetCurrentDirectory());
        var buildToolsService = new BuildToolsService(configService);
        _certificateService = new CertificateServices(buildToolsService);
        var filePathArgument = new Argument<string>("file-path")
        {
            Description = "Path to the file/package to sign"
        };
        var certPathArgument = new Argument<string>("cert-path")
        {
            Description = "Path to the certificate file (PFX format)"
        };
        var passwordOption = new Option<string>("--password")
        {
            Description = "Certificate password",
            DefaultValueFactory = (argumentResult) => "password"
        };
        var timestampOption = new Option<string>("--timestamp")
        {
            Description = "Timestamp server URL"
        };

        Arguments.Add(filePathArgument);
        Arguments.Add(certPathArgument);
        Options.Add(passwordOption);
        Options.Add(timestampOption);
        Options.Add(Program.VerboseOption);

        SetAction(async (parseResult, ct) =>
        {
            var filePath = parseResult.GetRequiredValue(filePathArgument);
            var certPath = parseResult.GetRequiredValue(certPathArgument);
            var password = parseResult.GetValue(passwordOption);
            var timestamp = parseResult.GetValue(timestampOption);
            var verbose = parseResult.GetValue(Program.VerboseOption);

            try
            {
                await _certificateService.SignFileAsync(filePath, certPath, password, timestamp, verbose, ct);

                Console.WriteLine($"🔐 Signed file: {filePath}");
                return 0;
            }
            catch (Exception error)
            {
                Console.Error.WriteLine($"❌ Failed to sign file: {error.Message}");
                return 1;
            }
        });
    }
}