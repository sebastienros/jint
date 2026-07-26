namespace Jint.Native.Object;

/// <summary>
/// How an <see cref="ObjectInstance"/> subclass resolves property reads.
/// <para>
/// The engine <em>derives</em> this per type and never asks the host to declare it: a subclass that overrides
/// <see cref="ObjectInstance.Get(JsValue, JsValue)"/> is <see cref="Exotic"/>, one that does not is
/// <see cref="Ordinary"/>. <see cref="ObjectInstance.SetPropertyAccessSemantics"/> exists only to override that
/// answer for the two shapes the rule cannot see.
/// </para>
/// <para>
/// The value is a statement about <see cref="ObjectInstance.Get(JsValue, JsValue)"/> and
/// <see cref="ObjectInstance.GetOwnProperty"/> only. It says nothing about writes, deletes or enumeration,
/// which keep their ordinary virtual dispatch either way.
/// </para>
/// </summary>
public enum PropertyAccessSemantics
{
    /// <summary>
    /// <see cref="ObjectInstance.Get(JsValue, JsValue)"/> is exactly the ordinary one: an own property is
    /// returned as <c>UnwrapJsValue(GetOwnProperty(property), receiver)</c>, and otherwise the read continues
    /// up the prototype chain. The interpreter may then resolve a read from a <em>single</em>
    /// <see cref="ObjectInstance.GetOwnProperty"/> probe instead of probing once to prove the own property
    /// exists and a second time inside <c>Get</c> to fetch it.
    /// <para>
    /// This is what a subclass that does not override <c>Get</c> derives, and it is correct for such a type by
    /// definition — its <c>Get</c> <em>is</em> the ordinary implementation, so the single probe the interpreter
    /// does is precisely the one <c>base.Get</c> would have done. Overriding <c>GetOwnProperty</c> alone does
    /// not affect that.
    /// </para>
    /// <para>
    /// Declaring it explicitly is only needed for a type that overrides <c>Get</c> and is nevertheless ordinary
    /// — an override that adds tracing, or one that special-cases a name and delegates everything else to
    /// <c>base.Get</c> while still agreeing with its own <c>GetOwnProperty</c>. Debug builds of Jint verify the
    /// claim on every read, so running an integration suite against a Debug build acts as a checker.
    /// </para>
    /// </summary>
    Ordinary = 0,

    /// <summary>
    /// <see cref="ObjectInstance.Get(JsValue, JsValue)"/> and/or <see cref="ObjectInstance.GetOwnProperty"/>
    /// deviate from ordinary semantics — for example <c>Get</c> synthesises or lazily materializes a value for
    /// a name that owns no descriptor yet. The interpreter then never resolves a read against this object from
    /// a cached or probed descriptor and always calls <c>Get</c>, which is what such an object needs in order
    /// to observe every read.
    /// <para>
    /// This is what a subclass that overrides <c>Get</c> derives. Declaring it explicitly is only needed for a
    /// type that does <em>not</em> override <c>Get</c> and is still not ordinary — for instance one whose
    /// <c>GetOwnProperty</c> is not stable across calls for the same name.
    /// </para>
    /// </summary>
    Exotic = 1,
}
