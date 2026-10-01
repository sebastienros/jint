using System.Runtime.CompilerServices;
using Jint.Browser.Dom;
using Jint.HtmlParser;
using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.Browser.Fonts;

/// <summary>
/// One realm's CSS Font Loading interfaces — <c>FontFace</c> and <c>FontFaceSet</c> — built the first time a
/// page reaches either, and the <c>FontFaceSet</c> each document's <c>fonts</c> answers.
/// </summary>
/// <remarks>
/// <para>
/// https://drafts.csswg.org/css-font-loading/. One per <see cref="DomRealm"/>, as
/// <see cref="Geometry.GeometryRealm"/> is, so a frame's <c>FontFace</c> is its own.
/// </para>
/// <para>
/// Nothing here renders text, so a font is never decoded: a face "loads" when its bytes arrive and carry a
/// font file's signature, which is what lets <c>ready</c>, <c>check()</c> and the load events a page waits on
/// settle as a browser's do. <c>@font-face</c> rules are not reflected into <c>document.fonts</c> — no face
/// is CSS-connected — which <c>docs/packages/jint-browser/limitations.md</c> records.
/// </para>
/// </remarks>
internal sealed class FontRealm
{
    /// <summary>The globals <see cref="DomBindings.InstallOn"/> installs, lazily.</summary>
    internal static readonly string[] InterfaceNames = ["FontFace", "FontFaceSet"];

    private readonly ConditionalWeakTable<Document, JsFontFaceSet> _documentFonts = new();
    private (ObjectInstance Prototype, HostInterfaceObject Interface) _face;
    private (ObjectInstance Prototype, HostInterfaceObject Interface) _set;

    internal FontRealm(DomRealm dom)
    {
        Dom = dom;
    }

    internal DomRealm Dom { get; }

    internal Engine Engine => Dom.Engine;

    internal Realm Realm => Dom.OwningRealm;

    internal ObjectInstance FacePrototype => Faces().Prototype;

    internal ObjectInstance SetPrototype => Sets().Prototype;

    /// <summary>The interface object a global named <paramref name="name"/> answers.</summary>
    internal JsValue InterfaceObject(string name) => name switch
    {
        "FontFace" => Faces().Interface,
        "FontFaceSet" => Sets().Interface,
        _ => JsValue.Undefined,
    };

    /// <summary>
    /// https://drafts.csswg.org/css-font-loading/#dom-fontfacesource-fonts — the document's font source, the
    /// same object on every read.
    /// </summary>
    internal static JsValue DocumentFonts(DomRealm realm, Document document)
    {
        var fonts = realm.Fonts;
        return fonts._documentFonts.GetValue(document, _ => new JsFontFaceSet(fonts));
    }

    private (ObjectInstance Prototype, HostInterfaceObject Interface) Faces()
    {
        if (_face.Prototype is null)
        {
            _face = Build(FontShapes.FontFace, "FontFace", 2, args => JsFontFace.Construct(this, args), default);
        }

        return _face;
    }

    private (ObjectInstance Prototype, HostInterfaceObject Interface) Sets()
    {
        if (_set.Prototype is null)
        {
            // https://drafts.csswg.org/css-font-loading/#fontfaceset — an EventTarget with no constructor.
            _set = Build(FontShapes.FontFaceSet, "FontFaceSet", 0, construct: null,
                (Realm.Intrinsics.EventTarget.PrototypeObject, Realm.Intrinsics.EventTarget));

            // https://webidl.spec.whatwg.org/#es-setlike — keys and @@iterator are the very function object
            // values is. Settled now, so a page that later replaces values cannot change what they are.
            _ = _set.Prototype.Get("keys");
            _ = _set.Prototype.Get(Native.Symbol.GlobalSymbolRegistry.Iterator);
        }

        return _set;
    }

    private (ObjectInstance Prototype, HostInterfaceObject Interface) Build(
        JsObjectShape shape,
        string name,
        int length,
        Func<JsValue[], ObjectInstance>? construct,
        (ObjectInstance? Prototype, ObjectInstance? Interface) parent)
    {
        using var scope = new RealmScope(Engine, Realm);
        var prototype = shape.Instantiate(Engine, parent.Prototype ?? Realm.Intrinsics.Object.PrototypeObject);
        JsObjectShape.SetHostState(prototype, Dom);
        var iface = new HostInterfaceObject(Engine, Realm, name, prototype, length, construct, parent.Interface);
        prototype.DefineOwnPropertyUnchecked("constructor", new PropertyDescriptor(iface, PropertyFlag.NonEnumerable));
        return (prototype, iface);
    }
}
