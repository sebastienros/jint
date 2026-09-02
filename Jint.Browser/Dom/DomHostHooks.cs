using AngleSharp.Dom;
using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;

namespace Jint.Browser.Dom;

/// <summary>
/// The seam for the DOM members whose real behaviour belongs to a browser runtime that does not exist yet:
/// the ones that parse markup into the tree, and so have to reach the script scheduler.
/// </summary>
/// <remarks>
/// <para>
/// The generated members call these instead of AngleSharp directly, and the default implementations below
/// <em>are</em> the AngleSharp call — so today the binding behaves exactly as it would without the seam, and
/// the parser driver (campaign item R3) replaces the bodies by handing the realm a subclass without a line of
/// generated code changing. That is the whole point of routing them through here now rather than later: the
/// override table names them once, and the day the driver arrives it is one assignment.
/// </para>
/// <para>
/// What the driver has to do that this cannot: a <c>&lt;script&gt;</c> inserted through <c>innerHTML</c> must
/// be prepared and executed, and <c>document.write</c> during a parse writes into the live text source while
/// the parser is parked. Both need the baton the driver owns.
/// </para>
/// </remarks>
internal class DomHostHooks
{
    /// <summary>The behaviour a binding with no runtime behind it has: AngleSharp, called directly.</summary>
    internal static readonly DomHostHooks Default = new();

    /// <summary>
    /// A wrapper has just been created and is the one this engine will keep for its object; the runtime may
    /// add members the generator could not.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Called once per object per engine, from the wrapper cache, and never again — so a member added here is
    /// an own property of a wrapper rather than of a prototype, which is what keeps every generated prototype
    /// shaped. It is the seam for a member whose <i>whole</i> body belongs to the runtime rather than one
    /// whose body is replaced: <c>form.submit()</c> is a navigation and <c>form.requestSubmit()</c> does not
    /// exist in AngleSharp at all, so neither is generated and neither can be.
    /// </para>
    /// <para>
    /// The cost is one virtual call per wrapper creation, not per member access.
    /// </para>
    /// </remarks>
    /// <param name="realm">The DOM state of the engine the wrapper belongs to.</param>
    /// <param name="target">The AngleSharp object being wrapped.</param>
    /// <param name="wrapper">The wrapper, before anything has read a property off it.</param>
    internal virtual void WrapperCreated(DomRealm realm, object target, ObjectInstance wrapper)
    {
        // The handler content attributes an element's markup declares, registered the moment its wrapper wins
        // the cache — which is before anything can dispatch through it or add a listener of its own, and is
        // what puts a markup handler ahead of a script's in the listener list. It is here rather than in
        // DomNodeObject's constructor because this hook fires exactly once for the wrapper that won, and a
        // re-entrant member that built a second one would otherwise have scanned the loser too.
        if (wrapper is DomNodeObject node)
        {
            Events.EventHandlerContentAttributes.InstallFromMarkup(node);
        }
    }

    /// <summary>https://html.spec.whatwg.org/multipage/dynamic-markup-insertion.html#dom-innerhtml</summary>
    internal virtual void SetInnerHtml(DomRealm realm, IElement element, string markup) => element.InnerHtml = markup;

    /// <summary>https://html.spec.whatwg.org/multipage/dynamic-markup-insertion.html#dom-outerhtml</summary>
    internal virtual void SetOuterHtml(DomRealm realm, IElement element, string markup) => element.OuterHtml = markup;

    /// <summary>https://html.spec.whatwg.org/multipage/dynamic-markup-insertion.html#dom-insertadjacenthtml</summary>
    internal virtual void InsertAdjacentHtml(DomRealm realm, IElement element, JsValue[] arguments)
    {
        var position = DomEnums.ToAdjacentPosition(DomConvert.At(arguments, 0), "Element.insertAdjacentHTML");
        element.Insert(position, DomConvert.RequiredText(arguments, 1, "Element.insertAdjacentHTML"));
    }

    /// <summary>https://html.spec.whatwg.org/multipage/dynamic-markup-insertion.html#dom-document-write</summary>
    internal virtual void Write(DomRealm realm, IDocument document, JsValue[] arguments)
        => document.Write(Join(arguments));

    /// <summary>https://html.spec.whatwg.org/multipage/dynamic-markup-insertion.html#dom-document-writeln</summary>
    internal virtual void WriteLine(DomRealm realm, IDocument document, JsValue[] arguments)
        => document.WriteLine(Join(arguments));

    /// <summary>
    /// <c>document.write</c> takes a variadic <c>DOMString...</c> and concatenates it; AngleSharp's signature
    /// takes one string, so the concatenation happens here.
    /// </summary>
    private static string Join(JsValue[] arguments)
    {
        if (arguments.Length == 0)
        {
            return "";
        }

        if (arguments.Length == 1)
        {
            return TypeConverter.ToString(arguments[0]);
        }

        var builder = new System.Text.StringBuilder();
        foreach (var argument in arguments)
        {
            builder.Append(TypeConverter.ToString(argument));
        }

        return builder.ToString();
    }
}
