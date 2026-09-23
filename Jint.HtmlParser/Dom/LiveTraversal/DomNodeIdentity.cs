using System.Runtime.CompilerServices;

namespace Jint.HtmlParser;

// Attr is deliberately not a Node. This value identifies exactly one native object.
internal readonly struct DomNodeIdentity : IEquatable<DomNodeIdentity>
{
    internal DomNodeIdentity(Node node) => Node = node ?? throw new ArgumentNullException(nameof(node));

    internal DomNodeIdentity(Attr attribute) => Attribute = attribute ?? throw new ArgumentNullException(nameof(attribute));

    internal Node? Node { get; }
    internal Attr? Attribute { get; }
    internal bool IsValid => Node is not null || Attribute is not null;

    public bool Equals(DomNodeIdentity other)
        => ReferenceEquals(Node, other.Node) && ReferenceEquals(Attribute, other.Attribute);

    public override bool Equals(object? other) => other is DomNodeIdentity identity && Equals(identity);

    public override int GetHashCode()
        => Node is { } node ? RuntimeHelpers.GetHashCode(node)
            : Attribute is { } attribute ? RuntimeHelpers.GetHashCode(attribute) : 0;
}
