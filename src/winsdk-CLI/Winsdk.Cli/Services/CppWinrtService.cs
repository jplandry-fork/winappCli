// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Text;

namespace Winsdk.Cli.Services;

internal sealed class CppWinrtService(ILogger<CppWinrtService> logger) : ICppWinrtService
{
    public string? FindCppWinrtExe(string packagesDir, IDictionary<string, string> usedVersions)
    {
        var pkgName = "Microsoft.Windows.CppWinRT";
        if (!usedVersions.TryGetValue(pkgName, out var v))
        {
            return null;
        }

        var baseDir = Path.Combine(packagesDir, $"{pkgName}.{v}");
        var exe = Path.Combine(baseDir, "bin", "cppwinrt.exe");
        return File.Exists(exe) ? exe : null;
    }

    public async Task RunWithRspAsync(string cppwinrtExe, IEnumerable<string> winmdInputs, string outputDir, string workingDirectory, CancellationToken cancellationToken = default)
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
        if (logger.IsEnabled(LogLevel.Debug))
        {
            sb.AppendLine("-verbose");
        }

        await File.WriteAllTextAsync(rspPath, sb.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), cancellationToken);

        logger.LogDebug("cppwinrt: {CppWinrtExe} @{RspPath}", cppwinrtExe, rspPath);

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
        var stdout = await p.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderr = await p.StandardError.ReadToEndAsync(cancellationToken);
        await p.WaitForExitAsync(cancellationToken);

        if (!string.IsNullOrWhiteSpace(stdout))
        {
            logger.LogDebug("{StdOut}", stdout);
        }

        if (!string.IsNullOrWhiteSpace(stderr))
        {
            logger.LogDebug("{StdErr}", stderr);
        }

        if (p.ExitCode != 0)
        {
            throw new InvalidOperationException("cppwinrt execution failed");
        }
    }
}
