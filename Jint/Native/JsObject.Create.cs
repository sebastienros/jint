using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors.Specialized;

namespace Jint.Native;

/// <summary>
/// Host-facing factories that build plain objects straight into the hidden-class (shape) representation.
/// </summary>
public sealed partial class JsObject
{
    /// <summary>
    /// Creates an object with <paramref name="layout"/>'s properties, in layout order, taking their values
    /// from <paramref name="values"/> by slot. The result is a completely ordinary object whose prototype is
    /// the engine's <c>Object.prototype</c> and whose properties are configurable, enumerable and writable —
    /// indistinguishable from the equivalent object literal, including own-key order.
    /// <para>
    /// Unlike populating a fresh <see cref="JsObject"/> through
    /// <see cref="ObjectInstance.FastSetDataProperty(string, JsValue)"/> — which stores raw descriptors and
    /// therefore builds a property dictionary — this resolves the layout to an interned hidden class once per
    /// (engine, layout) and then only fills value slots. Every object created from the same layout in the
    /// same engine shares that hidden class, so a script reading the same property across a batch of such
    /// objects keeps a monomorphic inline cache.
    /// </para>
    /// <para>
    /// Anything the representation cannot express falls back automatically and correctly: adding or deleting
    /// a property, defining an accessor or a non-default attribute, freezing or sealing all convert the
    /// object to the ordinary dictionary representation, exactly as they do for an object literal.
    /// </para>
    /// </summary>
    /// <param name="engine">The engine the object belongs to.</param>
    /// <param name="layout">The property layout; see <see cref="JsObjectLayout"/>.</param>
    /// <param name="values">
    /// One value per layout property, in layout order. A <c>null</c> entry is stored as
    /// <see cref="JsValue.Undefined"/> — except at a property the layout declared through
    /// <see cref="JsObjectLayout.Builder.AddLazy"/>, where <c>null</c> is required and the layout supplies
    /// the value. Use the
    /// <see cref="Create(Engine, JsObjectLayout, ReadOnlySpan{JsValue}, object)"/> overload to give those
    /// factories per-object state; this one passes <c>null</c>.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="engine"/> or <paramref name="layout"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException"><paramref name="values"/> does not have exactly <see cref="JsObjectLayout.Count"/> entries.</exception>
    public static JsObject Create(Engine engine, JsObjectLayout layout, ReadOnlySpan<JsValue> values)
        => Create(engine, layout, values, lazySlotState: null);

