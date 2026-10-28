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

    internal bool RequiresGlobalMeasurements(NativeMutationKind kind, string? attributeName)
    {
        PrepareRules();
        return _plan!.Dependencies.RequiresGlobal(kind, attributeName);
    }

    // Only the sheet-dependent plan survives DOM mutations. Matching, inline declarations,
    // computed values and candidate de-duplication remain owned by each new query.
    private sealed record RulePlan(NativeCssSheet[] Sheets, CssStyleSheetRevisionSnapshot Revisions,
        CssMediaEnvironment Media, ShadowRoot?[] Scopes,
        List<(CssStyleRule Rule, NativeCssOrigin Origin, long Order, string? NamespaceUri, NativeCssLayer Layer)> Rules,
        RuleIndex Index, MeasurementDependencies Dependencies);

    private void PrepareRules()
    {
        if (_rules is not null) return;
        if (Plans.TryGetValue(_document, out var cached) && PlanIsCurrent(cached))
        {
            Verify();
            _rules = cached.Rules;
            _index = cached.Index;
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
            foreach (var item in rules)
                dependencies.Include(item.Rule.Selector, _work);
            _plan = new(_sheets, _sheetRevisions, _media, scopes, rules, index, dependencies);
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
    private sealed class MeasurementDependencies
    {
        private readonly HashSet<string> _attributes = new(StringComparer.OrdinalIgnoreCase);
        private bool _relational;
        private bool _semantic;
        private bool _unknown;

        internal bool RequiresGlobal(NativeMutationKind kind, string? name) =>
            _unknown || kind != NativeMutationKind.Attributes && (_relational || _semantic)
            || kind == NativeMutationKind.Attributes && (
                _relational && (name is null || _attributes.Contains(name))
                || _semantic && name is not ("style" or "class"));

        internal void Include(CompiledSelector selector, CssValueWork work)
        {
            var pending = new Stack<CompiledSelector>();
            pending.Push(selector);
            var attributes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var relational = false;
            var semantic = false;
            while (pending.TryPop(out var program))
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
                    if (branch.LeadingCombinator is not null) relational = true;
                    foreach (var combinator in branch.CombinatorArray)
                    {
                        work.Charge(1);
                        if (combinator is not (Combinator.Child or Combinator.Descendant)) relational = true;
                    }
                    foreach (var compound in branch.CompoundArray)
                    {
                        foreach (var predicate in compound.PredicateArray)
                        {
                            work.Charge(1);
                            if (predicate.IsNestingReference) _unknown = true;
                            switch (predicate.Kind)
                            {
                                case PredicateKind.Id: attributes.Add("id"); break;
                                case PredicateKind.Class: attributes.Add("class"); break;
                                case PredicateKind.Attribute: attributes.Add(predicate.Name!); break;
                                case PredicateKind.Is:
                                case PredicateKind.Where:
                                case PredicateKind.Not:
                                    if (predicate.Arguments is { } nested) pending.Push(nested);
                                    else _unknown = true;
                                    break;
                                case PredicateKind.Has:
                                    relational = true;
                                    if (predicate.Arguments is { } relative) pending.Push(relative);
                                    else _unknown = true;
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
                                    relational = true;
                                    if (predicate.Arguments is { } positional) pending.Push(positional);
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
                                    attributes.Add("href");
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
                                    semantic = true;
                                    break;
                                default:
                                    _unknown = true;
                                    break;
                            }
                        }
                    }
                }
            }
            if (relational) _attributes.UnionWith(attributes);
            _relational |= relational;
            _semantic |= semantic;
        }
    }
}
