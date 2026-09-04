using System.Runtime.InteropServices;
using Jint.Native.Object;
using Jint.Native.Symbol;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;

namespace Jint.Native;

/// <summary>
/// An immutable, process-shared description of the members of a host-built prototype-like object:
/// methods, get/set accessors, immutable constants and per-realm value slots. Build one with
/// <see cref="Builder"/> (typically into a <c>static readonly</c> field), then create one object
/// per engine with <see cref="Instantiate(Engine, ObjectInstance)"/>.
/// <para>
/// This is the member-layout sibling of <see cref="JsObjectLayout"/>: a layout describes per-item
/// <em>data</em> objects built in the hidden-class representation, while a shape describes
/// <em>singleton</em> prototype/namespace objects built in the shared-layout representation the
/// engine uses for its own intrinsics (<c>Math</c>, <c>Generator.prototype</c>, …). An instantiated
/// object allocates no per-member state up front: each member's descriptor — and for methods and
/// accessors the function object itself — is created on first access in that engine and cached
/// there, so a shape with hundreds of members costs an engine only the members its scripts touch.
/// </para>
/// <para>
/// <b>Thread safety and sharing.</b> A built shape is immutable and safe to publish to and use from
/// any number of engines on any threads; so are the delegates and constant values placed into it,
/// which is why everything placed <em>into</em> a shape must be engine-independent — see the member
/// factory methods on <see cref="Builder"/> for the per-kind rules. Everything the shape
/// <em>materializes</em> is by contrast an ordinary engine-affine value: the object
/// <see cref="Instantiate(Engine, ObjectInstance)"/> returns, its per-engine descriptor array and
/// every function created for a method or accessor belong to the engine that instantiated it. The
/// shape itself holds no reference to any engine, realm or instantiated object, so a
/// <c>static readonly</c> shape never keeps an engine alive.
/// </para>
/// <para>
/// The representation is an implementation detail with no script-visible consequence: an
/// instantiated object behaves exactly like an ordinary object carrying the same properties, and
/// anything the shared layout cannot express (deleting a member, adding an integer-like key) falls
/// back to the ordinary property dictionary automatically and correctly. Because that fallback is
/// silent, <c>engine.Advanced.HasSharedShape(prototype)</c> is the supported way to assert — once,
/// during adoption — that a prototype really is on the shared layout. It observes without perturbing,
/// so touch a member first: an instantiated object installs the layout on its first property access.
/// </para>
/// </summary>
/// <example>
/// <code>
/// private static readonly JsObjectShape NodeShape = new JsObjectShape.Builder()
///     .Method("appendChild", static (t, args) =&gt; Dom.AppendChild(t, args), length: 1)
///     .Accessor("firstChild", getter: static (t, _) =&gt; Dom.GetFirstChild(t))
///     .Constant("ELEMENT_NODE", 1)
///     .PerRealmSlot("constructor", static o =&gt; HostRealmState.For(o.Engine).NodeConstructor, enumerable: false)
///     .ToStringTag("Node")
///     .Build();
///
/// // once per engine, cached by the host:
/// var nodeProto = NodeShape.Instantiate(engine, eventTargetProto);
/// </code>
/// </example>
public sealed class JsObjectShape
{
    /// <summary>
    /// The largest number of members a shape can describe. Function dispatch ids are 16-bit, and the
    /// widest real-world prototype (a Web IDL interface with every inherited member flattened onto it)
    /// runs to a few hundred, so this bound exists only to turn a runaway generator into a clear error.
    /// </summary>
    internal const int MaxMembers = 4096;

    private readonly BuiltinShape _layout;
    private readonly FunctionEntry[] _functions;
    private readonly PerRealmValueSlot[] _perRealmValueSlots;
    private readonly SymbolMember[] _symbolMembers;

    private JsObjectShape(
        BuiltinShape layout,
        FunctionEntry[] functions,
        PerRealmValueSlot[] perRealmValueSlots,
        SymbolMember[] symbolMembers)
    {
        _layout = layout;
        _functions = functions;
        _perRealmValueSlots = perRealmValueSlots;
        _symbolMembers = symbolMembers;
    }

    /// <summary>The number of string-keyed members this shape declares.</summary>
    public int Count => _layout.Count;

    /// <summary>
    /// Creates one object with this shape in <paramref name="engine"/>, using the engine's
    /// <c>Object.prototype</c> as its prototype. Equivalent to
    /// <c>Instantiate(engine, engine.Realm.Intrinsics.Object.PrototypeObject)</c>.
    /// </summary>
    /// <param name="engine">The engine the object belongs to.</param>
    /// <exception cref="ArgumentNullException"><paramref name="engine"/> is <c>null</c>.</exception>
    public ObjectInstance Instantiate(Engine engine)
    {
        if (engine is null)
        {
            Throw.ArgumentNullException(nameof(engine));
        }

        return new SharedShapeObject(engine, this, engine.Realm.Intrinsics.Object.PrototypeObject);
    }

