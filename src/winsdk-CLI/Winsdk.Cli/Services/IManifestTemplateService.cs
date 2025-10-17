// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Winsdk.Cli.Services;

internal interface IManifestTemplateService
{
    Task GenerateCompleteManifestAsync(
        string outputDirectory,
        string packageName,
        string publisherName,
        string version,
        string executable,
        bool sparse,
        string description,
        CancellationToken cancellationToken = default);
}
