using Jint.Runtime.Descriptors;

namespace Jint.Native.Object;

/// <summary>
/// Per-host storage surface for a built-in whose string-keyed own properties are a shared, immutable
/// <see cref="BuiltinShape"/> plus a per-realm, lazily-filled descriptor array (rather than a per-realm
/// property dictionary). The host carries <see cref="Jint.Runtime.InternalTypes.BuiltinShapeMode"/>;
/// <see cref="ObjectInstance"/>'s property virtuals (GetOwnProperty / SetOwnProperty / RemoveOwnProperty /
/// GetOwnProperties / GetOwnPropertyKeys) dispatch to the shared shape logic when that flag is set.
/// <para>
/// This interface exposes only the per-host <b>storage</b> — no behavior — so the mechanism composes
/// across host base classes (<see cref="Jint.Native.Prototype"/>, <c>Constructor</c>/<c>Function</c>,
/// instance types like <c>ArrayInstance</c>) that cannot all share a single <see cref="BuiltinShapeObject"/>
/// base under single inheritance. <see cref="BuiltinShapeObject"/> implements it with a field; the source
/// generator emits an equivalent field + implementation onto any other shaped host.
/// </para>
/// </summary>
internal interface IBuiltinShaped
{
    /// <summary>The process-shared layout for this built-in type (typically a <c>static readonly</c> field).</summary>
    BuiltinShape BuiltinShape { get; }

    /// <summary>
    /// The per-realm descriptor array (parallel to <see cref="BuiltinShape.Names"/>): constants point at the
    /// shared static descriptors, functions/accessors are null until first materialized. <c>null</c> once the
    /// host has deopted to the ordinary <c>_properties</c> dictionary.
    /// </summary>
    PropertyDescriptor?[]? BuiltinDescriptors { get; set; }

    /// <summary>Creates the dispatcher function for a function slot (e.g. <c>new __XxxFunction(this, (Slot) slot)</c>).</summary>
    Jint.Native.Function.Function MakeBuiltinFunction(ushort slot);
}
