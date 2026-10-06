namespace Jint.HtmlParser.Sanitization;

/// <summary>
/// A canonical <c>SanitizerElementNamespace</c> or <c>SanitizerAttributeNamespace</c>: a local name and a
/// namespace, where the empty namespace has already been folded to <see langword="null"/>.
/// </summary>
/// <remarks>
/// https://html.spec.whatwg.org/multipage/dynamic-markup-insertion.html#canonicalize-a-sanitizer-name. Two
/// names are equal when both members are equal, compared ordinally, which is what the record's generated
/// equality already does.
/// </remarks>
internal readonly record struct SanitizerName(string Name, string? Namespace)
{
    internal static SanitizerName Html(string name) => new(name, Namespaces.Html);

    internal static SanitizerName Attribute(string name) => new(name, null);

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/dynamic-markup-insertion.html#sanitizerconfig-compare-sanitizer-items
    /// as a three-way comparison: a null namespace sorts first, then namespaces and names by code unit.
    /// </summary>
    internal static int Compare(SanitizerName a, SanitizerName b)
    {
        if (a.Namespace is null)
        {
            if (b.Namespace is not null) return -1;
        }
        else
        {
            if (b.Namespace is null) return 1;
            var byNamespace = string.CompareOrdinal(a.Namespace, b.Namespace);
            if (byNamespace != 0) return byNamespace;
        }

        return string.CompareOrdinal(a.Name, b.Name);
    }

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/dom.html#custom-data-attribute: no namespace, a name starting
    /// with <c>data-</c> with at least one character after the hyphen, and no ASCII upper alpha.
    /// </summary>
    internal bool IsCustomDataAttribute
    {
        get
        {
            if (Namespace is not null || Name.Length <= 5 || !Name.StartsWith("data-", StringComparison.Ordinal))
            {
                return false;
            }

            foreach (var c in Name)
            {
                if (c is >= 'A' and <= 'Z') return false;
            }

            return true;
        }
    }
}
