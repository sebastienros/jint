using System.Runtime.CompilerServices;
using Jint.HtmlParser;
using Jint.HtmlParser.Css.Media;
using Jint.HtmlParser.Css.Model;
using Jint.HtmlParser.Css.Selectors;
using Jint.HtmlParser.Css.Values;
using static Jint.HtmlParser.Css.Selectors.CompiledSelector;

namespace Jint.Browser.Styling;

internal sealed partial class NativeCssQuery
{
    private static readonly ConditionalWeakTable<Document, RulePlan> Plans = new();
    private RulePlan? _plan;

    internal object MeasurementPlan { get { PrepareRules(); return _plan!; } }

    /// <summary>
    /// Which of the page's interaction witnesses the document's rules can observe, once the rule plan has
    /// been prepared; <see langword="false"/> before then, when the caller must assume all of them.
    /// </summary>
    internal bool TryGetInteractionDependencies(out CssInteractionDependencies dependencies)
    {
        dependencies = _plan?.Dependencies.Interactions ?? CssInteractionDependencies.All;
        return _plan is not null;
    }

    /// <summary>
    /// Which of the page's interaction witnesses the rules that can change a measurement observe: a press that only
    /// recolours a button keeps every box where it was.
    /// </summary>
    internal bool TryGetMeasurementInteractionDependencies(out CssInteractionDependencies dependencies)
    {
        dependencies = _plan?.Dependencies.MeasurementInteractions ?? CssInteractionDependencies.All;
        return _plan is not null;
    }

    internal bool RequiresGlobalMeasurements(NativeMutationKind kind, string? attributeName)
    {
        PrepareRules();
        return _plan!.Dependencies.RequiresGlobal(kind, attributeName);
    }

    /// <summary>
    /// Whether a change to <paramref name="attributeName"/> can restyle the changed element's following siblings, as
    /// a sibling combinator does, besides its own subtree.
    /// </summary>
    internal bool RequiresSiblingMeasurements(NativeMutationKind kind, string? attributeName)
    {
        PrepareRules();
        return _plan!.Dependencies.RequiresSiblings(kind, attributeName);
    }

    /// <summary>
    /// The :has() anchors a change to <paramref name="attributeName"/> can rematch, or <see langword="null"/>: each
    /// is the changed element or one of its ancestors that carries the anchor's key, and restyles its own subtree,
    /// and its following siblings' when the anchor's compound is followed by a sibling combinator.
    /// </summary>
    internal IReadOnlyList<MeasurementAnchor>? MeasurementAnchors(NativeMutationKind kind, string? attributeName)
    {
        PrepareRules();
        return _plan!.Dependencies.AnchorsOf(kind, attributeName);
    }

    /// <summary>The key an anchor of a descendant- or child-relative :has() carries, and whether it restyles its following siblings.</summary>
    internal readonly record struct MeasurementAnchor(SelectorSubjectKey Key, bool Siblings)
    {
        /// <summary>Whether <paramref name="element"/> may carry the key, compared as loosely as quirks mode and HTML type names allow.</summary>
        internal bool MayBeCarriedBy(Element element)
        {
            switch (Key.Kind)
            {
                case SelectorSubjectKeyKind.Type:
                    return string.Equals(element.LocalName, Key.Name, StringComparison.OrdinalIgnoreCase);
                case SelectorSubjectKeyKind.Id:
                    return string.Equals(element.GetAttribute("id"), Key.Name, StringComparison.OrdinalIgnoreCase);
                default:
                    if (element.GetAttribute("class") is not { } classes) return false;
                    foreach (var token in classes.Split([' ', '\t', '\n', '\f', '\r'], StringSplitOptions.RemoveEmptyEntries))
                    {
                        if (string.Equals(token, Key.Name, StringComparison.OrdinalIgnoreCase)) return true;
                    }

                    return false;
            }
        }
    }

    // Only the sheet-dependent plan survives DOM mutations. Matching, inline declarations,
    // computed values and candidate de-duplication remain owned by each new query.
    private sealed record RulePlan(NativeCssSheet[] Sheets, CssStyleSheetRevisionSnapshot Revisions,
        CssMediaEnvironment Media, ShadowRoot?[] Scopes,
        List<(CssStyleRule Rule, NativeCssOrigin Origin, long Order, string? NamespaceUri, NativeCssLayer Layer)> Rules,
        RuleIndex Index, MeasurementDependencies Dependencies, Dictionary<CssStyleRule, CssScopeRule[]>? ScopeChains);

