using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.Native.Object;

/// <summary>
/// The object <see cref="JsObjectShape.Instantiate(Engine, ObjectInstance)"/> hands a host: an ordinary
/// object whose string-keyed own properties are the shape's process-shared <see cref="BuiltinShape"/> plus
/// a per-engine, lazily-filled descriptor array — the exact storage Jint's own shaped intrinsics use,
/// reached through <see cref="IBuiltinShaped"/> and dispatched from <see cref="ObjectInstance"/>'s property
/// virtuals under <see cref="InternalTypes.BuiltinShapeMode"/>.
/// <para>
/// The type is deliberately <b>internal and sealed</b>, and a host reaches it only through the factory. Two
/// reasons. First, those <c>BuiltinShapeMode</c> branches reach this object's storage through
/// <c>Unsafe.As&lt;IBuiltinShaped&gt;</c> and trust the shape's invariants — names never leave the layout
/// without a deopt, materialization never bumps <c>_propertiesVersion</c>, post-initialization additions live
/// in the side dictionary — which a host override of <c>GetOwnProperty</c> / <c>Get</c> / <c>Delete</c> could
/// silently break. Sealing makes them invariants by construction. Second, it lets the object be built
/// through the internal <see cref="ObjectInstance"/> constructor, so it carries neither
/// <see cref="InternalTypes.OrdinaryGet"/> nor <see cref="InternalTypes.ExoticGet"/> — exactly like an in-box
/// intrinsic. That is what makes it eligible as a prototype-method inline-cache holder: its version does
/// witness its own name set, so a host instance reading an inherited member through it caches like a script
/// object reading through <c>Object.prototype</c>.
/// </para>
/// <para>
/// Nothing is materialized at construction. <see cref="Initialize"/> — which the base class runs on the first
/// property access of any kind — installs the shared layout, pre-fills the value-form per-realm slots and
/// applies the symbol-keyed members; each method, accessor and factory slot then produces its descriptor on
/// the first access of that member and caches it, so identity is stable from then on.
/// </para>
/// </summary>
internal sealed class SharedShapeObject : ObjectInstance, IBuiltinShaped
{
    private readonly JsObjectShape _shape;

    // The realm captured at instantiation. Materialization can be triggered while a *different* realm is
    // active (a ShadowRealm callback reading a member of a host prototype, say), and the functions this
    // object hands out must belong to the realm the object itself belongs to.
    private readonly Realm _realm;

    // Parallel to the shape's member list: constants point at the shared descriptors, everything else is
    // null until first materialized. Null once the object has deopted to the ordinary dictionary.
    private PropertyDescriptor?[]? _builtinDescriptors;

    /// <summary>
    /// Opaque per-instance state the host attached through <see cref="JsObjectShape.SetHostState"/>. The
    /// engine never reads it; it exists so that a host does not have to keep a side table keyed by the
    /// object it was handed. It lives here rather than on the shape precisely because it is per-instance:
    /// engine-affine state is fine in it, and it dies with this object.
    /// </summary>
    internal object? HostState;

    internal SharedShapeObject(Engine engine, JsObjectShape shape, ObjectInstance? prototype)
        : base(engine, ObjectClass.Object, InternalTypes.Object)
    {
        _shape = shape;
        _realm = engine.Realm;
        // Assigned unconditionally: the base constructor defaults to the realm's Object.prototype, and a
        // caller asking for a prototype-less object must get one.
        _prototype = prototype;
    }

    protected override void Initialize()
    {
        InitializeBuiltinShape();

        // Value-form per-realm slots are the one kind the shared storage cannot produce on demand — it has
        // no function to call and no factory to run — so they start life as `undefined` with their declared
        // attributes, ready for the host to fill.
        var valueSlots = _shape.PerRealmValueSlots;
        for (var i = 0; i < valueSlots.Length; i++)
        {
            SetBuiltinInstanceDescriptor(valueSlots[i].Slot, Undefined, valueSlots[i].Flags);
        }

        var symbolMembers = _shape.SymbolMembers;
        if (symbolMembers.Length > 0)
        {
            // Symbols live outside the shared string-keyed layout, so these are one descriptor per object —
            // and unmaterialized ones for every kind but an accessor pair, so declaring `Symbol.iterator` on
            // a prototype costs nothing until a script reaches for it.
            var symbols = new SymbolDictionary(symbolMembers.Length);
            for (var i = 0; i < symbolMembers.Length; i++)
            {
                symbols[symbolMembers[i].Key] = _shape.MakeSymbolDescriptor(this, _engine, _realm, i);
            }

            SetSymbols(symbols);
        }
    }

    BuiltinShape IBuiltinShaped.BuiltinShape => _shape.Layout;

    PropertyDescriptor?[]? IBuiltinShaped.BuiltinDescriptors
    {
        get => _builtinDescriptors;
        set => _builtinDescriptors = value;
    }

    Function.Function IBuiltinShaped.MakeBuiltinFunction(ushort slot) => _shape.MakeFunction(_engine, _realm, slot);
}
