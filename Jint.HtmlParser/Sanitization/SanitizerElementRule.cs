namespace Jint.HtmlParser.Sanitization;

/// <summary>
/// A canonical <c>SanitizerElementNamespaceWithAttributes</c>: an element name and its local attribute
/// allow- and remove-lists, where a <see langword="null"/> list is one that does not exist.
/// </summary>
/// <remarks>
/// https://html.spec.whatwg.org/multipage/dynamic-markup-insertion.html#canonicalize-a-sanitizerelementwithattributes
/// leaves at least one of the two lists present; <see cref="SanitizerConfiguration.Canonicalize"/> is what
/// supplies the empty <c>removeAttributes</c> when neither was given.
/// </remarks>
internal sealed class SanitizerElementRule
{
    internal SanitizerElementRule(SanitizerName name, List<SanitizerName>? attributes = null,
        List<SanitizerName>? removeAttributes = null)
    {
        Name = name;
        Attributes = attributes;
        RemoveAttributes = removeAttributes;
    }

    internal SanitizerName Name { get; }

    internal List<SanitizerName>? Attributes { get; set; }

    internal List<SanitizerName>? RemoveAttributes { get; set; }

    internal SanitizerElementRule Clone()
        => new(Name, Attributes is null ? null : [.. Attributes], RemoveAttributes is null ? null : [.. RemoveAttributes]);

    /// <summary>
    /// Dictionary equality: the same name and the same lists, where a list is compared as the set it is
    /// used as — outside <c>get()</c> the order of a sanitizer's items is unobservable.
    /// </summary>
    internal bool IsEquivalentTo(SanitizerElementRule other)
        => Name == other.Name && SameSet(Attributes, other.Attributes) && SameSet(RemoveAttributes, other.RemoveAttributes);

    private static bool SameSet(List<SanitizerName>? a, List<SanitizerName>? b)
    {
        if (a is null || b is null) return a is null && b is null;
        if (a.Count != b.Count) return false;
        foreach (var item in a)
        {
            if (!b.Contains(item)) return false;
        }

        return true;
    }
}