    /// <summary>
    /// Creates one object with this shape in <paramref name="engine"/> whose <c>[[Prototype]]</c> is
    /// <paramref name="prototype"/> (<c>null</c> for a prototype-less object, as in
    /// <c>Object.create(null)</c>). The call itself allocates only the object; members materialize
    /// lazily on first access. Each call creates a new, independent object — keeping one prototype
    /// per engine is the caller's job, typically through the host's existing per-engine prototype
    /// cache.
    /// </summary>
    /// <param name="engine">The engine the object belongs to.</param>
    /// <param name="prototype">The prototype of the created object, or <c>null</c> for none.</param>
    /// <exception cref="ArgumentNullException"><paramref name="engine"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException"><paramref name="prototype"/> belongs to a different engine.</exception>
    public ObjectInstance Instantiate(Engine engine, ObjectInstance? prototype)
    {
        if (engine is null)
        {
            Throw.ArgumentNullException(nameof(engine));
        }

        if (prototype is not null && !ReferenceEquals(prototype.Engine, engine))
        {
            // An object's prototype must belong to its own engine; accepting a foreign one would build a
            // cross-engine object graph, which nothing in Jint supports. Easy to get wrong in exactly the
            // multi-engine host code this API is written for, so it fails instead of misbehaving later.
            Throw.ArgumentException("The prototype belongs to a different engine.", nameof(prototype));
        }

        return new SharedShapeObject(engine, this, prototype);
    }

    /// <summary>
    /// Attaches opaque host state to an object created by <see cref="Instantiate(Engine, ObjectInstance)"/>,
    /// replacing anything attached before. The engine never reads the value; this exists so that a host whose
    /// prototypes carry their own bookkeeping — a type token, a back-reference to the constructor, the
    /// collection an indexer projects — can keep it <em>on</em> the object instead of in a side table keyed
    /// by it.
    /// <para>
    /// The state is <b>per instance</b>, not per shape, so unlike everything placed into the shape itself it
    /// may be freely engine-affine: a <see cref="JsValue"/>, a constructor, anything belonging to the engine
    /// that created this object. It is held by that object and by nothing else, so it becomes collectable
    /// exactly when the object does — a process-held shape never retains the state of an instance whose
    /// engine has been dropped.
    /// </para>
    /// <para>
    /// Attaching after creation rather than during it is deliberate, and it is what makes the common case
    /// expressible: a prototype's state usually includes a reference to the constructor whose
    /// <c>prototype</c> property <em>is</em> this object, so the object has to exist first.
    /// </para>
    /// </summary>
    /// <param name="target">An object returned by <see cref="Instantiate(Engine, ObjectInstance)"/>.</param>
    /// <param name="hostState">The state to attach, or <c>null</c> to detach.</param>
    /// <exception cref="ArgumentNullException"><paramref name="target"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="target"/> was not created by a <see cref="JsObjectShape"/>. Note that an object which
    /// has merely fallen back to the ordinary property representation is still such an object and still
    /// accepts state — the representation is an implementation detail, the identity is not.
    /// </exception>
    public static void SetHostState(ObjectInstance target, object? hostState)
    {
        if (target is null)
        {
            Throw.ArgumentNullException(nameof(target));
        }

        if (target is not SharedShapeObject shaped)
        {
            // Silently dropping the state would strand it: every later GetHostState would answer null and
            // the host would be debugging a lookup that never had a chance of working.
            Throw.ArgumentException(
                "Host state can only be attached to an object created by JsObjectShape.Instantiate.",
                nameof(target));
            return;
        }

        shaped.HostState = hostState;
    }

    /// <summary>
    /// The host state attached to <paramref name="target"/> by <see cref="SetHostState"/>, or <c>null</c>
    /// when there is none — including when <paramref name="target"/> is <c>null</c> or was not created by a
    /// <see cref="JsObjectShape"/> at all. Tolerating both keeps the natural retrieval pattern a single
    /// expression: a member implementation receives the <em>instance</em> as its receiver and walks up to the
    /// prototype that owns it, and a walk that runs off the end of the chain simply yields <c>null</c>.
    /// </summary>
    /// <example>
    /// <code>
    /// static JsValue Describe(JsValue thisObject, JsValue[] arguments)
    /// {
    ///     for (var o = (thisObject as ObjectInstance)?.Prototype; o is not null; o = o.Prototype)
    ///     {
    ///         if (JsObjectShape.GetHostState(o) is NodeTypeInfo info)
    ///         {
    ///             return new JsString(info.InterfaceName);
    ///         }
    ///     }
    ///
    ///     return JsValue.Undefined;
    /// }
    /// </code>
    /// </example>
    /// <param name="target">The object to read the state from.</param>
    /// <remarks>
    /// The state is stored on the object created by <c>Instantiate</c>, which keeps it for its whole life —
    /// including after script has done something the shared layout cannot express (deleting a member, adding
    /// an integer-like key) and the object has fallen back to the ordinary property representation. Only that
    /// object carries it: an object merely <em>inheriting</em> from a shaped prototype has none of its own.
    /// </remarks>
    public static object? GetHostState(ObjectInstance? target) => (target as SharedShapeObject)?.HostState;

    /// <summary>The shared member layout, handed to the engine's <c>IBuiltinShaped</c> storage protocol.</summary>
    internal BuiltinShape Layout => _layout;

