using System.Text;

namespace Jint.NodeCompat;

/// <summary>
/// The POSIX flavour of <c>node:path</c> — what <c>path.posix</c> and <c>node:path/posix</c> expose.
/// <para>
/// https://nodejs.org/api/path.html#pathposix
/// </para>
/// </summary>
/// <remarks>
/// The only separator is <c>/</c>; a <c>\</c> is an ordinary character in a file name, which is exactly why a
/// script that has to reason about a POSIX path on a Windows host reaches for this flavour by name rather than
/// for the platform default.
/// </remarks>
internal static class NodePosixPath
{
    internal const char Separator = '/';
    internal const char Delimiter = ':';

    /// <summary>
    /// <c>path.resolve([...paths])</c>: "resolves a sequence of paths or path segments into an absolute path",
    /// processing them right to left until one is absolute, then normalizing and stripping trailing slashes.
    /// <para>
    /// https://nodejs.org/api/path.html#pathresolvepaths
    /// </para>
    /// </summary>
    /// <param name="cwd">
    /// What Node reads from <c>process.cwd()</c>. Supplied by the caller so that nothing here can reach the
    /// host's real directory.
    /// </param>
    /// <param name="paths">The segments, already coerced to strings.</param>
    internal static string Resolve(string cwd, IReadOnlyList<string> paths)
    {
        if (paths.Count == 0 || (paths.Count == 1 && (paths[0].Length == 0 || string.Equals(paths[0], ".", StringComparison.Ordinal))))
        {
            if (cwd.Length > 0 && cwd[0] == Separator)
            {
                return cwd;
            }
        }

        // Right to left, stopping at the first absolute segment, then written back out left to right - the
        // shape Node's own loop produces by prepending "segment + '/'" to what it has so far.
        var segments = new List<string>(paths.Count + 1);
        var resolvedAbsolute = false;

        for (var i = paths.Count - 1; i >= 0 && !resolvedAbsolute; i--)
        {
            var path = paths[i];
            if (path.Length == 0)
            {
                continue;
            }

            segments.Add(path);
            resolvedAbsolute = path[0] == Separator;
        }

        if (!resolvedAbsolute)
        {
            segments.Add(cwd);
            resolvedAbsolute = cwd.Length > 0 && cwd[0] == Separator;
        }

        var resolvedPath = new ValueStringBuilder(stackalloc char[128]);
        try
        {
            for (var i = segments.Count - 1; i >= 0; i--)
            {
                resolvedPath.Append(segments[i]);
                resolvedPath.Append(Separator);
            }

            // At this point the path should be resolved to a full absolute path, but a relative one is still
            // handled: a host may have configured a working directory that is not itself absolute.
            var normalized = NodePathAlgorithms.NormalizeString(resolvedPath.AsSpan().ToString(), !resolvedAbsolute, Separator, windowsSeparators: false);

            if (resolvedAbsolute)
            {
                return "/" + normalized;
            }

            return normalized.Length > 0 ? normalized : ".";
        }
        finally
        {
            resolvedPath.Dispose();
        }
    }

    /// <summary>
    /// <c>path.normalize(path)</c>: "resolving <c>'..'</c> and <c>'.'</c> segments", collapsing runs of
    /// separators, and preserving a trailing separator. A zero-length path answers <c>'.'</c>.
    /// <para>
    /// https://nodejs.org/api/path.html#pathnormalizepath
    /// </para>
    /// </summary>
    internal static string Normalize(string path)
    {
        if (path.Length == 0)
        {
            return ".";
        }

        var isAbsolute = path[0] == Separator;
        var trailingSeparator = path[path.Length - 1] == Separator;

        var normalized = NodePathAlgorithms.NormalizeString(path, !isAbsolute, Separator, windowsSeparators: false);

        if (normalized.Length == 0)
        {
            if (isAbsolute)
            {
                return "/";
            }

            return trailingSeparator ? "./" : ".";
        }

        if (trailingSeparator)
        {
            normalized += "/";
        }

        return isAbsolute ? "/" + normalized : normalized;
    }

