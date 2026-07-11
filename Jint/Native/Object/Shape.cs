using System.Runtime.CompilerServices;

namespace Jint.Native.Object;

/// <summary>
/// A hidden class ("shape") describing the string-keyed own-property layout shared by all plain
/// objects with the same prototype that were given the same properties, in the same order, with the
/// same attributes. The values themselves live in a per-instance <c>JsValue[]</c> slot array on the
/// <see cref="ObjectInstance"/>; the shape only maps each property name to its slot index, so a
/// thousand <c>{a,b,c}</c> literals share one immutable <see cref="Shape"/> and allocate no
/// <see cref="Jint.Runtime.Descriptors.PropertyDescriptor"/> per property.
/// <para>
/// A shape is an immutable node in a transition tree: the root (per prototype) holds no property, and
/// each child adds exactly one property relative to its parent. Adding the same key to the same parent
/// always returns the same interned child, which is what lets objects of identical layout end up
/// referencing the very same <see cref="Shape"/> instance (the property that makes the inline cache
/// monomorphic across objects).
/// </para>
/// <para>
/// v1 scope: every shape property is a configurable + enumerable + writable data property. Anything
/// else (accessors, non-default attributes, deletes, freeze/seal, prototype changes) makes the owning
/// object fall back to the legacy dictionary representation via
/// <see cref="ObjectInstance.ConvertToDictionaryMode"/>.
/// </para>
/// </summary>
internal sealed class Shape
{
    // Below this own-property count, key lookup walks the parent chain (a hash compare + ordinal
    // compare per step) which is cheaper than a dictionary probe for the dominant small objects.
    // At or above it, a lazily-built key->slot index is used instead.
    private const int LinearScanLimit = 16;

    // Megamorphic guards for incremental shape growth (constructor this.x= via TryShapeAdd). An object
    // that grows past MaxShapeProperties, or a shape that sprouts more than MaxFanout distinct child
    // transitions (the object-used-as-a-hashmap pattern), deopts to the dictionary representation so the
    // shared per-prototype transition tree cannot grow without bound. Ordinary constructors never hit these.
    internal const int MaxShapeProperties = 64;
    internal const int MaxFanout = 64;

    /// <summary>Parent shape (one fewer property), or null for the empty root shape.</summary>
    internal readonly Shape? Parent;

    /// <summary>The single property added relative to <see cref="Parent"/> (default for the root).</summary>
    internal readonly Key AddedKey;

    /// <summary>Number of own string properties (= slot count). The added property lives at slot <c>SlotCount - 1</c>.</summary>
    internal readonly int SlotCount;

    /// <summary>Number of distinct add-property transitions out of this shape (fan-out).</summary>
    internal int TransitionCount => _transitions?.Count ?? 0;

    // Add-property transitions: key -> child shape. Lazily allocated; this is what interns layouts.
    private Dictionary<Key, Shape>? _transitions;

    // Lazily-built key -> slot index, only for wide shapes (SlotCount >= LinearScanLimit).
    private Dictionary<Key, int>? _index;

    // Slot-ordered keys, memoized on first use (shapes are immutable, so the layout never changes)
    // and shared by every object of this layout — e.g. the JSON serializer's shaped fast path reads
    // property names from here without building a per-object key list.
    private Key[]? _orderedKeys;

    /// <summary>Creates an empty root shape. There is one root per prototype, interned by the engine's
    /// empty-shape table, so all objects sharing a prototype build their layouts from the same tree.</summary>
    internal Shape()
    {
        Parent = null;
        AddedKey = default;
        SlotCount = 0;
    }

    private Shape(Shape parent, in Key addedKey)
    {
        Parent = parent;
        AddedKey = addedKey;
        SlotCount = parent.SlotCount + 1;
    }

    /// <summary>
    /// Returns the interned child shape that adds <paramref name="key"/> to this shape. Repeated calls
    /// with the same key return the same instance, so identical layouts share their whole chain.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal Shape Add(in Key key) => Add(key, out _);

    /// <summary>
    /// Same as <see cref="Add(in Key)"/>, additionally reporting whether a new transition node was
    /// interned (<paramref name="created"/>) or an already-memoized child was reused. Callers that
    /// bound transition-tree growth (JSON parsing) charge their budget only for newly created nodes.
    /// </summary>
    internal Shape Add(in Key key, out bool created)
    {
        var transitions = _transitions ??= new Dictionary<Key, Shape>();
        if (!transitions.TryGetValue(key, out var child))
        {
            child = new Shape(this, key);
            transitions[key] = child;
            created = true;
        }
        else
        {
            created = false;
        }

        return child;
    }

    /// <summary>
    /// Resolves a property name to its slot index. Returns false if this shape does not carry the key.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal bool TryGetSlot(in Key key, out int slotIndex)
    {
        if (SlotCount < LinearScanLimit)
        {
            // Walk from the leaf (newest property) up to the root. The Key's precomputed hash makes
            // each comparison an int compare plus an ordinal string compare.
            for (var shape = this; shape.Parent is not null; shape = shape.Parent)
            {
                if (shape.AddedKey == key)
                {
                    slotIndex = shape.SlotCount - 1;
                    return true;
                }
            }

            slotIndex = -1;
            return false;
        }

        return (_index ??= BuildIndex()).TryGetValue(key, out slotIndex);
    }

    private Dictionary<Key, int> BuildIndex()
    {
        var index = new Dictionary<Key, int>(SlotCount);
        for (var shape = this; shape.Parent is not null; shape = shape.Parent)
        {
            // Shape-mode objects only ever carry distinct keys (literals are built from distinct keys;
            // post-construction adds deopt), so each key maps to exactly one slot.
            index[shape.AddedKey] = shape.SlotCount - 1;
        }

        return index;
    }

    /// <summary>
    /// Fills <paramref name="dest"/> (length &gt;= <see cref="SlotCount"/>) with this shape's keys in
    /// slot (= insertion) order.
    /// </summary>
    internal void CollectKeys(Span<Key> dest)
    {
        for (var shape = this; shape.Parent is not null; shape = shape.Parent)
        {
            dest[shape.SlotCount - 1] = shape.AddedKey;
        }
    }

    /// <summary>
    /// This shape's keys in slot (= insertion) order, computed once and shared by all objects of this
    /// layout. Callers must treat the array as read-only.
    /// </summary>
    internal Key[] OrderedKeys
    {
        get
        {
            if (_orderedKeys is null)
            {
                var keys = new Key[SlotCount];
                CollectKeys(keys);
                _orderedKeys = keys;
            }

            return _orderedKeys;
        }
    }
}
