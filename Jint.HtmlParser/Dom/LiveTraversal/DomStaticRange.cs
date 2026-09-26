namespace Jint.HtmlParser;

// DOM Standard §5.4, StaticRange constructor and valid static range.
// https://dom.spec.whatwg.org/#interface-staticrange
/// <summary>An immutable endpoint snapshot that reports current validity without repairing its points.</summary>
/// <remarks>Construction accepts reversed, out-of-bounds and different-root points, but rejects Attr and DocumentType containers.</remarks>
public sealed class DomStaticRange
{
    /// <summary>Creates an immutable endpoint snapshot.</summary>
    public DomStaticRange(BoundaryPoint start, BoundaryPoint end)
    {
        ValidateContainer(start.Container, nameof(start));
        ValidateContainer(end.Container, nameof(end));
        Start = start;
        End = end;
    }

    /// <summary>Gets the start boundary point.</summary>
    public BoundaryPoint Start { get; }
    /// <summary>Gets the end boundary point.</summary>
    public BoundaryPoint End { get; }
    /// <summary>Gets whether both endpoints are the same boundary point.</summary>
    public bool Collapsed => Start.Equals(End);

    /// <summary>Tests current lengths, shared root and endpoint order without changing the stored points.</summary>
    /// <exception cref="OperationCanceledException">The supplied cancellation token is canceled.</exception>
    public bool IsValid(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var startLength = BoundaryOrder.GetLength(Start.Container);
        cancellationToken.ThrowIfCancellationRequested();
        if (Start.Offset > startLength)
        {
            return false;
        }

        var endLength = BoundaryOrder.GetLength(End.Container);
        cancellationToken.ThrowIfCancellationRequested();
        if (End.Offset > endLength)
        {
            return false;
        }

        try
        {
            var valid = BoundaryOrder.Compare(Start, End, cancellationToken) <= 0;
            cancellationToken.ThrowIfCancellationRequested();
            return valid;
        }
        catch (DomException exception) when (exception.Name == "WrongDocumentError")
        {
            cancellationToken.ThrowIfCancellationRequested();
            return false;
        }
    }

    private static void ValidateContainer(DomNodeIdentity identity, string parameter)
    {
        if (!identity.IsValid)
        {
            throw new ArgumentException("A node identity is required.", parameter);
        }

        if (identity.Attribute is not null || identity.Node is DocumentType)
        {
            throw new DomException("InvalidNodeTypeError", "StaticRange endpoints cannot be attributes or document types.");
        }
    }
}
