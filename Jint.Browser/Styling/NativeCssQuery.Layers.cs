using Jint.HtmlParser;
using Jint.HtmlParser.Css.Model;
using Jint.HtmlParser.Css.Values;

namespace Jint.Browser.Styling;

// Query-owned layer identities. Nested layers precede their parent's implicit final sublayer.
internal sealed class NativeCssLayer
{
    private readonly Dictionary<string, NativeCssLayer> _named = new(StringComparer.Ordinal);
    internal List<NativeCssLayer> Children { get; } = [];
    internal int Rank { get; set; }

    internal NativeCssLayer Declare(CssLayerName? name, CssValueWork work)
    {
        if (name is null)
        {
            var anonymous = new NativeCssLayer();
            Children.Add(anonymous);
            return anonymous;
        }
        var current = this;
        foreach (var segment in name.Segments)
        {
            work.Charge(segment.Length + 1);
            if (!current._named.TryGetValue(segment, out var child))
            {
                child = new NativeCssLayer();
                current._named.Add(segment, child);
                current.Children.Add(child);
            }
            current = child;
        }
        return current;
    }
}

internal sealed partial class NativeCssQuery
{
    // https://drafts.csswg.org/css-cascade-5/#layer-ordering
    private List<(CssStyleRule Rule, NativeCssOrigin Origin, long Order, string? NamespaceUri, NativeCssLayer Layer)> BuildRules()
    {
        var rules = new List<(CssStyleRule, NativeCssOrigin, long, string?, NativeCssLayer)>();
        var roots = new Dictionary<(NativeCssOrigin, Node?), NativeCssLayer>();
        long order = 0;
        foreach (var input in _sheets)
        {
            _work.Charge(1);
            var key = (input.Origin, (Node?) input.Sheet.EffectiveOwnerNode(_work)?.TreeShadowRoot);
            if (!roots.TryGetValue(key, out var root))
            {
                root = new NativeCssLayer();
                roots.Add(key, root);
            }
            var contexts = new Dictionary<CssRule, NativeCssLayer>();
            foreach (var rule in input.Sheet.ApplicableRules(_media, _work))
            {
                _work.Charge(1);
                var layer = rule.ParentRule is { } parent && contexts.TryGetValue(parent, out var inherited) ? inherited : root;
                switch (rule)
                {
                    case CssLayerBlockRule block:
                        layer = layer.Declare(block.LayerName, _work);
                        break;
                    case CssLayerStatementRule statement:
                        foreach (var name in statement.Names) layer.Declare(name, _work);
                        break;
                    case CssStyleRule style:
                        rules.Add((style, input.Origin, order++, input.NamespaceUri, layer));
                        break;
                }
                contexts.Add(rule, layer);
            }
        }
        foreach (var root in roots.Values)
        {
            var stack = new Stack<(NativeCssLayer Layer, bool Expanded)>();
            stack.Push((root, false));
            var rank = 0;
            while (stack.TryPop(out var item))
            {
                _work.Charge(1);
                if (item.Expanded) item.Layer.Rank = rank++;
                else
                {
                    stack.Push((item.Layer, true));
                    for (var i = item.Layer.Children.Count - 1; i >= 0; i--)
                    {
                        _work.Charge(1);
                        stack.Push((item.Layer.Children[i], false));
                    }
                }
            }
        }
        Verify();
        return rules;
    }

    // Roll back the whole interval between this layer's normal and important levels.
    // Element-attached declarations form a separate rollback boundary.
    private bool LayerExcluded(NativeCssSource candidate, List<NativeCssSource>? rollbacks)
    {
        if (rollbacks is null) return false;
        foreach (var source in rollbacks)
        {
            _work.Charge(1);
            if (candidate.Origin > source.Origin) return true;
            if (candidate.Origin != source.Origin) continue;
            if (candidate.Inline || !source.Inline && candidate.Layer!.Rank >= source.Layer!.Rank) return true;
        }
        return false;
    }
}
