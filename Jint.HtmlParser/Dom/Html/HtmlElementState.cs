namespace Jint.HtmlParser;

/// <summary>The stable native HTML view of an element.</summary>
internal sealed class HtmlElementState
{
    internal HtmlElementState(Element element) => Element = element;

    internal Element Element { get; }
    internal Element? FormOwner => HtmlFormState.GetOwner(Element);
}