    /// <summary>
    /// The slots declared through the value form of <c>PerRealmSlot</c>, which the engine's shared-layout
    /// storage requires to carry a live descriptor from initialization on (unlike function and factory
    /// slots, it has no way to produce one on demand).
    /// </summary>
    internal PerRealmValueSlot[] PerRealmValueSlots => _perRealmValueSlots;

    /// <summary>
    /// The symbol-keyed members, in declaration order. Symbols are orthogonal to the shared string-keyed
    /// layout, so these are applied per instantiated object rather than through it.
    /// </summary>
    internal SymbolMember[] SymbolMembers => _symbolMembers;

    /// <summary>
    /// Creates the function object for a method slot or an accessor half, in <paramref name="realm"/>.
    /// Called once per (instantiated object, member) by the engine's lazy slot materialization.
    /// </summary>
    internal Jint.Native.Function.Function MakeFunction(Engine engine, Realm realm, ushort slot)
    {
        var entry = _functions[slot];
        return new ClrFunction(engine, realm, entry.Name, entry.Implementation, entry.Length);
    }

    /// <summary>
    /// The descriptor one instantiated object gets for symbol member <paramref name="index"/>. Built per
    /// object, never shared: a symbol member is configurable or writable in every shape WebIDL and
    /// ECMAScript give one, and one shared mutable descriptor would let one engine rewrite what another
    /// reads — the hazard <see cref="Builder.Constant"/> states in full for the string-keyed case.
    /// </summary>
    /// <remarks>
    /// A method and a per-realm slot are lazy through <see cref="PropertyDescriptor.CreateLazy{TState}"/>,
    /// so declaring one costs an unmaterialized descriptor and nothing else until a script reads the member.
    /// An accessor pair is not: <c>Object.getOwnPropertyDescriptor</c> has to observe a settled pair, so both
    /// halves are created together, here.
    /// </remarks>
    internal PropertyDescriptor MakeSymbolDescriptor(ObjectInstance owner, Engine engine, Realm realm, int index)
    {
        var member = _symbolMembers[index];
        switch (member.Kind)
        {
            case SymbolMemberKind.Constant:
                return new PropertyDescriptor(member.Constant!, member.Flags);

            case SymbolMemberKind.Method:
                return PropertyDescriptor.CreateLazy(
                    (Shape: this, Engine: engine, Realm: realm, member.GetterSlot),
                    static state => state.Shape.MakeFunction(state.Engine, state.Realm, state.GetterSlot),
                    member.Flags);

            case SymbolMemberKind.PerRealmFactory:
                return PropertyDescriptor.CreateLazy(
                    (Owner: owner, Factory: member.Factory!),
                    static state => state.Factory(state.Owner) ?? JsValue.Undefined,
                    member.Flags);

            default:
                return new GetSetPropertyDescriptor(
                    member.GetterSlot == BuiltinShape.NotAFunction ? null : MakeFunction(engine, realm, member.GetterSlot),
                    member.SetterSlot == BuiltinShape.NotAFunction ? null : MakeFunction(engine, realm, member.SetterSlot),
                    member.Flags);
        }
    }

    /// <summary>What a symbol-keyed member is; the symbol siblings of the string-keyed member kinds.</summary>
    internal enum SymbolMemberKind : byte
    {
        Constant,
        Method,
        Accessor,
        PerRealmFactory,
    }

    /// <summary>One symbol-keyed member: its key, its kind, its attributes and whichever payload it needs.</summary>
    [StructLayout(LayoutKind.Auto)]
    internal readonly record struct SymbolMember(
        JsSymbol Key,
        SymbolMemberKind Kind,
        PropertyFlag Flags,
        JsValue? Constant,
        ushort GetterSlot,
        ushort SetterSlot,
        Func<ObjectInstance, JsValue>? Factory);

    /// <summary>One entry of the shape's process-shared function table: everything a per-realm function needs.</summary>
    [StructLayout(LayoutKind.Auto)]
    private readonly record struct FunctionEntry(string Name, JsCallDelegate Implementation, int Length);

    /// <summary>A value-form per-realm slot: which slot to pre-fill, and with which attributes.</summary>
    [StructLayout(LayoutKind.Auto)]
    internal readonly record struct PerRealmValueSlot(int Slot, PropertyFlag Flags);

    /// <summary>
    /// Accumulates member declarations, in own-key (declaration) order, and produces the shared shape.
    /// The builder is <b>not</b> thread-safe and must not be used after <see cref="Build"/>; the shape it
    /// builds is thread-safe and process-shareable.
    /// <para>
    /// Member names must be non-empty, distinct, and must not start with a digit — an integer-index-like
    /// key has to enumerate in ascending numeric order ahead of the string keys, which a fixed member list
    /// cannot express.
    /// </para>
    /// </summary>
    public sealed class Builder
    {
        private readonly List<MemberDeclaration> _members = [];
        private readonly List<FunctionEntry> _functions = [];
        private readonly HashSet<string> _declaredNames = new(StringComparer.Ordinal);
        private readonly List<SymbolMember> _symbolMembers = [];
        private readonly HashSet<JsSymbol> _declaredSymbols = [];
        private bool _toStringTagDeclared;
        private bool _built;

