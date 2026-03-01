using System.Reflection;

namespace Hpoll.Core;

public static class BuildInfo
{
    private static readonly Dictionary<string, string> _metadata;

    static BuildInfo()
    {
        var assembly = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
        _metadata = assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .ToDictionary(a => a.Key, a => a.Value ?? string.Empty);
    }

    public static string Branch => Get("BuildBranch");
    public static string Commit => Get("BuildCommit");
    public static string BuildNumber => Get("BuildNumber");
    public static string RunId => Get("BuildRunId");
    public static string Timestamp => Get("BuildTimestamp");
    public static string PullRequest => Get("PullRequestNumber");

    public static string ShortCommit => Commit.Length >= 7 ? Commit[..7] : Commit;

    public static bool IsCI => !string.IsNullOrEmpty(BuildNumber);

    private static string Get(string key) =>
        _metadata.TryGetValue(key, out var value) ? value : string.Empty;
}
