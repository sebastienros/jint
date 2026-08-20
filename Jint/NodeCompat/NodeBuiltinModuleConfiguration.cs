namespace Jint.NodeCompat;

/// <summary>
/// The immutable snapshot of a <see cref="NodeBuiltinModuleOptions"/> that the registration keeps: taken once,
/// when <c>UseNodeBuiltinModules</c> returns, and read afterwards by every engine built from those options.
/// </summary>
/// <remarks>
/// It exists so that nothing an engine build reads can be written by the host at the same time — the same
/// reason <see cref="NodeProcessConfiguration"/> exists, and what keeps a shared <see cref="Options"/>
/// instance safe to hand to any number of concurrently constructed engines.
/// </remarks>
internal sealed class NodeBuiltinModuleConfiguration
{
    private NodeBuiltinModuleConfiguration(
        string platform,
        string workingDirectory,
        string posixWorkingDirectory,
        bool allowUnprefixedSpecifiers)
    {
        Platform = platform;
        PlatformIsWindows = NodePlatform.IsWindows(platform);
        WorkingDirectory = workingDirectory;
        PosixWorkingDirectory = posixWorkingDirectory;
        AllowUnprefixedSpecifiers = allowUnprefixedSpecifiers;
    }

    internal string Platform { get; }

    /// <summary>Whether <c>node:path</c> defaults to the Windows flavour.</summary>
    internal bool PlatformIsWindows { get; }

    /// <summary>The working directory as the Windows flavour reads it: verbatim.</summary>
    internal string WorkingDirectory { get; }

    /// <summary>The working directory as the POSIX flavour reads it — Node's <c>posixCwd</c>.</summary>
    internal string PosixWorkingDirectory { get; }

    internal bool AllowUnprefixedSpecifiers { get; }

    internal static NodeBuiltinModuleConfiguration Snapshot(NodeBuiltinModuleOptions options)
    {
        var platform = options.Platform;
        var workingDirectory = options.WorkingDirectory;

        return new NodeBuiltinModuleConfiguration(
            platform,
            workingDirectory,
            ToPosixWorkingDirectory(workingDirectory, NodePlatform.IsWindows(platform)),
            options.AllowUnprefixedSpecifiers);
    }

    /// <summary>
    /// Node's <c>posixCwd</c>: on Windows the working directory is spelled with backslashes and carries a
    /// drive letter, neither of which means anything to <c>path.posix</c>, so the separators are turned around
    /// and everything before the first <c>/</c> is dropped. On every other platform it is already a POSIX path.
    /// </summary>
    private static string ToPosixWorkingDirectory(string workingDirectory, bool platformIsWindows)
    {
        if (!platformIsWindows)
        {
            return workingDirectory;
        }

        var forwardSlashed = workingDirectory.Replace('\\', '/');
        var index = forwardSlashed.IndexOf('/');
        if (index >= 0)
        {
            return forwardSlashed.Substring(index);
        }

        // What JavaScript's slice(-1) answers for a string with no separator at all: its last character, and
        // nothing for an empty string.
        return forwardSlashed.Length == 0 ? string.Empty : forwardSlashed.Substring(forwardSlashed.Length - 1);
    }
}
