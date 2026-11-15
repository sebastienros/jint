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
    // :is() inside :is() inside a subject is rare; past this depth the selector is simply tried everywhere.
    private const int MaximumDepth = 4;

    internal static bool TryCollect(CompiledSelector selector, List<SelectorSubjectKey> keys)
        => TryCollect(selector, keys, 0);

    private static bool TryCollect(CompiledSelector selector, List<SelectorSubjectKey> keys, int depth)
    {
        var start = keys.Count;
        foreach (var branch in selector.Branches)
        {
            if (branch.LeadingCombinator is null && branch.Compounds.Count != 0 && TryKeysOf(branch.Compounds[^1], keys, depth))
                continue;
            keys.RemoveRange(start, keys.Count - start);
            return false;
        }
        return true;
    }

    // Selectors 4 §3.1: every simple selector of the subject compound must hold for the subject. Its own id, class
    // or type name is one key. Without one, an :is() or :where() argument list whose every branch is keyed gives a
    // union: the subject matches one of those branches, so it carries one of their keys. CSS Nesting §4: & is
    // :is(<parent list>), and is keyed the same way, which is what lets a nested rule such as `.card { &:hover {} }`
    // be bucketed under .card rather than tried on every element. Like Blink's RuleSet, a :not() gives no key.
    private static bool TryKeysOf(Compound compound, List<SelectorSubjectKey> keys, int depth)
    {
        string? id = null;
        string? className = null;
        CompiledSelector? logical = null;
        foreach (var predicate in compound.Predicates)
        {
            switch (predicate.Kind)
            {
                case PredicateKind.Id:
                    id ??= predicate.Name;
                    break;
                case PredicateKind.Class:
                    className ??= predicate.Name;
                    break;
                case PredicateKind.Is:
                case PredicateKind.Where:
                    logical ??= predicate.Arguments;
                    break;
                // These match an element other than the one the compound's own names describe.
                case PredicateKind.PseudoElement:
                case PredicateKind.WebkitUnknownPseudoElement:
                case PredicateKind.Picker:
                case PredicateKind.Slotted:
                case PredicateKind.Host:
                case PredicateKind.HostContext:
                    return false;
            }
        }
        if (id is not null) keys.Add(new(SelectorSubjectKeyKind.Id, id));
        else if (className is not null) keys.Add(new(SelectorSubjectKeyKind.Class, className));
        else if (compound.TypeName is { } type) keys.Add(new(SelectorSubjectKeyKind.Type, type));
        else return logical is not null && depth < MaximumDepth && TryCollect(logical, keys, depth + 1);
        return true;
    }
}
