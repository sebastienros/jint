using System.Text.Json;

namespace Jint.Browser.Runtime.Parsing;

/// <summary>
/// A document's import map: the table a bare module specifier is resolved through.
/// </summary>
/// <remarks>
/// <para>
/// https://html.spec.whatwg.org/multipage/webappapis.html#import-maps — one map per document, declared by a
/// <c>&lt;script type="importmap"&gt;</c> that must come before the first module script. Both halves are
/// implemented: <c>imports</c>, and <c>scopes</c> whose longest matching prefix of the importing module's
/// URL wins.
/// </para>
/// <para>
/// <b>A key ending in a slash is a prefix mapping</b>, so <c>"lib/": "/vendor/lib/"</c> resolves
/// <c>lib/a.js</c> to <c>/vendor/lib/a.js</c>; a key that does not is an exact match. Within one table the
/// longest matching key wins, which is what makes an exact entry beat the prefix that contains it. Every
/// address is resolved against the document's base URL when the map is parsed, so resolution afterwards is
/// string work.
/// </para>
/// <para>
/// <b>What is not implemented</b>: <c>integrity</c> (accepted and ignored, like the attribute), and the
/// standard's rule that a map is invalid — rather than partly applied — when an entry is malformed. Here a
/// malformed entry is dropped with a page error naming it and the rest of the map still applies, because a
/// page that mostly works is more useful to a caller than one that reports nothing.
/// </para>
/// </remarks>
internal sealed class ImportMap
{
    private readonly List<Entry> _imports;
    private readonly List<Scope> _scopes;

    private ImportMap(List<Entry> imports, List<Scope> scopes)
    {
        _imports = imports;
        _scopes = scopes;
    }

    /// <summary>Parses the JSON of an import map, resolving every address against <paramref name="baseUrl"/>.</summary>
    /// <param name="json">The element's text.</param>
    /// <param name="baseUrl">The document's base URL.</param>
    /// <param name="problems">Every entry that could not be read, so a page error can name it.</param>
    internal static ImportMap Parse(string json, string baseUrl, List<string> problems)
    {
        var imports = new List<Entry>();
        var scopes = new List<Scope>();

        JsonDocument document;

        try
        {
            document = JsonDocument.Parse(json, new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip });
        }
        catch (JsonException exception)
        {
            problems.Add("the import map is not valid JSON: " + exception.Message);
            return new ImportMap(imports, scopes);
        }

        using (document)
        {
            var root = document.RootElement;

            if (root.ValueKind != JsonValueKind.Object)
            {
                problems.Add("the import map's top level is not an object");
                return new ImportMap(imports, scopes);
            }

            if (root.TryGetProperty("imports", out var table))
            {
                ReadTable(table, baseUrl, "imports", imports, problems);
            }

            if (root.TryGetProperty("scopes", out var scopeTable) && scopeTable.ValueKind == JsonValueKind.Object)
            {
                foreach (var scope in scopeTable.EnumerateObject())
                {
                    var prefix = PageUrl.Resolve(scope.Name, baseUrl);
                    if (prefix is null)
                    {
                        problems.Add("the import map scope '" + scope.Name + "' is not a URL");
                        continue;
                    }

                    var entries = new List<Entry>();
                    ReadTable(scope.Value, baseUrl, "scopes['" + scope.Name + "']", entries, problems);
                    scopes.Add(new Scope(prefix, entries));
                }
            }
        }

        // The longest scope prefix wins, so ordering them once here is what makes resolution a first match.
        scopes.Sort(static (a, b) => b.Prefix.Length.CompareTo(a.Prefix.Length));
        return new ImportMap(imports, scopes);
    }

    /// <summary>
    /// The URL <paramref name="specifier"/> maps to, or <see langword="null"/> when the map says nothing
    /// about it.
    /// </summary>
    /// <param name="specifier">The specifier as written in the importing module.</param>
    /// <param name="referrer">The importing module's URL, which selects the scope.</param>
    internal string? Resolve(string specifier, string? referrer)
    {
        if (referrer is { Length: > 0 })
        {
            foreach (var scope in _scopes)
            {
                if (referrer.StartsWith(scope.Prefix, StringComparison.Ordinal)
                    && Match(scope.Entries, specifier) is { } scoped)
                {
                    return scoped;
                }
            }
        }

        return Match(_imports, specifier);
    }

    private static string? Match(List<Entry> entries, string specifier)
    {
        string? best = null;
        var bestLength = -1;

        foreach (var entry in entries)
        {
            if (entry.IsPrefix)
            {
                if (specifier.StartsWith(entry.Key, StringComparison.Ordinal) && entry.Key.Length > bestLength)
                {
                    best = entry.Address + specifier[entry.Key.Length..];
                    bestLength = entry.Key.Length;
                }

                continue;
            }

            if (string.Equals(entry.Key, specifier, StringComparison.Ordinal) && entry.Key.Length > bestLength)
            {
                best = entry.Address;
                bestLength = entry.Key.Length;
            }
        }

        return best;
    }

    private static void ReadTable(JsonElement table, string baseUrl, string where, List<Entry> entries, List<string> problems)
    {
        if (table.ValueKind != JsonValueKind.Object)
        {
            problems.Add("the import map's '" + where + "' is not an object");
            return;
        }

        foreach (var property in table.EnumerateObject())
        {
            if (property.Value.ValueKind != JsonValueKind.String)
            {
                problems.Add("the import map entry '" + where + "." + property.Name + "' is not a string");
                continue;
            }

            var address = property.Value.GetString() ?? "";
            var resolved = PageUrl.Resolve(address, baseUrl);

            if (resolved is null)
            {
                problems.Add("the import map entry '" + where + "." + property.Name + "' names '" + address + "', which is not a URL");
                continue;
            }

            var key = property.Name;

            if (key.EndsWith('/') != address.EndsWith('/'))
            {
                problems.Add("the import map entry '" + where + "." + key + "' must end with '/' on both sides or on neither");
                continue;
            }

            entries.Add(new Entry(key, resolved, key.EndsWith('/')));
        }
    }

    private readonly record struct Entry(string Key, string Address, bool IsPrefix);

    private sealed record Scope(string Prefix, List<Entry> Entries);
}
