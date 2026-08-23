using System.Text;

namespace Jint.NodeCompat;

/// <summary>
/// The Windows flavour of <c>node:path</c> — what <c>path.win32</c> and <c>node:path/win32</c> expose.
/// <para>
/// https://nodejs.org/api/path.html#pathwin32
/// </para>
/// </summary>
/// <remarks>
/// <para>
/// Three things separate it from <see cref="NodePosixPath"/>, and every documented Windows oddity follows from
/// one of them: both <c>/</c> and <c>\</c> are accepted as separators while only <c>\</c> is ever written; a
/// path can carry a <em>device</em> — a drive letter, a UNC share, or a <c>\\?\</c> namespace prefix — that
/// sits above the part <c>..</c> is allowed to pop; and a drive letter without a separator after it
/// (<c>C:foo</c>) is <em>relative to that drive</em> rather than absolute.
/// </para>
/// <para>
/// The reserved device names (<c>CON</c>, <c>NUL</c>, <c>COM1</c> …) are honoured because Windows resolves
/// them from any directory: a path carrying one is deliberately not normalized into something the operating
/// system would reopen as a device. That, and the <c>.\</c> prefixing of a drive-relative result, are Node's
/// own mitigations rather than embellishments here.
/// </para>
/// </remarks>
internal static class NodeWin32Path
{
    internal const char Separator = '\\';
    internal const char Delimiter = ';';

    /// <summary>
    /// The DOS device names Windows resolves from every directory. The superscript spellings are the ones
    /// Node carries too: <c>COM¹</c>, <c>COM²</c>, <c>COM³</c> and their <c>LPT</c> counterparts map to the
    /// same devices.
    /// </summary>
    private static readonly string[] ReservedNames =
    [
        "CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9",
        "COM\u00b9", "COM\u00b2", "COM\u00b3",
        "LPT\u00b9", "LPT\u00b2", "LPT\u00b3",
    ];

