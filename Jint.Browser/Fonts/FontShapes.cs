using Jint.Browser.SystemState;
using Jint.HtmlParser.Css.Values;
using Jint.Native;
using Jint.Runtime;
using Jint.WebApi.Events;
using Jint.WebApi.Streams;

namespace Jint.Browser.Fonts;

/// <summary>The engine-independent prototype shapes of <c>FontFace</c> and <c>FontFaceSet</c>.</summary>
internal static class FontShapes
{
    internal static readonly JsObjectShape FontFace = BuildFontFace();
    internal static readonly JsObjectShape FontFaceSet = BuildFontFaceSet();

    private static JsFontFace Face(JsValue t, string member) => SystemBrand.Of<JsFontFace>(t, "FontFace", member);

    private static JsFontFaceSet Set(JsValue t, string member) => SystemBrand.Of<JsFontFaceSet>(t, "FontFaceSet", member);

    /// <summary>https://drafts.csswg.org/css-font-loading/#fontface-interface, in IDL order.</summary>
    private static JsObjectShape BuildFontFace() => new JsObjectShape.Builder()
        .PerRealmSlot("constructor")
        .ToStringTag("FontFace")
        .Accessor("family",
            static (t, _) => JsString.Create(Face(t, "family").Family),
            static (t, args) =>
            {
                var face = Face(t, "family");
                face.Family = TypeConverter.ToString(args.At(0));
                return JsValue.Undefined;
            })
        .Descriptor("style", CssFontFaceDescriptor.Style)
        .Descriptor("weight", CssFontFaceDescriptor.Weight)
        .Descriptor("width", CssFontFaceDescriptor.Stretch)
        .Descriptor("stretch", CssFontFaceDescriptor.Stretch)
        .Descriptor("unicodeRange", CssFontFaceDescriptor.UnicodeRange)
        .Descriptor("variant", CssFontFaceDescriptor.Variant)
        .Descriptor("featureSettings", CssFontFaceDescriptor.FeatureSettings)
        .Descriptor("variationSettings", CssFontFaceDescriptor.VariationSettings)
        .Descriptor("display", CssFontFaceDescriptor.Display)
        .Descriptor("ascentOverride", CssFontFaceDescriptor.AscentOverride)
        .Descriptor("descentOverride", CssFontFaceDescriptor.DescentOverride)
        .Descriptor("lineGapOverride", CssFontFaceDescriptor.LineGapOverride)
        .Descriptor("sizeAdjust", CssFontFaceDescriptor.SizeAdjust)
        .Accessor("status", static (t, _) => JsString.Create(Face(t, "status").Status))
        .PromiseMethod("load", static (t, _) => Face(t, "load").Load())
        .PromiseAccessor("loaded", static t => Face(t, "loaded").Loaded)
        .Build();

    /// <summary>https://drafts.csswg.org/css-font-loading/#fontfaceset, with WebIDL's setlike members.</summary>
    private static JsObjectShape BuildFontFaceSet() => new JsObjectShape.Builder()
        .PerRealmSlot("constructor")
        .ToStringTag("FontFaceSet")
        .Accessor("size", static (t, _) => JsNumber.Create(Set(t, "size").Size))
        .Method("entries", static (t, _) => Set(t, "entries").Entries())
        .PerRealmSlot("keys", static o => o.Get("values"), enumerable: true)
        .Method("values", static (t, _) => Set(t, "values").Values())
        .PerRealmSlot(Native.Symbol.GlobalSymbolRegistry.Iterator, static o => o.Get("values"))
        .Method("forEach", static (t, args) =>
        {
            var set = Set(t, "forEach");
            if (args.At(0) is not ICallable callback)
            {
                Throw.TypeError(set.Owner.Realm, "Failed to execute 'forEach' on 'FontFaceSet': The callback provided as parameter 1 is not a function.");
                return JsValue.Undefined;
            }

            set.ForEach(callback, args.At(1));
            return JsValue.Undefined;
        }, length: 1)
        .Method("has", static (t, args) =>
        {
            var set = Set(t, "has");
            return JsBoolean.Create(set.Has(FaceArgument(set, args, "has")));
        }, length: 1)
        .Method("add", static (t, args) =>
        {
            var set = Set(t, "add");
            return set.Add(FaceArgument(set, args, "add"));
        }, length: 1)
        .Method("delete", static (t, args) =>
        {
            var set = Set(t, "delete");
            return JsBoolean.Create(set.Delete(FaceArgument(set, args, "delete")));
        }, length: 1)
        .Method("clear", static (t, _) =>
        {
            Set(t, "clear").Clear();
            return JsValue.Undefined;
        })
        .Handler("onloading", "loading")
        .Handler("onloadingdone", "loadingdone")
        .Handler("onloadingerror", "loadingerror")
        .PromiseMethod("load", static (t, args) =>
        {
            var set = Set(t, "load");
            var font = RequiredString(set, args, "load");
            return set.Load(font, Text(args));
        }, length: 1)
        .Method("check", static (t, args) =>
        {
            var set = Set(t, "check");
            var font = RequiredString(set, args, "check");
            return JsBoolean.Create(set.Check(font, Text(args)));
        }, length: 1)
        .PromiseAccessor("ready", static t => Set(t, "ready").Ready)
        .Accessor("status", static (t, _) => JsString.Create(Set(t, "status").Status))
        .Build();

