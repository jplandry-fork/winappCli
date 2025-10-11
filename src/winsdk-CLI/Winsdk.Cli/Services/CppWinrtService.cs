using System.Diagnostics;
using System.Text;

namespace Winsdk.Cli.Services;

internal sealed class CppWinrtService : ICppWinrtService
{
    public string? FindCppWinrtExe(string packagesDir, IDictionary<string, string> usedVersions)
    {
        var pkgName = "Microsoft.Windows.CppWinRT";
        if (!usedVersions.TryGetValue(pkgName, out var v)) return null;
        var baseDir = Path.Combine(packagesDir, $"{pkgName}.{v}");
        var exe = Path.Combine(baseDir, "bin", "cppwinrt.exe");
        return File.Exists(exe) ? exe : null;
    }

    public async Task RunWithRspAsync(string cppwinrtExe, IEnumerable<string> winmdInputs, string outputDir, string workingDirectory, bool verbose, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(outputDir);
        var rspPath = Path.Combine(outputDir, ".cppwinrt.rsp");

        var sb = new StringBuilder();
        sb.AppendLine("-input sdk+");
        foreach (var winmd in winmdInputs)
        {
            sb.AppendLine($"-input \"{winmd}\"");
        }
        sb.AppendLine("-optimize");
        sb.AppendLine($"-output \"{outputDir}\"");
        if (verbose) sb.AppendLine("-verbose");

        await File.WriteAllTextAsync(rspPath, sb.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), cancellationToken);

        if (verbose)
        {
            Console.WriteLine($"cppwinrt: {cppwinrtExe} @{rspPath}");
        }

        var psi = new ProcessStartInfo
        {
            FileName = cppwinrtExe,
            Arguments = $"@{rspPath}",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            WorkingDirectory = workingDirectory
        };

        using var p = Process.Start(psi)!;
        var so = await p.StandardOutput.ReadToEndAsync(cancellationToken);
        var se = await p.StandardError.ReadToEndAsync(cancellationToken);
        await p.WaitForExitAsync(cancellationToken);

        if (verbose)
        {
            if (!string.IsNullOrWhiteSpace(so)) Console.WriteLine(so);
            if (!string.IsNullOrWhiteSpace(se)) Console.WriteLine(se);
        }

        if (p.ExitCode != 0)
        {
            throw new InvalidOperationException("cppwinrt execution failed");
        }
    }
}
