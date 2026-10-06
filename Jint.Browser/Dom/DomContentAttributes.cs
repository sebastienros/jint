using Jint.HtmlParser;

namespace Jint.Browser.Dom;

/// <summary>HTML content attributes have a null namespace (HTML §2.6.1).</summary>
internal static class DomContentAttributes
{
    internal static string? Get(DomRealm realm, Element element, string name)
    {
        var work = new DomReadWork(realm.NativeReadCheckpoint, realm.CancellationToken);
        work.Token.ThrowIfCancellationRequested();
        var value = work.Attribute(element, name);
        work.Check();
        return value;
    }
}
