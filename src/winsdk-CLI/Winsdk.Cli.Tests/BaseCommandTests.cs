// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Winsdk.Cli.Helpers;

namespace Winsdk.Cli.Tests;

public class BaseCommandTests : IDisposable
{
    private ServiceProvider _serviceProvider;
    protected StringWriter ConsoleStdOut { get; } = new StringWriter();
    protected StringWriter ConsoleStdErr { get; } = new StringWriter();

    public BaseCommandTests()
    {
        var services = new ServiceCollection()
            .ConfigureServices()
            .ConfigureCommands()
            .AddLogging(b =>
            {
                b.ClearProviders();
                b.AddTextWriterLogger(ConsoleStdOut, ConsoleStdErr);
                b.SetMinimumLevel(LogLevel.Debug);
            });

        _serviceProvider = services.BuildServiceProvider();
    }

    public void Dispose()
    {
        _serviceProvider?.Dispose();
        ConsoleStdOut?.Dispose();
        ConsoleStdErr?.Dispose();
        GC.SuppressFinalize(this);
    }

    protected T GetRequiredService<T>() where T : notnull
    {
        return _serviceProvider.GetRequiredService<T>();
    }
}
