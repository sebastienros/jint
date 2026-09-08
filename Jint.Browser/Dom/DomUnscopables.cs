using Jint.Native;
using Jint.Native.Object;

namespace Jint.Browser.Dom;

/// <summary>
/// WebIDL's <a href="https://webidl.spec.whatwg.org/#es-unscopable">unscopable object</a>: the
/// <c>@@unscopables</c> an interface prototype object carries when the interface has <c>[Unscopable]</c>
/// members.
/// </summary>
/// <remarks>
/// <para>
/// <b>It exists for one thing a page does and one thing HTML does.</b> A <c>with</c> statement and, more to
/// the point, an
/// <a href="https://html.spec.whatwg.org/multipage/webappapis.html#getting-the-current-value-of-the-event-handler">event
/// handler content attribute</a> — whose scope chain is object environments over the document, the form owner
/// and the element, each with the <i>withEnvironment</i> flag — consult this object before letting a name
/// resolve to a member. Without it <c>&lt;div onclick="remove()"&gt;</c> calls the element's <c>remove</c>
/// where the standard resolves the global's, which is exactly what
/// <c>dom/nodes/remove-unscopable.html</c> measures.
/// </para>
/// <para>
/// <b>The object is per interface prototype object and mutable</b>, which is why the shape declares it as a
/// per-realm slot rather than a constant: WebIDL creates one when the prototype is created, and a page may
/// write to it — <c>compile-event-handler-symbol-unscopables.html</c> does exactly that. Its
/// <c>[[Prototype]]</c> is null, so a name reaches it only when the interface really declares that member.
/// </para>
/// <para>
/// The property's attributes are WebIDL's: not writable, not enumerable, configurable — the opposite of the
/// <c>enumerable: true</c> every ordinary WebIDL member on a prototype carries, because this one is not a
/// member.
/// </para>
/// </remarks>
internal static class DomUnscopables
{
    /// <summary>Builds the unscopable object for one interface prototype object.</summary>
    /// <param name="prototype">The interface prototype object the slot is being filled on.</param>
    /// <param name="members">The interface's <c>[Unscopable]</c> member names, already sorted.</param>
    internal static JsValue Create(ObjectInstance prototype, string[] members)
    {
        var unscopables = new JsObject(prototype.Engine)
        {
            _prototype = null,
        };

        foreach (var member in members)
        {
            unscopables.DefineOwnDataPropertyUnchecked(member, JsBoolean.True);
        }

        return unscopables;
    }
}
