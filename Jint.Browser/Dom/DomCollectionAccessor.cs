using Jint.Native;

namespace Jint.Browser.Dom;

/// <summary>
/// How one collection interface answers the indexed and named questions WebIDL's
/// <a href="https://webidl.spec.whatwg.org/#idl-indexed-properties">indexed</a> and
/// <a href="https://webidl.spec.whatwg.org/#idl-named-properties">named</a> property getters ask. One
/// process-shared singleton per interface, written by the generator from AngleSharp's
/// <c>[DomAccessor]</c> metadata.
/// </summary>
/// <remarks>
/// <para>
/// It exists so that there is exactly one collection wrapper class rather than twenty near-identical ones:
/// <see cref="Collections.DomCollectionObject"/> derives the whole property model from
/// <see cref="Length"/> and <see cref="TryGetIndex"/> through <c>ArrayLikeObject</c>, and reaches the named
/// half only for an interface whose accessor says it has one.
/// </para>
/// <para>
/// Every method takes the target and the realm rather than closing over them, because the accessor is shared
/// by every engine in the process for the same reason a <c>JsObjectShape</c> is.
/// </para>
/// </remarks>
internal abstract class DomCollectionAccessor
{
    /// <summary>
    /// The collection's current element count. Re-read on every operation, so a live view is live. The
    /// default is for an interface with a named getter and nothing indexed — <c>DOMStringMap</c>.
    /// </summary>
    internal virtual uint Length(object target) => 0;

    /// <summary>
    /// Reads element <paramref name="index"/>. <see langword="false"/> means the collection has no own
    /// element there — the authoritative miss <c>ArrayLikeObject</c> requires.
    /// </summary>
    internal virtual bool TryGetIndex(DomRealm realm, object target, uint index, out JsValue value)
    {
        value = JsValue.Undefined;
        return false;
    }

    /// <summary>Whether this interface has a named property getter at all.</summary>
    internal virtual bool HasNamedGetter => false;

    /// <summary>
    /// The names the named getter currently supports, in order. WebIDL calls these the
    /// <a href="https://webidl.spec.whatwg.org/#dfn-supported-property-names">supported property names</a>,
    /// and they are what <c>Object.keys</c> and <c>for..in</c> see beside the indices.
    /// </summary>
    internal virtual IReadOnlyList<string> SupportedNames(object target) => [];

    /// <summary>
    /// Whether the supported property names enumerate. WebIDL's
    /// <a href="https://webidl.spec.whatwg.org/#LegacyUnenumerableNamedProperties">[LegacyUnenumerableNamedProperties]</a>
    /// answers <see langword="false"/>, and every named getter in the generated surface but
    /// <c>DOMStringMap</c>'s carries it — <c>NamedNodeMap</c> and <c>HTMLCollection</c> both do in their own
    /// specifications. The names still exist for <c>in</c> and <c>hasOwnProperty</c>; they are simply not
    /// listed by <c>Object.keys</c> or <c>for..in</c>.
    /// </summary>
    internal virtual bool AreNamesEnumerable => true;

    /// <summary>Reads the named element. <see langword="false"/> is an authoritative miss.</summary>
    internal virtual bool TryGetNamed(DomRealm realm, object target, string name, out JsValue value)
    {
        value = JsValue.Undefined;
        return false;
    }

    /// <summary>Whether an assignment to a supported name is routed to <see cref="TrySetNamed"/>.</summary>
    internal virtual bool IsNameWritable => false;

    /// <summary>One <c>[[Set]]</c> of a named element. <see langword="false"/> refuses it.</summary>
    internal virtual bool TrySetNamed(DomRealm realm, object target, string name, JsValue value) => false;

    /// <summary>One <c>[[Delete]]</c> of a named element. <see langword="false"/> refuses it.</summary>
    internal virtual bool TryDeleteNamed(object target, string name) => false;
}
