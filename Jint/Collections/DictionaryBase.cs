using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace Jint.Collections;

internal abstract class DictionaryBase<TValue> : IEngineDictionary<Key, TValue>
{
    public ref TValue this[Key key]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => ref GetValueRefOrAddDefault(key, out _);
    }

    public bool TryGetValue(Key key, [NotNullWhen(true)] out TValue? value)
    {
        value = default;
        ref var temp = ref GetValueRefOrNullRef(key);
        if (Unsafe.IsNullRef(ref temp))
        {
            return false;
        }

        value = temp!;
        return true;
    }

    public bool ContainsKey(Key key)
    {
        ref var valueRefOrNullRef = ref GetValueRefOrNullRef(key);
        return !Unsafe.IsNullRef(ref valueRefOrNullRef);
    }

    public abstract int Count { get; }

    public abstract ref TValue GetValueRefOrNullRef(Key key);
    public abstract ref TValue GetValueRefOrAddDefault(Key key, out bool exists);
}
