using Winsdk.Cli.Models;

namespace Winsdk.Cli.Services;

internal interface IConfigService
{
    string ConfigPath { get; set; }
    bool Exists();
    WinsdkConfig Load();
    void Save(WinsdkConfig cfg);
}
