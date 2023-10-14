namespace Jint.Tests.TestClasses;

public class IndexedProperty<TIndex, TValue>
{
    Action<TIndex, TValue> Setter { get; }
    Func<TIndex, TValue> Getter { get; }

    public IndexedProperty(Func<TIndex, TValue> getter, Action<TIndex, TValue> setter)
    {
        Getter = getter;
        Setter = setter;
    }

    public TValue this[TIndex i]
    {
        get => Getter(i);
        set => Setter(i, value);
    }
}

public class IndexedPropertyReadOnly<TIndex, TValue>
{
    Func<TIndex, TValue> Getter { get; }

    public IndexedPropertyReadOnly(Func<TIndex, TValue> getter)
    {
        Getter = getter;
    }

    public TValue this[TIndex i]
    {
        get => Getter(i);
    }
}

public class IndexedPropertyWriteOnly<TIndex, TValue>
{
    Action<TIndex, TValue> Setter { get; }

    public IndexedPropertyWriteOnly(Action<TIndex, TValue> setter)
    {
        Setter = setter;
    }

    public TValue this[TIndex i]
    {
        set => Setter(i, value);
    }
}
