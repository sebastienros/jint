using Jint.HtmlParser;

namespace Jint.Browser.Dom;

/// <summary>Per-read host checks for native links, attributes and string comparisons.</summary>
internal sealed class DomReadWork(Action<int>? checkpoint, CancellationToken token)
{
    private int _pending;
    internal CancellationToken Token => token;
    internal void Check()
    {
        token.ThrowIfCancellationRequested();
        checkpoint?.Invoke(_pending);
        token.ThrowIfCancellationRequested();
        _pending = 0;
    }
    internal void Step()
    {
        if (++_pending == 256) Check();
    }
    internal bool Equal(string? value, string expected)
    {
        Step();
        if (value is null || value.Length != expected.Length) return false;
        for (var i = 0; i < value.Length; i++)
        {
            Step();
            if (value[i] != expected[i]) return false;
        }
        return true;
    }
    internal bool EqualAsciiIgnoreCase(string? value, string expected)
    {
        Step();
        if (value is null || value.Length != expected.Length) return false;
        for (var i = 0; i < value.Length; i++)
        {
            Step();
            var c = value[i];
            if (c is >= 'A' and <= 'Z') c = (char) (c + ('a' - 'A'));
            if (c != expected[i]) return false;
        }
        return true;
    }
    internal string? Attribute(Element element, string name)
    {
        for (uint i = 0; i < (uint) element.AttributeCount; i++)
        {
            Step();
            var attribute = element.GetAttributeAt(i)!;
            if (attribute.NamespaceUri is null && Equal(attribute.LocalName, name)) return attribute.Value;
        }
        return null;
    }
    internal Node Root(Node node)
    {
        while (node.ParentNode is { } parent)
        {
            Step();
            node = parent;
        }
        return node;
    }
}