    /// <summary>
    /// <c>path.isAbsolute(path)</c>. "If the given path is a zero-length string, <c>false</c> will be
    /// returned."
    /// <para>
    /// https://nodejs.org/api/path.html#pathisabsolutepath
    /// </para>
    /// </summary>
    internal static bool IsAbsolute(string path) => path.Length > 0 && path[0] == Separator;

    /// <summary>
    /// <c>path.join([...paths])</c>: joins with the separator and normalizes. "Zero-length path segments are
    /// ignored. If the joined path string is a zero-length string then <c>'.'</c> will be returned."
    /// <para>
    /// https://nodejs.org/api/path.html#pathjoinpaths
    /// </para>
    /// </summary>
    internal static string Join(IReadOnlyList<string> paths)
    {
        if (paths.Count == 0)
        {
            return ".";
        }

        var joined = new ValueStringBuilder(stackalloc char[128]);
        try
        {
            for (var i = 0; i < paths.Count; ++i)
            {
                var segment = paths[i];
                if (segment.Length == 0)
                {
                    continue;
                }

                if (joined.Length > 0)
                {
                    joined.Append(Separator);
                }

                joined.Append(segment);
            }

            if (joined.Length == 0)
            {
                return ".";
            }

            return Normalize(joined.AsSpan().ToString());
        }
        finally
        {
            joined.Dispose();
        }
    }

    /// <summary>
    /// <c>path.relative(from, to)</c>: "the relative path from <c>from</c> to <c>to</c>". Both are resolved
    /// first, so a zero-length argument means the working directory, and two paths resolving to the same place
    /// answer with a zero-length string.
    /// <para>
    /// https://nodejs.org/api/path.html#pathrelativefrom-to
    /// </para>
    /// </summary>
    internal static string Relative(string cwd, string from, string to)
    {
        if (string.Equals(from, to, StringComparison.Ordinal))
        {
            return string.Empty;
        }

        from = Resolve(cwd, [from]);
        to = Resolve(cwd, [to]);

        if (string.Equals(from, to, StringComparison.Ordinal))
        {
            return string.Empty;
        }

        const int FromStart = 1;
        var fromEnd = from.Length;
        var fromLen = fromEnd - FromStart;
        const int ToStart = 1;
        var toLen = to.Length - ToStart;

        // The longest common path from the root.
        var length = fromLen < toLen ? fromLen : toLen;
        var lastCommonSep = -1;
        var i = 0;
        for (; i < length; i++)
        {
            var fromCode = from[FromStart + i];
            if (fromCode != to[ToStart + i])
            {
                break;
            }

            if (fromCode == Separator)
            {
                lastCommonSep = i;
            }
        }

        if (i == length)
        {
            if (toLen > length)
            {
                if (to[ToStart + i] == Separator)
                {
                    // `from` is the exact base path of `to`: from='/foo/bar', to='/foo/bar/baz'.
                    return to.Substring(ToStart + i + 1);
                }

                if (i == 0)
                {
                    // `from` is the root: from='/', to='/foo'.
                    return to.Substring(ToStart + i);
                }
            }
            else if (fromLen > length)
            {
                if (from[FromStart + i] == Separator)
                {
                    // `to` is the exact base path of `from`: from='/foo/bar/baz', to='/foo/bar'.
                    lastCommonSep = i;
                }
                else if (i == 0)
                {
                    // `to` is the root: from='/foo/bar', to='/'.
                    lastCommonSep = 0;
                }
            }
        }

        var result = new ValueStringBuilder(stackalloc char[64]);
        try
        {
            for (i = FromStart + lastCommonSep + 1; i <= fromEnd; ++i)
            {
                if (i == fromEnd || from[i] == Separator)
                {
                    result.Append(result.Length == 0 ? ".." : "/..");
                }
            }

            result.Append(to.AsSpan(ToStart + lastCommonSep));
            return result.AsSpan().ToString();
        }
        finally
        {
            result.Dispose();
        }
    }

