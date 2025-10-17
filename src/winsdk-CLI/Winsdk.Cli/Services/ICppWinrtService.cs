// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Winsdk.Cli.Services;

internal interface ICppWinrtService
{
    public string? FindCppWinrtExe(string packagesDir, IDictionary<string, string> usedVersions);
    public Task RunWithRspAsync(string cppwinrtExe, IEnumerable<string> winmdInputs, string outputDir, string workingDirectory, CancellationToken cancellationToken = default);
}
