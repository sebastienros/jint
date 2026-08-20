#if NET8_0_OR_GREATER
using System.Runtime.InteropServices;
using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.WebApi.Fetch;
using Jint.WebApi.Url.Parsing;

namespace Jint.WebApi.Caches;

/// <summary>
/// The <c>CacheQueryOptions</c> dictionary, https://w3c.github.io/ServiceWorker/#dictdef-cachequeryoptions —
/// three booleans, each defaulting to false.
/// </summary>
/// <param name="IgnoreSearch">Compare URLs with their query strings removed.</param>
/// <param name="IgnoreMethod">Do not refuse a query whose method is not <c>GET</c>.</param>
/// <param name="IgnoreVary">Do not honour a cached response's <c>Vary</c> header.</param>
[StructLayout(LayoutKind.Auto)]
internal readonly record struct CacheQueryOptions(bool IgnoreSearch, bool IgnoreMethod, bool IgnoreVary);

/// <summary>
/// The two matching algorithms every <c>Cache</c> method is defined in terms of:
/// <see href="https://w3c.github.io/ServiceWorker/#query-cache-algorithm">Query Cache</see> and
/// <see href="https://w3c.github.io/ServiceWorker/#request-matches-cached-item-algorithm">Request Matches
/// Cached Item</see>.
/// </summary>
/// <remarks>
/// A query runs against the snapshot a <see cref="CacheStore.Entries"/> read returned, and answers with
/// <i>indexes</i> into that snapshot rather than with the entries themselves — because that is what a
/// <see cref="CacheWrite"/> names, so the same result serves both a read (<c>matchAll</c>, <c>keys</c>) and a
/// write (<c>put</c>, <c>delete</c>) with nothing recomputed in between.
/// </remarks>
internal static class CacheQuery
{
    private const string VaryName = "vary";
    private const string GetMethod = "GET";

    /// <summary>
    /// Query Cache: the indexes of every entry in <paramref name="entries"/> the query matches, in list
    /// order.
    /// </summary>
    internal static List<int> Run(IReadOnlyList<CacheEntry> entries, JsRequest query, CacheQueryOptions options)
    {
        var matches = new List<int>();
        for (var i = 0; i < entries.Count; i++)
        {
            if (Matches(query, entries[i], options))
            {
                matches.Add(i);
            }
        }

        return matches;
    }

