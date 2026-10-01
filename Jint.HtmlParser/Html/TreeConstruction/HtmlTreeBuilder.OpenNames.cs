using System.Collections.Generic;

namespace Jint.HtmlParser.Html;

internal sealed partial class HtmlTreeBuilder
{
    // Stack positions of each open element name, ascending. Nearly every lookup is an HTML name,
    // so those use a string-keyed map: its default comparer starts with the non-randomized string
    // hash and switches to randomized hashing itself once collisions pile up, which keeps crafted
    // names from degrading it. A (namespace, name) tuple key would pay randomized hashing always.
    private sealed class OpenNameIndex
    {
        private readonly Dictionary<string, List<int>> _html = [];
        private Dictionary<(string? Namespace, string Name), List<int>>? _foreign;

        internal List<int>? Html(string name) => _html.TryGetValue(name, out var indexes) ? indexes : null;

        internal List<int> For(Element element) => element.NamespaceUri == Namespaces.Html
            ? _html[element.LocalName]
            : _foreign![(element.NamespaceUri, element.LocalName)];

        // An emptied list is kept: the same name reopens constantly and would reallocate it.
        internal List<int> GetOrAdd(Element element)
        {
            if (element.NamespaceUri == Namespaces.Html)
            {
                if (!_html.TryGetValue(element.LocalName, out var html)) _html.Add(element.LocalName, html = []);
                return html;
            }
            _foreign ??= [];
            var key = (element.NamespaceUri, element.LocalName);
            if (!_foreign.TryGetValue(key, out var foreign)) _foreign.Add(key, foreign = []);
            return foreign;
        }
    }
}
