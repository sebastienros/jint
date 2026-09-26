using Jint.HtmlParser;

namespace Jint.Browser.Dom;

/// <summary>Input facts that do not require materializing current value or selection state.</summary>
internal static class DomInputMembers
{
    // https://html.spec.whatwg.org/multipage/input.html#dom-input-type
    internal static string Type(DomRealm realm, Element input)
    {
        var work = new DomReadWork(realm.NativeReadCheckpoint, realm.CancellationToken);
        work.Check();
        var type = HtmlInputTypes.Parse(work.Attribute(input, "type"));
        work.Check();
        return HtmlInputTypes.Info(type).Keyword;
    }
}
