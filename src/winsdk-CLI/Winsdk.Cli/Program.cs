// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Text;
using Winsdk.Cli.Commands;
using Winsdk.Cli.Helpers;
using Winsdk.Cli.Telemetry.Events;

namespace Winsdk.Cli;

internal static class Program
{
    static async Task<int> Main(string[] args)
    {
        // Ensure UTF-8 I/O for emoji-capable terminals; fall back silently if not supported
        try
        {
            Console.OutputEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
            Console.InputEncoding = Encoding.UTF8;
        }
        catch
        {
            // ignore
        }
        
        var minimumLogLevel = LogLevel.Information;
        bool quiet = false;
        bool verbose = false;

        if (args.Contains(WinSdkRootCommand.VerboseOption.Name) || args.Any(WinSdkRootCommand.VerboseOption.Aliases.Contains))
        {
            minimumLogLevel = LogLevel.Debug;
            verbose = true;
        }
        else if (args.Contains(WinSdkRootCommand.QuietOption.Name) || args.Any(WinSdkRootCommand.QuietOption.Aliases.Contains))
        {
            minimumLogLevel = LogLevel.Warning;
            quiet = true;
        }

        if (quiet && verbose)
        {
            Console.Error.WriteLine($"Cannot specify both --quiet and --verbose options together.");
            return 1;
        }

        var services = new ServiceCollection()
            .ConfigureServices()
            .ConfigureCommands()
            .AddLogging(b =>
            {
                b.ClearProviders();
                b.AddTextWriterLogger(Console.Out, Console.Error);
                b.SetMinimumLevel(minimumLogLevel);
            });

        using var serviceProvider = services.BuildServiceProvider();

        var rootCommand = serviceProvider.GetRequiredService<WinSdkRootCommand>();

        var parseResult = rootCommand.Parse(args);

        CommandExecutedEvent.Log(parseResult.CommandResult.Command.GetType().FullName!);

        return await parseResult.InvokeAsync();
    }

    internal static bool PromptYesNo(string message)
    {
        Console.Write(message);
        var input = Console.ReadLine()?.Trim() ?? "";
        return input.Equals("y", StringComparison.OrdinalIgnoreCase) ||
               input.Equals("yes", StringComparison.OrdinalIgnoreCase);
    }
}
