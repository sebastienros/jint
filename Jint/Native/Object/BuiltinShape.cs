using Jint.Collections;
using Jint.Runtime.Descriptors;

namespace Jint.Native.Object;

/// <summary>How a <see cref="BuiltinShape"/> slot's descriptor is produced.</summary>
internal enum BuiltinSlotKind : byte
{
    // Value is the process-shared descriptor already present in ConstTemplate.
    Constant,
    // Per-realm data property (e.g. a prototype's `constructor`); descriptor filled by SetBuiltinInstanceDescriptor.
    Instance,
    // Lazily-materialized function; FunctionSlots holds the dispatcher slot id.
    Function,
    // Lazily-materialized accessor; FunctionSlots holds the getter dispatcher slot (or NotAFunction) and
    // SetterSlots the setter dispatcher slot (or NotAFunction).
    Accessor,
    // Shares another slot's materialized descriptor (spec function-identity aliases, e.g.
    // Set.prototype.keys === Set.prototype.values). FunctionSlots holds the target slot index.
    Alias,
    // Descriptor produced by a process-shared per-slot factory over the host instance
    // (realm-intrinsic references like globalThis.Array, thrower accessors like
    // Function.prototype.arguments). The factory result should itself be cheap/lazy —
    // it is also what a deopt stores, so it must not force expensive materialization.
    Factory,
}

/// <summary>
/// Immutable, process-shared description of a built-in object's string-keyed own-property layout
/// (names, attributes, and how each slot is produced), built once per built-in type. Paired with a
/// per-realm value array on a host that implements <see cref="IBuiltinShaped"/>, it replaces the per-realm
/// dictionary of descriptors a built-in would otherwise allocate in <c>Initialize</c>. Symbols are
/// orthogonal and stay in the object's symbol dictionary.
/// </summary>
internal sealed class BuiltinShape
{
    internal const ushort NotAFunction = ushort.MaxValue;

    // Property names in own-property (insertion) order.
    internal readonly Key[] Names;
    // Per slot: how the slot's descriptor is produced.
    internal readonly BuiltinSlotKind[] Kinds;
    // Per slot: the process-shared descriptor for an immutable constant, or null for a lazily-filled slot
    // (function / accessor / instance). Cloned into each instance as the starting per-realm descriptor array.
    internal readonly PropertyDescriptor?[] ConstTemplate;
    // Per slot: the dispatcher slot id to materialize for a function / accessor getter, or NotAFunction.
    internal readonly ushort[] FunctionSlots;
    // Per slot: the setter dispatcher slot id for an Accessor slot, or NotAFunction (also for non-accessors).
    internal readonly ushort[] SetterSlots;
    // Per slot: the attributes for a function / accessor slot (unused for constants — those carry their own flags).
    internal readonly PropertyFlag[] FunctionFlags;
    // Per slot: descriptor factory for Factory-kind slots (null for every other kind). Process-shared,
    // so factories must be static lambdas taking the host instance.
    internal readonly Func<ObjectInstance, PropertyDescriptor>?[]? Factories;
    // name -> slot index.
    internal readonly StringDictionarySlim<int> Index;

    // Slot-ordered names as shared, immutable JsString instances, memoized on first use. for-in over a
    // built-in (e.g. an object's Object.prototype level) hands these out instead of recreating a JsString
    // per name per enumeration. Built-in names are never integer-index-like, so no numeric-sort concern.
    private JsValue[]? _namesAsJsStrings;

    internal int Count => Names.Length;

    /// <summary>
    /// This built-in's own property names as shared <see cref="JsString"/> instances in slot order,
    /// memoized once per built-in type. Read-only; identity is unobservable to script.
    /// </summary>
    internal JsValue[] NamesAsJsStrings
    {
        get
        {
            // A BuiltinShape is a process-shared singleton (one static instance per built-in type), so
            // this memo can be raced by engines on different threads. Build into a local and publish
            // with a release/acquire pair: a concurrent reader that sees the reference is guaranteed to
            // see the fully-populated array (matters on weak memory models). A rare double-build is
            // benign — the contents are a deterministic function of Names.
            var cached = System.Threading.Volatile.Read(ref _namesAsJsStrings);
            if (cached is not null)
            {
                return cached;
            }

            var names = Names;
            JsValue[] arr;
            if (names.Length == 0)
            {
                arr = System.Array.Empty<JsValue>();
            }
            else
            {
                arr = new JsValue[names.Length];
                for (var i = 0; i < names.Length; i++)
                {
                    arr[i] = JsString.Create(names[i].Name);
                }
            }

            System.Threading.Volatile.Write(ref _namesAsJsStrings, arr);
            return arr;
        }
    }

    private BuiltinShape(Key[] names, BuiltinSlotKind[] kinds, PropertyDescriptor?[] constTemplate, ushort[] functionSlots, ushort[] setterSlots, PropertyFlag[] functionFlags, Func<ObjectInstance, PropertyDescriptor>?[]? factories, StringDictionarySlim<int> index)
    {
        Names = names;
        Kinds = kinds;
        ConstTemplate = constTemplate;
        FunctionSlots = functionSlots;
        SetterSlots = setterSlots;
        FunctionFlags = functionFlags;
        Factories = factories;
        Index = index;
    }

