namespace Jint.Runtime.Descriptors.Specialized;

/// <summary>
/// Marks a <see cref="PropertyDescriptor"/> whose <see cref="PropertyDescriptor.CustomValue"/> override is
/// nothing but a memo over the inherited <c>_value</c>/<c>_flags</c> fields: it resolves on first read, writes
/// the result back into <c>_value</c>, and clears <see cref="PropertyFlag.CustomJsValue"/> so later reads skip
/// the indirection entirely. For such a descriptor those two fields <em>are</em> the authoritative state, so
/// <see cref="Engine.AdvancedOperations.RestoreGlobalSnapshot"/> may revert it by writing them directly —
/// including back to the unmaterialized pair, which is exactly "return to the captured state".
/// </summary>
/// <remarks>
/// Deliberately internal, and deliberately not something a host descriptor can claim. A host
/// <see cref="PropertyDescriptor.CustomValue"/> override is free to keep its value anywhere — a field of its
/// own, a CLR property, a lookup on live host state — and the engine cannot revert what it cannot see.
/// Writing the inherited field on such a descriptor would not restore it; it would only desynchronize that
/// field from the value reads actually come from. So a snapshot restore reinstates a host
/// <see cref="PropertyFlag.CustomJsValue"/> descriptor by reference (and reverts its attribute flags), and
/// leaves its value alone.
/// </remarks>
internal interface IFieldBackedLazyDescriptor
{
}
