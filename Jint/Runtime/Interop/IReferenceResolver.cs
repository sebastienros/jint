using Jint.Native;

namespace Jint.Runtime.Interop;

/// <summary>
/// Reference resolver allows customizing behavior for reference resolving. This can be useful in cases where
/// you want to ignore long chain of property accesses that might throw if anything is null or undefined.
/// An example of such is <code>var a = obj.field.subField.value</code>. Custom resolver could accept chain to return
/// null/undefined on first occurrence.
/// </summary>
public interface IReferenceResolver
{
    /// <summary>
    /// When unresolvable reference occurs, check if another value can be provided instead of it.
    /// </summary>
    /// <remarks>
    /// A reference error will be thrown if this method return false.
    /// </remarks>
    /// <param name="engine">The current engine instance.</param>
    /// <param name="reference">The reference that is being processed.</param>
    /// <param name="value">Value that should be used instead of undefined.</param>
    /// <returns>Whether to use <paramref name="value" /> instead of undefined.</returns>
    bool TryUnresolvableReference(Engine engine, Reference reference, out JsValue value);

    /// <summary>
    /// When property reference is being processed, resolve to other value if needed.
    /// </summary>
    /// <param name="engine">The current engine instance.</param>
    /// <param name="reference">The reference that is being processed.</param>
    /// <param name="value">Value that should be used instead of reference target.</param>
    /// <returns>Whether to use <paramref name="value" /> instead of reference's value.</returns>
    bool TryPropertyReference(Engine engine, Reference reference, ref JsValue value);

    /// <summary>
    /// When evaluating a function call and a target that is not an object is encountered,
    /// custom implementation can return a value to call.
    /// </summary>
    /// <remarks>
    /// A reference error will be thrown if this method return false.
    /// </remarks>
    /// <param name="engine">The current engine instance.</param>
    /// <param name="callee">The callee.</param>
    /// <param name="value">Value that should be used when this method return true. Should be <see cref="ICallable"/>.</param>
    /// <returns>Whether to use <paramref name="value" /> instead of undefined.</returns>
    bool TryGetCallable(Engine engine, object callee, out JsValue value);

    /// <summary>
    /// Check whether objects property value is valid.
    /// </summary>
    /// <param name="value">The value to check</param>
    /// <returns>Whether to accept the value.</returns>
    bool CheckCoercible(JsValue value);
}
