using System.Runtime.CompilerServices;
using Jint.Native.Object;
using Jint.Runtime;

namespace Jint.Native;

/// <summary>
/// An immutable description of a fixed own-property layout — a set of property names in a fixed order —
/// that host code can hand to <see cref="JsObject.Create(Engine, JsObjectLayout, ReadOnlySpan{JsValue})"/>
/// to build objects directly in Jint's hidden-class representation.
/// <para>
/// Intended for the common embedding pattern where every item of a batch carries the <em>same</em>
/// properties: declare the layout once (it is engine-agnostic and safe to share across engines and
/// threads — typically a <c>static readonly</c> field), then create one object per item.
/// Every object built from the same layout in the same engine shares one hidden class, so a script
/// reading <c>item.name</c> in a loop over the batch keeps a monomorphic inline cache and no
/// per-property descriptor or property dictionary is allocated. Contrast with populating a fresh
/// <see cref="JsObject"/> through
/// <see cref="ObjectInstance.FastSetDataProperty(string, JsValue)"/>, which stores raw descriptors and
/// therefore forces the dictionary representation.
/// </para>
/// <para>
/// The property names are validated once, here, so object creation itself has nothing left to check.
/// </para>
/// </summary>
/// <example>
/// <code>
/// private static readonly JsObjectLayout PointLayout = new("x", "y", "label");
///
/// JsObject ToJs(Engine engine, Point p) => JsObject.Create(
///     engine,
///     PointLayout,
///     [JsNumber.Create(p.X), JsNumber.Create(p.Y), new JsString(p.Label)]);
/// </code>
/// </example>
public sealed class JsObjectLayout
{
    // Below this count IndexOf walks the keys (a precomputed-hash compare plus an ordinal compare per
    // step), which beats a dictionary probe for the small layouts this type targets. Mirrors Shape.
    private const int LinearScanLimit = 16;

    // Property names in slot (= insertion) order, pre-hashed so resolving the layout to a Shape is a
    // straight walk of interned transitions with no re-hashing.
    private readonly Key[] _keys;

    // Lazily built for wide layouts only.
    private Dictionary<Key, int>? _index;

    /// <summary>
    /// Creates a layout for the given property names, in the order the properties should appear in the
    /// created objects' own-key order.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="propertyNames"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException">
    /// A name is <c>null</c> or empty, two names are equal, a name starts with a digit (such a key must be
    /// enumerated in ascending numeric order ahead of the string keys, which a fixed layout cannot
    /// express), or there are more names than a hidden class can describe.
    /// </exception>
    public JsObjectLayout(params string[] propertyNames) : this((IReadOnlyList<string>) propertyNames)
    {
    }

    /// <summary>
    /// Creates a layout for the given property names, in the order the properties should appear in the
    /// created objects' own-key order.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="propertyNames"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException">
    /// A name is <c>null</c> or empty, two names are equal, a name starts with a digit (such a key must be
    /// enumerated in ascending numeric order ahead of the string keys, which a fixed layout cannot
    /// express), or there are more names than a hidden class can describe.
    /// </exception>
    public JsObjectLayout(IReadOnlyList<string> propertyNames)
    {
        if (propertyNames is null)
        {
            Throw.ArgumentNullException(nameof(propertyNames));
        }

        var count = propertyNames.Count;
        if (count > Shape.MaxShapeProperties)
        {
            Throw.ArgumentException(
                $"A layout can describe at most {Shape.MaxShapeProperties} properties, but {count} property names were given.",
                nameof(propertyNames));
        }

        var keys = count > 0 ? new Key[count] : System.Array.Empty<Key>();
        for (var i = 0; i < count; i++)
        {
            var name = propertyNames[i];
            if (string.IsNullOrEmpty(name))
            {
                Throw.ArgumentException(
                    $"Property name at index {i} is null or empty; layout property names must be non-empty strings.",
                    nameof(propertyNames));
            }

            if (Shape.IsIntegerIndexLikeKey(name))
            {
                Throw.ArgumentException(
                    $"Property name '{name}' at index {i} starts with a digit. Integer-index-like keys must be enumerated in ascending numeric order before the string keys, which a fixed layout cannot express; build such objects with JsObject.CreateFromEntries instead.",
                    nameof(propertyNames));
            }

            Key key = name;
            for (var j = 0; j < i; j++)
            {
                if (keys[j] == key)
                {
                    Throw.ArgumentException(
                        $"Duplicate property name '{name}' at index {i}; layout property names must be distinct.",
                        nameof(propertyNames));
                }
            }

            keys[i] = key;
        }

        _keys = keys;
    }

    /// <summary>The number of properties this layout describes.</summary>
    public int Count => _keys.Length;

    /// <summary>
    /// The slot of <paramref name="name"/> in this layout — the index its value occupies in the
    /// <c>values</c> span passed to
    /// <see cref="JsObject.Create(Engine, JsObjectLayout, ReadOnlySpan{JsValue})"/> — or <c>-1</c> when the
    /// layout does not describe that property.
    /// </summary>
    public int IndexOf(string name)
    {
        if (name is null)
        {
            return -1;
        }

        Key key = name;
        var keys = _keys;
        if (keys.Length < LinearScanLimit)
        {
            for (var i = 0; i < keys.Length; i++)
            {
                if (keys[i] == key)
                {
                    return i;
                }
            }

            return -1;
        }

        return (_index ??= BuildIndex()).TryGetValue(key, out var index) ? index : -1;
    }

    /// <summary>Property names in slot order, pre-hashed. Must be treated as read-only.</summary>
    internal Key[] Keys
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _keys;
    }

    private Dictionary<Key, int> BuildIndex()
    {
        var keys = _keys;
        var index = new Dictionary<Key, int>(keys.Length);
        for (var i = 0; i < keys.Length; i++)
        {
            index[keys[i]] = i;
        }

        return index;
    }
}
