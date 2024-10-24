namespace Jint.Runtime;

internal sealed class OrderedSet<T>
{
    internal List<T> _list;
    internal HashSet<T> _set;

    public OrderedSet(HashSet<T> values)
    {
        _list = new List<T>(values);
        _set = new HashSet<T>(values);
    }

    public OrderedSet(IEqualityComparer<T> comparer)
    {
        _list = new List<T>();
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
            _list = new List<T>(this._list)
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

    public int Count => _list.Count;

    public bool Remove(T item)
    {
        _set.Remove(item);
        return _list.Remove(item);
    }
}
