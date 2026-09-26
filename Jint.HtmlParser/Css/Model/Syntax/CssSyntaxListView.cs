using System.Collections;

namespace Jint.HtmlParser.Css.Model.Syntax;

internal sealed class CssSyntaxListView<T> : IReadOnlyList<T>
{
    private List<T> _items;

    internal CssSyntaxListView(List<T> items) => _items = items;

    public int Count => _items.Count;
    public T this[int index] => _items[index];
    public IEnumerator<T> GetEnumerator() => _items.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    internal void ReplaceItems(List<T> items) => _items = items;
}
