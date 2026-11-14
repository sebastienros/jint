using Jint.HtmlParser;
using Jint.HtmlParser.Css;
using Jint.HtmlParser.Css.Model;
using Jint.HtmlParser.Css.Selectors;

namespace Jint.Browser.Styling;

// CSS Cascade 6 §3.5 https://drafts.csswg.org/css-cascade-6/#scoped-styles
// A candidate scoping root is identified by its position on the subject's inclusive-ancestor path in the
// subject's own node tree; position == path length is the tree's root (the document, or the shadow host
// seen as the featureless shadow root). That position is also the generational scope proximity (§3.6).
internal sealed partial class NativeCssQuery
{
    private Dictionary<CssStyleRule, CssScopeRule[]>? _scopeChains;
    private Dictionary<(CssScopeRule Scope, Element Subject), int[]>? _scopeRoots;
    private Dictionary<Element, List<Element>>? _scopePaths;

    private bool TryMatchScoped(Element element, CssStyleRule rule, CssScopeRule[] chain, ShadowRoot? shadow,
        ref SelectorMatchWork matching, out SelectorSpecificity specificity, out int proximity)
    {
        specificity = default;
        proximity = int.MaxValue;
        var path = ScopePath(element);
        foreach (var root in ScopeRoots(element, chain, path, shadow, ref matching))
        {
            _work.Charge(1);
            if (!rule.TryMatch(element, out specificity, ScopeNode(path, root, shadow), _selectors, ref matching, shadow))
                continue;
            proximity = root;
            return true;
        }
        specificity = default;
        return false;
    }

    // The subject's inclusive ancestors within its node tree, nearest first. Shared by every scoped
    // candidate rule of the subject.
    private List<Element> ScopePath(Element element)
    {
        _scopePaths ??= new();
        if (_scopePaths.TryGetValue(element, out var cached)) return cached;
        var path = new List<Element>();
        for (Node? node = element; node is Element current; node = current.ParentNode)
        {
            _work.Charge(1);
            path.Add(current);
        }
        _scopePaths.Add(element, path);
        return path;
    }

    // The innermost rule's scoping roots that keep the subject in scope, nearest first. The subject
    // is out of a root's scope at or below a limit (§3.5.4). A nested @scope's root is an inclusive
    // descendant of an outer root, and the subject must stay in scope at every level (§3.5.5: the
    // result is constrained by both rules). Shared by every scoped rule of one @scope block.
    private int[] ScopeRoots(Element element, CssScopeRule[] chain, List<Element> path, ShadowRoot? shadow,
        ref SelectorMatchWork matching)
    {
        var innermost = chain[^1];
        _scopeRoots ??= new();
        if (_scopeRoots.TryGetValue((innermost, element), out var cached)) return cached;
        List<int>? previous = null;
        for (var level = 0; level < chain.Length; level++)
        {
            var scope = chain[level];
            var current = new List<int>();
            if (scope.StartSelector is not { } start)
            {
                if (previous is not null) current.AddRange(previous);
                else if (ImplicitRoot(scope, path) is { } implicitRoot) current.Add(implicitRoot);
            }
            else
            {
                for (var position = 0; position < path.Count; position++)
                {
                    _work.Charge(1);
                    if (previous is null)
                    {
                        if (SelectorMatcher.TryMatch(start, path[position], out _, ScopeNode(path, path.Count, shadow),
                            _selectors, ref matching, shadow))
                            current.Add(position);
                        continue;
                    }
                    // The subject is in each outer root's scope, so is every ancestor below that root.
                    foreach (var outer in previous)
                    {
                        _work.Charge(1);
                        if (position > outer) continue;
                        if (!SelectorMatcher.TryMatch(start, path[position], out _, ScopeNode(path, outer, shadow),
                            _selectors, ref matching, shadow))
                            continue;
                        current.Add(position);
                        break;
                    }
                }
            }
            var inScope = new List<int>(current.Count);
            foreach (var root in current)
            {
                _work.Charge(1);
                if (InScope(path, 0, root, scope, shadow, ref matching)) inScope.Add(root);
            }
            previous = inScope;
            if (inScope.Count == 0) break;
        }
        previous!.Sort();
        var result = previous.ToArray();
        _scopeRoots.Add((innermost, element), result);
        return result;
    }

    // §3.5.4: without <scope-start>, the parent element of the owner node; in a shadow tree with no
    // such element, the shadow host; otherwise the root of the containing node tree.
    private int? ImplicitRoot(CssScopeRule scope, List<Element> path)
    {
        var owner = scope.ParentStyleSheet?.EffectiveOwnerNode(_work);
        if (owner?.ParentNode is not Element parent) return path.Count;
        var position = path.IndexOf(parent);
        _work.Charge(position < 0 ? path.Count : position + 1);
        return position < 0 ? null : position;
    }

    // An element at `position` is in the scope rooted at `root` unless it, or an ancestor strictly
    // below the root, is a scoping limit. <scope-end> is relative to the root, as compiled.
    private bool InScope(List<Element> path, int position, int root, CssScopeRule scope, ShadowRoot? shadow,
        ref SelectorMatchWork matching)
    {
        if (position > root) return false;
        if (scope.EndSelector is not { } end) return true;
        var scopingRoot = ScopeNode(path, root, shadow);
        for (var i = position; i < root; i++)
        {
            _work.Charge(1);
            if (SelectorMatcher.TryMatch(end, path[i], out _, scopingRoot, _selectors, ref matching, shadow)) return false;
        }
        return true;
    }

    // The node :scope matches for a root position. A shadow tree's root is its shadow root, which the
    // matcher then treats as the featureless shadow host (§3.5.1).
    private Node? ScopeNode(List<Element> path, int position, ShadowRoot? shadow) =>
        position < path.Count ? path[position] : shadow ?? (Node) _document;
}
