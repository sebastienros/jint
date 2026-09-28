using static Jint.HtmlParser.Css.Selectors.CompiledSelector;

namespace Jint.HtmlParser.Css.Selectors;

internal enum SelectorSubjectKeyKind : byte
{
    Id,
    Class,
    Type
}

/// <summary>
/// An id, class or type name every element matched by one selector branch must carry. Names compare
/// ASCII case-insensitively in quirks mode and for HTML type selectors, so an index built from these keys
/// must look them up case-insensitively; it is then a superset filter and the matcher stays authoritative.
/// </summary>
internal readonly record struct SelectorSubjectKey(SelectorSubjectKeyKind Kind, string Name);

internal static class SelectorSubjectKeys
{
    /// <summary>
    /// Adds one key per branch and returns <see langword="true"/>, or returns <see langword="false"/> when any
    /// branch has no key, in which case the selector must be tried on every element.
    /// </summary>
    internal static bool TryCollect(CompiledSelector selector, List<SelectorSubjectKey> keys)
    {
        if (selector.ContainsNesting) return false;
        var start = keys.Count;
        foreach (var branch in selector.Branches)
        {
            if (branch.LeadingCombinator is null && branch.Compounds.Count != 0 && KeyOf(branch.Compounds[^1]) is { } key)
            {
                keys.Add(key);
                continue;
            }
            keys.RemoveRange(start, keys.Count - start);
            return false;
        }
        return true;
    }

    // Selectors 4 §3.1: every simple selector of the subject compound must hold for the subject.
    private static SelectorSubjectKey? KeyOf(Compound compound)
    {
        string? id = null;
        string? className = null;
        foreach (var predicate in compound.Predicates)
        {
            if (predicate.IsNestingReference) return null;
            switch (predicate.Kind)
            {
                case PredicateKind.Id:
                    id ??= predicate.Name;
                    break;
                case PredicateKind.Class:
                    className ??= predicate.Name;
                    break;
                // These match an element other than the one the compound's own names describe.
                case PredicateKind.PseudoElement:
                case PredicateKind.WebkitUnknownPseudoElement:
                case PredicateKind.Picker:
                case PredicateKind.Slotted:
                case PredicateKind.Host:
                case PredicateKind.HostContext:
                    return null;
            }
        }
        if (id is not null) return new(SelectorSubjectKeyKind.Id, id);
        if (className is not null) return new(SelectorSubjectKeyKind.Class, className);
        return compound.TypeName is { } type ? new(SelectorSubjectKeyKind.Type, type) : null;
    }
}
