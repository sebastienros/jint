using System.Collections.ObjectModel;
using Jint.HtmlParser.Css.Selectors;

namespace Jint.HtmlParser.Css;

internal sealed class SelectorParseContext
{
    internal SelectorParseContext(
        IEnumerable<KeyValuePair<string, string>>? namespaceBindings = null,
        ParseLimits? limits = null, CompiledSelector? nestingParent = null, bool scopedStyle = false)
    {
        ScopedStyle = scopedStyle && nestingParent is not null;
        var bindings = new Dictionary<string, string>(StringComparer.Ordinal);
        if (namespaceBindings is not null)
        {
            foreach (var binding in namespaceBindings)
            {
                if (binding.Key is null || binding.Value is null)
                {
                    throw new ArgumentException("Namespace keys and values must not be null.", nameof(namespaceBindings));
                }
                if (!bindings.TryAdd(binding.Key, binding.Value))
                {
                    throw new ArgumentException("Namespace prefixes must be unique.", nameof(namespaceBindings));
                }
            }
        }

        NamespaceBindings = new ReadOnlyDictionary<string, string>(bindings);
        Limits = limits ?? ParseLimits.Unbounded;
        NestingParent = nestingParent;
    }

    internal IReadOnlyDictionary<string, string> NamespaceBindings { get; }
    internal ParseLimits Limits { get; }
    internal CompiledSelector? NestingParent { get; }

    // CSS Cascade 6 §3.5.3: in a scoped style rule (or <scope-end>), :scope as well as & makes a
    // selector non-relative, so it is not prefixed with the implied `:where(:scope) `.
    internal bool ScopedStyle { get; }

    internal static SelectorParseContext For(ParseLimits? limits, Model.CssStyleRule? nestingParent) =>
        new(limits: limits, nestingParent: nestingParent?.Selector, scopedStyle: nestingParent?.IsScopeAnchor == true);
}