    /// <summary>
    /// Creates an object with <paramref name="layout"/>'s properties exactly as
    /// <see cref="Create(Engine, JsObjectLayout, ReadOnlySpan{JsValue})"/> does, additionally handing
    /// <paramref name="lazySlotState"/> to the factories of any properties the layout declared through
    /// <see cref="JsObjectLayout.Builder.AddLazy"/>.
    /// <para>
    /// The result is indistinguishable from the equivalent object literal — same own-key order, same
    /// configurable/enumerable/writable data properties, same hidden class as every other object built from
    /// this layout — <em>except</em> for when a lazy property's value comes into existence: on the first read
    /// that observes it, not here. Nothing in this call runs a factory.
    /// </para>
    /// </summary>
    /// <param name="engine">The engine the object belongs to.</param>
    /// <param name="layout">The property layout; see <see cref="JsObjectLayout"/>.</param>
    /// <param name="values">
    /// One value per layout property, in layout order. A <c>null</c> entry for an ordinary property is stored
    /// as <see cref="JsValue.Undefined"/>; the entry for a lazy property MUST be <c>null</c>, since the
    /// layout supplies that value.
    /// </param>
    /// <param name="lazySlotState">
    /// The per-object state passed to every lazy factory of this object — typically the host record the
    /// object projects, so several lazy properties can read different parts of one payload. Unlike the
    /// layout, it is per object and may be engine-affine. Ignored by an all-eager layout.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="engine"/> or <paramref name="layout"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="values"/> does not have exactly <see cref="JsObjectLayout.Count"/> entries, or a
    /// non-<c>null</c> value was given for a lazy property.
    /// </exception>
    public static JsObject Create(Engine engine, JsObjectLayout layout, ReadOnlySpan<JsValue> values, object? lazySlotState)
    {
        if (engine is null)
        {
            Throw.ArgumentNullException(nameof(engine));
        }

        if (layout is null)
        {
            Throw.ArgumentNullException(nameof(layout));
        }

        var keys = layout.Keys;
        if (values.Length != keys.Length)
        {
            Throw.ArgumentException(
                $"The layout describes {keys.Length} properties but {values.Length} values were given.",
                nameof(values));
        }

        var hasLazySlots = layout.HasLazySlots;
        if (hasLazySlots)
        {
            // Catch the mistake — "I passed the parsed body as well" — at creation rather than letting the
            // value be silently shadowed by the factory's result, or vice versa.
            for (var i = 0; i < keys.Length; i++)
            {
                if (values[i] is not null && layout.GetFactory(i) is not null)
                {
                    Throw.ArgumentException(
                        $"The value at index {i} ('{keys[i].Name}') must be null: that property is produced by the layout's lazy factory.",
                        nameof(values));
                }
            }
        }

        var obj = new JsObject(engine);

        // The JsObject constructor already anchored the object to the active realm's Object.prototype;
        // resolve the layout against that exact prototype, because shapes are interned per prototype.
        var shape = obj._prototype is { } proto ? engine.TryGetLayoutShape(proto, layout) : null;
        if (shape is null)
        {
            // No prototype (only reachable while the realm is still being built) or the engine's host
            // transition budget is spent: build the same object in the ordinary dictionary representation.
            // A lazy property becomes the same descriptor the shaped object's deopt would have produced, so
            // the fallback is behaviorally identical — including that its factory has still not run.
            var fallbackSentinel = hasLazySlots ? new UnmaterializedSlots(layout, lazySlotState) : null;
            for (var i = 0; i < keys.Length; i++)
            {
                if (fallbackSentinel is not null && layout.GetFactory(i) is not null)
                {
                    obj.FastSetProperty(keys[i].Name, new LazySlotPropertyDescriptor(obj, fallbackSentinel, i));
                }
                else
                {
                    obj.FastSetDataProperty(keys[i].Name, values[i] ?? Undefined);
                }
            }

            return obj;
        }

        // Install the interned layout and fill the slots. A layout within the in-object slot capacity makes
        // the object itself the only allocation.
        obj.InitShape(shape);
        for (var i = 0; i < values.Length; i++)
        {
            obj.SetSlot(i, values[i] ?? Undefined);
        }

        if (hasLazySlots)
        {
            // One sentinel shared by every lazy slot, so an object with four of them costs one extra
            // allocation, not four closures.
            obj.InstallLazySlots(layout, lazySlotState);
        }

        return obj;
    }

    /// <summary>
    /// Creates an object from a sequence of name/value entries, in the order given, for host data whose key
    /// set is only known at runtime. Later entries with a name already present overwrite the earlier value
    /// in place, keeping the first occurrence's position — the same rule an object literal and
    /// <c>Object.fromEntries</c> follow.
    /// <para>
    /// Entries are added through the incremental hidden-class path, so repeated calls presenting the same
    /// key sequence produce objects that share one interned hidden class — which is what keeps a script
    /// reading those objects monomorphic. Key sets too varied for that (see the remarks) degrade to the
    /// ordinary dictionary representation, never to incorrect behavior.
    /// </para>
    /// </summary>
    /// <remarks>
    /// The object falls back to the dictionary representation — mid-build, preserving everything added so
    /// far — when an entry name starts with a digit (integer-index-like keys must enumerate in ascending
    /// numeric order ahead of the string keys), when it would exceed the number of properties a hidden class
    /// can describe, or when the layout it is growing has already sprouted too many distinct continuations
    /// (the object-used-as-a-hash-map pattern). Each engine also bounds how many new layouts host code may
    /// intern over its lifetime; past that, this method keeps working and simply returns dictionary-mode
    /// objects.
    /// <para>
    /// Entries are always eager. Lazily-produced properties are declared on a layout
    /// (<see cref="JsObjectLayout.Builder.AddLazy"/>), and a key set only known at runtime has nowhere to
    /// declare them; build such an object with <see cref="Create(Engine, JsObjectLayout, ReadOnlySpan{JsValue}, object)"/>
    /// instead, or install a <see cref="Jint.Runtime.Descriptors.PropertyFlag.CustomJsValue"/> descriptor.
    /// </para>
    /// </remarks>
    /// <param name="engine">The engine the object belongs to.</param>
    /// <param name="entries">The property names and values, in the order they should appear.</param>
    /// <exception cref="ArgumentNullException"><paramref name="engine"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException">An entry name is <c>null</c>.</exception>
    public static JsObject CreateFromEntries(Engine engine, ReadOnlySpan<KeyValuePair<string, JsValue>> entries)
    {
        if (engine is null)
        {
            Throw.ArgumentNullException(nameof(engine));
        }

        var obj = new JsObject(engine);
        var state = new EntryBuildState();
        for (var i = 0; i < entries.Length; i++)
        {
            AddEntry(obj, engine, entries[i].Key, entries[i].Value, ref state);
        }

        return obj;
    }

