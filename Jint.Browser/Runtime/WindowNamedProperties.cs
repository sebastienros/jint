using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using Jint.Native;
using Jint.Native.Object;

namespace Jint.Browser.Runtime;

/// <summary>
/// HTML's <b>named access on the Window object</b>: the object that makes <c>&lt;div id=x&gt;</c> and
/// <c>&lt;iframe name=x&gt;</c> reach script as <c>x</c>.
/// <para>
/// https://html.spec.whatwg.org/multipage/nav-history-apis.html#named-access-on-the-window-object
/// </para>
/// </summary>
/// <remarks>
/// <para>
/// <b>It is a link in the prototype chain, not a property of the global object</b> — which is both what WebIDL
/// says (the <i>named properties object</i> is the interface prototype object's <c>[[Prototype]]</c>) and the
/// only place it can go without costing anything. The chain here is
/// <c>window → Window.prototype → WindowProperties → EventTarget.prototype → Object.prototype</c>, so a name
/// the global object owns, and a member <c>Window.prototype</c> declares, are both found before this object is
/// asked at all. What reaches it is a <b>miss</b>: an identifier that resolved nowhere else, which is exactly
/// where HTML puts the lookup.
/// </para>
/// <para>
/// <b>Nothing about an ordinary global read changes, and that is checked rather than assumed.</b> The
/// global-identifier inline cache admits only a descriptor the global object owns —
/// <c>JintIdentifierExpression.TryRememberGlobalBinding</c> re-probes the global's own storage and declines a
/// name resolved from its prototype chain, because a prototype mutation bumps no global version — so a name
/// answered here is never cached and never displaces one that is.
/// </para>
/// <para>
/// <b>The indexed properties are answered here too</b>, which is the one place this object is not WebIDL's:
/// HTML puts <c>window[0]</c> on the <c>WindowProxy</c>, above the named properties, and there is no
/// <c>WindowProxy</c> here to put it on. The observable difference is small and stated rather than hidden —
/// a key that is an array index is answered before any element is looked at, so an element with
/// <c>id="0"</c> is shadowed by the first frame where HTML shadows it too. It is computed per access rather
/// than installed, so a frame the parse has not reached yet appears the moment it does.
/// </para>
/// <para>
/// <b>One divergence, about a name several elements answer to.</b> HTML collects every matching element in
/// tree order and answers with an <c>HTMLCollection</c> when there is more than one; this answers the first
/// it finds, preferring an <c>id</c> to a <c>name</c>. An <c>&lt;iframe name=x&gt;</c> used to be a second:
/// it answered the frame element where HTML answers the frame's <c>WindowProxy</c>, because a frame had no
/// window. It has one now (<c>Runtime/FrameWindows</c>), so the name answers it.
/// </para>
/// </remarks>
internal sealed class WindowNamedProperties : NamedPropertyObject
{
    private readonly PageRuntime _runtime;

    internal WindowNamedProperties(PageRuntime runtime) : base(runtime.Engine)
    {
        _runtime = runtime;
    }

    /// <inheritdoc />
    /// <remarks>
    /// Read only by an enumeration — <c>for..in</c> over the window, <c>Object.getOwnPropertyNames</c> of this
    /// object — so it walks the document there and nowhere else. Every read goes through
    /// <see cref="TryGetNamedValue"/>, which asks the document two direct questions instead.
    /// </remarks>
    public override int NameCount => Names().Count;

    /// <inheritdoc />
    public override string NameAt(int index) => Names()[index];

