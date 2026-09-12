using Jint.Native;
using Jint.Native.Object;

namespace Jint.Browser.Dom;

/// <summary>
/// The <c>@@unscopables</c> object of one interface prototype.
/// <para>
/// https://webidl.spec.whatwg.org/#es-unscopables
/// </para>
/// </summary>
/// <remarks>
/// <para>
/// <b>What it is for is one statement, and that statement is what a page's inline event handlers run inside.</b>
/// HTML compiles <c>&lt;div onclick="remove()"&gt;</c> with the element, its form owner and the document on the
/// scope chain, which is <c>with</c> semantics — so without this object every <c>ChildNode</c> and
/// <c>ParentNode</c> member would shadow a global of the same name, and <c>remove</c>, <c>append</c> and
/// <c>before</c> are names pages really do use. DOM §4.2.8 and §4.2.9 mark every member of both mixins
/// <c>[Unscopable]</c> for exactly that reason, and ECMAScript's
/// <see href="https://tc39.es/ecma262/#sec-object-environment-records-hasbinding-n">object environment
/// record</see> is what consults it.
/// </para>
/// <para>
/// <b>Per engine, and deliberately mutable.</b> The object belongs to one realm because it is an object, and
/// nothing freezes it: a page may add its own names, which is how it opts one of its own expandos out of the
/// same shadowing — <c>html/webappapis/scripting/events/compile-event-handler-symbol-unscopables.html</c> is
/// that written down. The property holding it is <c>{ writable: false, enumerable: false, configurable: true }</c>,
/// which is the shape declaration's business rather than this method's.
/// </para>
/// </remarks>
internal static class DomUnscopables
{
    /// <summary>
    /// Builds one interface prototype's <c>@@unscopables</c> object: an ordinary object with a <b>null</b>
    /// <c>[[Prototype]]</c> carrying one <see langword="true"/> per name, in the order the generator emitted
    /// them.
    /// </summary>
    /// <param name="prototype">The interface prototype object the slot belongs to; only its engine is read.</param>
    /// <param name="names">The interface's <c>[Unscopable]</c> member names — a process-shared array.</param>
    internal static JsValue Create(ObjectInstance prototype, string[] names)
    {
        // OrdinaryObjectCreate(null), which is step 1 of the algorithm: a null prototype is what keeps a name
        // on Object.prototype from accidentally reading as unscopable.
        var unscopables = ObjectInstance.OrdinaryObjectCreate(prototype.Engine, null);

        foreach (var name in names)
        {
            unscopables.CreateDataProperty(name, JsBoolean.True);
        }

        return unscopables;
    }
}
