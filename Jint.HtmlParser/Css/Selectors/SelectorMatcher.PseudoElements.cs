using Jint.HtmlParser.Css.Syntax;
using static Jint.HtmlParser.Css.Selectors.CompiledSelector;

namespace Jint.HtmlParser.Css.Selectors;

/// <summary>The tree-abiding pseudo-elements the cascade can answer for (CSS Pseudo-Elements 4 §4).</summary>
internal enum SelectorPseudoElement : byte { Before, After }

// Selectors 4 §3.6.1 (https://drafts.csswg.org/selectors/#pseudo-element-attachment): a branch whose subject
// compound ends in ::before/::after represents that pseudo-element of its originating element, the element the
// rest of the branch matches. Element-only APIs never take this lane: there a pseudo-element subject matches
// nothing (see Matches/TryMatch), and the originating programs below are only ever read by the cascade.
internal static partial class SelectorMatcher
{
    internal static bool TryMatchPseudoElement(CompiledSelector program, Element element, SelectorPseudoElement pseudoElement,
        out SelectorSpecificity specificity, Node? scopingRoot, in SelectorEnvironment environment,
        ref SelectorMatchWork work, ShadowRoot? shadowScope = null)
    {
        ArgumentNullException.ThrowIfNull(program);
        var originating = OriginatingProgram(program, pseudoElement);
        if (originating is null)
        {
            specificity = default;
            return false;
        }
        return TryMatch(originating, element, out specificity, scopingRoot, environment, ref work, shadowScope);
    }

    private static CompiledSelector? OriginatingProgram(CompiledSelector program, SelectorPseudoElement pseudoElement)
    {
        var programs = Volatile.Read(ref program.OriginatingPrograms);
        if (programs is null)
        {
            // Derived once from the immutable program; a racing publication computes equal programs.
            programs = [Originating(program, "before"), Originating(program, "after")];
            Volatile.Write(ref program.OriginatingPrograms, programs);
        }
        return programs[(int) pseudoElement];
    }

    // The branches whose subject compound ends in exactly this pseudo-element, with it removed. A compound
    // with another pseudo-element (::slotted(x)::after, ::picker(select)) or with a user-action pseudo-class
    // after the pseudo-element (::after:hover) has no originating-element form here, so it matches nothing.
    // Each branch keeps its own specificity, which already counts the pseudo-element (Selectors 4 §17).
    private static CompiledSelector? Originating(CompiledSelector program, string name)
    {
        List<Complex>? branches = null;
        var maximum = default(SelectorSpecificity);
        foreach (var branch in program.BranchArray)
        {
            if (branch.LeadingCombinator is not null || branch.CompoundArray.Length == 0) continue;
            var subject = branch.CompoundArray[^1];
            var predicates = subject.PredicateArray;
            if (predicates.Length == 0 || predicates[^1] is not { Kind: PredicateKind.PseudoElement } last ||
                !CssAscii.EqualsIgnoreCase(last.Name!, name)) continue;
            var other = false;
            for (var i = 0; i < predicates.Length - 1; i++)
                other |= predicates[i].Kind is PredicateKind.PseudoElement or PredicateKind.WebkitUnknownPseudoElement
                    or PredicateKind.Picker or PredicateKind.Slotted;
            if (other) continue;
            var compounds = (Compound[]) branch.CompoundArray.Clone();
            compounds[^1] = new Compound(subject.NamespaceMode, subject.NamespaceUri, subject.TypeName,
                subject.HasExplicitType, predicates[..^1], subject.Span);
            (branches ??= []).Add(new Complex(compounds, branch.CombinatorArray, null, branch.Span, branch.Specificity));
            if (branch.Specificity.CompareTo(maximum) > 0) maximum = branch.Specificity;
        }
        return branches is null ? null : new CompiledSelector(branches, maximum, program.ContainsNesting);
    }
}