    /// <inheritdoc />
    public override bool TryGetNamedValue(string name, out JsValue value)
    {
        // The indexed properties first, because `frames[0]` is a lookup by the key "0" and a document may
        // also hold an element whose id is "0". HTML puts the indexed properties on the WindowProxy, above
        // the named ones, so index wins — and this is the only place a miss can be answered from here.
        if (Index(name) is { } index)
        {
            value = FrameWindows.At(_runtime, index);
            return !value.IsUndefined();
        }

        if (name.Length != 0 && _runtime.Document is { } document)
        {
            if (document.GetElementById(name) is { } byId)
            {
                value = _runtime.Dom.WrapNode(byId);
                return true;
            }

            foreach (var element in document.GetElementsByName(name))
            {
                if (!IsNamedAccessKind(element))
                {
                    continue;
                }

                // HTML answers a frame's WindowProxy for `<iframe name=x>` and the element for everything
                // else; a frame with no document has no window, and then the element is all there is.
                if (element is IHtmlInlineFrameElement frame && FrameWindows.For(_runtime, frame) is { } window && !window.IsNull())
                {
                    value = window;
                    return true;
                }

                value = _runtime.Dom.WrapNode(element);
                return true;
            }
        }

        value = JsValue.Undefined;
        return false;
    }

    /// <inheritdoc />
    /// <remarks>
    /// <b>Writable, and the write is still refused</b> — which is WebIDL's named properties object exactly.
    /// Its <c>[[GetOwnProperty]]</c> reports <c>writable: true</c> and its <c>[[DefineOwnProperty]]</c>
    /// returns false, so a direct <c>Reflect.set</c> on it does nothing; what the flag is for is the write a
    /// page really makes. <c>window.x = 1</c> walks the prototype chain, finds a writable data property here
    /// and therefore creates an <b>own</b> property of the window that shadows it — which is what every page
    /// that assigns to a global named after one of its own elements depends on.
    /// </remarks>
    protected override bool IsNameWritable(string name) => true;

    /// <inheritdoc />
    /// <remarks>The other half of the paragraph above: there is nowhere to write, and nothing may be.</remarks>
    protected override bool TrySetNamedValue(string name, JsValue value) => false;

    /// <summary>
    /// The array index a property key is, or <see langword="null"/> when it is not one.
    /// </summary>
    /// <remarks>
    /// An array index in WebIDL's sense: the canonical decimal form of a number, so <c>"0"</c> is one and
    /// <c>"00"</c>, <c>"-1"</c>, <c>"1.0"</c> and <c>" 1"</c> are names. Getting that wrong the other way
    /// would make an element with <c>id="01"</c> unreachable.
    /// </remarks>
    private static int? Index(string name)
    {
        if (name.Length == 0 || name.Length > 9)
        {
            return null;
        }

        if (name.Length > 1 && name[0] == '0')
        {
            return null;
        }

        var index = 0;

        foreach (var c in name)
        {
            if (!char.IsAsciiDigit(c))
            {
                return null;
            }

            index = (index * 10) + (c - '0');
        }

        return index;
    }

    /// <summary>
    /// The element kinds whose <c>name</c> content attribute is a supported property name. An <c>id</c> is one
    /// for <b>every</b> element; a <c>name</c> is one only for these.
    /// </summary>
    private static bool IsNamedAccessKind(IElement element) => element is IHtmlAnchorElement
        or IHtmlAreaElement
        or IHtmlEmbedElement
        or IHtmlFormElement
        or IHtmlInlineFrameElement
        or IHtmlImageElement
        or IHtmlObjectElement
        || element is IHtmlElement { LocalName: "frame" or "frameset" };

    /// <summary>
    /// Every supported property name, in tree order and without repeats — the two obligations
    /// <see cref="NamedPropertyObject.NameAt"/> carries, and what host-contract verification checks.
    /// </summary>
    private List<string> Names()
    {
        var names = new List<string>();

        if (_runtime.Document is not { } document)
        {
            return names;
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var element in document.All)
        {
            if (element.Id is { Length: > 0 } id && seen.Add(id))
            {
                names.Add(id);
            }

            if (IsNamedAccessKind(element)
                && element.GetAttribute("name") is { Length: > 0 } name
                && seen.Add(name))
            {
                names.Add(name);
            }
        }

        return names;
    }
}
