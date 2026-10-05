using Jint.HtmlParser;

namespace Jint.Browser.Dom;

/// <summary>Per-read host checks for native links, attributes and string comparisons.</summary>
internal sealed class DomReadWork(Action<int>? checkpoint, CancellationToken token, Node? root = null)
{
    private readonly Document? _owner = root as Document ?? root?.OwnerDocument;
    private readonly ulong? _stamp = (root as Document ?? root?.OwnerDocument)?.MutationStamp;
    private int _pending;
    internal CancellationToken Token => token;
    internal void Check()
    {
        token.ThrowIfCancellationRequested();
        Verify();
        checkpoint?.Invoke(_pending);
        token.ThrowIfCancellationRequested();
        Verify();
        _pending = 0;
    }
    private void Verify()
    {
        if (root is not null && (!ReferenceEquals(_owner, root as Document ?? root.OwnerDocument)
            || _owner is not null && (_stamp == ulong.MaxValue || _stamp != _owner.MutationStamp)))
            throw new InvalidOperationException("The live collection read was invalidated by mutation.");
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
    internal void Account(int count)
    {
        while (count >= 256 - _pending)
        {
            count -= 256 - _pending;
            _pending = 256;
            Check();
        }
        _pending += count;
    }
    internal bool EqualSpan(ReadOnlySpan<char> value, ReadOnlySpan<char> expected)
    {
        Step();
        if (value.Length != expected.Length) return false;
        for (var offset = 0; offset < value.Length; offset += 256)
        {
            var length = Math.Min(256, value.Length - offset);
            Account(length);
            if (!value.Slice(offset, length).SequenceEqual(expected.Slice(offset, length))) return false;
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
