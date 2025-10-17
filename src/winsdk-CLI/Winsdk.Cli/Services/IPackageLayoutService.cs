// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Winsdk.Cli.Services;

internal interface IPackageLayoutService
{
    public void CopyIncludesFromPackages(string pkgsDir, string includeOut);
    public void CopyLibsAllArch(string pkgsDir, string libRoot);
    public void CopyRuntimesAllArch(string pkgsDir, string binRoot);
    public IEnumerable<string> FindWinmds(string pkgsDir, Dictionary<string, string> usedVersions);
}
