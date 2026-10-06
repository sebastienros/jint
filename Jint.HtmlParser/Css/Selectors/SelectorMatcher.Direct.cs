using static Jint.HtmlParser.Css.Selectors.CompiledSelector;
using ComplexSelector = Jint.HtmlParser.Css.Selectors.CompiledSelector.Complex;

namespace Jint.HtmlParser.Css.Selectors;

// Selectors §4 and §16: https://drafts.csswg.org/selectors/#match-a-selector-against-an-element
// Allocation-free right-to-left matcher for the selectors queries use most: compounds of
// ordinary predicates joined by the four tree combinators, optionally wrapped in :is(),
// :where() or :not(). Anything else (:has(), nesting, column, :host(), nth-child "of S",
// featureless subjects) stays on the general evaluator in SelectorMatcher.Relational.cs.
internal static partial class SelectorMatcher
{
    private const byte DirectShapeComputed = 1;
    private const byte DirectShapeEligible = 2;
    private const byte DirectShapeRelational = 4;

    // Recursion is bounded by compounds per complex selector times logical-argument depth.
    private const int DirectMaximumCompounds = 32;
    private const int DirectMaximumDepth = 8;

    // The classic failure classification (as in WebKit's SelectorChecker) prunes backtracking:
    // a failed descendant search cannot succeed from a higher ancestor, and a failed sibling
    // search cannot succeed from an earlier sibling.
    private enum DirectMatch : byte { Matches, FailsLocally, FailsAllSiblings, FailsCompletely }

    private static bool TryMatchDirect(CompiledSelector program, Element element, Node? scope,
        ref Work work, out bool matched, out SelectorSpecificity specificity)
    {
        var shape = program.DirectMatchShape;
        if (shape == 0)
        {
            shape = (byte) (DirectShapeComputed | ClassifyDirect(program, 0));
            program.DirectMatchShape = shape;
        }
        // A DocumentFragment scope is a featureless predecessor only the general evaluator models.
        if ((shape & DirectShapeEligible) == 0 ||
            (shape & DirectShapeRelational) != 0 && scope is DocumentFragment)
        {
            matched = false;
            specificity = default;
            return false;
        }
        matched = DirectMatchesProgram(program, element, scope, suppressSubjectNamespace: false,
            needSpecificity: true, ref work, out specificity);
        return true;
    }

    private static byte ClassifyDirect(CompiledSelector program, int depth)
    {
        if (depth > DirectMaximumDepth) return 0;
        var shape = DirectShapeEligible;
        var branches = program.BranchArray;
        for (var b = 0; b < branches.Length; b++)
        {
            var branch = branches[b];
            var compounds = branch.CompoundArray;
            if (branch.LeadingCombinator is not null || compounds.Length == 0 ||
                compounds.Length > DirectMaximumCompounds) return 0;
            var combinators = branch.CombinatorArray;
            for (var c = 0; c < combinators.Length; c++)
            {
                if (combinators[c] == Combinator.Column) return 0;
                shape |= DirectShapeRelational;
            }
            for (var c = 0; c < compounds.Length; c++)
            {
                var predicates = compounds[c].PredicateArray;
                for (var p = 0; p < predicates.Length; p++)
                {
                    var predicate = predicates[p];
                    switch (predicate.Kind)
                    {
                        case PredicateKind.Is:
                        case PredicateKind.Where:
                        case PredicateKind.Not:
                            if (predicate.IsNestingReference || predicate.Arguments is null) return 0;
                            var nested = ClassifyDirect(predicate.Arguments, depth + 1);
                            if ((nested & DirectShapeEligible) == 0) return 0;
                            shape |= nested;
                            break;
                        case PredicateKind.Has:
                            return 0;
                        default:
                            if (predicate.Arguments is not null) return 0;
                            break;
                    }
                }
            }
        }
        return shape;
    }

