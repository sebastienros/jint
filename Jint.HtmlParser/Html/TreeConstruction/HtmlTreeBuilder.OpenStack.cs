using System.Collections;
using System.Collections.Generic;

namespace Jint.HtmlParser.Html;

internal sealed partial class HtmlTreeBuilder
{
    // HTML Standard §13.2.4.2, the stack of open elements, kept beside the name-index list of each
    // entry: a pop, a removal or a shift then reaches its element's positions without looking the
    // name up again, and every change to the stack moves both together.
    // The read-only list view is for tests that inspect the stack.
    private sealed class OpenElements : IReadOnlyList<Element>
    {
        private readonly List<Element> _elements = [];
        private readonly List<List<int>> _names = [];

        public int Count => _elements.Count;
        public Element this[int index] => _elements[index];
        internal List<int> NamesAt(int index) => _names[index];
        public IEnumerator<Element> GetEnumerator() => _elements.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => _elements.GetEnumerator();

        internal void Add(Element element, List<int> names)
        {
            _elements.Add(element);
            _names.Add(names);
        }

        internal void Set(int index, Element element, List<int> names)
        {
            _elements[index] = element;
            _names[index] = names;
        }

        internal void Move(int from, int to)
        {
            _elements[to] = _elements[from];
            _names[to] = _names[from];
        }

        internal void RemoveAt(int index)
        {
            _elements.RemoveAt(index);
            _names.RemoveAt(index);
        }

        internal void RemoveRange(int index, int count)
        {
            _elements.RemoveRange(index, count);
            _names.RemoveRange(index, count);
        }
    }
}