    private void PrepareRules()
    {
        if (_rules is not null) return;
        if (Plans.TryGetValue(_document, out var cached) && PlanIsCurrent(cached))
        {
            Verify();
            _rules = cached.Rules;
            _index = cached.Index;
            _scopeChains = cached.ScopeChains;
            _plan = cached;
            _diagnostics?.RulePlanReused();
        }
        else
        {
            var rules = BuildRules();
            var index = new RuleIndex(rules, _work);
            var scopes = new ShadowRoot?[_sheets.Length];
            for (var i = 0; i < scopes.Length; i++)
            {
                _work.Charge(1);
                scopes[i] = _sheets[i].Sheet.EffectiveOwnerNode(_work)?.TreeShadowRoot;
            }
            Verify();
            var dependencies = new MeasurementDependencies();
            for (var i = 0; i < rules.Count; i++)
                dependencies.Include(rules[i].Rule.Selector, index.AffectsMeasurements(i), _work);
            // Scoping roots and limits are matched against ancestors; no retained row is safe.
            if (_scopeChains is not null) dependencies.IncludeUnknown();
            _plan = new(_sheets, _sheetRevisions, _media, scopes, rules, index, dependencies, _scopeChains);
            Plans.AddOrUpdate(_document, _plan);
            _rules = rules;
            _index = index;
            _diagnostics?.RulePlanBuilt();
        }
        _seenCandidates = new int[_rules.Count];
    }

    private bool PlanIsCurrent(RulePlan plan)
    {
        if (plan.Media != _media || plan.Sheets.Length != _sheets.Length || !plan.Revisions.IsCurrent(_work))
            return false;
        for (var i = 0; i < _sheets.Length; i++)
        {
            _work.Charge(1);
            if (plan.Sheets[i] != _sheets[i] ||
                !ReferenceEquals(plan.Scopes[i], _sheets[i].Sheet.EffectiveOwnerNode(_work)?.TreeShadowRoot))
                return false;
        }
        return true;
    }

    // Selectors 4 §§3–4. Ancestor/child matching propagates down the changed subtree.
    // Reverse, sibling, positional and semantic dependencies fall back globally for the
    // kinds of writes they can observe. Unknown programs never authorize retained rows.
    // Measurements are read from the rules that declare a layout or custom property alone, so only theirs count.
    private sealed class MeasurementDependencies
    {
        // How far a change to each attribute can reach, when further than the changed element's subtree: named left of
        // a sibling combinator, its following siblings' subtrees; inside :has() or a positional pseudo-class's
        // argument, anywhere.
        private readonly Dictionary<string, Reach> _attributes = new(StringComparer.OrdinalIgnoreCase);

        // Selectors 4 §4.5: inside a descendant- or child-relative :has(), a name changed on an element can only change
        // which of that element and its ancestors the :has() matches, so only those carrying the anchor's key restyle.
        private readonly Dictionary<string, List<MeasurementAnchor>> _anchored = new(StringComparer.OrdinalIgnoreCase);
        private bool _relational;
        private bool _semantic;
        private bool _unknown;
        private CssInteractionDependencies _interactions;
        private CssInteractionDependencies _measurementInteractions;

        /// <summary>The interaction pseudo-classes any rule's selector names, including pseudo-element branches.</summary>
        internal CssInteractionDependencies Interactions => _unknown ? CssInteractionDependencies.All : _interactions;

        /// <summary>The interaction pseudo-classes the selectors of the rules that can change a measurement name.</summary>
        internal CssInteractionDependencies MeasurementInteractions => _unknown ? CssInteractionDependencies.All : _measurementInteractions;

        // No state a semantic pseudo-class reads (HTML §4.16.3) comes from an ARIA or a data attribute.
        internal bool RequiresGlobal(NativeMutationKind kind, string? name) =>
            _unknown || kind != NativeMutationKind.Attributes && (_relational || _semantic)
            || kind == NativeMutationKind.Attributes && (
                (name is null ? _attributes.Count != 0 || _anchored.Count != 0 : _attributes.GetValueOrDefault(name) == Reach.Global)
                || _semantic && !(name is "style" or "class"
                    || name is not null && (name.StartsWith("aria-", StringComparison.OrdinalIgnoreCase)
                        || name.StartsWith("data-", StringComparison.OrdinalIgnoreCase))));