    /// <summary>
    /// Request Matches Cached Item, with the cached item's response always present — an entry the engine
    /// stored, or one a provider hydrated, always carries one.
    /// </summary>
    /// <remarks>
    /// The cached URL is re-parsed here rather than compared as text, because the two have to be compared as
    /// <i>URLs</i>: the fragment is excluded and, under <c>ignoreSearch</c>, so is the query. A stored URL
    /// that does not parse can never match, which is the documented fate of an entry a provider hydrated
    /// badly.
    /// </remarks>
    internal static bool Matches(JsRequest query, CacheEntry entry, CacheQueryOptions options)
    {
        // Step 1. The cached request is the one whose method is inspected, not the query's — the caller has
        // already refused a non-GET query where the algorithm above it says to. Everything the engine stores
        // is a GET, so this only ever fires for an entry a provider hydrated itself.
        if (!options.IgnoreMethod && !string.Equals(entry.Request.Method, GetMethod, StringComparison.Ordinal))
        {
            return false;
        }

        // Steps 2-6.
        var cachedUrl = UrlParser.Parse(entry.Request.Url);
        if (cachedUrl is null)
        {
            return false;
        }

        var queryText = SerializeForComparison(query.Url, options.IgnoreSearch);
        var cachedText = SerializeForComparison(cachedUrl, options.IgnoreSearch);
        if (!string.Equals(queryText, cachedText, StringComparison.Ordinal))
        {
            return false;
        }

        // Step 7.
        if (options.IgnoreVary)
        {
            return true;
        }

        var vary = CombinedValue(entry.Response.Headers, VaryName);
        if (vary is null)
        {
            return true;
        }

        // Steps 8-9: every field the response varies by must have the same combined value in the cached
        // request as in the query, "same" including both being absent.
        var fields = new List<string>();
        AppendFieldValues(vary, fields);

        foreach (var field in fields)
        {
            if (IsWildcard(field))
            {
                return false;
            }

            var cachedValue = CombinedValue(entry.Request.Headers, field);
            var queryValue = query.Headers.List.Get(field);
            if (!string.Equals(cachedValue, queryValue, StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Whether an entry is reachable at all.
    /// </summary>
    /// <remarks>
    /// An entry whose stored URL does not parse can never match a query, so it would otherwise be visible to
    /// the request-less forms of <c>matchAll</c> and invisible to everything else — including <c>keys</c>,
    /// which cannot build a <c>Request</c> for it at all. One rule instead: such an entry does not exist as
    /// far as the script is concerned. It cannot be deleted or replaced either, which is the honest
    /// consequence of a provider handing back a URL nothing can address.
    /// </remarks>
    internal static bool IsAddressable(CacheEntry entry) => UrlParser.Parse(entry.Request.Url) is not null;

    /// <summary>
    /// The text two URLs are compared as: the serialization with the fragment excluded, and — under
    /// <c>ignoreSearch</c> — without the query either.
    /// </summary>
    /// <remarks>
    /// The algorithm sets both URLs' queries to the empty string, which appends a bare <c>?</c> to both
    /// serializations; dropping the query from both instead is the same comparison with one fewer allocation.
    /// The first <c>?</c> in a fragment-excluded serialization always begins the query, because <c>?</c> is in
    /// the path percent-encode set and a host may not contain one.
    /// </remarks>
    private static string SerializeForComparison(UrlRecord url, bool ignoreSearch)
    {
        var serialized = url.Serialize(excludeFragment: true);
        if (!ignoreSearch)
        {
            return serialized;
        }

        var query = serialized.IndexOf('?');
        return query < 0 ? serialized : serialized.Substring(0, query);
    }

    /// <summary>
    /// https://fetch.spec.whatwg.org/#concept-header-list-get over the flattened headers of a cached request
    /// or response: every value with that name joined by <c>", "</c>, or <see langword="null"/> when there is
    /// none.
    /// </summary>
    /// <remarks>
    /// Names are compared ASCII-case-insensitively rather than ordinally, unlike
    /// <see cref="HeaderList.Get"/>: the engine writes them lowercased, but a provider that round-tripped
    /// them through a store which corrects casing must not thereby lose a header.
    /// </remarks>
    internal static string? CombinedValue(IReadOnlyList<CachedHeader> headers, string name)
    {
        string? first = null;
        List<string>? all = null;

        for (var i = 0; i < headers.Count; i++)
        {
            var header = headers[i];
            if (!string.Equals(header.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (first is null)
            {
                first = header.Value;
                continue;
            }

            all ??= new List<string> { first };
            all.Add(header.Value);
        }

        return all is null ? first : string.Join(", ", all);
    }

    /// <summary>
    /// Whether a <c>Vary</c> value names <c>*</c>, which is what <c>put</c> and <c>addAll</c> refuse:
    /// a response that varies by everything can never be matched again, so storing one is always a mistake.
    /// </summary>
    internal static bool VaryContainsWildcard(string? vary)
    {
        if (vary is null)
        {
            return false;
        }

        var fields = new List<string>();
        AppendFieldValues(vary, fields);

        foreach (var field in fields)
        {
            if (IsWildcard(field))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// The field-values of a <c>Vary</c> header: comma-separated, each stripped of the surrounding HTTP
    /// whitespace — https://www.rfc-editor.org/rfc/rfc9110#name-vary.
    /// </summary>
    private static void AppendFieldValues(string value, List<string> into)
    {
        var start = 0;
        while (start <= value.Length)
        {
            var comma = value.IndexOf(',', start);
            var end = comma < 0 ? value.Length : comma;

            var field = value.Substring(start, end - start).Trim('\t', '\n', '\r', ' ');
            if (field.Length != 0)
            {
                into.Add(field);
            }

            if (comma < 0)
            {
                return;
            }

            start = comma + 1;
        }
    }

    private static bool IsWildcard(string field) => string.Equals(field, "*", StringComparison.Ordinal);

    /// <summary>
    /// The <c>CacheQueryOptions</c> conversion, https://webidl.spec.whatwg.org/#es-dictionary: the members
    /// are read in lexicographical order of their identifiers, and an explicitly passed <c>undefined</c> takes
    /// the declared default.
    /// </summary>
    internal static CacheQueryOptions ReadOptions(Realm realm, JsValue value, string operation, string target)
    {
        var dictionary = ToDictionary(realm, value, operation, target);

        return new CacheQueryOptions(
            IgnoreSearch: ReadFlag(dictionary, "ignoreSearch"),
            IgnoreMethod: ReadFlag(dictionary, "ignoreMethod"),
            IgnoreVary: ReadFlag(dictionary, "ignoreVary"));
    }

    /// <summary>
    /// The <c>MultiCacheQueryOptions</c> conversion,
    /// https://w3c.github.io/ServiceWorker/#dictdef-multicachequeryoptions — <c>CacheQueryOptions</c> plus an
    /// optional <c>cacheName</c>, which has no default, so an absent one is a different thing from an empty
    /// one.
    /// </summary>
    /// <remarks>
    /// An inherited dictionary's members are converted before the deriving one's, so the read order is
    /// <c>ignoreMethod</c>, <c>ignoreSearch</c>, <c>ignoreVary</c>, then <c>cacheName</c> — observable
    /// through an options bag whose members are getters.
    /// </remarks>
    internal static CacheQueryOptions ReadMultiOptions(Realm realm, JsValue value, string operation, string target, out string? cacheName)
    {
        var dictionary = ToDictionary(realm, value, operation, target);

        var ignoreMethod = ReadFlag(dictionary, "ignoreMethod");
        var ignoreSearch = ReadFlag(dictionary, "ignoreSearch");
        var ignoreVary = ReadFlag(dictionary, "ignoreVary");

        var name = RequestConstructor.Member(dictionary, "cacheName");
        cacheName = name is null ? null : TypeConverter.ToString(name);

        return new CacheQueryOptions(ignoreSearch, ignoreMethod, ignoreVary);
    }

    private static bool ReadFlag(ObjectInstance? dictionary, string name)
    {
        var member = RequestConstructor.Member(dictionary, name);
        return member is not null && TypeConverter.ToBoolean(member);
    }

    private static ObjectInstance? ToDictionary(Realm realm, JsValue value, string operation, string target)
    {
        if (value.IsNullOrUndefined())
        {
            return null;
        }

        if (value is not ObjectInstance dictionary)
        {
            Throw.TypeError(realm, $"Failed to execute '{operation}' on '{target}': The provided value is not of type 'CacheQueryOptions'");
            return null;
        }

        return dictionary;
    }
}
#endif
