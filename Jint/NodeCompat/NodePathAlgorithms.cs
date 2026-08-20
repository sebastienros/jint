using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace Jint.NodeCompat;

/// <summary>
/// What <c>path.parse()</c> answers with, and what <c>path.format()</c> consumes.
/// <para>
/// https://nodejs.org/api/path.html#pathparsepath
/// </para>
/// </summary>
[StructLayout(LayoutKind.Auto)]
internal readonly record struct ParsedPath(string Root, string Dir, string Base, string Ext, string Name)
{
    /// <summary>The all-empty result <c>path.parse('')</c> answers with.</summary>
    internal static readonly ParsedPath Empty = new("", "", "", "", "");
}

/// <summary>
/// The parts of Node's <c>node:path</c> that the POSIX and Windows flavours share: <c>normalizeString</c>,
/// which resolves <c>.</c> and <c>..</c> segments, and <c>_format</c>.
/// <para>
/// https://nodejs.org/api/path.html
/// </para>
/// </summary>
/// <remarks>
/// <para>
/// Everything here is string arithmetic over the strings the caller supplies. Nothing reads the file system,
/// and nothing consults the real working directory — <c>resolve</c> and <c>relative</c> take one as an
/// argument, which is what keeps the whole module answerable without the host process leaking into it. It is
/// also why this compiles for every target framework Jint has: it depends on nothing but <see cref="string"/>.
/// </para>
/// <para>
/// The algorithms follow Node's own implementation rather than only its prose, because the prose does not
/// describe the corners — what a trailing separator does to <c>normalize</c> but not to <c>resolve</c>, when a
/// <c>..</c> stops popping, how a drive-relative <c>C:foo</c> differs from an absolute <c>C:\foo</c>. The
/// documentation is the contract; the implementation is what settles the cases the contract leaves unsaid.
/// </para>
/// </remarks>
internal static class NodePathAlgorithms
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool IsPosixSeparator(char c) => c == '/';

    /// <summary>
    /// On Windows "both forward slash (<c>/</c>) and backward slash (<c>\</c>) are accepted as path segment
    /// separators; however, the <c>path</c> methods only add backward slashes".
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool IsWindowsSeparator(char c) => c == '/' || c == '\\';

    /// <summary>Whether <paramref name="c"/> can be the letter of a Windows device root, as in <c>C:</c>.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool IsWindowsDeviceRoot(char c) => (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z');

    /// <summary>
    /// Node's <c>normalizeString</c>: resolves <c>.</c> and <c>..</c> segments and collapses runs of
    /// separators, writing <paramref name="separator"/> between the segments it keeps.
    /// </summary>
    /// <param name="path">The path to normalize, with its root (if any) already stripped off.</param>
    /// <param name="allowAboveRoot">
    /// Whether a <c>..</c> that has nothing left to pop may be kept. True for a relative path, where
    /// <c>../a</c> still names something, and false for an absolute one, where the root is where popping stops.
    /// </param>
    /// <param name="separator">The separator to write, <c>/</c> or <c>\</c>.</param>
    /// <param name="windowsSeparators">Whether <c>\</c> counts as a separator on input as well as <c>/</c>.</param>
    internal static string NormalizeString(string path, bool allowAboveRoot, char separator, bool windowsSeparators)
    {
        var res = new ValueStringBuilder(stackalloc char[128]);
        try
        {
            var lastSegmentLength = 0;
            var lastSlash = -1;
            var dots = 0;
            var code = '\0';

            for (var i = 0; i <= path.Length; ++i)
            {
                if (i < path.Length)
                {
                    code = path[i];
                }
                else if (IsSeparator(code, windowsSeparators))
                {
                    // The trailing separator was already accounted for by the iteration that saw it.
                    break;
                }
                else
                {
                    // One virtual separator past the end, so the final segment is flushed by the same branch
                    // every other segment goes through.
                    code = '/';
                }

                if (IsSeparator(code, windowsSeparators))
                {
                    if (lastSlash == i - 1 || dots == 1)
                    {
                        // An empty segment, or a "." segment: nothing to write.
                    }
                    else if (dots == 2)
                    {
                        if (res.Length < 2
                            || lastSegmentLength != 2
                            || res[res.Length - 1] != '.'
                            || res[res.Length - 2] != '.')
                        {
                            if (res.Length > 2)
                            {
                                var lastSlashIndex = res.Length - lastSegmentLength - 1;
                                if (lastSlashIndex == -1)
                                {
                                    res.Length = 0;
                                    lastSegmentLength = 0;
                                }
                                else
                                {
                                    res.Length = lastSlashIndex;
                                    lastSegmentLength = res.Length - 1 - res.AsSpan().LastIndexOf(separator);
                                }

                                lastSlash = i;
                                dots = 0;
                                continue;
                            }

                            if (res.Length != 0)
                            {
                                res.Length = 0;
                                lastSegmentLength = 0;
                                lastSlash = i;
                                dots = 0;
                                continue;
                            }
                        }

                        if (allowAboveRoot)
                        {
                            if (res.Length > 0)
                            {
                                res.Append(separator);
                            }

                            res.Append("..");
                            lastSegmentLength = 2;
                        }
                    }
                    else
                    {
                        if (res.Length > 0)
                        {
                            res.Append(separator);
                        }

                        res.Append(path.AsSpan(lastSlash + 1, i - lastSlash - 1));
                        lastSegmentLength = i - lastSlash - 1;
                    }

                    lastSlash = i;
                    dots = 0;
                }
                else if (code == '.' && dots != -1)
                {
                    ++dots;
                }
                else
                {
                    dots = -1;
                }
            }

            return res.AsSpan().ToString();
        }
        finally
        {
            res.Dispose();
        }
    }

    /// <summary>
    /// Node's <c>_format</c>, the body behind both <c>path.posix.format</c> and <c>path.win32.format</c>:
    /// "<c>pathObject.root</c> is ignored if <c>pathObject.dir</c> is provided", "<c>pathObject.ext</c> and
    /// <c>pathObject.name</c> are ignored if <c>pathObject.base</c> exists", and "the dot will be added if it
    /// is not specified in <c>ext</c>".
    /// </summary>
    /// <remarks>
    /// The three "provided" tests are JavaScript truthiness over the already-coerced strings, so an empty
    /// string counts as absent — which is why <c>{ root: '/', base: 'file.txt' }</c> formats as
    /// <c>/file.txt</c> rather than <c>//file.txt</c>: <c>dir</c> is empty, so <c>root</c> stands in for it,
    /// and a <c>dir</c> equal to <c>root</c> is joined without a separator.
    /// </remarks>
    internal static string Format(char separator, string dir, string root, string @base, string name, string ext)
    {
        var directory = dir.Length != 0 ? dir : root;
        var fileName = @base.Length != 0 ? @base : name + FormatExtension(ext);

        if (directory.Length == 0)
        {
            return fileName;
        }

        return string.Equals(directory, root, StringComparison.Ordinal)
            ? directory + fileName
            : directory + separator + fileName;
    }

    /// <summary>
    /// <c>String.prototype.slice</c> for the one place the path algorithms need its exact semantics: the
    /// <c>basename</c> suffix branch, whose <c>end</c> can still be <c>-1</c> or below <c>start</c> when the
    /// path is nothing but separators. JavaScript answers with an empty string there; <c>Substring</c> would
    /// throw, and clamping the wrong way would answer with the separators themselves.
    /// </summary>
    internal static string Slice(string value, int start, int end)
    {
        var length = value.Length;

        if (start < 0)
        {
            start = Math.Max(length + start, 0);
        }
        else if (start > length)
        {
            start = length;
        }

        if (end < 0)
        {
            end = Math.Max(length + end, 0);
        }
        else if (end > length)
        {
            end = length;
        }

        return end <= start ? string.Empty : value.Substring(start, end - start);
    }

    private static string FormatExtension(string ext)
    {
        if (ext.Length == 0)
        {
            return string.Empty;
        }

        return ext[0] == '.' ? ext : "." + ext;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsSeparator(char c, bool windowsSeparators)
        => windowsSeparators ? IsWindowsSeparator(c) : IsPosixSeparator(c);
}
