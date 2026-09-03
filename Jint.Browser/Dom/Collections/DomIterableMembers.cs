using Jint.Native;
using Jint.Native.Object;

namespace Jint.Browser.Dom.Collections;

/// <summary>
/// WebIDL's <a href="https://webidl.spec.whatwg.org/#es-iterable">value iterator</a>: the four members an
/// <c>iterable&lt;T&gt;</c> declaration puts on an interface prototype object.
/// </summary>
/// <remarks>
/// <para>
/// <b>Their values are <c>%Array.prototype%</c>'s own functions, not forwarders</b> — WebIDL says the
/// <c>entries</c>, <c>keys</c>, <c>values</c> and <c>forEach</c> properties of a value-iterator prototype are
/// the corresponding <c>%Array.prototype%</c> properties, and
/// <c>dom/lists/DOMTokenList-iteration.html</c> asserts exactly that with <c>assert_equals</c>. So this is a
/// per-realm data slot rather than a member with a body, which is why it is the <c>extend</c> form of an
/// addition rather than four member entries: the member form goes through the model as an operation and an
/// operation has a body.
/// </para>
/// <para>
/// The slot's factory is lazy and engine-independent — it reads the intrinsic off the object it is handed —
/// so the shape stays process-shared and every engine gets its own realm's function. It pairs with
/// <see cref="DomIterator"/>, which puts the same realm's <c>Array.prototype[Symbol.iterator]</c> on the
/// instance; the two together are the whole of what <c>iterable&lt;T&gt;</c> declares.
/// </para>
/// <para>
/// This is only correct for a collection whose indexed getter really answers its <i>i</i>th value, which is
/// what <c>ArrayLikeObject</c>'s <c>TryGetIndex</c> is: <c>Array.prototype.keys</c> and its siblings read
/// <c>length</c> and index the receiver, and a <c>DomCollectionObject</c> answers both.
/// </para>
/// </remarks>
internal static class DomIterableMembers
{
    /// <summary>Declares the four members on <paramref name="builder"/>.</summary>
    internal static void ValueIterator(JsObjectShape.Builder builder)
    {
        Add(builder, "entries");
        Add(builder, "keys");
        Add(builder, "values");
        Add(builder, "forEach");
    }

    // Enumerable, because WebIDL puts these on the prototype as ordinary operations and an operation is
    // enumerable — WebIdlPropertyAttributeTests holds this whole surface to that rule.
    private static void Add(JsObjectShape.Builder builder, string name)
        => builder.PerRealmSlot(name, o => ArrayPrototypeMember(o, name), enumerable: true);

    /// <remarks>
    /// The <b>principal</b> realm's <c>Array.prototype</c>, for the reason <c>DomRealm</c> gives: a prototype
    /// belongs to the engine, not to whichever realm happened to be executing when a member was first read.
    /// </remarks>
    private static JsValue ArrayPrototypeMember(ObjectInstance instance, string name)
        => instance.Engine._mainRealm.Intrinsics.Array.PrototypeObject.Get(name);
}