    /// <summary>
    /// Enumerable overload of
    /// <see cref="CreateFromEntries(Engine, ReadOnlySpan{KeyValuePair{string, JsValue}})"/>, for hosts whose
    /// data already lives in a <see cref="Dictionary{TKey,TValue}"/> or another sequence that cannot be
    /// viewed as a span — materializing one just to call this API would cost the very allocation the shaped
    /// representation saves. Enumeration order is the source's order, and it is walked exactly once.
    /// </summary>
    /// <param name="engine">The engine the object belongs to.</param>
    /// <param name="entries">The property names and values, in the order they should appear.</param>
    /// <exception cref="ArgumentNullException"><paramref name="engine"/> or <paramref name="entries"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException">An entry name is <c>null</c>.</exception>
    public static JsObject CreateFromEntries(Engine engine, IEnumerable<KeyValuePair<string, JsValue>> entries)
    {
        if (engine is null)
        {
            Throw.ArgumentNullException(nameof(engine));
        }

        if (entries is null)
        {
            Throw.ArgumentNullException(nameof(entries));
        }

        var obj = new JsObject(engine);
        var state = new EntryBuildState();
        foreach (var entry in entries)
        {
            AddEntry(obj, engine, entry.Key, entry.Value, ref state);
        }

        return obj;
    }

    /// <summary>
    /// The per-object locals both <c>CreateFromEntries</c> overloads carry through
    /// <see cref="AddEntry"/>: whether the object is still growing a hidden class, and whether the next
    /// entry is the first one (shaping starts lazily so an empty entry set stays a bare property-less
    /// object).
    /// </summary>
    private struct EntryBuildState
    {
        internal bool Shaped;
        internal bool Started;
    }

    /// <summary>
    /// Adds one host entry, routing through the hidden-class machinery so identically-keyed items share an
    /// interned layout, and dropping the object to the dictionary representation — preserving insertion
    /// order — the moment a key or a growth guard says the layout cannot continue.
    /// </summary>
    private static void AddEntry(JsObject obj, Engine engine, string? name, JsValue? value, ref EntryBuildState state)
    {
        if (name is null)
        {
            Throw.ArgumentException("Entry property names must not be null.", "entries");
        }

        var v = value ?? Undefined;
        var integerIndexLike = Shape.IsIntegerIndexLikeKey(name);

        if (!state.Started)
        {
            state.Started = true;
            // Start shaping lazily on the first entry, and only when that entry can actually live in a
            // shape — an object whose first key is integer-index-like would deopt immediately.
            if (!integerIndexLike && engine.HostShapeBudgetAvailable && obj._prototype is { } proto)
            {
                obj.StartShapeBuilding(engine.GetEmptyShape(proto));
                state.Shaped = true;
            }
        }

        if (state.Shaped)
        {
            if (!integerIndexLike)
            {
                Key key = name;
                if (obj.ShapeOf.TryGetSlot(in key, out var slot))
                {
                    // Duplicate name: last value wins at the first occurrence's position, matching the
                    // dictionary representation's replace-in-place.
                    obj.SetSlot(slot, v);
                    return;
                }

                if (obj.TryShapeAdd(in key, v, out var created))
                {
                    if (created)
                    {
                        // Only newly interned transitions consume the engine's budget; the repeated-layout
                        // case — the one this API exists for — costs nothing.
                        engine.ChargeHostShapeTransition();
                    }

                    return;
                }
            }

            // Integer-index-like key, or a growth guard (own-property count / transition fan-out) refused
            // the add: finish this object as a dictionary, keeping everything added so far.
            obj.ConvertToDictionaryMode();
            state.Shaped = false;
        }

        obj.FastSetDataProperty(name, v);
    }
}
