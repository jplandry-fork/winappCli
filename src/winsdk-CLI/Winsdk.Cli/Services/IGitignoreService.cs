// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Winsdk.Cli.Services;

internal interface IGitignoreService
{
    bool UpdateGitignore(string projectDirectory);
    bool AddCertificateToGitignore(string projectDirectory, string certificateFileName);
}
