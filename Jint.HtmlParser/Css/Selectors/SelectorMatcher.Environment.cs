using System.Collections.ObjectModel;
using static Jint.HtmlParser.Css.Selectors.CompiledSelector;

namespace Jint.HtmlParser.Css.Selectors;

internal static partial class SelectorMatcher
{
    internal static bool Matches(CompiledSelector program, Element element, Node? scopingRoot,
        in SelectorEnvironment environment, ref SelectorMatchWork work)
        => TryMatch(program, element, out _, scopingRoot, environment, ref work);

    internal static bool TryMatch(CompiledSelector program, Element element, out SelectorSpecificity specificity,
        Node? scopingRoot, in SelectorEnvironment environment, ref SelectorMatchWork work, ShadowRoot? shadowScope = null)
    {
        ArgumentNullException.ThrowIfNull(program);
        ArgumentNullException.ThrowIfNull(element);
        work.Enter(element, shadowScope ?? scopingRoot, environment);
        try
        {
            work.VerifyRead();
            var local = new Work(ref work, environment, shadowScope);
            ValidateImplemented(program, ref local, work.Token);
            var matched = TryMatchCore(program, element, ScopeFor(shadowScope ?? scopingRoot ?? element, ref local),
                ref local, out specificity);
            work.VerifyRead();
            return matched;
        }
        finally { work.Exit(); }
    }

    internal static Element? Closest(CompiledSelector program, Element element,
        in SelectorEnvironment environment, ref SelectorMatchWork work)
    {
        ArgumentNullException.ThrowIfNull(program);
        ArgumentNullException.ThrowIfNull(element);
        work.Enter(element, null, environment);
        try
        {
            work.VerifyRead();
            var local = new Work(ref work, environment);
            ValidateImplemented(program, ref local, work.Token);
            for (Node? current = element; current is not null; current = current.ParentNode)
            {
                work.Step();
                if (current is Element candidate && TryMatchCore(program, candidate, element, ref local, out _))
                {
                    work.VerifyRead();
                    return candidate;
                }
            }
            work.VerifyRead();
            return null;
        }
        finally { work.Exit(); }
    }

    internal static Element? QuerySelector(CompiledSelector program, Node root,
        in SelectorEnvironment environment, ref SelectorMatchWork work)
    {
        ArgumentNullException.ThrowIfNull(program);
        ArgumentNullException.ThrowIfNull(root);
        work.Enter(root, null, environment);
        try
        {
            work.VerifyRead();
            var local = new Work(ref work, environment);
            ValidateImplemented(program, ref local, work.Token);
            var scope = ScopeFor(root, ref local);
            for (var node = root.FirstChild; node is not null; node = NextWithin(root, node, ref local))
            {
                work.Step();
                if (node is Element candidate && TryMatchCore(program, candidate, scope, ref local, out _))
                {
                    work.VerifyRead();
                    return candidate;
                }
            }
            work.VerifyRead();
            return null;
        }
        finally { work.Exit(); }
    }

    internal static IReadOnlyList<Element> QuerySelectorAll(CompiledSelector program, Node root,
        in SelectorEnvironment environment, ref SelectorMatchWork work)
    {
        ArgumentNullException.ThrowIfNull(program);
        ArgumentNullException.ThrowIfNull(root);
        work.Enter(root, null, environment);
        try
        {
            work.VerifyRead();
            var local = new Work(ref work, environment);
            ValidateImplemented(program, ref local, work.Token);
            var scope = ScopeFor(root, ref local);
            var results = new List<Element>();
            for (var node = root.FirstChild; node is not null; node = NextWithin(root, node, ref local))
            {
                work.Step();
                if (node is Element candidate && TryMatchCore(program, candidate, scope, ref local, out _))
                {
                    work.Step();
                    results.Add(candidate);
                }
            }
            var snapshot = new Element[results.Count];
            for (var i = 0; i < snapshot.Length; i++)
            {
                work.Step();
                snapshot[i] = results[i];
            }
            work.VerifyRead();
            work.Check();
            return new ReadOnlyCollection<Element>(snapshot);
        }
        finally { work.Exit(); }
    }

    // HTML §4.15 and Selectors §9: https://html.spec.whatwg.org/multipage/semantics-other.html#pseudo-classes
    // https://drafts.csswg.org/selectors-4/#useraction-pseudos
    private static bool MatchEnvironment(PredicateKind kind, Element element, ref Work work)
    {
        var environment = work.Environment;
        if (environment.Document is not { } document) return false;
        var cell = work.Shared.EnsureCell();
        work.Shared.Observe(document);
        switch (kind)
        {
            case PredicateKind.Target:
                if (!cell.TargetResolved)
                {
                    if (environment.TargetElement is { } target)
                    {
                        work.Shared.Observe(target);
                        if (ReferenceEquals(SelectorStateTraversal.OrdinaryRoot(target, ref work.Shared), document))
                            cell.Target = target;
                    }
                    cell.TargetResolved = true;
                }
                work.Step();
                return ReferenceEquals(element, cell.Target);
            case PredicateKind.Focus:
            case PredicateKind.FocusWithin:
                if (cell.Focus is null)
                    SelectorStateTraversal.BuildFocus(environment, ref work.Shared);
                if (kind == PredicateKind.FocusWithin && cell.FocusWithin is null)
                {
                    var within = new HashSet<Element>(ReferenceEqualityComparer.Instance);
                    foreach (var focused in cell.Focus!)
                    {
                        work.Step();
                        within.Add(focused);
                    }
                    if (environment.FocusedElement is { } seed &&
                        SelectorStateTraversal.Connected(seed, document, ref work.Shared))
                        SelectorStateTraversal.AddFlatAncestors(seed, document, within, ref work.Shared);
                    cell.FocusWithin = within;
                }
                work.Step();
                return (kind == PredicateKind.Focus ? cell.Focus! : cell.FocusWithin!).Contains(element);
            case PredicateKind.Active:
                if (cell.ActiveElements is null)
                    SelectorStateTraversal.BuildActive(environment, ref work.Shared);
                work.Step();
                return cell.ActiveElements!.Contains(element);
            default:
                throw Unsupported(kind.ToString());
        }
    }
}
