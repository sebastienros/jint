using Jint.HtmlParser;
using Jint.Browser.Dom.Views;
using Jint.Browser.Events;
using Jint.Browser.Runtime;
using Jint.Browser.Styling;
using Jint.HtmlParser.Css.Model;
using Jint.HtmlParser.Css.Model.Syntax;
using Jint.HtmlParser.Css.Values;

namespace Jint.Browser.Layout;

internal sealed partial class PageLayout
{
    private FlatLayout.SizeQuery? _sizes;
    private FlatLayout? _layout;
    private Document? _cachedDocument;
    private PageMediaEnvironment? _cachedMedia;
    private ulong _cachedNativeStamp;
    private ulong _cachedControlRevision;
    private string? _cachedUrl;
    private Element? _cachedFocus;
    private Element? _cachedPress;
    private bool? _supportedStyles;
    private CssMutationStamp _cachedResources;
    private CssStyleSheetRevisionSnapshot? _cachedSheetRevisions;
    private CssValueWork? _cachedSheetWork;
    private bool _reuseDisabled;
    private int _mutationDepth;

    /// <summary>An opaque Browser-owned revision; saturation permanently declines reuse.</summary>
    internal ulong Version { get; private set; }

    /// <summary>
    /// A collection count may be retained only while Browser owns every writer and no mutation or
    /// parser callback is in progress. Collection membership does not depend on the CSS cascade.
    /// </summary>
    internal bool TryGetCollectionVersion(out ulong version)
    {
        version = Version;
        return !_reuseDisabled && _mutationDepth == 0 && _runtime.ReadyState == "complete"
            && _runtime.Options.EngineConfiguration.Count == 0;
    }

    /// <summary>
    /// https://drafts.csswg.org/cssom-view/#dom-element-getboundingclientrect requires current boxes.
    /// Suspend retention throughout native writes: conversions, reactions and callbacks can reenter
    /// geometry before the write returns, and a failed operation can already have changed the DOM.
    /// </summary>
    internal MutationScope BeginMutation()
    {
        if (_mutationDepth++ == 0)
        {
            Invalidate();
        }
        return new MutationScope(this);
    }

    internal void Invalidate()
    {
        _sizes = null;
        _layout = null;
        _cachedDocument = null;
        _cachedFocus = null;
        _cachedPress = null;
        _supportedStyles = null;
        _cachedSheetRevisions = null;
        _cachedSheetWork = null;
        if (Version == ulong.MaxValue)
        {
            _reuseDisabled = true;
        }
        else
        {
            Version++;
        }
    }

    /// <summary>Native asynchronous attachment or host-owned writes require a fresh query.</summary>
    internal void DisableReuse()
    {
        _reuseDisabled = true;
        Invalidate();
    }

    private bool CanReuse()
    {
        // ConfigureEngine can install arbitrary native writers, converters and selector services.
        // Keep its existing contract, without requiring hosts to adopt a new invalidation API.
        if (_runtime.Document?.MutationStamp == ulong.MaxValue || _reuseDisabled || _mutationDepth != 0 || _runtime.ReadyState != "complete"
            || _runtime.Options.EngineConfiguration.Count != 0 || CssRuleUsage.IsTrackingDocument(_runtime.Document))
        {
            _sizes = null;
            _layout = null;
            return false;
        }

        // StylesChanged may invoke a host checkpoint. Read every scalar witness after it returns.
        var stylesChanged = StylesChanged();
        _runtime.Dom.CancellationToken.ThrowIfCancellationRequested();
        var events = BrowserEventRealm.Of(_runtime.Engine);
        var document = _runtime.Document;
        var controlRevision = document is null ? 0 : Dom.BrowserSelectorSemanticRevision.Read(document);
        var url = document is null ? null : Dom.DomDocumentState.Of(document).Url;
        if (!ReferenceEquals(_cachedDocument, document) || _cachedNativeStamp != document?.MutationStamp || _cachedMedia != _runtime.Media
            || document is not null && (!_cachedResources.CanReuse || _cachedResources != NativeCssStyleSheets.Stamp(document)) || stylesChanged
            || controlRevision == ulong.MaxValue || _cachedControlRevision != controlRevision
            || _cachedUrl != url || !ReferenceEquals(_cachedFocus, events.FocusedElement)
            || !ReferenceEquals(_cachedPress, events.MousePressTarget))
        {
            Invalidate();
            _cachedDocument = document;
            _cachedNativeStamp = document?.MutationStamp ?? 0;
            _cachedControlRevision = controlRevision;
            _cachedResources = document is null ? default : NativeCssStyleSheets.Stamp(document);
            _cachedMedia = _runtime.Media;
            _cachedUrl = url;
            _cachedFocus = events.FocusedElement;
            _cachedPress = events.MousePressTarget;
        }

        // Source installation and CSSOM edits are independent of the native DOM revision.
        return _supportedStyles ??= SupportsStyles(document);
    }

    private bool SupportsStyles(Document? document)
    {
        if (document is null || NativeCssStyleSheets.RealmOf(document) is not { } realm) return false;
        var nativeStamp = document.MutationStamp;
        var resources = NativeCssStyleSheets.Stamp(document);
        var version = Version;
        var work = new CssValueWork(realm.CancellationToken, realm.Engine.Constraints.Check);
        var sheets = NativeCssStyleSheets.Get(document, work, includeShadow: true);
        var roots = new CssStyleSheet[sheets.Count];
        for (var i = 0; i < roots.Length; i++)
        {
            work.Charge(1);
            roots[i] = sheets[i].Sheet;
        }
        var revisions = CssStyleSheetRevisionSnapshot.Capture(roots, work);
        realm.CancellationToken.ThrowIfCancellationRequested();
        if (version != Version || !ReferenceEquals(_cachedDocument, document) || nativeStamp == ulong.MaxValue ||
            nativeStamp != document.MutationStamp || !resources.CanReuse || resources != NativeCssStyleSheets.Stamp(document) ||
            _cachedControlRevision != Dom.BrowserSelectorSemanticRevision.Read(document))
            throw new InvalidOperationException(NativeCssQuery.Invalidated);
        _cachedSheetRevisions = revisions;
        _cachedSheetWork = work;
        _cachedResources = resources;
        return true;
    }

    private bool StylesChanged()
    {
        return _cachedSheetRevisions is { } revisions && !revisions.IsCurrent(_cachedSheetWork!);
    }

    internal readonly struct MutationScope(PageLayout layout) : IDisposable
    {
        private readonly PageLayout? _layout = layout;

        public void Dispose()
        {
            if (_layout is { } owner && --owner._mutationDepth == 0)
            {
                owner.Invalidate();
            }
        }
    }
}
