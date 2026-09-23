namespace Jint.HtmlParser;

// DOM Standard §5.4, StaticRange constructor and valid static range.
// https://dom.spec.whatwg.org/#interface-staticrange
internal sealed class DomStaticRange
{
    internal DomStaticRange(BoundaryPoint start, BoundaryPoint end)
    {
        ValidateContainer(start.Container, nameof(start));
        ValidateContainer(end.Container, nameof(end));
        Start = start;
        End = end;
    }

    internal BoundaryPoint Start { get; }
    internal BoundaryPoint End { get; }
    internal bool Collapsed => Start.Equals(End);

    internal bool IsValid(CancellationToken cancellationToken)
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