        /// <summary>Creates an empty builder.</summary>
        public Builder()
        {
        }

        /// <summary>
        /// Declares a method: a data property whose value is a function created per engine on first access,
        /// exactly like an intrinsic prototype method. Attribute defaults follow Web IDL operations
        /// (<c>{ writable: true, enumerable: true, configurable: true }</c>); pass
        /// <paramref name="enumerable"/><c>: false</c> for ECMAScript-built-in style.
        /// <para>
        /// <paramref name="implementation"/> is invoked as <c>(thisObject, arguments)</c> — the same
        /// signature <see cref="ClrFunction"/> takes — and MUST be engine-independent: a static lambda, or
        /// one capturing only engine-independent CLR state (a <c>MemberInfo</c>, a compiled open delegate,
        /// attribute metadata). It may run on behalf of any engine that instantiated the shape, concurrently
        /// with other engines; reach the current engine through the receiver
        /// (<c>((ObjectInstance) thisObject).Engine</c>) when it is needed.
        /// </para>
        /// </summary>
        /// <param name="name">The property name.</param>
        /// <param name="implementation">The method body.</param>
        /// <param name="length">The function's <c>length</c> — its declared parameter count.</param>
        /// <param name="enumerable">Whether the property is enumerable.</param>
        /// <param name="writable">Whether the property is writable.</param>
        /// <param name="configurable">Whether the property is configurable.</param>
        /// <returns>This builder, for chaining.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="implementation"/> is <c>null</c>.</exception>
        /// <exception cref="ArgumentException"><paramref name="name"/> is invalid or already declared.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="length"/> is negative.</exception>
        /// <exception cref="InvalidOperationException">The shape has already been built.</exception>
        public Builder Method(
            string name,
            Func<JsValue, JsValue[], JsValue> implementation,
            int length = 0,
            bool enumerable = true,
            bool writable = true,
            bool configurable = true)
        {
            DeclareName(name);

            if (implementation is null)
            {
                Throw.ArgumentNullException(nameof(implementation));
            }

            if (length < 0)
            {
                Throw.ArgumentOutOfRangeException(nameof(length), "A function's length must not be negative.");
            }

            var slot = AddFunction(name, implementation, length);
            _members.Add(new MemberDeclaration(
                name,
                MemberKind.Method,
                DataFlags(enumerable, writable, configurable),
                slot,
                BuiltinShape.NotAFunction,
                Constant: null,
                Factory: null));
            return this;
        }

        /// <summary>
        /// Declares an accessor property. The getter and setter functions are created per engine on first
        /// access of the property, both halves together so that
        /// <c>Object.getOwnPropertyDescriptor</c> observes a settled pair; an absent half surfaces as
        /// <c>undefined</c>, per spec. The materialized functions are named <c>"get name"</c> /
        /// <c>"set name"</c> per spec convention. Defaults follow Web IDL attributes
        /// (<c>{ enumerable: true, configurable: true }</c>).
        /// <para>
        /// The delegates carry the same engine-independence contract as
        /// <see cref="Method(string, Func{JsValue, JsValue[], JsValue}, int, bool, bool, bool)"/>.
        /// </para>
        /// </summary>
        /// <param name="name">The property name.</param>
        /// <param name="getter">The getter body, or <c>null</c> for a set-only property.</param>
        /// <param name="setter">The setter body, or <c>null</c> for a get-only property.</param>
        /// <param name="enumerable">Whether the property is enumerable.</param>
        /// <param name="configurable">Whether the property is configurable.</param>
        /// <returns>This builder, for chaining.</returns>
        /// <exception cref="ArgumentException">
        /// <paramref name="name"/> is invalid or already declared, or both <paramref name="getter"/> and
        /// <paramref name="setter"/> are <c>null</c>.
        /// </exception>
        /// <exception cref="InvalidOperationException">The shape has already been built.</exception>
        public Builder Accessor(
            string name,
            Func<JsValue, JsValue[], JsValue>? getter,
            Func<JsValue, JsValue[], JsValue>? setter = null,
            bool enumerable = true,
            bool configurable = true)
        {
            DeclareName(name);

            if (getter is null && setter is null)
            {
                Throw.ArgumentException(
                    $"Accessor '{name}' declares neither a getter nor a setter; at least one half is required.",
                    nameof(getter));
            }

            var getterSlot = getter is null ? BuiltinShape.NotAFunction : AddFunction("get " + name, getter, 0);
            var setterSlot = setter is null ? BuiltinShape.NotAFunction : AddFunction("set " + name, setter, 1);

            // GetSetPropertyDescriptor owns the writable bits (an accessor has none), so only the
            // enumerable/configurable claims travel with the slot.
            var flags = (enumerable ? PropertyFlag.Enumerable : PropertyFlag.EnumerableSet)
                        | (configurable ? PropertyFlag.Configurable : PropertyFlag.ConfigurableSet);

            _members.Add(new MemberDeclaration(
                name,
                MemberKind.Accessor,
                flags,
                getterSlot,
                setterSlot,
                Constant: null,
                Factory: null));
            return this;
        }

