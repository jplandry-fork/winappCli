// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Winsdk.Cli.Services;

internal interface IWorkspaceSetupService
{
    public string? FindWindowsAppSdkMsixDirectory(Dictionary<string, string>? usedVersions = null);
    public Task<int> SetupWorkspaceAsync(WorkspaceSetupOptions options, CancellationToken cancellationToken = default);
    public Task InstallWindowsAppRuntimeAsync(string msixDir, CancellationToken cancellationToken);
}
