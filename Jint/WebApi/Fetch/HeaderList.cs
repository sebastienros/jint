#if NET8_0_OR_GREATER
using System.Runtime.InteropServices;

namespace Jint.WebApi.Fetch;

/// <summary>
/// One header of a header list, https://fetch.spec.whatwg.org/#concept-header.
/// </summary>
/// <param name="LowerName">
/// The header's name, already byte-lowercased. The Fetch Standard compares names byte-case-insensitively
/// everywhere and its iteration order is over lowercased names, so the original casing is not observable
/// through the <c>Headers</c> API at all; storing the lowercased form is what makes every lookup an ordinal
/// comparison.
/// </param>
/// <param name="Value">The header's value, already normalized and validated.</param>
[StructLayout(LayoutKind.Auto)]
internal readonly record struct HeaderEntry(string LowerName, string Value);

/// <summary>
/// A header list's guard, https://fetch.spec.whatwg.org/#concept-headers-guard — which mutations the
/// <c>Headers</c> object in front of the list allows.
/// </summary>
/// <remarks>
/// Only <see cref="Immutable"/> changes behaviour here. The <c>request</c>, <c>request-no-cors</c> and
/// <c>response</c> guards exist in the standard to enforce the <i>forbidden header name</i> and <i>forbidden
/// response header name</i> lists, which are a browser's protection of headers the user agent alone controls
/// (<c>Host</c>, <c>Cookie</c>, <c>Origin</c>, …). Jint runs server-side, where those headers are exactly what
/// a script legitimately needs to set, so the lists are deliberately not enforced — the same choice Node and
/// Deno make. The guards are still tracked, so that the two objects the standard makes immutable
/// (<c>Response.error()</c>'s and <c>Response.redirect()</c>'s headers) really are.
/// </remarks>
internal enum HeadersGuard
{
    /// <summary>A header list nobody restricts — a bare <c>new Headers()</c>.</summary>
    None,

    /// <summary>A <c>Request</c>'s headers.</summary>
    Request,

    /// <summary>A <c>Response</c>'s headers.</summary>
    Response,

    /// <summary>Refuses every mutation with a <c>TypeError</c>.</summary>
    Immutable,
}

/// <summary>
/// A header list, https://fetch.spec.whatwg.org/#concept-header-list, and the algorithms the <c>Headers</c>
/// class is defined in terms of.
/// </summary>
/// <remarks>
/// <para>
/// Deliberately engine-free — it mentions no <c>Engine</c>, <c>Realm</c> or <c>JsValue</c> — for the same
/// reason <c>Jint.WebApi.Url.Parsing</c> is: the HTTP half of <c>fetch</c> runs on a thread pool thread, where
/// touching the engine is forbidden, and it still has to read a request's headers and build a response's.
/// Validation failures are reported as a <see langword="bool"/> so the caller decides which realm's
/// <c>TypeError</c> to raise.
/// </para>
/// <para>
/// A list, not a dictionary: the order of equally-named headers is observable through <c>getSetCookie</c> and
/// through the value <c>get</c> combines, and header lists are small enough that a linear scan beats a hash.
/// </para>
/// </remarks>
internal sealed class HeaderList
{
    /// <summary>
    /// The one header name the Fetch Standard never combines, because a <c>Set-Cookie</c> value may itself
    /// contain a comma — https://fetch.spec.whatwg.org/#concept-header-list-sort-and-combine.
    /// </summary>
    internal const string SetCookieName = "set-cookie";

    private readonly List<HeaderEntry> _entries;

    internal HeaderList()
    {
        _entries = new List<HeaderEntry>();
    }

    private HeaderList(List<HeaderEntry> entries)
    {
        _entries = entries;
    }

    /// <summary>The guard of the <c>Headers</c> object in front of this list.</summary>
    internal HeadersGuard Guard { get; set; }

    /// <summary>The headers, in insertion order.</summary>
    internal List<HeaderEntry> Entries => _entries;

    /// <summary>
    /// A copy holding the same headers and nothing else — notably not the guard, which every caller of this
    /// sets for itself.
    /// </summary>
    internal HeaderList Clone() => new(new List<HeaderEntry>(_entries));

    /// <summary>
    /// https://fetch.spec.whatwg.org/#concept-header-list-append — no combining, no replacing: a header list
    /// may hold any number of headers with one name.
    /// </summary>
    internal void Append(string name, string value) => _entries.Add(new HeaderEntry(Lowercase(name), value));