        /// <summary>
        /// Declares an immutable constant: <c>{ value, writable: false, enumerable, configurable: false }</c>
        /// — the Web IDL constant shape.
        /// <para>
        /// The attributes are forced rather than offered because the descriptor is created once and shared
        /// by every engine that instantiates the shape: a writable or configurable shared descriptor could
        /// be mutated through one engine and observed through another.
        /// </para>
        /// <para>
        /// <paramref name="value"/> must be a primitive — number, string, boolean, bigint, <c>null</c>,
        /// <c>undefined</c>, or a symbol. Object values are engine-affine and are rejected; declare an
        /// object-valued member with
        /// <see cref="PerRealmSlot(string, Func{ObjectInstance, JsValue}, bool, bool, bool)"/> instead.
        /// </para>
        /// </summary>
        /// <param name="name">The property name.</param>
        /// <param name="value">The constant value; must be a primitive.</param>
        /// <param name="enumerable">Whether the property is enumerable.</param>
        /// <returns>This builder, for chaining.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="value"/> is <c>null</c>.</exception>
        /// <exception cref="ArgumentException">
        /// <paramref name="name"/> is invalid or already declared, or <paramref name="value"/> is an object.
        /// </exception>
        /// <exception cref="InvalidOperationException">The shape has already been built.</exception>
        public Builder Constant(string name, JsValue value, bool enumerable = true)
        {
            DeclareName(name);

            if (value is null)
            {
                Throw.ArgumentNullException(nameof(value));
            }

            if (value is ObjectInstance)
            {
                Throw.ArgumentException(
                    $"Constant '{name}' has an object value. A constant's descriptor is shared by every engine that instantiates the shape, and an object belongs to exactly one engine; declare an object-valued member with PerRealmSlot instead.",
                    nameof(value));
            }

            // writable:false + configurable:false is what makes sharing one descriptor across engines sound.
            // ValidateAndApplyPropertyDescriptor mutates the *current* descriptor in place, and for a constant
            // slot "current" is the process-shared instance — so a writable or configurable constant would let
            // one engine's `p.X = 1` or `Object.defineProperty(p, 'X', …)` rewrite what every other engine
            // reads. With both attributes off, every mutation that reaches the descriptor is provably a
            // same-value store: a value write requires SameValue to have passed first, and an attribute write
            // re-stores the flag it already carries; anything that would actually change is rejected by the
            // non-configurable checks before the descriptor is touched.
            var flags = enumerable ? PropertyFlag.OnlyEnumerable : PropertyFlag.AllForbidden;
            _members.Add(new MemberDeclaration(
                name,
                MemberKind.Constant,
                flags,
                BuiltinShape.NotAFunction,
                BuiltinShape.NotAFunction,
                new PropertyDescriptor(value, flags),
                Factory: null));
            return this;
        }

        /// <summary>
        /// Declares a data property whose value differs per engine — the canonical case being
        /// <c>constructor</c>. The slot starts as <c>undefined</c> with the declared attributes on every
        /// instantiated object; the host fills it after creating the object, either with
        /// <see cref="ObjectInstance.DefineOwnPropertyUnchecked(string, PropertyDescriptor)"/> (a setup-time in-place
        /// slot replacement — on a shaped object a declared name never falls back to a dictionary) or with
        /// <c>DefineOwnProperty</c> / <c>Set</c>, which are spec-validated and therefore need the declared
        /// attributes to permit the write. That is why the slot must be declared writable or configurable.
        /// </summary>
        /// <param name="name">The property name.</param>
        /// <param name="enumerable">Whether the property is enumerable.</param>
        /// <param name="writable">Whether the property is writable.</param>
        /// <param name="configurable">Whether the property is configurable.</param>
        /// <returns>This builder, for chaining.</returns>
        /// <exception cref="ArgumentException">
        /// <paramref name="name"/> is invalid or already declared, or the slot is declared neither writable
        /// nor configurable, which would make it impossible to fill.
        /// </exception>
        /// <exception cref="InvalidOperationException">The shape has already been built.</exception>
        public Builder PerRealmSlot(
            string name,
            bool enumerable = false,
            bool writable = true,
            bool configurable = true)
        {
            DeclareName(name);

            if (!writable && !configurable)
            {
                Throw.ArgumentException(
                    $"Per-realm slot '{name}' is declared neither writable nor configurable, so nothing could ever fill it; declare it writable or configurable, or use Constant for an immutable member.",
                    nameof(writable));
            }

            _members.Add(new MemberDeclaration(
                name,
                MemberKind.PerRealmValue,
                DataFlags(enumerable, writable, configurable),
                BuiltinShape.NotAFunction,
                BuiltinShape.NotAFunction,
                Constant: null,
                Factory: null));
            return this;
        }