    private static JsObjectShape.Builder Descriptor(this JsObjectShape.Builder builder, string name, CssFontFaceDescriptor descriptor)
        => builder.Accessor(
            name,
            (t, _) => JsString.Create(Face(t, name).Descriptor(descriptor)),
            (t, args) =>
            {
                Face(t, name).SetDescriptor(descriptor, name, args.At(0));
                return JsValue.Undefined;
            });

    private static JsObjectShape.Builder Handler(this JsObjectShape.Builder builder, string name, string type)
        => builder.Accessor(
            name,
            (t, _) => EventHandlerAttributes.Get(Set(t, name), type),
            (t, args) => EventHandlerAttributes.Set(Set(t, name), type, args.At(0)));

    /// <summary>
    /// https://webidl.spec.whatwg.org/#es-operations — an operation returning a promise reports a failed
    /// receiver or argument conversion as a rejection, not a throw.
    /// </summary>
    private static JsObjectShape.Builder PromiseMethod(
        this JsObjectShape.Builder builder,
        string name,
        Func<JsValue, JsValue[], JsValue> implementation,
        int length = 0)
        => builder.Method(name, (thisObject, arguments) =>
        {
            try
            {
                return implementation(thisObject, arguments);
            }
            catch (JavaScriptException exception) when (thisObject is Native.Object.ObjectInstance receiver)
            {
                return StreamPromises.RejectedWith(receiver.Engine, receiver.Engine._mainRealm, exception.Error);
            }
        }, length);

    /// <summary>
    /// https://webidl.spec.whatwg.org/#dfn-attribute-getter — a promise-typed attribute's getter reports a
    /// failed receiver as a rejected promise too.
    /// </summary>
    private static JsObjectShape.Builder PromiseAccessor(this JsObjectShape.Builder builder, string name, Func<JsValue, JsValue> getter)
        => builder.Accessor(name, (thisObject, _) =>
        {
            try
            {
                return getter(thisObject);
            }
            catch (JavaScriptException exception) when (thisObject is Native.Object.ObjectInstance receiver)
            {
                return StreamPromises.RejectedWith(receiver.Engine, receiver.Engine._mainRealm, exception.Error);
            }
        });

    private static JsFontFace FaceArgument(JsFontFaceSet set, JsValue[] args, string member)
    {
        if (args.Length == 0)
        {
            Throw.TypeError(set.Owner.Realm, "Failed to execute '" + member + "' on 'FontFaceSet': 1 argument required, but only 0 present.");
        }

        if (args[0] is not JsFontFace face)
        {
            Throw.TypeError(set.Owner.Realm, "Failed to execute '" + member + "' on 'FontFaceSet': parameter 1 is not of type 'FontFace'.");
            return null!;
        }

        return face;
    }

    private static string RequiredString(JsFontFaceSet set, JsValue[] args, string member)
    {
        if (args.Length == 0)
        {
            Throw.TypeError(set.Owner.Realm, "Failed to execute '" + member + "' on 'FontFaceSet': 1 argument required, but only 0 present.");
        }

        return TypeConverter.ToString(args[0]);
    }

    /// <summary>The <c>optional CSSOMString text = " "</c> argument.</summary>
    private static string Text(JsValue[] args) => args.At(1) is { } text && !text.IsUndefined() ? TypeConverter.ToString(text) : " ";
}
