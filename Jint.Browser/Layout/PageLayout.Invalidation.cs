using AngleSharp.Css.Dom;
using AngleSharp.Dom;
using Jint.Browser.Dom.Views;
using Jint.Browser.Events;
using Jint.Browser.Runtime;

namespace Jint.Browser.Layout;

internal sealed partial class PageLayout
{
    private FlatLayout.SizeQuery? _sizes;
    private FlatLayout? _layout;
    private IDocument? _cachedDocument;
    private PageMediaEnvironment? _cachedMedia;
    private string? _cachedUrl;
    private IElement? _cachedFocus;
    private IElement? _cachedPress;
    private bool? _supportedStyles;
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
        if (_reuseDisabled || _mutationDepth != 0 || _runtime.ReadyState != "complete"
            || _runtime.Options.EngineConfiguration.Count != 0 || CssRuleUsage.IsTrackingDocument(_runtime.Document))
        {
            _sizes = null;
            _layout = null;
            return false;
        }

        var events = BrowserEventRealm.Of(_runtime.Engine);
        var document = _runtime.Document;
        var url = document?.Url;
        if (!ReferenceEquals(_cachedDocument, document) || _cachedMedia != _runtime.Media
            || _cachedUrl != url || !ReferenceEquals(_cachedFocus, events.FocusedElement)
            || !ReferenceEquals(_cachedPress, events.MousePressTarget))
        {
            Invalidate();
            _cachedDocument = document;
            _cachedMedia = _runtime.Media;
            _cachedUrl = url;
            _cachedFocus = events.FocusedElement;
            _cachedPress = events.MousePressTarget;
        }

        // An import can finish without a call through our bindings. Inspect once per revision, not
        // on unchanged reads. Imported sheets retain the established query-local behavior.
        return _supportedStyles ??= SupportsStyles(document);
    }

    private static bool SupportsStyles(IDocument? document)
    {
        if (document is null)
        {
            return false;
        }

        foreach (var sheet in document.StyleSheets)
        {
            if (sheet is not ICssStyleSheet css || HasImport(css.Rules))
            {
                return false;
            }
        }
        return true;
    }

    private static bool HasImport(ICssRuleList rules)
    {
        var pending = new Stack<ICssRuleList>();
        pending.Push(rules);
        while (pending.TryPop(out var list))
        {
            foreach (var rule in list)
            {
                if (rule is ICssImportRule)
                {
                    return true;
                }
                if (rule is ICssGroupingRule group)
                {
                    pending.Push(group.Rules);
                }
            }
        }
        return false;
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
