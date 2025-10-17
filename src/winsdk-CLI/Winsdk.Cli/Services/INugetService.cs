// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Winsdk.Cli.Services;

internal interface INugetService
{
    Task EnsureNugetExeAsync(string winsdkDir, CancellationToken cancellationToken = default);
    Task<string> GetLatestVersionAsync(string packageName, bool includePrerelease, CancellationToken cancellationToken = default);
    Task<Dictionary<string, string>> InstallPackageAsync(string winsdkDir, string package, string version, string outputDir, CancellationToken cancellationToken = default);
}