        /// <summary>
        /// Per-realm slot variant that stays fully lazy: <paramref name="factory"/> runs at most once per
        /// instantiated object, on the first access of that member in that engine, and the result is cached
        /// there. This is how a prototype's <c>constructor</c> reference is declared without forcing the
        /// constructor — or the prototype's member table — into existence at setup time.
        /// <para>
        /// The factory receives the instantiated object (reach the engine through
        /// <see cref="ObjectInstance.Engine"/>) and must itself be engine-independent — a static lambda, with
        /// all per-engine state coming from the argument. Note that the factory also runs if the object ever
        /// falls back to the ordinary property dictionary (for instance because a member was deleted) while
        /// this member is still untouched, so it should be cheap and free of side effects beyond producing
        /// the value.
        /// </para>
        /// </summary>
        /// <param name="name">The property name.</param>
        /// <param name="factory">Produces the value for one instantiated object.</param>
        /// <param name="enumerable">Whether the property is enumerable.</param>
        /// <param name="writable">Whether the property is writable.</param>
        /// <param name="configurable">Whether the property is configurable.</param>
        /// <returns>This builder, for chaining.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="factory"/> is <c>null</c>.</exception>
        /// <exception cref="ArgumentException"><paramref name="name"/> is invalid or already declared.</exception>
        /// <exception cref="InvalidOperationException">The shape has already been built.</exception>
        public Builder PerRealmSlot(
            string name,
            Func<ObjectInstance, JsValue> factory,
            bool enumerable = false,
            bool writable = true,
            bool configurable = true)
        {
            DeclareName(name);

            if (factory is null)
            {
                Throw.ArgumentNullException(nameof(factory));
            }

            var flags = DataFlags(enumerable, writable, configurable);

            // Captures only the host's factory and the declared attributes — both process-shared — so the
            // closure keeps the engine-independence the shared layout requires of a slot factory.
            _members.Add(new MemberDeclaration(
                name,
                MemberKind.PerRealmFactory,
                flags,
                BuiltinShape.NotAFunction,
                BuiltinShape.NotAFunction,
                Constant: null,
                o => new PropertyDescriptor(factory(o) ?? JsValue.Undefined, flags)));
            return this;
        }

        /// <summary>
        /// Declares a symbol-keyed method — <c>Symbol.iterator</c>, <c>Symbol.hasInstance</c>,
        /// <c>Symbol.toPrimitive</c> — whose function is created per engine on first access of the member.
        /// Attribute defaults are the ones both Web IDL and ECMAScript give a symbol-keyed operation
        /// (<c>{ writable: true, enumerable: false, configurable: true }</c>): a symbol-keyed member is
        /// <em>not</em> enumerable, which is the one place Web IDL agrees with ECMAScript rather than
        /// diverging from it.
        /// <para>
        /// The materialized function is named the way
        /// <see href="https://tc39.es/ecma262/#sec-setfunctionname">SetFunctionName</see> names one under a
        /// symbol key — <c>"[Symbol.iterator]"</c> — so nothing has to be passed for it.
        /// <paramref name="implementation"/> carries the same engine-independence contract as
        /// <see cref="Method(string, Func{JsValue, JsValue[], JsValue}, int, bool, bool, bool)"/>.
        /// </para>
        /// </summary>
        /// <param name="key">The symbol key. Well-known symbols are process-shared and are the intended use.</param>
        /// <param name="implementation">The method body.</param>
        /// <param name="length">The function's <c>length</c> — its declared parameter count.</param>
        /// <param name="enumerable">Whether the property is enumerable.</param>
        /// <param name="writable">Whether the property is writable.</param>
        /// <param name="configurable">Whether the property is configurable.</param>
        /// <returns>This builder, for chaining.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="key"/> or <paramref name="implementation"/> is <c>null</c>.</exception>
        /// <exception cref="ArgumentException"><paramref name="key"/> is already declared.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="length"/> is negative.</exception>
        /// <exception cref="InvalidOperationException">The shape has already been built.</exception>
        public Builder Method(
            JsSymbol key,
            Func<JsValue, JsValue[], JsValue> implementation,
            int length = 0,
            bool enumerable = false,
            bool writable = true,
            bool configurable = true)
        {
            DeclareSymbol(key);

            if (implementation is null)
            {
                Throw.ArgumentNullException(nameof(implementation));
            }

            if (length < 0)
            {
                Throw.ArgumentOutOfRangeException(nameof(length), "A function's length must not be negative.");
            }

            var slot = AddFunction(FunctionName(key), implementation, length);
            _symbolMembers.Add(new SymbolMember(
                key,
                SymbolMemberKind.Method,
                DataFlags(enumerable, writable, configurable),
                Constant: null,
                slot,
                BuiltinShape.NotAFunction,
                Factory: null));
            return this;
        }