        internal bool RequiresSiblings(NativeMutationKind kind, string? name) =>
            kind == NativeMutationKind.Attributes && name is not null && _attributes.GetValueOrDefault(name) == Reach.Siblings;

        internal List<MeasurementAnchor>? AnchorsOf(NativeMutationKind kind, string? name) =>
            kind == NativeMutationKind.Attributes && name is not null ? _anchored.GetValueOrDefault(name) : null;

        internal void IncludeUnknown() => _unknown = true;

        internal void Include(CompiledSelector selector, bool measurement, CssValueWork work)
        {
            var interactions = InteractionsOf(selector, work);
            _interactions |= interactions;
            if (!measurement) return;
            _measurementInteractions |= interactions;
            IncludeNames(selector, Reach.Subtree, null, work);
        }

        // A compound followed by a child or descendant combinator matches an ancestor of what the rest of the branch
        // matches, so a change to one of its names restyles the changed element's subtree alone; one followed by a
        // sibling combinator matches a preceding sibling, and the change reaches its following siblings' subtrees.
        private void IncludeNames(CompiledSelector program, Reach reach, MeasurementAnchor? anchor, CssValueWork work)
        {
            work.Charge(1);
            if (program.ContainsNesting) _unknown = true;
            foreach (var branch in program.BranchArray)
            {
                work.Charge(1);
                // These positive predicates can never match an element in the native matcher.
                // Do not turn harmless ::selection/::before rules into global invalidators.
                var impossible = false;
                foreach (var compound in branch.CompoundArray)
                {
                    foreach (var predicate in compound.PredicateArray)
                    {
                        work.Charge(1);
                        impossible |= predicate.Kind is PredicateKind.PseudoElement
                            or PredicateKind.WebkitUnknownPseudoElement or PredicateKind.Picker
                            or PredicateKind.Slotted or PredicateKind.Host;
                    }
                }
                if (impossible) continue;
                var branchReach = reach;
                if (branch.LeadingCombinator is not null)
                {
                    _relational = true;
                    branchReach = Reach.Global;
                }

                var compounds = branch.CompoundArray;
                for (var c = compounds.Length - 1; c >= 0; c--)
                {
                    work.Charge(1);
                    var compoundReach = branchReach;
                    if (c < compounds.Length - 1)
                    {
                        var combinator = branch.CombinatorArray[c];
                        if (combinator is not (Combinator.Child or Combinator.Descendant)) _relational = true;
                        var own = combinator switch
                        {
                            Combinator.Child or Combinator.Descendant => Reach.Subtree,
                            Combinator.NextSibling or Combinator.SubsequentSibling => Reach.Siblings,
                            _ => Reach.Global,
                        };
                        if (own > compoundReach) compoundReach = own;
                    }

                    foreach (var predicate in compounds[c].PredicateArray)
                    {
                        work.Charge(1);
                        if (predicate.IsNestingReference) _unknown = true;
                        switch (predicate.Kind)
                        {
                            case PredicateKind.Id: Name("id", compoundReach, anchor); break;
                            case PredicateKind.Class: Name("class", compoundReach, anchor); break;
                            case PredicateKind.Attribute: Name(predicate.Name!, compoundReach, anchor); break;
                            case PredicateKind.Is:
                            case PredicateKind.Where:
                            case PredicateKind.Not:
                                if (predicate.Arguments is { } nested) IncludeNames(nested, compoundReach, anchor, work);
                                else _unknown = true;
                                break;
                            case PredicateKind.Has:
                                _relational = true;
                                if (predicate.Arguments is not { } relative)
                                {
                                    _unknown = true;
                                }
                                else if (anchor is null && compoundReach != Reach.Global && IsDescendantRelative(relative)
                                    && SelectorSubjectKeys.AncestorKeyOf(compounds[c]) is { } key)
                                {
                                    IncludeNames(relative, Reach.Subtree, new MeasurementAnchor(key, compoundReach == Reach.Siblings), work);
                                }
                                else
                                {
                                    IncludeNames(relative, Reach.Global, null, work);
                                }

                                break;
                            case PredicateKind.Empty:
                            case PredicateKind.NthChild:
                            case PredicateKind.NthLastChild:
                            case PredicateKind.NthOfType:
                            case PredicateKind.NthLastOfType:
                            case PredicateKind.FirstChild:
                            case PredicateKind.LastChild:
                            case PredicateKind.OnlyChild:
                            case PredicateKind.FirstOfType:
                            case PredicateKind.LastOfType:
                            case PredicateKind.OnlyOfType:
                                // A position depends on the siblings, which only a child-list change moves; an
                                // `of S` argument depends on their names too.
                                _relational = true;
                                if (predicate.Arguments is { } positional) IncludeNames(positional, Reach.Global, anchor, work);
                                break;
                            case PredicateKind.Scope:
                            case PredicateKind.Root:
                            case PredicateKind.Focus:
                            case PredicateKind.FocusVisible:
                            case PredicateKind.Active:
                            case PredicateKind.Hover:
                            case PredicateKind.Target:
                                break; // Page's independent interaction/URL witnesses invalidate globally.
                            case PredicateKind.AnyLink:
                            case PredicateKind.Link:
                            case PredicateKind.Visited:
                                Name("href", compoundReach, anchor);
                                break;
                            case PredicateKind.Lang:
                            case PredicateKind.FocusWithin:
                            case PredicateKind.Dir:
                            case PredicateKind.Checked:
                            case PredicateKind.Unchecked:
                            case PredicateKind.Indeterminate:
                            case PredicateKind.Default:
                            case PredicateKind.Enabled:
                            case PredicateKind.Disabled:
                            case PredicateKind.Required:
                            case PredicateKind.Optional:
                            case PredicateKind.Valid:
                            case PredicateKind.Invalid:
                            case PredicateKind.InRange:
                            case PredicateKind.OutOfRange:
                            case PredicateKind.ReadOnly:
                            case PredicateKind.ReadWrite:
                            case PredicateKind.PlaceholderShown:
                            case PredicateKind.Open:
                            case PredicateKind.Closed:
                                _semantic = true;
                                break;
                            default:
                                _unknown = true;
                                break;
                        }
                    }
                }
            }
        }

