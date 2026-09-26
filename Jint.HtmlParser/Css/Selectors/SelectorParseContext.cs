using System.Collections.ObjectModel;
using Jint.HtmlParser.Css.Selectors;

namespace Jint.HtmlParser.Css;

internal sealed class SelectorParseContext
{
    internal SelectorParseContext(
        IEnumerable<KeyValuePair<string, string>>? namespaceBindings = null,
        ParseLimits? limits = null, CompiledSelector? nestingParent = null)
    {
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
}