        /// <summary>
        /// Declares a symbol-keyed accessor property — <c>Symbol.toStringTag</c> as a getter,
        /// <c>Symbol.species</c>. Both halves are created together when the object initializes, so that
        /// <c>Object.getOwnPropertyDescriptor</c> observes a settled pair; an absent half surfaces as
        /// <c>undefined</c>, per spec. Defaults are <c>{ enumerable: false, configurable: true }</c>.
        /// <para>
        /// The delegates carry the same engine-independence contract as
        /// <see cref="Method(string, Func{JsValue, JsValue[], JsValue}, int, bool, bool, bool)"/>, and the
        /// materialized functions are named <c>"get [Symbol.x]"</c> / <c>"set [Symbol.x]"</c>.
        /// </para>
        /// </summary>
        /// <param name="key">The symbol key.</param>
        /// <param name="getter">The getter body, or <c>null</c> for a set-only property.</param>
        /// <param name="setter">The setter body, or <c>null</c> for a get-only property.</param>
        /// <param name="enumerable">Whether the property is enumerable.</param>
        /// <param name="configurable">Whether the property is configurable.</param>
        /// <returns>This builder, for chaining.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="key"/> is <c>null</c>.</exception>
        /// <exception cref="ArgumentException">
        /// <paramref name="key"/> is already declared, or both <paramref name="getter"/> and
        /// <paramref name="setter"/> are <c>null</c>.
        /// </exception>
        /// <exception cref="InvalidOperationException">The shape has already been built.</exception>
        public Builder Accessor(
            JsSymbol key,
            Func<JsValue, JsValue[], JsValue>? getter,
            Func<JsValue, JsValue[], JsValue>? setter = null,
            bool enumerable = false,
            bool configurable = true)
        {
            DeclareSymbol(key);

            if (getter is null && setter is null)
            {
                Throw.ArgumentException(
                    $"Accessor '{key}' declares neither a getter nor a setter; at least one half is required.",
                    nameof(getter));
            }

            var name = FunctionName(key);
            var getterSlot = getter is null ? BuiltinShape.NotAFunction : AddFunction("get " + name, getter, 0);
            var setterSlot = setter is null ? BuiltinShape.NotAFunction : AddFunction("set " + name, setter, 1);

            // GetSetPropertyDescriptor owns the writable bits (an accessor has none), so only the
            // enumerable/configurable claims travel with the member.
            var flags = (enumerable ? PropertyFlag.Enumerable : PropertyFlag.EnumerableSet)
                        | (configurable ? PropertyFlag.Configurable : PropertyFlag.ConfigurableSet);

            _symbolMembers.Add(new SymbolMember(
                key,
                SymbolMemberKind.Accessor,
                flags,
                Constant: null,
                getterSlot,
                setterSlot,
                Factory: null));
            return this;
        }

        /// <summary>
        /// Declares a symbol-keyed data property whose value differs per engine, produced by
        /// <paramref name="factory"/> on the first access of that member in that engine. This is what a
        /// member whose value is an <em>intrinsic</em> needs: Web IDL's
        /// <see href="https://webidl.spec.whatwg.org/#es-iterable-entries">iterable&lt;&gt;</see> declaration
        /// makes <c>@@iterator</c> the very <c>%Array.prototype.values%</c> function object of the realm, not
        /// a wrapper around it, and only a per-realm value can be that.
        /// <para>
        /// The factory receives the instantiated object (reach the engine through
        /// <see cref="ObjectInstance.Engine"/>) and must itself be engine-independent — a static lambda, with
        /// all per-engine state coming from the argument.
        /// </para>
        /// </summary>
        /// <param name="key">The symbol key.</param>
        /// <param name="factory">Produces the value for one instantiated object.</param>
        /// <param name="enumerable">Whether the property is enumerable.</param>
        /// <param name="writable">Whether the property is writable.</param>
        /// <param name="configurable">Whether the property is configurable.</param>
        /// <returns>This builder, for chaining.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="key"/> or <paramref name="factory"/> is <c>null</c>.</exception>
        /// <exception cref="ArgumentException"><paramref name="key"/> is already declared.</exception>
        /// <exception cref="InvalidOperationException">The shape has already been built.</exception>
        public Builder PerRealmSlot(
            JsSymbol key,
            Func<ObjectInstance, JsValue> factory,
            bool enumerable = false,
            bool writable = true,
            bool configurable = true)
        {
            DeclareSymbol(key);

            if (factory is null)
            {
                Throw.ArgumentNullException(nameof(factory));
            }

            _symbolMembers.Add(new SymbolMember(
                key,
                SymbolMemberKind.PerRealmFactory,
                DataFlags(enumerable, writable, configurable),
                Constant: null,
                BuiltinShape.NotAFunction,
                BuiltinShape.NotAFunction,
                factory));
            return this;
        }

        /// <summary>
        /// Declares the <c>Symbol.toStringTag</c> value (non-writable, non-enumerable, configurable — the
        /// intrinsic convention), which is what <c>Object.prototype.toString.call(obj)</c> reports. Sugar for
        /// the one symbol-keyed constant every host prototype wants.
        /// <para>
        /// Symbols are stored per object — the shared string-keyed layout is orthogonal to them — so this
        /// costs one descriptor per instantiated object, applied on first touch along with the rest of the
        /// object's initialization, and the same is true of every other symbol-keyed member declared here.
        /// </para>
        /// </summary>
        /// <param name="value">The tag, e.g. the Web IDL interface name.</param>
        /// <returns>This builder, for chaining.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="value"/> is <c>null</c>.</exception>
        /// <exception cref="InvalidOperationException">
        /// The tag has already been declared, or the shape has already been built.
        /// </exception>
        public Builder ToStringTag(string value)
        {
            ThrowIfBuilt();

            if (value is null)
            {
                Throw.ArgumentNullException(nameof(value));
            }

            if (_toStringTagDeclared)
            {
                Throw.InvalidOperationException("Symbol.toStringTag has already been declared on this shape.");
            }

            _toStringTagDeclared = true;
            DeclareSymbol(GlobalSymbolRegistry.ToStringTag);
            _symbolMembers.Add(new SymbolMember(
                GlobalSymbolRegistry.ToStringTag,
                SymbolMemberKind.Constant,
                PropertyFlag.Configurable,
                JsString.Create(value),
                BuiltinShape.NotAFunction,
                BuiltinShape.NotAFunction,
                Factory: null));
            return this;
        }