        // Inside an anchored :has(), whatever the position, the elements a change rematches are the changed element's
        // ancestors and theirs: the anchor's key bounds them.
        private void Name(string attribute, Reach reach, MeasurementAnchor? anchor)
        {
            if (anchor is { } anchored)
            {
                if (!_anchored.TryGetValue(attribute, out var anchors)) _anchored[attribute] = anchors = [];
                if (!anchors.Contains(anchored)) anchors.Add(anchored);
                return;
            }

            if (reach > _attributes.GetValueOrDefault(attribute)) _attributes[attribute] = reach;
        }

        // Every branch looks below the anchor: a leading sibling combinator looks beside it, where an ancestor walk
        // from the changed element does not find the anchor.
        private static bool IsDescendantRelative(CompiledSelector relative)
        {
            foreach (var branch in relative.BranchArray)
            {
                if (branch.LeadingCombinator is not (null or Combinator.Descendant or Combinator.Child)) return false;
            }

            return true;
        }

        // Ordered: a name's reach is the widest of the positions it is named in.
        private enum Reach
        {
            Subtree,
            Siblings,
            Global,
        }

        // Selectors 4 §§11-12: :hover, :active, :focus, :focus-visible and :focus-within match against the
        // page's pointer and focus state. Every branch counts, a pseudo-element's too: a rule such as
        // a:hover::after never sizes a box, but getComputedStyle(a, '::after') reads it.
        private static CssInteractionDependencies InteractionsOf(CompiledSelector selector, CssValueWork work)
        {
            var interactions = CssInteractionDependencies.None;
            var pending = new Stack<CompiledSelector>();
            pending.Push(selector);
            while (pending.TryPop(out var program))
            {
                foreach (var branch in program.BranchArray)
                {
                    foreach (var compound in branch.CompoundArray)
                    {
                        foreach (var predicate in compound.PredicateArray)
                        {
                            work.Charge(1);
                            interactions |= predicate.Kind switch
                            {
                                PredicateKind.Hover => CssInteractionDependencies.Hover,
                                PredicateKind.Active => CssInteractionDependencies.Active,
                                PredicateKind.Focus or PredicateKind.FocusVisible or PredicateKind.FocusWithin => CssInteractionDependencies.Focus,
                                _ => CssInteractionDependencies.None,
                            };
                            if (predicate.Arguments is { } nested) pending.Push(nested);
                        }
                    }
                }
            }

            return interactions;
        }
    }
}

/// <summary>The page interaction state a document's style rules can observe.</summary>
[Flags]
internal enum CssInteractionDependencies
{
    None = 0,
    Hover = 1,
    Active = 2,
    Focus = 4,
    All = Hover | Active | Focus,
}