    private static bool DirectMatchesProgram(CompiledSelector program, Element element, Node? scope,
        bool suppressSubjectNamespace, bool needSpecificity, ref Work work, out SelectorSpecificity specificity)
    {
        var found = false;
        specificity = default;
        var branches = program.BranchArray;
        for (var b = 0; b < branches.Length; b++)
        {
            work.Step();
            var branch = branches[b];
            if (DirectMatchFrom(branch, branch.CompoundArray.Length - 1, element, scope, suppressSubjectNamespace,
                    ref work) != DirectMatch.Matches) continue;
            if (!needSpecificity) return true;
            found = true;
            if (branch.Specificity.CompareTo(specificity) > 0) specificity = branch.Specificity;
        }
        return found;
    }

    private static DirectMatch DirectMatchFrom(ComplexSelector branch, int part, Element element, Node? scope,
        bool suppressSubjectNamespace, ref Work work)
    {
        work.Step();
        var compounds = branch.CompoundArray;
        if (!DirectMatchesCompound(compounds[part], element, scope,
                suppressSubjectNamespace && part == compounds.Length - 1, ref work))
            return DirectMatch.FailsLocally;
        if (part == 0) return DirectMatch.Matches;
        var combinator = branch.CombinatorArray[part - 1];
        switch (combinator)
        {
            case Combinator.Descendant:
                for (var ancestor = element.ParentNode as Element; ancestor is not null;
                     ancestor = ancestor.ParentNode as Element)
                {
                    work.Step();
                    var result = DirectMatchFrom(branch, part - 1, ancestor, scope, false, ref work);
                    if (result is DirectMatch.Matches or DirectMatch.FailsCompletely) return result;
                }
                return DirectMatch.FailsCompletely;
            case Combinator.Child:
                return element.ParentNode is Element parent
                    ? DirectMatchFrom(branch, part - 1, parent, scope, false, ref work)
                    : DirectMatch.FailsCompletely;
            case Combinator.NextSibling:
                var previous = PreviousElementSibling(element, ref work);
                return previous is not null
                    ? DirectMatchFrom(branch, part - 1, previous, scope, false, ref work)
                    : DirectMatch.FailsAllSiblings;
            case Combinator.SubsequentSibling:
                for (var sibling = PreviousElementSibling(element, ref work); sibling is not null;
                     sibling = PreviousElementSibling(sibling, ref work))
                {
                    var result = DirectMatchFrom(branch, part - 1, sibling, scope, false, ref work);
                    if (result != DirectMatch.FailsLocally) return result;
                }
                return DirectMatch.FailsAllSiblings;
            default:
                throw Unsupported(combinator.ToString());
        }
    }

    private static Element? PreviousElementSibling(Node node, ref Work work)
    {
        for (var previous = node.PreviousSibling; previous is not null; previous = previous.PreviousSibling)
        {
            work.Step();
            if (previous is Element element) return element;
        }
        return null;
    }

    private static bool DirectMatchesCompound(Compound compound, Element element, Node? scope,
        bool suppressSubjectNamespace, ref Work work)
    {
        if ((!suppressSubjectNamespace || compound.HasExplicitType) &&
            !NamespaceMatches(compound.NamespaceMode, compound.NamespaceUri, element.NamespaceUri) ||
            compound.TypeName is not null &&
            !SelectorNameMatches(compound.TypeName, element.LocalName, IsHtmlElement(element), ref work))
            return false;
        var predicates = compound.PredicateArray;
        for (var p = 0; p < predicates.Length; p++)
        {
            work.Step();
            var predicate = predicates[p];
            bool matched;
            switch (predicate.Kind)
            {
                case PredicateKind.Is:
                case PredicateKind.Where:
                    matched = DirectMatchesProgram(predicate.Arguments!, element, scope,
                        suppressSubjectNamespace: true, needSpecificity: false, ref work, out _);
                    break;
                case PredicateKind.Not:
                    matched = !DirectMatchesProgram(predicate.Arguments!, element, scope,
                        suppressSubjectNamespace: true, needSpecificity: false, ref work, out _);
                    break;
                default:
                    matched = MatchPredicate(predicate, element, scope, ref work);
                    break;
            }
            if (!matched) return false;
        }
        return true;
    }
}
