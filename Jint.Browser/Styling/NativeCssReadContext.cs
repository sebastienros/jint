using Jint.Browser.Accessibility;
using Jint.Browser.Dom.Views;
using Jint.Browser.Layout;
using Jint.HtmlParser;
using Jint.HtmlParser.Css.Selectors;

namespace Jint.Browser.Styling;

// One synchronous Browser read: a native query, its existing cascade traversal and optional
// synthetic size query. The provider receives captured values; no page/engine/realm is stored here.
internal sealed class NativeCssReadContext
{
    private readonly Document _document;
    private readonly ElementVisibility _visibility;
    private readonly double _viewportWidth;
    private readonly Action _verify;
    private readonly CancellationToken _token;
    private FlatLayout.SizeQuery? _sizes;
    private bool _failed;

    internal NativeCssReadContext(NativeCssQuery query, CssCascade.Traversal traversal,
        Document document, ElementVisibility visibility, double viewportWidth,
        Action verify, CancellationToken token)
    {
        Query = query;
        Traversal = traversal;
        _document = document;
        _visibility = visibility;
        _viewportWidth = viewportWidth;
        _verify = verify;
        _token = token;
    }

    internal NativeCssQuery Query { get; }
    internal CssCascade.Traversal Traversal { get; }
    internal bool HasSizeQuery => _sizes is not null;

    internal void Verify()
    {
        if (_failed) throw new InvalidOperationException("The native CSS read context was aborted.");
        _token.ThrowIfCancellationRequested();
        _verify();
        Query.Verify();
        _token.ThrowIfCancellationRequested();
    }

    internal FlatLayout.SizeQuery MeasureSizes()
    {
        try
        {
            Verify();
            // No Visibility.CreateTraversal here: metric callbacks enter the SAME cascade/query.
            return _sizes ??= new(_document, _visibility, _viewportWidth, Traversal, Verify, _token);
        }
        catch { Discard(); throw; }
    }

    private void Discard() { _failed = true; _sizes = null; }
}
