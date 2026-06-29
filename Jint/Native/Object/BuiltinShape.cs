using Jint.Collections;
using Jint.Runtime.Descriptors;

namespace Jint.Native.Object;

/// <summary>
/// Immutable, process-shared description of a built-in object's string-keyed own-property layout
/// (names, attributes, and how each slot is produced), built once per built-in type. Paired with a
/// per-realm value array on <see cref="BuiltinShapeObject"/>, it replaces the per-realm dictionary of
/// descriptors a built-in would otherwise allocate in <c>Initialize</c>. Symbols are orthogonal and stay
/// in the object's symbol dictionary.
/// </summary>
internal sealed class BuiltinShape
{
    internal const ushort NotAFunction = ushort.MaxValue;

    // Property names in own-property (insertion) order.
    internal readonly Key[] Names;
    // Per slot: the process-shared descriptor for an immutable constant, or null for a function slot
    // (filled lazily per realm). Cloned into each instance as the starting per-realm descriptor array.
    internal readonly PropertyDescriptor?[] ConstTemplate;
    // Per slot: the dispatcher slot id to materialize for a function, or NotAFunction for a constant.
    internal readonly ushort[] FunctionSlots;
    // Per slot: the attributes for a function slot (unused for constants — those carry their own flags).
    internal readonly PropertyFlag[] FunctionFlags;
    // name -> slot index.
    internal readonly StringDictionarySlim<int> Index;

    internal int Count => Names.Length;

    private BuiltinShape(Key[] names, PropertyDescriptor?[] constTemplate, ushort[] functionSlots, PropertyFlag[] functionFlags, StringDictionarySlim<int> index)
    {
        Names = names;
        ConstTemplate = constTemplate;
        FunctionSlots = functionSlots;
        FunctionFlags = functionFlags;
        Index = index;
    }

    /// <summary>
    /// Accumulates a built-in's entries (constants first, then functions, matching the order a generated
    /// property dictionary would use) and produces the shared <see cref="BuiltinShape"/>.
    /// </summary>
    internal sealed class Builder
    {
        private readonly Key[] _names;
        private readonly PropertyDescriptor?[] _constTemplate;
        private readonly ushort[] _functionSlots;
        private readonly PropertyFlag[] _functionFlags;
        private readonly StringDictionarySlim<int> _index;
        private int _next;

        internal Builder(int capacity)
        {
            _names = new Key[capacity];
            _constTemplate = new PropertyDescriptor?[capacity];
            _functionSlots = new ushort[capacity];
            _functionFlags = new PropertyFlag[capacity];
            _index = new StringDictionarySlim<int>(capacity);
            _next = 0;
        }

        internal void Constant(in Key name, PropertyDescriptor descriptor)
        {
            _names[_next] = name;
            _constTemplate[_next] = descriptor;
            _functionSlots[_next] = NotAFunction;
            _index[name] = _next;
            _next++;
        }

        internal void Function(in Key name, ushort slot, PropertyFlag flags)
        {
            _names[_next] = name;
            _constTemplate[_next] = null;
            _functionSlots[_next] = slot;
            _functionFlags[_next] = flags;
            _index[name] = _next;
            _next++;
        }

        internal BuiltinShape Build() => new(_names, _constTemplate, _functionSlots, _functionFlags, _index);
    }
}
