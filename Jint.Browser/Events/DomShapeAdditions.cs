using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using Jint.Browser.Dom;
using Jint.Native;

namespace Jint.Browser.Events;

/// <summary>
/// The event handler IDL attributes, spliced into a generated shape by the binding generator's
/// <c>additions</c> extend form.
/// <para>
/// https://html.spec.whatwg.org/multipage/webappapis.html#event-handler-idl-attributes
/// </para>
/// </summary>
/// <remarks>
/// <para>
/// <b>Why these are the extend form and everything else is not.</b> An <c>additions</c> entry normally names
/// one member and carries its body, which is better in every way that matters — the generated file names the
/// member, it sorts with the rest, and a member AngleSharp later grows under the same name is reported as a
/// collision. That form cannot express a <em>family</em>: <c>GlobalEventHandlers</c> and
/// <c>DocumentAndElementEventHandlers</c> are eighty-odd names on <c>HTMLElement</c> and as many again on
/// <c>Document</c>, each an identically shaped accessor pair over an interned type name, and as member entries
/// they would be a hundred and seventy near-identical rows of a table whose purpose is to hold decisions.
/// So the list is computed here, in one loop, and the generator hands this method the builder.
/// </para>
/// <para>
/// The price is that the generator cannot see the names, so it cannot report a collision with a member
/// AngleSharp grew; what catches that instead is <c>JsObjectShape.Builder</c> refusing a duplicate name, which
/// <c>DomPrototypeTests</c> reaches for every interface.
/// </para>
/// <para>
/// Every body is a static lambda over process-shared state — an interned name — so the shape stays valid for
/// every engine that instantiates it, which is what <c>JsObjectShape.Builder</c> requires.
/// </para>
/// </remarks>
internal static class DomShapeAdditions
{
    /// <summary><c>HTMLElement</c>: <c>GlobalEventHandlers</c> and <c>DocumentAndElementEventHandlers</c>.</summary>
    internal static void HtmlElementHandlers(JsObjectShape.Builder builder)
        => AddHandlers<IHtmlElement>(builder, "HTMLElement", EventHandlerContentAttributes.ElementHandlers);

    /// <summary><c>Document</c>: the same two mixins, plus the two handlers only a document carries.</summary>
    internal static void DocumentHandlers(JsObjectShape.Builder builder)
    {
        AddHandlers<IDocument>(builder, "Document", EventHandlerContentAttributes.ElementHandlers);
        AddHandlers<IDocument>(builder, "Document", ["readystatechange", "visibilitychange"]);
    }

    /// <summary>
    /// Declares one accessor per handler name. The pair's whole definition is "get and set the entry of the
    /// event handler map"; the content attribute's half is <see cref="EventHandlerContentAttributes"/>.
    /// </summary>
    private static void AddHandlers<T>(JsObjectShape.Builder builder, string owner, string[] types) where T : class
    {
        foreach (var type in types)
        {
            var accessor = new HandlerAccessor<T>(owner, type);
            builder.Accessor("on" + type, accessor.Get, accessor.Set);
        }
    }

    /// <summary>
    /// One handler IDL attribute's get/set pair. It captures the interned type and the interface name and
    /// nothing else, so it is engine-independent — the requirement a shape member body carries.
    /// </summary>
    private sealed class HandlerAccessor<T> where T : class
    {
        private readonly string _member;
        private readonly string _type;

        internal HandlerAccessor(string owner, string type)
        {
            _member = owner + ".on" + type;
            _type = type;
        }

        internal JsValue Get(JsValue thisObject, JsValue[] arguments)
        {
            DomBindings.Bind<T>(thisObject, _member);
            return EventHandlerContentAttributes.Get((DomNodeObject) thisObject, _type);
        }

        internal JsValue Set(JsValue thisObject, JsValue[] arguments)
        {
            DomBindings.Bind<T>(thisObject, _member);
            return EventHandlerContentAttributes.Set(
                (DomNodeObject) thisObject,
                _type,
                arguments.Length > 0 ? arguments[0] : JsValue.Undefined);
        }
    }
}