    /// <summary>
    /// Accumulates a built-in's entries (constants/instances first, then functions, then accessors — matching
    /// the order a generated property dictionary would use) and produces the shared <see cref="BuiltinShape"/>.
    /// </summary>
    internal sealed class Builder
    {
        private readonly Key[] _names;
        private readonly BuiltinSlotKind[] _kinds;
        private readonly PropertyDescriptor?[] _constTemplate;
        private readonly ushort[] _functionSlots;
        private readonly ushort[] _setterSlots;
        private readonly PropertyFlag[] _functionFlags;
        private readonly StringDictionarySlim<int> _index;
        private Func<ObjectInstance, PropertyDescriptor>?[]? _factories;
        private int _next;

        internal Builder(int capacity)
        {
            _names = new Key[capacity];
            _kinds = new BuiltinSlotKind[capacity];
            _constTemplate = new PropertyDescriptor?[capacity];
            _functionSlots = new ushort[capacity];
            _setterSlots = new ushort[capacity];
            _functionFlags = new PropertyFlag[capacity];
            _index = new StringDictionarySlim<int>(capacity);
            _next = 0;
        }

        internal void Constant(in Key name, PropertyDescriptor descriptor)
        {
            _names[_next] = name;
            _kinds[_next] = BuiltinSlotKind.Constant;
            _constTemplate[_next] = descriptor;
            _functionSlots[_next] = NotAFunction;
            _setterSlots[_next] = NotAFunction;
            _index[name] = _next;
            _next++;
        }

        internal void Function(in Key name, ushort slot, PropertyFlag flags)
        {
            _names[_next] = name;
            _kinds[_next] = BuiltinSlotKind.Function;
            _constTemplate[_next] = null;
            _functionSlots[_next] = slot;
            _setterSlots[_next] = NotAFunction;
            _functionFlags[_next] = flags;
            _index[name] = _next;
            _next++;
        }

        /// <summary>
        /// Reserves a slot for a lazily-materialized accessor. <paramref name="getterSlot"/> /
        /// <paramref name="setterSlot"/> are dispatcher slot ids, or <see cref="NotAFunction"/> for the
        /// absent half. The <see cref="GetSetPropertyDescriptor"/> is created on first access.
        /// </summary>
        internal void Accessor(in Key name, ushort getterSlot, ushort setterSlot, PropertyFlag flags)
        {
            _names[_next] = name;
            _kinds[_next] = BuiltinSlotKind.Accessor;
            _constTemplate[_next] = null;
            _functionSlots[_next] = getterSlot;
            _setterSlots[_next] = setterSlot;
            _functionFlags[_next] = flags;
            _index[name] = _next;
            _next++;
        }

        /// <summary>
        /// Reserves a slot for a per-realm instance property (a data property whose value varies per
        /// realm, e.g. a prototype's <c>constructor</c> reference). The descriptor is filled at
        /// initialization by the owner via <see cref="ObjectInstance.SetBuiltinInstanceDescriptor"/>,
        /// not lazily materialized — so its template slot stays null and it is never treated as a function.
        /// </summary>
        internal void Instance(in Key name)
        {
            _names[_next] = name;
            _kinds[_next] = BuiltinSlotKind.Instance;
            _constTemplate[_next] = null;
            _functionSlots[_next] = NotAFunction;
            _setterSlots[_next] = NotAFunction;
            _index[name] = _next;
            _next++;
        }

        /// <summary>
        /// Reserves an alias slot that shares the descriptor of an earlier slot named <paramref name="target"/>
        /// (which must already be added), so the two names resolve to the same materialized function/accessor
        /// — the spec function-identity aliases like <c>Set.prototype.keys === Set.prototype.values</c>.
        /// </summary>
        internal void Alias(in Key name, in Key target)
        {
            _names[_next] = name;
            _kinds[_next] = BuiltinSlotKind.Alias;
            _constTemplate[_next] = null;
            _functionSlots[_next] = (ushort) _index[target]; // target slot index
            _setterSlots[_next] = NotAFunction;
            _index[name] = _next;
            _next++;
        }

        /// <summary>
        /// Reserves a slot whose descriptor a process-shared factory produces from the host instance —
        /// realm-intrinsic references and thrower accessors. The factory must be a static lambda and its
        /// result should be cheap/lazy (it is also what a deopt stores for an untouched slot).
        /// </summary>
        internal void Factory(in Key name, Func<ObjectInstance, PropertyDescriptor> factory)
        {
            _factories ??= new Func<ObjectInstance, PropertyDescriptor>?[_names.Length];
            _names[_next] = name;
            _kinds[_next] = BuiltinSlotKind.Factory;
            _constTemplate[_next] = null;
            _functionSlots[_next] = NotAFunction;
            _setterSlots[_next] = NotAFunction;
            _factories[_next] = factory;
            _index[name] = _next;
            _next++;
        }

        internal BuiltinShape Build() => new(_names, _kinds, _constTemplate, _functionSlots, _setterSlots, _functionFlags, _factories, _index);
    }
}
