#if !NET8_0_OR_GREATER && !NETFRAMEWORK
using System.Runtime.InteropServices;
#endif

namespace Jint.NodeCompat;

/// <summary>
/// The one platform answer the Node compatibility shims share: what <c>process.platform</c> reports, and
/// therefore which flavour <c>node:path</c> defaults to.
/// </summary>
/// <remarks>
/// A script that branches on the platform has to see one answer, not two — <c>process.platform === 'win32'</c>
/// deciding a separator while <c>path.sep</c> answers <c>'/'</c> would be worse than either being wrong on its
/// own. So both read their default from here, and each option group lets the host override it independently
/// for the cases where that is deliberate.
/// </remarks>
internal static class NodePlatform
{
    internal const string Windows = "win32";
    internal const string MacOs = "darwin";
    internal const string Linux = "linux";

    /// <summary>
    /// The running platform as one of Node's platform strings.
    /// </summary>
    /// <remarks>
    /// Not an API gap but a genuine three-way, and the same one <c>DefaultTimeZoneProvider</c> answers:
    /// <see cref="OperatingSystem"/>'s predicates arrived in .NET 5, netstandard has to ask the runtime, and
    /// <c>net462</c> only ever runs on Windows.
    /// </remarks>
    internal static string Default()
    {
#if NET8_0_OR_GREATER
        if (OperatingSystem.IsWindows())
        {
            return Windows;
        }

        return OperatingSystem.IsMacOS() ? MacOs : Linux;
#elif NETFRAMEWORK
        return Windows;
#else
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return Windows;
        }

        return RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? MacOs : Linux;
#endif
    }

    /// <summary>
    /// Whether <paramref name="platform"/> is the one platform whose paths are spelled the Windows way. Every
    /// other value Node can report — <c>darwin</c>, <c>linux</c>, <c>aix</c>, <c>freebsd</c> — is POSIX.
    /// </summary>
    internal static bool IsWindows(string platform)
        => string.Equals(platform, Windows, StringComparison.Ordinal);
}