    /// <summary>
    /// https://fetch.spec.whatwg.org/#concept-header-list-contains
    /// </summary>
    internal bool Contains(string name)
    {
        var key = Lowercase(name);
        foreach (var entry in _entries)
        {
            if (string.Equals(entry.LowerName, key, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// https://fetch.spec.whatwg.org/#concept-header-list-get — every value with that name, separated by
    /// 0x2C 0x20 (", "), or <see langword="null"/> when the list holds none.
    /// </summary>
    internal string? Get(string name)
    {
        var key = Lowercase(name);

        // The overwhelmingly common case is one header with the name, which then needs no builder and no
        // copy: the value string itself is the answer.
        string? first = null;
        List<string>? all = null;
        foreach (var entry in _entries)
        {
            if (!string.Equals(entry.LowerName, key, StringComparison.Ordinal))
            {
                continue;
            }

            if (first is null)
            {
                first = entry.Value;
                continue;
            }

            all ??= new List<string> { first };
            all.Add(entry.Value);
        }

        return all is null ? first : string.Join(", ", all);
    }

    /// <summary>
    /// https://fetch.spec.whatwg.org/#concept-header-list-delete
    /// </summary>
    internal void Delete(string name)
    {
        var key = Lowercase(name);
        _entries.RemoveAll(entry => string.Equals(entry.LowerName, key, StringComparison.Ordinal));
    }

    /// <summary>
    /// https://fetch.spec.whatwg.org/#concept-header-list-set — the first header with the name takes the new
    /// value and the rest are removed, so the header keeps its position in the list.
    /// </summary>
    internal void Set(string name, string value)
    {
        var key = Lowercase(name);
        var seen = false;

        for (var i = 0; i < _entries.Count; i++)
        {
            if (!string.Equals(_entries[i].LowerName, key, StringComparison.Ordinal))
            {
                continue;
            }

            if (!seen)
            {
                seen = true;
                _entries[i] = new HeaderEntry(key, value);
                continue;
            }

            _entries.RemoveAt(i);
            i--;
        }

        if (!seen)
        {
            _entries.Add(new HeaderEntry(key, value));
        }
    }

    /// <summary>
    /// Every <c>Set-Cookie</c> value, in list order — https://fetch.spec.whatwg.org/#dom-headers-getsetcookie.
    /// </summary>
    internal List<string> GetSetCookie()
    {
        var values = new List<string>();
        foreach (var entry in _entries)
        {
            if (string.Equals(entry.LowerName, SetCookieName, StringComparison.Ordinal))
            {
                values.Add(entry.Value);
            }
        }

        return values;
    }

    /// <summary>
    /// https://fetch.spec.whatwg.org/#concept-header-list-sort-and-combine — the pairs every iteration of a
    /// <c>Headers</c> object walks: one entry per distinct name in ascending byte order, with the values
    /// combined, except for <c>Set-Cookie</c> which contributes one entry per value.
    /// </summary>
    internal List<HeaderEntry> SortAndCombine()
    {
        if (_entries.Count == 0)
        {
            return new List<HeaderEntry>();
        }

        var names = new List<string>();
        foreach (var entry in _entries)
        {
            // List<string>.Contains uses the default equality comparer, which for string is ordinal.
            if (!names.Contains(entry.LowerName))
            {
                names.Add(entry.LowerName);
            }
        }

        // Header names are ASCII tokens, so an ordinal sort is the specification's "byte less than". The
        // names are distinct, so the sort's stability cannot be observed.
        names.Sort(StringComparer.Ordinal);

        var combined = new List<HeaderEntry>(names.Count);
        foreach (var name in names)
        {
            if (string.Equals(name, SetCookieName, StringComparison.Ordinal))
            {
                foreach (var entry in _entries)
                {
                    if (string.Equals(entry.LowerName, SetCookieName, StringComparison.Ordinal))
                    {
                        combined.Add(entry);
                    }
                }

                continue;
            }

            combined.Add(new HeaderEntry(name, Get(name)!));
        }

        return combined;
    }

    /// <summary>
    /// https://fetch.spec.whatwg.org/#concept-header-name — a non-empty sequence of RFC 9110 <c>tchar</c>s.
    /// </summary>
    internal static bool IsName(string name)
    {
        if (name.Length == 0)
        {
            return false;
        }

        foreach (var c in name)
        {
            if (!IsTokenChar(c))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// https://fetch.spec.whatwg.org/#concept-header-value — no leading or trailing HTTP tab or space, and no
    /// NUL, CR or LF anywhere. The last of those is what stops a script splicing a second request or a second
    /// response into the one it asked for.
    /// </summary>
    internal static bool IsValue(string value)
    {
        if (value.Length != 0 && (IsTabOrSpace(value[0]) || IsTabOrSpace(value[value.Length - 1])))
        {
            return false;
        }

        foreach (var c in value)
        {
            if (c is '\0' or '\n' or '\r')
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// https://fetch.spec.whatwg.org/#concept-header-value-normalize — strip leading and trailing HTTP
    /// whitespace, which is tab, LF, CR and space. Interior newlines survive this and are then refused by
    /// <see cref="IsValue"/>; only the surrounding ones are forgiven.
    /// </summary>
    internal static string Normalize(string value)
    {
        var start = 0;
        var end = value.Length;

        while (start < end && IsHttpWhitespace(value[start]))
        {
            start++;
        }

        while (end > start && IsHttpWhitespace(value[end - 1]))
        {
            end--;
        }

        return start == 0 && end == value.Length ? value : value.Substring(start, end - start);
    }

    /// <summary>
    /// Byte-lowercasing a header name. Every name reaching this has passed <see cref="IsName"/>, so it is
    /// ASCII and the invariant lowercase is the byte lowercase.
    /// </summary>
    internal static string Lowercase(string name)
    {
        foreach (var c in name)
        {
            if (c is >= 'A' and <= 'Z')
            {
                return name.ToLowerInvariant();
            }
        }

        return name;
    }

    /// <summary>
    /// RFC 9110 <c>tchar</c>, https://www.rfc-editor.org/rfc/rfc9110#name-tokens.
    /// </summary>
    internal static bool IsTokenChar(char c) => c switch
    {
        >= 'a' and <= 'z' => true,
        >= 'A' and <= 'Z' => true,
        >= '0' and <= '9' => true,
        '!' or '#' or '$' or '%' or '&' or '\'' or '*' or '+' or '-' or '.' or '^' or '_' or '`' or '|' or '~' => true,
        _ => false,
    };

    /// <summary>https://fetch.spec.whatwg.org/#http-tab-or-space</summary>
    private static bool IsTabOrSpace(char c) => c is '\t' or ' ';

    /// <summary>https://fetch.spec.whatwg.org/#http-whitespace</summary>
    internal static bool IsHttpWhitespace(char c) => c is '\t' or '\n' or '\r' or ' ';
}
#endif
