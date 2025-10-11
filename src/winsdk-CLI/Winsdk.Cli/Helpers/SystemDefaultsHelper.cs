using System.Text.RegularExpressions;

namespace Winsdk.Cli.Helpers;

internal static class SystemDefaultsHelper
{
    public static string GetDefaultPackageName(string dir)
    {
        var folder = new DirectoryInfo(dir).Name;
        var normalized = Regex.Replace(folder.Trim(), @"\s+", "-").ToLowerInvariant();
        return string.IsNullOrWhiteSpace(normalized) ? "app" : normalized;
    }

    public static string GetDefaultPublisherCN()
    {
        var user = Environment.UserName;
        if (string.IsNullOrWhiteSpace(user)) user = "Developer";
        return $"CN={user}";
    }
}