        /// <summary>
        /// Builds the immutable shape. The builder must not be used again afterwards.
        /// </summary>
        /// <returns>The shared, thread-safe shape.</returns>
        /// <exception cref="ArgumentException">More than <c>4096</c> members were declared.</exception>
        /// <exception cref="InvalidOperationException">The shape has already been built.</exception>
        public JsObjectShape Build()
        {
            ThrowIfBuilt();

            var count = _members.Count;
            if (count > MaxMembers)
            {
                Throw.ArgumentException(
                    $"A shape can describe at most {MaxMembers} members, but {count} were declared.");
            }

            _built = true;

            var layoutBuilder = new BuiltinShape.Builder(count);
            var perRealmValueSlots = new List<PerRealmValueSlot>();
            for (var i = 0; i < count; i++)
            {
                var member = _members[i];
                Key key = member.Name;
                switch (member.Kind)
                {
                    case MemberKind.Constant:
                        layoutBuilder.Constant(in key, member.Constant!);
                        break;
                    case MemberKind.Method:
                        layoutBuilder.Function(in key, member.GetterSlot, member.Flags);
                        break;
                    case MemberKind.Accessor:
                        layoutBuilder.Accessor(in key, member.GetterSlot, member.SetterSlot, member.Flags);
                        break;
                    case MemberKind.PerRealmValue:
                        layoutBuilder.Instance(in key);
                        perRealmValueSlots.Add(new PerRealmValueSlot(i, member.Flags));
                        break;
                    default:
                        layoutBuilder.Factory(in key, member.Factory!);
                        break;
                }
            }

            return new JsObjectShape(
                layoutBuilder.Build(),
                _functions.ToArray(),
                perRealmValueSlots.Count > 0 ? perRealmValueSlots.ToArray() : [],
                _symbolMembers.Count > 0 ? _symbolMembers.ToArray() : []);
        }

        private ushort AddFunction(string name, JsCallDelegate implementation, int length)
        {
            if (_functions.Count >= BuiltinShape.NotAFunction)
            {
                Throw.ArgumentException(
                    $"A shape can describe at most {MaxMembers} members, and this one has already exhausted the function-slot space.");
            }

            var slot = (ushort) _functions.Count;
            _functions.Add(new FunctionEntry(name, implementation, length));
            return slot;
        }

        private void DeclareName(string name)
        {
            ThrowIfBuilt();

            if (string.IsNullOrEmpty(name))
            {
                Throw.ArgumentException("Member names must be non-empty strings.", nameof(name));
            }

            if (Shape.IsIntegerIndexLikeKey(name))
            {
                Throw.ArgumentException(
                    $"Member name '{name}' starts with a digit. Integer-index-like keys must be enumerated in ascending numeric order before the string keys, which a fixed member list cannot express.",
                    nameof(name));
            }

            if (!_declaredNames.Add(name))
            {
                Throw.ArgumentException($"Member '{name}' has already been declared; member names must be distinct.", nameof(name));
            }
        }

        private void DeclareSymbol(JsSymbol key)
        {
            ThrowIfBuilt();

            if (key is null)
            {
                Throw.ArgumentNullException(nameof(key));
            }

            if (!_declaredSymbols.Add(key))
            {
                Throw.ArgumentException($"Member '{key}' has already been declared; member keys must be distinct.", nameof(key));
            }
        }

        /// <summary>
        /// <see href="https://tc39.es/ecma262/#sec-setfunctionname">SetFunctionName</see> for a symbol key:
        /// the description in brackets, or the empty string for a symbol that has none.
        /// </summary>
        private static string FunctionName(JsSymbol key)
            => key._value.IsUndefined() ? "" : "[" + key._value.AsString() + "]";

        private void ThrowIfBuilt()
        {
            if (_built)
            {
                Throw.InvalidOperationException("The shape has already been built; a builder must not be reused or modified after Build().");
            }
        }

        private static PropertyFlag DataFlags(bool enumerable, bool writable, bool configurable)
            => (enumerable ? PropertyFlag.Enumerable : PropertyFlag.EnumerableSet)
               | (writable ? PropertyFlag.Writable : PropertyFlag.WritableSet)
               | (configurable ? PropertyFlag.Configurable : PropertyFlag.ConfigurableSet);

        private enum MemberKind : byte
        {
            Constant,
            Method,
            Accessor,
            PerRealmValue,
            PerRealmFactory,
        }

        [StructLayout(LayoutKind.Auto)]
        private readonly record struct MemberDeclaration(
            string Name,
            MemberKind Kind,
            PropertyFlag Flags,
            ushort GetterSlot,
            ushort SetterSlot,
            PropertyDescriptor? Constant,
            Func<ObjectInstance, PropertyDescriptor>? Factory);
    }
}
