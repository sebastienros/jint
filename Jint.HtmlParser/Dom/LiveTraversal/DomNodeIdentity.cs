using System.Runtime.CompilerServices;

namespace Jint.HtmlParser;

// Attr is deliberately not a Node. This value identifies exactly one native object.
/// <summary>Identifies exactly one native node or attribute by reference identity; a default value is invalid.</summary>
public readonly struct DomNodeIdentity : IEquatable<DomNodeIdentity>
{
    /// <summary>Creates an identity for a non-null native object.</summary>
    public DomNodeIdentity(Node node) => Node = node ?? throw new ArgumentNullException(nameof(node));

    /// <summary>Creates an identity for a non-null native object.</summary>
    public DomNodeIdentity(Attr attribute) => Attribute = attribute ?? throw new ArgumentNullException(nameof(attribute));

    /// <summary>Gets the node identity, or null when this value identifies an attribute.</summary>
    public Node? Node { get; }
    /// <summary>Gets the attribute identity, or null when this value identifies a node.</summary>
    public Attr? Attribute { get; }
    /// <summary>Gets whether the identity names a native node or attribute.</summary>
    public bool IsValid => Node is not null || Attribute is not null;

    /// <summary>Tests equality by underlying reference identity.</summary>
    public bool Equals(DomNodeIdentity other)
        => ReferenceEquals(Node, other.Node) && ReferenceEquals(Attribute, other.Attribute);

    /// <summary>Tests equality by underlying reference identity.</summary>
    public override bool Equals(object? obj) => obj is DomNodeIdentity identity && Equals(identity);

    /// <summary>Compares the underlying native identities.</summary>
    public static bool operator ==(DomNodeIdentity left, DomNodeIdentity right) => left.Equals(right);
    /// <summary>Compares the underlying native identities.</summary>
    public static bool operator !=(DomNodeIdentity left, DomNodeIdentity right) => !left.Equals(right);

    /// <summary>Returns the reference-identity hash, or zero for an invalid identity.</summary>
    public override int GetHashCode()
        => Node is { } node ? RuntimeHelpers.GetHashCode(node)
            : Attribute is { } attribute ? RuntimeHelpers.GetHashCode(attribute) : 0;
}
