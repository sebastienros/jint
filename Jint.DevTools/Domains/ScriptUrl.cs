using System.Text;

namespace Jint.DevTools.Domains;

/// <summary>
/// Turns the name a host parsed a program under into the URL the protocol publishes it as.
/// </summary>
/// <remarks>
/// <para>
/// <b>The engine's source names stay exactly what the host passed.</b> They are what a stack trace prints
/// and what <c>Options.Interop.BuildCallStackHandler</c> is handed, so they are the host's to choose; this
/// is the protocol's own vocabulary sitting on top of them. Chrome's navigator groups scripts by the origin
/// of their URL, and a bare filesystem path has none — which is why a script the Jint REPL ran appeared
/// under "(no domain)" with its whole path for a name, where V8 publishes a <c>file://</c> URL and the
/// navigator shows a <c>file://</c> folder and <c>app.js</c> inside it.
/// </para>
/// <para>
/// So a source name that <i>is</i> an absolute filesystem path — a drive letter, a UNC share, or a leading
/// slash — becomes a <c>file://</c> URL, and every other name is published unchanged: <c>&lt;anonymous&gt;</c>,
/// <c>stdin</c>, a module specifier, a name that already carries a scheme. The shapes are recognized without
/// asking the operating system, because a source name reaches an engine from wherever the host got it and a
/// Windows path published from a Linux host is still a Windows path.
/// </para>
/// </remarks>
internal static class ScriptUrl
{
    /// <summary>The URL a source name is published as.</summary>
    /// <param name="sourceName">The name the program was parsed under, which may be <see langword="null"/>.</param>
    internal static string From(string? sourceName)
    {
        if (string.IsNullOrEmpty(sourceName))
        {
            return "";
        }

        var name = sourceName!;
        if (HasScheme(name))
        {
            return name;
        }

        // A UNC share is the one shape whose authority is part of the path: \\server\share\file becomes
        // file://server/share/file, with no third slash.
        if (name.Length > 2 && (name[0] == '\\' || name[0] == '/') && name[0] == name[1] && name[2] != name[0])
        {
            return "file://" + Escape(name.Substring(2).Replace('\\', '/'), skipFirstSegment: true);
        }

        if (IsDriveRooted(name))
        {
            return "file:///" + Escape(name.Replace('\\', '/'), skipFirstSegment: true);
        }

        if (name[0] == '/' || name[0] == '\\')
        {
            return "file://" + Escape(name.Replace('\\', '/'), skipFirstSegment: false);
        }

        return name;
    }

    /// <summary>Whether two names denote the same script, so a client may send either form.</summary>
    /// <remarks>
    /// A client reads a URL out of <c>Debugger.scriptParsed</c> and sends it back on
    /// <c>Debugger.setBreakpointByUrl</c>, so the mapped form is the one that has to match. A host driving
    /// the protocol itself has only ever seen the source name it passed, so that has to match too.
    /// </remarks>
    internal static bool Same(string? sourceName, string url)
        => string.Equals(From(sourceName), url, StringComparison.Ordinal)
            || string.Equals(sourceName ?? "", url, StringComparison.Ordinal);

    /// <summary>
    /// Whether a name starts with a URI scheme, which is at least two characters so that a drive letter is
    /// not mistaken for one.
    /// </summary>
    private static bool HasScheme(string name)
    {
        if (name.Length == 0 || !char.IsAsciiLetter(name[0]))
        {
            return false;
        }

        for (var i = 1; i < name.Length; i++)
        {
            var c = name[i];
            if (c == ':')
            {
                return i > 1;
            }

            if (!char.IsAsciiLetterOrDigit(c) && c != '+' && c != '-' && c != '.')
            {
                return false;
            }
        }

        return false;
    }

    /// <summary>Whether a name is <c>D:\…</c> or <c>D:/…</c>, the shape a Windows absolute path has.</summary>
    private static bool IsDriveRooted(string name)
        => name.Length > 2 && char.IsAsciiLetter(name[0]) && name[1] == ':' && (name[2] == '\\' || name[2] == '/');

    /// <summary>
    /// Percent-encodes each segment of a slash-separated path, leaving the separators alone.
    /// </summary>
    /// <param name="path">The path, already using forward slashes.</param>
    /// <param name="skipFirstSegment">
    /// Whether the first segment is left verbatim, which a drive letter needs — <c>Uri.EscapeDataString</c>
    /// would turn <c>D:</c> into <c>D%3A</c> — and a UNC authority too.
    /// </param>
    private static string Escape(string path, bool skipFirstSegment)
    {
        var builder = new StringBuilder(path.Length + 8);
        var start = 0;
        var segment = 0;

        while (start <= path.Length)
        {
            var end = path.IndexOf('/', start);
            if (end < 0)
            {
                end = path.Length;
            }

            var part = path.Substring(start, end - start);
            builder.Append(segment == 0 && skipFirstSegment ? part : Uri.EscapeDataString(part));

            if (end == path.Length)
            {
                break;
            }

            builder.Append('/');
            start = end + 1;
            segment++;
        }

        return builder.ToString();
    }
}
