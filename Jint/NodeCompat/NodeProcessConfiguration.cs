namespace Jint.NodeCompat;

/// <summary>
/// The immutable snapshot of a <see cref="NodeProcessOptions"/> that the registration keeps: taken once, when
/// <c>UseNodeProcess</c> returns, and read afterwards by every engine built from those options.
/// </summary>
/// <remarks>
/// It exists so that nothing an engine build reads can be written by the host at the same time. The
/// allowlist and the overrides are collections the host owns and may keep mutating; copying them here means
/// a shared <see cref="Options"/> instance stays what every other option group promises to be — safe to hand
/// to any number of concurrently constructed engines.
/// </remarks>
internal sealed class NodeProcessConfiguration
{
    /// <summary>
    /// The override values, keyed as the host keyed them. Null when the host supplied none, which is the
    /// common case and saves the lookup entirely. Values are nullable on purpose: a null override means "this
    /// variable is absent", not "fall through to the real environment".
    /// </summary>
    private readonly Dictionary<string, string?>? _environmentOverrides;

    private NodeProcessConfiguration(
        string[] environmentVariableAllowlist,
        Dictionary<string, string?>? environmentOverrides,
        string platform,
        string version,
        string workingDirectory)
    {
        EnvironmentVariableAllowlist = environmentVariableAllowlist;
        _environmentOverrides = environmentOverrides;
        Platform = platform;
        Version = version;
        WorkingDirectory = workingDirectory;
    }

    /// <summary>The allowed names, in the order the host listed them, without duplicates.</summary>
    internal string[] EnvironmentVariableAllowlist { get; }

    internal string Platform { get; }

    internal string Version { get; }

    internal string WorkingDirectory { get; }

    internal static NodeProcessConfiguration Snapshot(NodeProcessOptions options)
    {
        return new NodeProcessConfiguration(
            CopyAllowlist(options.EnvironmentVariableAllowlist),
            CopyOverrides(options.EnvironmentOverrides),
            options.Platform,
            options.Version,
            options.WorkingDirectory);
    }

    /// <summary>
    /// Resolves one allowed name: an override entry wins outright, and only a name with no override at all is
    /// asked of the real environment. <see langword="null"/> means the variable is absent.
    /// </summary>
    internal string? ResolveEnvironmentVariable(string name)
    {
        if (_environmentOverrides is not null && _environmentOverrides.TryGetValue(name, out var overridden))
        {
            return overridden;
        }

        return Environment.GetEnvironmentVariable(name);
    }

    private static string[] CopyAllowlist(IReadOnlyCollection<string> allowlist)
    {
        if (allowlist.Count == 0)
        {
            return [];
        }

        var names = new List<string>(allowlist.Count);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var name in allowlist)
        {
            // A null entry cannot name a variable, and Environment.GetEnvironmentVariable would throw on it.
            if (name is not null && seen.Add(name))
            {
                names.Add(name);
            }
        }

        return names.ToArray();
    }

    private static Dictionary<string, string?>? CopyOverrides(IDictionary<string, string>? overrides)
    {
        if (overrides is null || overrides.Count == 0)
        {
            return null;
        }

        // Keeping the source's comparer matters: a host that built a case-insensitive dictionary to mirror
        // the way Windows treats environment variables would otherwise silently get ordinal lookups here.
        var comparer = (overrides as Dictionary<string, string>)?.Comparer ?? StringComparer.Ordinal;
        var copy = new Dictionary<string, string?>(overrides.Count, comparer);
        foreach (var pair in overrides)
        {
            if (pair.Key is not null)
            {
                copy[pair.Key] = pair.Value;
            }
        }

        return copy;
    }
}
