using System.Collections;

namespace Jint.Runtime;

internal sealed class OrderedSet<T> : IEnumerable<T>
{
    internal List<T> _list;
    internal HashSet<T> _set;

    public OrderedSet(HashSet<T> values)
    {
        _list = new List<T>(values);
        // carry the source comparer over: the copy has to keep answering equality the same way,
        // otherwise a derived set silently falls back to default equality
        _set = new HashSet<T>(values, values.Comparer);
    }

    public OrderedSet(IEqualityComparer<T> comparer)
    {
        _list = [];
        _set = new HashSet<T>(comparer);
    }

    public T this[int index]
    {
        get => _list[index];
        set
        {
            if (_set.Add(value))
            {
                _list[index] = value;
            }
        }
    }

    public OrderedSet<T> Clone()
    {
        return new OrderedSet<T>(EqualityComparer<T>.Default)
        {
            _set = new HashSet<T>(this._set, this._set.Comparer),
            _list = [.. this._list]
        };
    }

    public void Add(T item)
    {
        if (_set.Add(item))
        {
            _list.Add(item);
        }
    }

    public void Clear()
    {
        _list.Clear();
        _set.Clear();
    }

    public bool Contains(T item) => _set.Contains(item);

    /// <summary>
    /// Position of <paramref name="item"/> in insertion order, or -1. Scans with the set's own
    /// comparer: <see cref="List{T}.IndexOf(T)"/> would use the default one, which for
    /// <c>JsValue</c> reports NaN as equal to nothing at all and so contradicts
    /// <see cref="Contains"/> for a value the set demonstrably holds.
    /// </summary>
    public int IndexOf(T item)
    {
        var comparer = _set.Comparer;
        var list = _list;
        for (var i = 0; i < list.Count; i++)
        {
            if (comparer.Equals(list[i], item))
            {
                return i;
            }
        }

        return -1;
    }

    public int Count => _list.Count;

    public bool Remove(T item)
    {
        if (!_set.Remove(item))
        {
            return false;
        }

        // List.Remove would compare with the default comparer, which can disagree with the set's
        // one and leave the ordering list holding a value the set no longer has
        var index = IndexOf(item);
        if (index >= 0)
        {
            _list.RemoveAt(index);
        }

        return true;
    }

    public IEnumerator<T> GetEnumerator() => _list.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
