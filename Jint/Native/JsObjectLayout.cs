using System.Runtime.CompilerServices;
using Jint.Native.Object;
using Jint.Runtime;

namespace Jint.Native;

/// <summary>
/// Produces the value of one lazy layout slot for one object, on the first read that observes that value.
/// Declared through <see cref="JsObjectLayout.Builder.AddLazy"/>.
/// </summary>
/// <param name="instance">
/// The object being read. Reach the engine it belongs to through <see cref="ObjectInstance.Engine"/>.
/// </param>
/// <param name="state">
/// The per-object state handed to
/// <see cref="JsObject.Create(Engine, JsObjectLayout, ReadOnlySpan{JsValue}, object)"/> — typically the host
/// record the object projects, so several lazy slots of one object can read different parts of one payload.
/// </param>
/// <returns>
/// The property's value. <c>null</c> is stored as <see cref="JsValue.Undefined"/>.
/// </returns>
/// <remarks>
/// <para>
/// A factory MUST be engine-independent — a static lambda, or one capturing only engine-independent CLR
/// state. The <see cref="JsObjectLayout"/> carrying it is process-shared by design (a <c>static readonly</c>
/// field used by every engine in the process), exactly like a <see cref="JsObjectShape"/> member
/// implementation, so a captured <see cref="Engine"/>, <see cref="Realm"/> or <see cref="JsValue"/> would
/// leak one engine's state into another's objects. Everything engine- or item-specific belongs in
/// <paramref name="state"/>, which is per object.
/// </para>
/// <para>
/// A factory is morally a getter body: it runs on the engine's thread, during property resolution, and may
/// build values against <c>instance.Engine</c>. It must not read the very property it computes — that
/// recurses, exactly as a JavaScript getter reading its own property does. If it throws, the exception
/// propagates out of the operation performing the read and the slot stays unmaterialized, so the next read
/// runs the factory again.
/// </para>
/// </remarks>
public delegate JsValue LazySlotFactory(JsObject instance, object? state);

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
/// <para>
/// A record with members that are expensive to produce and rarely read — a body that must be parsed, a
/// field that must be decoded — declares those through <see cref="CreateBuilder"/> and
/// <see cref="Builder.AddLazy"/> and hands the raw payload to
/// <see cref="JsObject.Create(Engine, JsObjectLayout, ReadOnlySpan{JsValue}, object)"/>: such a member
/// comes into existence on the first read that observes its value, and the object is a hidden-class
/// object throughout, before and after.
/// </para>
/// <para>
/// A layout builds per-item <em>data records</em>: many short-lived objects sharing one set of plain
/// value properties. For the other shape of sharing — a singleton prototype whose methods, accessors
/// and constants materialize lazily per realm — use <see cref="JsObjectShape"/> instead.
/// </para>
/// </summary>
/// <seealso cref="JsObjectShape"/>
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

    // Parallel to _keys, and null for the overwhelmingly common all-eager layout: a null array means "no
    // lazy slots", and inside a non-null array a null entry means "that slot is eager". Kept as a plain
    // parallel array rather than folded into _keys so the eager layout carries no extra field-per-slot and
    // resolving the layout to a Shape stays a walk of _keys alone.
    private readonly LazySlotFactory?[]? _factories;

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
        CheckCount(count, nameof(propertyNames));

        var keys = count > 0 ? new Key[count] : System.Array.Empty<Key>();
        for (var i = 0; i < count; i++)
        {
            var name = propertyNames[i];
            CheckName(name, i, nameof(propertyNames));

            Key key = name!;
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

    /// <summary>
    /// Builder-only constructor: the names were validated one at a time as they were added, so nothing is
    /// re-checked here. <paramref name="factories"/> is <c>null</c> for an all-eager layout.
    /// </summary>
    private JsObjectLayout(Key[] keys, LazySlotFactory?[]? factories)
    {
        _keys = keys;
        _factories = factories;
    }

    private static void CheckCount(int count, string paramName)
    {
        if (count > Shape.MaxShapeProperties)
        {
            Throw.ArgumentException(
                $"A layout can describe at most {Shape.MaxShapeProperties} properties, but {count} property names were given.",
                paramName);
        }
    }

    private static void CheckName(string? name, int index, string paramName)
    {
        if (string.IsNullOrEmpty(name))
        {
            Throw.ArgumentException(
                $"Property name at index {index} is null or empty; layout property names must be non-empty strings.",
                paramName);
        }

        if (Shape.IsIntegerIndexLikeKey(name!))
        {
            Throw.ArgumentException(
                $"Property name '{name}' at index {index} starts with a digit. Integer-index-like keys must be enumerated in ascending numeric order before the string keys, which a fixed layout cannot express; build such objects with JsObject.CreateFromEntries instead.",
                paramName);
        }
    }

    /// <summary>
    /// Creates a builder for a layout that mixes eager properties with lazily-produced ones.
    /// </summary>
    /// <remarks>
    /// The constructors build an all-eager layout, every value of which is supplied at
    /// <see cref="JsObject.Create(Engine, JsObjectLayout, ReadOnlySpan{JsValue})"/> time. Use a builder when
    /// some members are expensive to produce and most items never have them read — the canonical case being a
    /// record whose few costly members each parse or decode a part of that item's raw payload. See
    /// <see cref="Builder.AddLazy"/>.
    /// </remarks>
    /// <example>
    /// <code>
    /// private static readonly JsObjectLayout EnvelopeLayout = JsObjectLayout.CreateBuilder()
    ///     .Add("id")
    ///     .Add("type")
    ///     .AddLazy("body", static (_, state) =&gt; ((Payload) state!).ParseBody())
    ///     .Build();
    ///
    /// JsObject ToJs(Engine engine, Payload p) =&gt; JsObject.Create(
    ///     engine,
    ///     EnvelopeLayout,
    ///     [new JsString(p.Id), new JsString(p.Type), null],
    ///     p);
    /// </code>
    /// </example>
    public static Builder CreateBuilder() => new Builder();

    /// <summary>The number of properties this layout describes.</summary>
    public int Count => _keys.Length;

    /// <summary>Whether any slot of this layout is produced by a <see cref="LazySlotFactory"/>.</summary>
    internal bool HasLazySlots
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _factories is not null;
    }

    /// <summary>
    /// The factory for <paramref name="slot"/>, or <c>null</c> when that slot is eager. The slot is always in
    /// range: it comes from the <see cref="Shape"/> this layout was resolved to.
    /// </summary>
    internal LazySlotFactory? GetFactory(int slot) => _factories?[slot];

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

    /// <summary>
    /// Declares a layout one property at a time, so that individual properties can be marked as produced on
    /// demand rather than supplied up front. Obtained from <see cref="CreateBuilder"/>; a builder is
    /// single-use and not thread-safe, while the <see cref="JsObjectLayout"/> it produces is immutable and
    /// safe to share.
    /// </summary>
    public sealed class Builder
    {
        private readonly List<Key> _keys = [];

        // Allocated on the first AddLazy and back-filled with nulls for the eager properties already added,
        // so the overwhelmingly common all-eager builder never allocates it.
        private List<LazySlotFactory?>? _factories;
        private bool _built;

        /// <summary>Creates an empty builder.</summary>
        public Builder()
        {
        }

        /// <summary>
        /// Declares an ordinary property, whose value is supplied at this slot's index in the <c>values</c>
        /// span passed to <c>JsObject.Create</c>.
        /// </summary>
        /// <param name="propertyName">The property name.</param>
        /// <returns>This builder, for chaining.</returns>
        /// <exception cref="ArgumentException">
        /// The name is <c>null</c> or empty, already declared, or starts with a digit; or the layout is
        /// already at the maximum a hidden class can describe.
        /// </exception>
        /// <exception cref="InvalidOperationException">The layout has already been built.</exception>
        public Builder Add(string propertyName) => Add(propertyName, factory: null);

        /// <summary>
        /// Declares a property whose value is produced by <paramref name="factory"/> on the first read that
        /// observes it, and memoized on the object from then on. The corresponding entry in the
        /// <c>values</c> span passed to <c>JsObject.Create</c> must be <c>null</c>; the per-object state the
        /// factory reads is the single <c>lazySlotState</c> argument of that same call.
        /// <para>
        /// A lazy property is an ordinary configurable/enumerable/writable data property in every observable
        /// respect: it appears in <c>Object.keys</c>, answers <c>in</c> and <c>hasOwnProperty</c>, and does
        /// so <em>without</em> running the factory — only observing the value runs it. Writing the property
        /// before anything reads it discards the factory entirely, and so does deleting it.
        /// </para>
        /// </summary>
        /// <remarks>
        /// <para>
        /// The factory is part of the layout, so it is shared by every object built from it and by every
        /// engine using it, and must be engine-independent — see <see cref="LazySlotFactory"/>. Reads that
        /// observe the value (<c>Object.values</c>/<c>entries</c>, <c>Object.assign</c>, spread,
        /// <c>JSON.stringify</c>, <c>getOwnPropertyDescriptor</c>, and of course an ordinary property read)
        /// run it; <c>Object.keys</c>, <c>for-in</c>, <c>Reflect.ownKeys</c>, <c>in</c>,
        /// <c>hasOwnProperty</c> and <c>propertyIsEnumerable</c> do not.
        /// </para>
        /// <para>
        /// Laziness survives everything that drops the object to the ordinary dictionary representation — a
        /// <c>delete</c>, a <c>defineProperty</c> on another key, <c>Object.freeze</c>, <c>Object.seal</c> —
        /// so a script touching one member never forces the rest to materialize, and a host that wants
        /// read-only members can freeze the object right after creating it. What materializes is only what
        /// observes the value: a redefinition of the lazy property that supplies a value discards the factory
        /// (it is a write), and one that must validate against the current value — redefining a
        /// non-writable, non-configurable property — runs it.
        /// </para>
        /// </remarks>
        /// <param name="propertyName">The property name.</param>
        /// <param name="factory">Produces the value on first observation.</param>
        /// <returns>This builder, for chaining.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="factory"/> is <c>null</c>.</exception>
        /// <exception cref="ArgumentException">
        /// The name is <c>null</c> or empty, already declared, or starts with a digit; or the layout is
        /// already at the maximum a hidden class can describe.
        /// </exception>
        /// <exception cref="InvalidOperationException">The layout has already been built.</exception>
        public Builder AddLazy(string propertyName, LazySlotFactory factory)
        {
            if (factory is null)
            {
                Throw.ArgumentNullException(nameof(factory));
            }

            return Add(propertyName, factory);
        }

        /// <summary>
        /// Produces the immutable layout. The builder cannot be used afterwards.
        /// </summary>
        /// <exception cref="InvalidOperationException">The layout has already been built.</exception>
        public JsObjectLayout Build()
        {
            CheckNotBuilt();
            _built = true;

            var keys = _keys.Count > 0 ? _keys.ToArray() : System.Array.Empty<Key>();
            LazySlotFactory?[]? factories = null;
            if (_factories is not null)
            {
                factories = new LazySlotFactory?[keys.Length];
                _factories.CopyTo(factories);
            }

            return new JsObjectLayout(keys, factories);
        }

        private Builder Add(string propertyName, LazySlotFactory? factory)
        {
            CheckNotBuilt();

            var index = _keys.Count;
            CheckCount(index + 1, nameof(propertyName));
            CheckName(propertyName, index, nameof(propertyName));

            Key key = propertyName;
            for (var i = 0; i < _keys.Count; i++)
            {
                if (_keys[i] == key)
                {
                    Throw.ArgumentException(
                        $"Duplicate property name '{propertyName}'; layout property names must be distinct.",
                        nameof(propertyName));
                }
            }

            _keys.Add(key);

            if (factory is not null && _factories is null)
            {
                // First lazy property: back-fill nulls for the eager ones already declared.
                _factories = new List<LazySlotFactory?>(index + 1);
                for (var i = 0; i < index; i++)
                {
                    _factories.Add(null);
                }
            }

            _factories?.Add(factory);
            return this;
        }

        private void CheckNotBuilt()
        {
            if (_built)
            {
                Throw.InvalidOperationException("The layout has already been built; a builder must not be reused or modified after Build().");
            }
        }
    }
}
