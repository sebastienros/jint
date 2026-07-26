namespace Jint.Native.Object;

/// <summary>
/// How a host-defined <see cref="ObjectInstance"/> subclass resolves property reads. Declared once from the
/// constructor via <see cref="ObjectInstance.SetPropertyAccessSemantics"/>; the engine never infers it.
/// <para>
/// The declaration is a promise about <see cref="ObjectInstance.Get(JsValue, JsValue)"/> and
/// <see cref="ObjectInstance.GetOwnProperty"/> only. It says nothing about writes, deletes or enumeration,
/// which keep their ordinary virtual dispatch either way.
/// </para>
/// </summary>
public enum PropertyAccessSemantics
{
    /// <summary>
    /// No declaration. The engine assumes nothing and resolves every read through the full
    /// <see cref="ObjectInstance.Get(JsValue, JsValue)"/> pipeline. This is the default and preserves the
    /// behaviour of releases that predate this API.
    /// </summary>
    Unspecified = 0,

    /// <summary>
    /// <see cref="ObjectInstance.Get(JsValue, JsValue)"/> is exactly the ordinary one: an own property is
    /// returned as <c>UnwrapJsValue(GetOwnProperty(property), receiver)</c>, and otherwise the read continues
    /// up the prototype chain. Declaring this lets the interpreter resolve a read from a
    /// <em>single</em> <see cref="ObjectInstance.GetOwnProperty"/> probe instead of probing once to prove the
    /// own property exists and a second time inside <c>Get</c> to fetch it.
    /// <para>
    /// The invariant the host must honour: whenever <c>GetOwnProperty(property)</c> returns a descriptor other
    /// than <see cref="Runtime.Descriptors.PropertyDescriptor.Undefined"/>,
    /// <c>Get(property, receiver)</c> must return the value that descriptor unwraps to for that receiver.
    /// Overriding <c>GetOwnProperty</c> alone (leaving <c>Get</c> inherited) satisfies it automatically.
    /// Debug builds of Jint verify the invariant on every read, so running an integration suite against a
    /// Debug build acts as a checker.
    /// </para>
    /// </summary>
    Ordinary = 1,

    /// <summary>
    /// <see cref="ObjectInstance.Get(JsValue, JsValue)"/> and/or <see cref="ObjectInstance.GetOwnProperty"/>
    /// deviate from ordinary semantics — for example <c>Get</c> synthesises a value for a name that owns no
    /// descriptor. The interpreter then never resolves a read against this object from a cached or probed
    /// descriptor and always calls <c>Get</c>, which is required for such an object to observe every read.
    /// </summary>
    Exotic = 2,
}
