namespace Jint.HtmlParser;

/// <summary>Invocation-local accounting of native form tree and index visits.</summary>
internal sealed class HtmlFormWorkProbe : IDisposable
{
    private readonly Node _root;

    internal HtmlFormWorkProbe(Node root)
    {
        ArgumentNullException.ThrowIfNull(root);
        if (root.ParentNode is not null || root.FormWorkProbe is not null)
        {
            throw new InvalidOperationException("A form work probe requires an unprobed ordinary tree root.");
        }

        _root = root;
        root.FormWorkProbe = this;
    }

    internal long Visits { get; private set; }

    internal void Visit() => Visits++;

    public void Dispose()
    {
        if (ReferenceEquals(_root.FormWorkProbe, this))
        {
            _root.FormWorkProbe = null;
        }
    }
}
