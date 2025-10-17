// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Winsdk.Cli.Services;

/// <summary>
/// Interface for resolving winsdk directory paths
/// </summary>
internal interface IWinsdkDirectoryService
{
    string GetGlobalWinsdkDirectory();
    string GetLocalWinsdkDirectory(string? baseDirectoryStr = null);
    void SetCacheDirectoryForTesting(string cacheDirectory);
}