    /// <summary>
    /// <c>path.toNamespacedPath(path)</c>. "On POSIX systems, this function is non-operational and always
    /// returns <c>path</c> without modifications."
    /// <para>
    /// https://nodejs.org/api/path.html#pathtonamespacedpathpath
    /// </para>
    /// </summary>
    internal static string ToNamespacedPath(string path) => path;

    /// <summary>
    /// <c>path.dirname(path)</c>: "the directory name of a <c>path</c>, similar to the Unix <c>dirname</c>
    /// command. Trailing directory separators are ignored."
    /// <para>
    /// https://nodejs.org/api/path.html#pathdirnamepath
    /// </para>
    /// </summary>
    internal static string Dirname(string path)
    {
        if (path.Length == 0)
        {
            return ".";
        }

        var hasRoot = path[0] == Separator;
        var end = -1;
        var matchedSlash = true;
        for (var i = path.Length - 1; i >= 1; --i)
        {
            if (path[i] == Separator)
            {
                if (!matchedSlash)
                {
                    end = i;
                    break;
                }
            }
            else
            {
                matchedSlash = false;
            }
        }

        if (end == -1)
        {
            return hasRoot ? "/" : ".";
        }

        if (hasRoot && end == 1)
        {
            return "//";
        }

        return path.Substring(0, end);
    }

    /// <summary>
    /// <c>path.basename(path[, suffix])</c>: "the last portion of a <c>path</c>, similar to the Unix
    /// <c>basename</c> command. Trailing directory separators are ignored."
    /// <para>
    /// https://nodejs.org/api/path.html#pathbasenamepath-suffix
    /// </para>
    /// </summary>
    /// <param name="path">The path to take the last portion of.</param>
    /// <param name="suffix">"An optional suffix to remove", or null when the argument was absent.</param>
    internal static string Basename(string path, string? suffix)
    {
        var start = 0;
        var end = -1;
        var matchedSlash = true;

        if (suffix is not null && suffix.Length > 0 && suffix.Length <= path.Length)
        {
            if (string.Equals(suffix, path, StringComparison.Ordinal))
            {
                return string.Empty;
            }

            var extIdx = suffix.Length - 1;
            var firstNonSlashEnd = -1;
            for (var i = path.Length - 1; i >= 0; --i)
            {
                var code = path[i];
                if (code == Separator)
                {
                    if (!matchedSlash)
                    {
                        start = i + 1;
                        break;
                    }
                }
                else
                {
                    if (firstNonSlashEnd == -1)
                    {
                        matchedSlash = false;
                        firstNonSlashEnd = i + 1;
                    }

                    if (extIdx >= 0)
                    {
                        if (code == suffix[extIdx])
                        {
                            if (--extIdx == -1)
                            {
                                end = i;
                            }
                        }
                        else
                        {
                            // The suffix does not match, so the result is the whole path component.
                            extIdx = -1;
                            end = firstNonSlashEnd;
                        }
                    }
                }
            }

            if (start == end)
            {
                end = firstNonSlashEnd;
            }
            else if (end == -1)
            {
                end = path.Length;
            }

            return NodePathAlgorithms.Slice(path, start, end);
        }

        for (var i = path.Length - 1; i >= 0; --i)
        {
            if (path[i] == Separator)
            {
                if (!matchedSlash)
                {
                    start = i + 1;
                    break;
                }
            }
            else if (end == -1)
            {
                matchedSlash = false;
                end = i + 1;
            }
        }

        if (end == -1)
        {
            return string.Empty;
        }

        return path.Substring(start, end - start);
    }

