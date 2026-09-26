using Jint.HtmlParser;
using Jint.Native;

namespace Jint.Browser.Dom;

// The retained obsolete interfaces reflect actual native content attributes.
// They do not add legacy convenience state to Element.
internal static class DomLegacyHtmlAttributes
{
    internal static string? Read(DomRealm realm, Element element, string name)
    {
        var work = new DomReadWork(realm.NativeReadCheckpoint, realm.CancellationToken);
        work.Check();
        var value = work.Attribute(element, name);
        work.Check();
        return value;
    }

    internal static JsValue Set(DomRealm realm, Element element, string name, string value)
    {
        var work = new DomReadWork(realm.NativeReadCheckpoint, realm.CancellationToken);
        work.Check();
        element.SetAttributeNS(null, name, value);
        work.Check();
        return JsValue.Undefined;
    }

    internal static JsValue SetFlag(DomRealm realm, Element element, string name, bool value)
    {
        var work = new DomReadWork(realm.NativeReadCheckpoint, realm.CancellationToken);
        work.Check();
        if (value) element.SetAttributeNS(null, name, "");
        else element.RemoveAttributeNS(null, name);
        work.Check();
        return JsValue.Undefined;
    }
}
