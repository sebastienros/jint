using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.Native.Object;

/// <summary>
/// Convenience base for built-in objects (Math, JSON, Reflect, ...) that store their string-keyed own
/// properties as a shared immutable <see cref="BuiltinShape"/> plus a per-realm, lazily-filled descriptor
/// array, instead of building a per-realm dictionary of descriptors in <c>Initialize</c>. Immutable constants
/// reuse process-shared descriptors; function descriptors are created on first access (their dispatcher
/// <see cref="Function"/> was already lazy, so nothing is materialized eagerly that wasn't already).
/// <para>
/// The behavior lives on <see cref="ObjectInstance"/>, gated by <see cref="InternalTypes.BuiltinShapeMode"/>
/// and reached through <see cref="IBuiltinShaped"/>; this base just supplies the per-realm descriptor field
/// and the two abstract hooks. Hosts that cannot derive from it (prototypes, constructors) get an equivalent
/// field + <see cref="IBuiltinShaped"/> implementation emitted by the source generator instead.
/// </para>
/// <para>
/// Redefining an existing property's attributes mutates the per-realm descriptor in place exactly as the
/// dictionary path does (so test262's verifyProperty needs no deopt); adding or deleting an own property
/// deopts to the ordinary dictionary, after which every path falls back to the unchanged base behavior.
/// </para>
/// <para>
/// A shaped host's own string properties are exactly those in its <see cref="BuiltinShape"/>. It must
/// therefore declare every own property through the generator surface — it must <b>not</b> add properties
/// via the raw <c>SetProperty</c> primitive in <c>Initialize</c> (those land in <c>_properties</c>, which
/// the shape lookup does not consult). A built-in that needs to (e.g. Intl, which registers constructor
/// references from the realm intrinsics) is not shape-eligible and should not derive from this type.
/// </para>
/// </summary>
internal abstract class BuiltinShapeObject : ObjectInstance, IBuiltinShaped
{
    // Lazily-filled descriptors, parallel to BuiltinShape.Names: constants point at the shared static
    // descriptors, functions are null until first accessed. Null == deopted to the base _properties.
    private PropertyDescriptor?[]? _builtinDescriptors;

    protected BuiltinShapeObject(Engine engine) : base(engine)
    {
    }

    /// <summary>The shared layout for this built-in (typically a <c>static readonly</c> field).</summary>
    private protected abstract BuiltinShape BuiltinShape { get; }

    /// <summary>Creates the dispatcher function for a function slot (e.g. <c>new __XxxFunction(this, (Slot) slot)</c>).</summary>
    private protected abstract Jint.Native.Function.Function MakeBuiltinFunction(ushort slot);

    BuiltinShape IBuiltinShaped.BuiltinShape => BuiltinShape;

    PropertyDescriptor?[]? IBuiltinShaped.BuiltinDescriptors
    {
        get => _builtinDescriptors;
        set => _builtinDescriptors = value;
    }

    Jint.Native.Function.Function IBuiltinShaped.MakeBuiltinFunction(ushort slot) => MakeBuiltinFunction(slot);
}