    /// <summary>
    /// Whether everything before <paramref name="colonIndex"/> names a reserved device. A
    /// <paramref name="colonIndex"/> of <c>-1</c> is deliberately not rejected: the callers that pass one
    /// through are asking about a path with no colon at all, and JavaScript's <c>slice(0, -1)</c> then tests
    /// the path without its last character, which is how <c>CON\</c> is recognized.
    /// </summary>
    private static bool IsReservedName(string path, int colonIndex)
    {
        var devicePart = NodePathAlgorithms.Slice(path, 0, colonIndex).ToUpperInvariant();
        for (var i = 0; i < ReservedNames.Length; i++)
        {
            if (string.Equals(ReservedNames[i], devicePart, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// <c>path.resolve([...paths])</c> for Windows paths.
    /// <para>
    /// https://nodejs.org/api/path.html#pathresolvepaths
    /// </para>
    /// </summary>
    /// <remarks>
    /// Node consults <c>process.env['=C:']</c> for the per-drive working directory Windows keeps, which is why
    /// "<c>path.resolve('C:\\')</c> can potentially return a different result than <c>path.resolve('C:')</c>".
    /// Jint has no such environment to read — <c>process.env</c> is an allowlist and this module never reaches
    /// it — so a drive-relative segment falls straight through to Node's own fallback: the configured working
    /// directory when it names that drive, and the drive's root otherwise.
    /// </remarks>
    /// <param name="cwd">What Node reads from <c>process.cwd()</c>.</param>
    /// <param name="platformIsWindows">
    /// Whether the configured platform is <c>win32</c>. It decides one thing only: whether the working
    /// directory is already spelled with backslashes, or is a POSIX path whose separators have to be turned
    /// around first — exactly the <c>isWindows</c> test Node makes at the same point.
    /// </param>
    /// <param name="paths">The segments, already coerced to strings.</param>
    internal static string Resolve(string cwd, bool platformIsWindows, IReadOnlyList<string> paths)
    {
        var resolvedDevice = string.Empty;
        var resolvedTail = new ValueStringBuilder(stackalloc char[128]);

        try
        {
            var resolvedAbsolute = false;

            for (var i = paths.Count - 1; i >= -1; i--)
            {
                string path;
                if (i >= 0)
                {
                    path = paths[i];
                    if (path.Length == 0)
                    {
                        continue;
                    }
                }
                else if (resolvedDevice.Length == 0)
                {
                    path = cwd;
                    if (paths.Count == 0
                        || (paths.Count == 1
                            && (paths[0].Length == 0 || string.Equals(paths[0], ".", StringComparison.Ordinal))
                            && path.Length > 0
                            && NodePathAlgorithms.IsWindowsSeparator(path[0])))
                    {
                        return platformIsWindows ? path : path.Replace('/', Separator);
                    }
                }
                else
                {
                    // A drive was resolved but no absolute path yet. Node asks for that drive's own working
                    // directory and falls back to process.cwd(); Jint has only the configured one.
                    path = cwd;
                    if (!string.Equals(NodePathAlgorithms.Slice(path, 0, 2), resolvedDevice, StringComparison.OrdinalIgnoreCase)
                        && path.Length > 2
                        && path[2] == Separator)
                    {
                        path = resolvedDevice + Separator;
                    }
                }

                var len = path.Length;
                var rootEnd = 0;
                var device = string.Empty;
                var isAbsolute = false;
                var code = len > 0 ? path[0] : '\0';

                if (len == 1)
                {
                    if (NodePathAlgorithms.IsWindowsSeparator(code))
                    {
                        rootEnd = 1;
                        isAbsolute = true;
                    }
                }
                else if (NodePathAlgorithms.IsWindowsSeparator(code))
                {
                    // A leading separator means the path is absolute one way or another, UNC or not.
                    isAbsolute = true;

                    if (NodePathAlgorithms.IsWindowsSeparator(path[1]))
                    {
                        var j = 2;
                        var last = j;
                        while (j < len && !NodePathAlgorithms.IsWindowsSeparator(path[j]))
                        {
                            j++;
                        }

                        if (j < len && j != last)
                        {
                            var firstPart = path.Substring(last, j - last);
                            last = j;
                            while (j < len && NodePathAlgorithms.IsWindowsSeparator(path[j]))
                            {
                                j++;
                            }

                            if (j < len && j != last)
                            {
                                last = j;
                                while (j < len && !NodePathAlgorithms.IsWindowsSeparator(path[j]))
                                {
                                    j++;
                                }

                                if (j == len || j != last)
                                {
                                    if (!string.Equals(firstPart, ".", StringComparison.Ordinal)
                                        && !string.Equals(firstPart, "?", StringComparison.Ordinal))
                                    {
                                        // A UNC root: \\server\share.
                                        device = @"\\" + firstPart + Separator + path.Substring(last, j - last);
                                        rootEnd = j;
                                    }
                                    else
                                    {
                                        // A device root, as in \\.\PHYSICALDRIVE0.
                                        device = @"\\" + firstPart;
                                        rootEnd = 4;
                                    }
                                }
                            }
                        }
                    }
                    else
                    {
                        rootEnd = 1;
                    }
                }
                else if (NodePathAlgorithms.IsWindowsDeviceRoot(code) && path[1] == ':')
                {
                    device = path.Substring(0, 2);
                    rootEnd = 2;
                    if (len > 2 && NodePathAlgorithms.IsWindowsSeparator(path[2]))
                    {
                        // A separator after the drive name is what makes it absolute rather than
                        // relative to that drive's own current directory.
                        isAbsolute = true;
                        rootEnd = 3;
                    }
                }

                if (device.Length > 0)
                {
                    if (resolvedDevice.Length > 0)
                    {
                        if (!string.Equals(device, resolvedDevice, StringComparison.OrdinalIgnoreCase))
                        {
                            // Another device entirely, so this segment cannot contribute.
                            continue;
                        }
                    }
                    else
                    {
                        resolvedDevice = device;
                    }
                }

                if (resolvedAbsolute)
                {
                    if (resolvedDevice.Length > 0)
                    {
                        break;
                    }
                }
                else
                {
                    resolvedTail.Insert(0, Separator, 1);
                    resolvedTail.Insert(0, path.Substring(rootEnd));
                    resolvedAbsolute = isAbsolute;
                    if (isAbsolute && resolvedDevice.Length > 0)
                    {
                        break;
                    }
                }
            }

            var normalized = NodePathAlgorithms.NormalizeString(
                resolvedTail.AsSpan().ToString(),
                !resolvedAbsolute,
                Separator,
                windowsSeparators: true);

            if (resolvedAbsolute)
            {
                return resolvedDevice + Separator + normalized;
            }

            var relative = resolvedDevice + normalized;
            return relative.Length > 0 ? relative : ".";
        }
        finally
        {
            resolvedTail.Dispose();
        }
    }

    /// <summary>
    /// <c>path.normalize(path)</c> for Windows paths. "Multiple, sequential path segment separation characters
    /// are replaced by a single instance of the platform-specific path segment separator", <c>..</c> and
    /// <c>.</c> are resolved, and a trailing separator is preserved.
    /// <para>
    /// https://nodejs.org/api/path.html#pathnormalizepath
    /// </para>
    /// </summary>
    internal static string Normalize(string path)
    {
        var len = path.Length;
        if (len == 0)
        {
            return ".";
        }

        var rootEnd = 0;
        string? device = null;
        var isAbsolute = false;
        var code = path[0];

        if (len == 1)
        {
            // A lone forward slash becomes the platform separator; anything else is already normal.
            return NodePathAlgorithms.IsPosixSeparator(code) ? "\\" : path;
        }

        if (NodePathAlgorithms.IsWindowsSeparator(code))
        {
            isAbsolute = true;

            if (NodePathAlgorithms.IsWindowsSeparator(path[1]))
            {
                var j = 2;
                var last = j;
                while (j < len && !NodePathAlgorithms.IsWindowsSeparator(path[j]))
                {
                    j++;
                }

                if (j < len && j != last)
                {
                    var firstPart = path.Substring(last, j - last);
                    last = j;
                    while (j < len && NodePathAlgorithms.IsWindowsSeparator(path[j]))
                    {
                        j++;
                    }

                    if (j < len && j != last)
                    {
                        last = j;
                        while (j < len && !NodePathAlgorithms.IsWindowsSeparator(path[j]))
                        {
                            j++;
                        }

                        if (j == len || j != last)
                        {
                            if (string.Equals(firstPart, ".", StringComparison.Ordinal)
                                || string.Equals(firstPart, "?", StringComparison.Ordinal))
                            {
                                // A device root, as in \\.\PHYSICALDRIVE0.
                                device = @"\\" + firstPart;
                                rootEnd = 4;

                                var deviceColonIndex = path.IndexOf(':');
                                var possibleDevice = NodePathAlgorithms.Slice(path, 4, deviceColonIndex + 1);
                                if (IsReservedName(possibleDevice, possibleDevice.Length - 1))
                                {
                                    // \\?\COM1: and friends: the reserved name is part of the root.
                                    device = @"\\?\" + possibleDevice;
                                    rootEnd = 4 + possibleDevice.Length;
                                }
                            }
                            else if (j == len)
                            {
                                // A UNC root and nothing else, so there is nothing left to normalize.
                                return @"\\" + firstPart + Separator + path.Substring(last) + Separator;
                            }
                            else
                            {
                                device = @"\\" + firstPart + Separator + path.Substring(last, j - last);
                                rootEnd = j;
                            }
                        }
                    }
                }
            }
            else
            {
                rootEnd = 1;
            }
        }
        else
        {
            var colonIndex = path.IndexOf(':');
            if (colonIndex > 0)
            {
                if (NodePathAlgorithms.IsWindowsDeviceRoot(code) && colonIndex == 1)
                {
                    device = path.Substring(0, 2);
                    rootEnd = 2;
                    if (len > 2 && NodePathAlgorithms.IsWindowsSeparator(path[2]))
                    {
                        isAbsolute = true;
                        rootEnd = 3;
                    }
                }
                else if (IsReservedName(path, colonIndex))
                {
                    device = path.Substring(0, colonIndex + 1);
                    rootEnd = colonIndex + 1;
                }
            }
        }

        var tail = rootEnd < len
            ? NodePathAlgorithms.NormalizeString(path.Substring(rootEnd), !isAbsolute, Separator, windowsSeparators: true)
            : string.Empty;

        if (tail.Length == 0 && !isAbsolute)
        {
            tail = ".";
        }

        if (tail.Length > 0 && NodePathAlgorithms.IsWindowsSeparator(path[len - 1]))
        {
            tail += Separator;
        }

        if (!isAbsolute && device is null && path.Contains(':'))
        {
            // A relative path that has not been pinned to a device must not normalize into something Windows
            // would read as absolute. See CVE-2024-36139.
            if (tail.Length >= 2 && NodePathAlgorithms.IsWindowsDeviceRoot(tail[0]) && tail[1] == ':')
            {
                return ".\\" + tail;
            }

            var index = path.IndexOf(':');
            do
            {
                if (index == len - 1 || NodePathAlgorithms.IsWindowsSeparator(path[index + 1]))
                {
                    return ".\\" + tail;
                }

                index = path.IndexOf(':', index + 1);
            }
            while (index != -1);
        }

        if (IsReservedName(path, path.IndexOf(':')))
        {
            return ".\\" + (device ?? string.Empty) + tail;
        }

        if (device is null)
        {
            return isAbsolute ? Separator + tail : tail;
        }

        return isAbsolute ? device + Separator + tail : device + tail;
    }

    /// <summary>
    /// <c>path.isAbsolute(path)</c> for Windows paths: a leading separator, or a drive letter followed by a
    /// colon and a separator. "It's not safe for mitigating path traversals."
    /// <para>
    /// https://nodejs.org/api/path.html#pathisabsolutepath
    /// </para>
    /// </summary>
    internal static bool IsAbsolute(string path)
    {
        var len = path.Length;
        if (len == 0)
        {
            return false;
        }

        var code = path[0];
        return NodePathAlgorithms.IsWindowsSeparator(code)
               || (len > 2
                   && NodePathAlgorithms.IsWindowsDeviceRoot(code)
                   && path[1] == ':'
                   && NodePathAlgorithms.IsWindowsSeparator(path[2]));
    }

    /// <summary>
    /// <c>path.join([...paths])</c> for Windows paths.
    /// <para>
    /// https://nodejs.org/api/path.html#pathjoinpaths
    /// </para>
    /// </summary>
    /// <remarks>
    /// The leading-slash dance is Node's: a joined path must not start with two separators, because
    /// <c>normalize</c> would then read it as a UNC root — unless the first non-empty segment itself starts
    /// with exactly two, which is how <c>path.join('//server', 'share')</c> deliberately builds one.
    /// </remarks>
    internal static string Join(IReadOnlyList<string> paths)
    {
        if (paths.Count == 0)
        {
            return ".";
        }

        var segments = new List<string>(paths.Count);
        for (var i = 0; i < paths.Count; ++i)
        {
            if (paths[i].Length > 0)
            {
                segments.Add(paths[i]);
            }
        }

        if (segments.Count == 0)
        {
            return ".";
        }

        var firstPart = segments[0];
        var joined = string.Join(Separator, segments);

        var needsReplace = true;
        var slashCount = 0;
        if (NodePathAlgorithms.IsWindowsSeparator(firstPart[0]))
        {
            ++slashCount;
            var firstLen = firstPart.Length;
            if (firstLen > 1 && NodePathAlgorithms.IsWindowsSeparator(firstPart[1]))
            {
                ++slashCount;
                if (firstLen > 2)
                {
                    if (NodePathAlgorithms.IsWindowsSeparator(firstPart[2]))
                    {
                        ++slashCount;
                    }
                    else
                    {
                        // The first part is itself a UNC path, so its two leading separators stay.
                        needsReplace = false;
                    }
                }
            }
        }

        if (needsReplace)
        {
            while (slashCount < joined.Length && NodePathAlgorithms.IsWindowsSeparator(joined[slashCount]))
            {
                slashCount++;
            }

            if (slashCount >= 2)
            {
                joined = Separator + joined.Substring(slashCount);
            }
        }

        // A reserved device name anywhere in the joined path skips normalization entirely; only the forward
        // slashes are turned around, so nothing collapses a segment Windows would resolve as a device.
        if (ContainsReservedSegment(joined))
        {
            return joined.Replace('/', Separator);
        }

        return Normalize(joined);
    }

    private static bool ContainsReservedSegment(string joined)
    {
        var start = 0;
        for (var i = 0; i <= joined.Length; i++)
        {
            if (i != joined.Length && joined[i] != Separator)
            {
                continue;
            }

            if (i > start)
            {
                var part = joined.Substring(start, i - start);
                var colonIndex = part.IndexOf(':');
                if (colonIndex != -1 && IsReservedName(part, colonIndex))
                {
                    return true;
                }
            }

            start = i + 1;
        }

        return false;
    }

    /// <summary>
    /// <c>path.relative(from, to)</c> for Windows paths. Comparison is case-insensitive, as the file system
    /// is; the answer is spelled with the casing <c>to</c> resolved to.
    /// <para>
    /// https://nodejs.org/api/path.html#pathrelativefrom-to
    /// </para>
    /// </summary>
    internal static string Relative(string cwd, bool platformIsWindows, string from, string to)
    {
        if (string.Equals(from, to, StringComparison.Ordinal))
        {
            return string.Empty;
        }

        var fromOrig = Resolve(cwd, platformIsWindows, [from]);
        var toOrig = Resolve(cwd, platformIsWindows, [to]);

        if (string.Equals(fromOrig, toOrig, StringComparison.Ordinal))
        {
            return string.Empty;
        }

        from = fromOrig.ToLowerInvariant();
        to = toOrig.ToLowerInvariant();

        if (string.Equals(from, to, StringComparison.Ordinal))
        {
            return string.Empty;
        }

        if (fromOrig.Length != from.Length || toOrig.Length != to.Length)
        {
            // Lowercasing changed the length, so the character-by-character walk below would compare
            // misaligned strings. Node falls back to a segment-wise comparison for exactly this case.
            return RelativeBySegments(fromOrig, toOrig);
        }

        // Leading backslashes, then trailing ones (which only a UNC path can carry).
        var fromStart = 0;
        while (fromStart < from.Length && from[fromStart] == Separator)
        {
            fromStart++;
        }

        var fromEnd = from.Length;
        while (fromEnd - 1 > fromStart && from[fromEnd - 1] == Separator)
        {
            fromEnd--;
        }

        var fromLen = fromEnd - fromStart;

        var toStart = 0;
        while (toStart < to.Length && to[toStart] == Separator)
        {
            toStart++;
        }

        var toEnd = to.Length;
        while (toEnd - 1 > toStart && to[toEnd - 1] == Separator)
        {
            toEnd--;
        }

        var toLen = toEnd - toStart;

        var length = fromLen < toLen ? fromLen : toLen;
        var lastCommonSep = -1;
        var i = 0;
        for (; i < length; i++)
        {
            var fromCode = from[fromStart + i];
            if (fromCode != to[toStart + i])
            {
                break;
            }

            if (fromCode == Separator)
            {
                lastCommonSep = i;
            }
        }

        if (i != length)
        {
            if (lastCommonSep == -1)
            {
                // The two paths diverge before the first shared separator, so there is no relative route.
                return toOrig;
            }
        }
        else
        {
            if (toLen > length)
            {
                if (to[toStart + i] == Separator)
                {
                    // `from` is the exact base path of `to`: from='C:\foo\bar', to='C:\foo\bar\baz'.
                    return toOrig.Substring(toStart + i + 1);
                }

                if (i == 2)
                {
                    // `from` is the device root: from='C:\', to='C:\foo'.
                    return toOrig.Substring(toStart + i);
                }
            }

            if (fromLen > length)
            {
                if (from[fromStart + i] == Separator)
                {
                    // `to` is the exact base path of `from`: from='C:\foo\bar', to='C:\foo'.
                    lastCommonSep = i;
                }
                else if (i == 2)
                {
                    // `to` is the device root: from='C:\foo\bar', to='C:\'.
                    lastCommonSep = 3;
                }
            }

            if (lastCommonSep == -1)
            {
                lastCommonSep = 0;
            }
        }

        var output = new ValueStringBuilder(stackalloc char[64]);
        try
        {
            for (i = fromStart + lastCommonSep + 1; i <= fromEnd; ++i)
            {
                if (i == fromEnd || from[i] == Separator)
                {
                    output.Append(output.Length == 0 ? ".." : @"\..");
                }
            }

            toStart += lastCommonSep;

            if (output.Length > 0)
            {
                output.Append(NodePathAlgorithms.Slice(toOrig, toStart, toEnd));
                return output.AsSpan().ToString();
            }

            if (toStart < toOrig.Length && toOrig[toStart] == Separator)
            {
                ++toStart;
            }

            return NodePathAlgorithms.Slice(toOrig, toStart, toEnd);
        }
        finally
        {
            output.Dispose();
        }
    }

    /// <summary>
    /// The segment-wise half of <c>relative</c>, taken when lowercasing changed a string's length — a Turkish
    /// dotted capital, say. Comparing segment by segment is what keeps the two sides aligned.
    /// </summary>
    private static string RelativeBySegments(string fromOrig, string toOrig)
    {
        var fromSplit = new List<string>(fromOrig.Split(Separator));
        var toSplit = new List<string>(toOrig.Split(Separator));

        if (fromSplit.Count > 0 && fromSplit[fromSplit.Count - 1].Length == 0)
        {
            fromSplit.RemoveAt(fromSplit.Count - 1);
        }

        if (toSplit.Count > 0 && toSplit[toSplit.Count - 1].Length == 0)
        {
            toSplit.RemoveAt(toSplit.Count - 1);
        }

        var fromLen = fromSplit.Count;
        var toLen = toSplit.Count;
        var length = fromLen < toLen ? fromLen : toLen;

        int i;
        for (i = 0; i < length; i++)
        {
            if (!string.Equals(fromSplit[i], toSplit[i], StringComparison.OrdinalIgnoreCase))
            {
                break;
            }
        }

        if (i == 0)
        {
            return toOrig;
        }

        if (i == length)
        {
            if (toLen > length)
            {
                return string.Join(Separator, toSplit.GetRange(i, toLen - i));
            }

            if (fromLen > length)
            {
                return Repeat(@"..\", fromLen - 1 - i) + "..";
            }

            return string.Empty;
        }

        return Repeat(@"..\", fromLen - i) + string.Join(Separator, toSplit.GetRange(i, toLen - i));
    }

    private static string Repeat(string value, int count)
    {
        if (count <= 0)
        {
            return string.Empty;
        }

        var builder = new ValueStringBuilder(stackalloc char[32]);
        try
        {
            for (var i = 0; i < count; i++)
            {
                builder.Append(value);
            }

            return builder.AsSpan().ToString();
        }
        finally
        {
            builder.Dispose();
        }
    }

    /// <summary>
    /// <c>path.toNamespacedPath(path)</c>: "an equivalent namespace-prefixed path", which is what lets Windows
    /// open a path longer than <c>MAX_PATH</c>. A path that is not absolute, or already prefixed, is returned
    /// unchanged.
    /// <para>
    /// https://nodejs.org/api/path.html#pathtonamespacedpathpath
    /// </para>
    /// </summary>
    internal static string ToNamespacedPath(string cwd, bool platformIsWindows, string path)
    {
        if (path.Length == 0)
        {
            return path;
        }

        var resolvedPath = Resolve(cwd, platformIsWindows, [path]);

        if (resolvedPath.Length <= 2)
        {
            return path;
        }

        if (resolvedPath[0] == Separator)
        {
            if (resolvedPath[1] == Separator)
            {
                var code = resolvedPath[2];
                if (code != '?' && code != '.')
                {
#pragma warning disable CA1845 // string.Concat(ReadOnlySpan<char>, ...) is netstandard2.1+, and net472 is a target
                    return @"\\?\UNC\" + resolvedPath.Substring(2);
#pragma warning restore CA1845
                }
            }
        }
        else if (NodePathAlgorithms.IsWindowsDeviceRoot(resolvedPath[0])
                 && resolvedPath[1] == ':'
                 && resolvedPath[2] == Separator)
        {
            return @"\\?\" + resolvedPath;
        }

        return resolvedPath;
    }

    /// <summary>
    /// <c>path.dirname(path)</c> for Windows paths. "Trailing directory separators are ignored", and a UNC
    /// root with nothing under it is its own directory.
    /// <para>
    /// https://nodejs.org/api/path.html#pathdirnamepath
    /// </para>
    /// </summary>
    internal static string Dirname(string path)
    {
        var len = path.Length;
        if (len == 0)
        {
            return ".";
        }

        var rootEnd = -1;
        var offset = 0;
        var code = path[0];

        if (len == 1)
        {
            return NodePathAlgorithms.IsWindowsSeparator(code) ? path : ".";
        }

        if (NodePathAlgorithms.IsWindowsSeparator(code))
        {
            rootEnd = offset = 1;

            if (NodePathAlgorithms.IsWindowsSeparator(path[1]))
            {
                var j = 2;
                var last = j;
                while (j < len && !NodePathAlgorithms.IsWindowsSeparator(path[j]))
                {
                    j++;
                }

                if (j < len && j != last)
                {
                    last = j;
                    while (j < len && NodePathAlgorithms.IsWindowsSeparator(path[j]))
                    {
                        j++;
                    }

                    if (j < len && j != last)
                    {
                        last = j;
                        while (j < len && !NodePathAlgorithms.IsWindowsSeparator(path[j]))
                        {
                            j++;
                        }

                        if (j == len)
                        {
                            // A UNC root and nothing else.
                            return path;
                        }

                        if (j != last)
                        {
                            // Past the separator that follows the UNC root, so the root is treated as an
                            // ordinary root on top of a root.
                            rootEnd = offset = j + 1;
                        }
                    }
                }
            }
        }
        else if (NodePathAlgorithms.IsWindowsDeviceRoot(code) && path[1] == ':')
        {
            rootEnd = len > 2 && NodePathAlgorithms.IsWindowsSeparator(path[2]) ? 3 : 2;
            offset = rootEnd;
        }

        var end = -1;
        var matchedSlash = true;
        for (var i = len - 1; i >= offset; --i)
        {
            if (NodePathAlgorithms.IsWindowsSeparator(path[i]))
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
            if (rootEnd == -1)
            {
                return ".";
            }

            end = rootEnd;
        }

        return path.Substring(0, end);
    }

    /// <summary>
    /// <c>path.basename(path[, suffix])</c> for Windows paths. "File extensions are treated case-sensitively
    /// even on Windows", which is why <c>path.win32.basename('C:\\foo.HTML', '.html')</c> answers
    /// <c>foo.HTML</c>.
    /// <para>
    /// https://nodejs.org/api/path.html#pathbasenamepath-suffix
    /// </para>
    /// </summary>
    internal static string Basename(string path, string? suffix)
    {
        var start = 0;
        var end = -1;
        var matchedSlash = true;

        // A drive prefix is skipped so that the separator after it is not mistaken for a trailing one.
        if (path.Length >= 2 && NodePathAlgorithms.IsWindowsDeviceRoot(path[0]) && path[1] == ':')
        {
            start = 2;
        }

        if (suffix is not null && suffix.Length > 0 && suffix.Length <= path.Length)
        {
            if (string.Equals(suffix, path, StringComparison.Ordinal))
            {
                return string.Empty;
            }

            var extIdx = suffix.Length - 1;
            var firstNonSlashEnd = -1;
            for (var i = path.Length - 1; i >= start; --i)
            {
                var code = path[i];
                if (NodePathAlgorithms.IsWindowsSeparator(code))
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

        for (var i = path.Length - 1; i >= start; --i)
        {
            if (NodePathAlgorithms.IsWindowsSeparator(path[i]))
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
    /// <c>path.extname(path)</c> for Windows paths.
    /// <para>
    /// https://nodejs.org/api/path.html#pathextnamepath
    /// </para>
    /// </summary>
    internal static string Extname(string path)
    {
        var start = 0;
        var startDot = -1;
        var startPart = 0;
        var end = -1;
        var matchedSlash = true;
        var preDotState = 0;

        if (path.Length >= 2 && path[1] == ':' && NodePathAlgorithms.IsWindowsDeviceRoot(path[0]))
        {
            start = startPart = 2;
        }

        for (var i = path.Length - 1; i >= start; --i)
        {
            var code = path[i];
            if (NodePathAlgorithms.IsWindowsSeparator(code))
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
    /// <c>path.parse(path)</c> for Windows paths. "If the directory is the root, use the entire root as the
    /// <c>dir</c> including the trailing slash if any."
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

        var len = path.Length;
        var rootEnd = 0;
        var code = path[0];

        if (len == 1)
        {
            if (NodePathAlgorithms.IsWindowsSeparator(code))
            {
                return new ParsedPath(path, path, "", "", "");
            }

            return new ParsedPath("", "", path, "", path);
        }

        if (NodePathAlgorithms.IsWindowsSeparator(code))
        {
            rootEnd = 1;
            if (NodePathAlgorithms.IsWindowsSeparator(path[1]))
            {
                var j = 2;
                var last = j;
                while (j < len && !NodePathAlgorithms.IsWindowsSeparator(path[j]))
                {
                    j++;
                }

                if (j < len && j != last)
                {
                    last = j;
                    while (j < len && NodePathAlgorithms.IsWindowsSeparator(path[j]))
                    {
                        j++;
                    }

                    if (j < len && j != last)
                    {
                        last = j;
                        while (j < len && !NodePathAlgorithms.IsWindowsSeparator(path[j]))
                        {
                            j++;
                        }

                        if (j == len)
                        {
                            rootEnd = j;
                        }
                        else if (j != last)
                        {
                            rootEnd = j + 1;
                        }
                    }
                }
            }
        }
        else if (NodePathAlgorithms.IsWindowsDeviceRoot(code) && path[1] == ':')
        {
            if (len <= 2)
            {
                return new ParsedPath(path, path, "", "", "");
            }

            rootEnd = 2;
            if (NodePathAlgorithms.IsWindowsSeparator(path[2]))
            {
                if (len == 3)
                {
                    return new ParsedPath(path, path, "", "", "");
                }

                rootEnd = 3;
            }
        }

        var root = rootEnd > 0 ? path.Substring(0, rootEnd) : string.Empty;

        var startDot = -1;
        var startPart = rootEnd;
        var end = -1;
        var matchedSlash = true;
        var preDotState = 0;

        for (var i = len - 1; i >= rootEnd; --i)
        {
            code = path[i];
            if (NodePathAlgorithms.IsWindowsSeparator(code))
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
            if (startDot == -1
                || preDotState == 0
                || (preDotState == 1 && startDot == end - 1 && startDot == startPart + 1))
            {
                baseName = name = path.Substring(startPart, end - startPart);
            }
            else
            {
                name = path.Substring(startPart, startDot - startPart);
                baseName = path.Substring(startPart, end - startPart);
                ext = path.Substring(startDot, end - startDot);
            }
        }

        var dir = startPart > 0 && startPart != rootEnd
            ? path.Substring(0, startPart - 1)
            : root;

        return new ParsedPath(root, dir, baseName, ext, name);
    }
}