    /// <summary>
    /// <c>path.extname(path)</c>: "the extension of the <c>path</c>, from the last occurrence of the <c>.</c>
    /// (period) character to end of string in the last portion of the <c>path</c>. If there is no <c>.</c> in
    /// the last portion of the <c>path</c>, or if there are no <c>.</c> characters other than the first
    /// character of the basename of <c>path</c>, an empty string is returned."
    /// <para>
    /// https://nodejs.org/api/path.html#pathextnamepath
    /// </para>
    /// </summary>
    internal static string Extname(string path)
    {
        var startDot = -1;
        var startPart = 0;
        var end = -1;
        var matchedSlash = true;

        // The state of the characters seen before the first dot and after any separator: 0 while nothing but
        // dots has been seen, 1 once a second dot follows, -1 once an ordinary character precedes the dot.
        var preDotState = 0;

        for (var i = path.Length - 1; i >= 0; --i)
        {
            var code = path[i];
            if (code == Separator)
            {
                if (!matchedSlash)
                {
                    startPart = i + 1;
                    break;
                }

                continue;
            }

            if (end == -1)
            {
                matchedSlash = false;
                end = i + 1;
            }

            if (code == '.')
            {
                if (startDot == -1)
                {
                    startDot = i;
                }
                else if (preDotState != 1)
                {
                    preDotState = 1;
                }
            }
            else if (startDot != -1)
            {
                preDotState = -1;
            }
        }

        // The last two disjuncts are the ones the prose describes: a non-dot character immediately before
        // the dot, and a right-most trimmed component that is exactly "..".
        if (startDot == -1
            || end == -1
            || preDotState == 0
            || (preDotState == 1 && startDot == end - 1 && startDot == startPart + 1))
        {
            return string.Empty;
        }

        return path.Substring(startDot, end - startDot);
    }

    /// <summary>
    /// <c>path.parse(path)</c>: "an object whose properties represent significant elements of the <c>path</c>.
    /// Trailing directory separators are ignored."
    /// <para>
    /// https://nodejs.org/api/path.html#pathparsepath
    /// </para>
    /// </summary>
    internal static ParsedPath Parse(string path)
    {
        if (path.Length == 0)
        {
            return ParsedPath.Empty;
        }

        var isAbsolute = path[0] == Separator;
        var root = isAbsolute ? "/" : string.Empty;
        var scanStart = isAbsolute ? 1 : 0;

        var startDot = -1;
        var startPart = 0;
        var end = -1;
        var matchedSlash = true;
        var preDotState = 0;

        for (var i = path.Length - 1; i >= scanStart; --i)
        {
            var code = path[i];
            if (code == Separator)
            {
                if (!matchedSlash)
                {
                    startPart = i + 1;
                    break;
                }

                continue;
            }

            if (end == -1)
            {
                matchedSlash = false;
                end = i + 1;
            }

            if (code == '.')
            {
                if (startDot == -1)
                {
                    startDot = i;
                }
                else if (preDotState != 1)
                {
                    preDotState = 1;
                }
            }
            else if (startDot != -1)
            {
                preDotState = -1;
            }
        }

        var name = string.Empty;
        var baseName = string.Empty;
        var ext = string.Empty;

        if (end != -1)
        {
            var start = startPart == 0 && isAbsolute ? 1 : startPart;
            if (startDot == -1
                || preDotState == 0
                || (preDotState == 1 && startDot == end - 1 && startDot == startPart + 1))
            {
                baseName = name = path.Substring(start, end - start);
            }
            else
            {
                name = path.Substring(start, startDot - start);
                baseName = path.Substring(start, end - start);
                ext = path.Substring(startDot, end - startDot);
            }
        }

        string dir;
        if (startPart > 0)
        {
            dir = path.Substring(0, startPart - 1);
        }
        else if (isAbsolute)
        {
            dir = "/";
        }
        else
        {
            dir = string.Empty;
        }

        return new ParsedPath(root, dir, baseName, ext, name);
    }
}
