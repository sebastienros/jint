using Jint.HtmlParser;
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
/// Every body is a static lambda over process-shared state — an interned name — so the shape stays valid for
/// every engine that instantiates it, which is what <c>JsObjectShape.Builder</c> requires.
/// </para>
/// </remarks>
internal static class DomShapeAdditions
{
    /// <summary><c>HTMLElement</c>: <c>GlobalEventHandlers</c> and <c>DocumentAndElementEventHandlers</c>.</summary>
    internal static void HtmlElementHandlers(JsObjectShape.Builder builder)
        => AddHandlers<Element>(builder, "HTMLElement", EventHandlerContentAttributes.ElementHandlers);

    /// <summary><c>Document</c>: the same two mixins, plus the two handlers only a document carries.</summary>
    internal static void DocumentHandlers(JsObjectShape.Builder builder)
    {
        AddHandlers<Document>(builder, "Document", EventHandlerContentAttributes.ElementHandlers);
        AddHandlers<Document>(builder, "Document", ["readystatechange", "visibilitychange"]);
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
