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
    private bool _cachedFocusIndicated;
    private Element? _cachedPress;
    private Element? _cachedHover;
    private bool? _supportedStyles;
    private CssMutationStamp _cachedResources;
    private CssStyleSheetRevisionSnapshot? _cachedSheetRevisions;
    private CssValueWork? _cachedSheetWork;
    private bool _reuseDisabled;
    private int _mutationDepth;

    // What the retained plan's rules can observe of focus, press and hover. Until a plan is read, all of it:
    // an unread plan proves nothing about the rules.
    private CssInteractionDependencies _cachedInteractions = CssInteractionDependencies.All;

    // What the rules that can change a measurement observe of them: the rest only retire the query.
    private CssInteractionDependencies _cachedMeasurementInteractions = CssInteractionDependencies.All;

    /// <summary>An opaque Browser-owned revision; saturation permanently declines reuse.</summary>
    internal ulong Version { get; private set; }

    /// <summary>
    /// A collection count may be retained only while Browser owns every writer and no mutation or
    /// parser callback is in progress. Collection membership does not depend on the CSS cascade.
    /// </summary>
    internal bool TryGetCollectionVersion(out ulong version)
    {
        version = Version;
        return !_reuseDisabled && _mutationDepth == 0 && _runtime.ReadyState is "interactive" or "complete"
            && _runtime.Options.EngineConfiguration.Count == 0;
    }

    /// <summary>
    /// https://drafts.csswg.org/cssom-view/#dom-element-getboundingclientrect requires current boxes.
    /// Suspend retention throughout native writes: conversions, reactions and callbacks can reenter
    /// geometry before the write returns, and a failed operation can already have changed the DOM.
    /// </summary>
    internal MutationScope BeginMutation(bool unknown = false)
    {
        if (unknown) ResetMeasurements("unknown-scope");
        if (_mutationDepth++ == 0)
        {
            RetireQueries();
        }
        return new MutationScope(this, unknown);
    }

    internal void Invalidate()
    {
        ResetMeasurements();
        RetireQueries();
        _cachedDocument = null;
        _cachedFocus = null;
        _cachedFocusIndicated = false;
        _cachedPress = null;
        _cachedHover = null;
        _supportedStyles = null;
        _cachedSheetRevisions = null;
        _cachedSheetWork = null;
        _cachedInteractions = CssInteractionDependencies.All;
        _cachedMeasurementInteractions = CssInteractionDependencies.All;
    }

    /// <summary>Narrows the interaction witnesses to what the plan behind retained measurements can observe.</summary>
    private void NoteInteractions(NativeCssQuery query)
    {
        if (query.TryGetInteractionDependencies(out var dependencies))
        {
            _cachedInteractions = dependencies;
        }

        if (query.TryGetMeasurementInteractionDependencies(out var measurement))
        {
            _cachedMeasurementInteractions = measurement;
        }
    }

    private void RetireQueries()
    {
        _sizes = null;
        _layout = null;
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
        if (_runtime.Document?.MutationStamp == ulong.MaxValue || _reuseDisabled || _mutationDepth != 0 || _runtime.ReadyState is not ("interactive" or "complete")
            || _runtime.Options.EngineConfiguration.Count != 0 || CssRuleUsage.IsTrackingDocument(_runtime.Document))
        {
            _sizes = null;
            _layout = null;
            if (_mutationDepth == 0) ResetMeasurements("reuse-disabled");
            return false;
        }

        // StylesChanged may invoke a host checkpoint. Read every scalar witness after it returns.
        var stylesChanged = StylesChanged();
        _runtime.Dom.CancellationToken.ThrowIfCancellationRequested();
        var events = BrowserEventRealm.Of(_runtime.Engine);
        var document = _runtime.Document;
        var controlRevision = document is null ? 0 : Dom.BrowserSelectorSemanticRevision.Read(document);
        var url = document is null ? null : Dom.DomDocumentState.Of(document).Url;
        // A focus, press or hover change moves no box unless some rule can match on it: Playwright's every
        // click presses and releases, and retiring measurements twice per click for a document with no
        // :active rule rebuilt the whole flat layout for nothing. A rule that matches on it and declares no
        // property the layout reads restyles without moving a box either: it retires the query alone.
        var interactions = _cachedInteractions;
        var measurementInteractions = _cachedMeasurementInteractions;
        var documentChange = !ReferenceEquals(_cachedDocument, document) || _cachedMedia != _runtime.Media
            || document is not null && (!_cachedResources.CanReuse || _cachedResources != NativeCssStyleSheets.Stamp(document)) || stylesChanged
            || controlRevision == ulong.MaxValue || _cachedControlRevision != controlRevision
            || _cachedUrl != url;
        var changed = InteractionChanges(events);
        var globalChange = documentChange || (changed & interactions) != 0;
        if (globalChange || _cachedNativeStamp != document?.MutationStamp)
        {
            var moved = changed & measurementInteractions;
            if (documentChange || moved != 0 || !ReferenceEquals(_observedDocument, document) || _observedStamp != document?.MutationStamp)
                ResetMeasurements(stylesChanged ? "stylesheet" : _cachedControlRevision != controlRevision ? "control-state"
                    : (moved & CssInteractionDependencies.Focus) != 0 ? "focus"
                    : (moved & CssInteractionDependencies.Active) != 0 ? "press"
                    : (moved & CssInteractionDependencies.Hover) != 0 ? "hover"
                    : "global-witness");
            RetireQueries();
            _supportedStyles = null;
            _cachedSheetRevisions = null;
            _cachedSheetWork = null;
            _cachedDocument = document;
            _cachedNativeStamp = document?.MutationStamp ?? 0;
            _cachedControlRevision = controlRevision;
            _cachedResources = document is null ? default : NativeCssStyleSheets.Stamp(document);
            _cachedMedia = _runtime.Media;
            _cachedUrl = url;
            _cachedFocus = events.FocusedElement;
            _cachedFocusIndicated = events.FocusIndicated;
            _cachedPress = events.MousePressTarget;
            _cachedHover = events.MouseHoverTarget;
            _cachedInteractions = CssInteractionDependencies.All;
            _cachedMeasurementInteractions = CssInteractionDependencies.All;
        }

        // Source installation and CSSOM edits are independent of the native DOM revision.
        return _supportedStyles ??= SupportsStyles(document);
    }

    // The interaction witnesses that moved since the query was made.
    private CssInteractionDependencies InteractionChanges(BrowserEventRealm events)
    {
        var changed = CssInteractionDependencies.None;
        if (!ReferenceEquals(_cachedFocus, events.FocusedElement) || _cachedFocusIndicated != events.FocusIndicated)
            changed |= CssInteractionDependencies.Focus;
        if (!ReferenceEquals(_cachedPress, events.MousePressTarget)) changed |= CssInteractionDependencies.Active;
        if (!ReferenceEquals(_cachedHover, events.MouseHoverTarget)) changed |= CssInteractionDependencies.Hover;
        return changed;
    }

    private bool SupportsStyles(Document? document)
    {
        if (document is null || NativeCssStyleSheets.RealmOf(document) is not { } realm) return false;
        var nativeStamp = document.MutationStamp;
        var resources = NativeCssStyleSheets.Stamp(document);
        var version = Version;
        var work = new CssValueWork(realm.CancellationToken, realm.CssCheckpoint);
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

    internal readonly struct MutationScope(PageLayout layout, bool unknown) : IDisposable
    {
        private readonly PageLayout? _layout = layout;
        private readonly bool _unknown = unknown;

        public void Dispose()
        {
            if (_layout is not { } owner) return;
            if (_unknown) owner.ResetMeasurements();
            if (--owner._mutationDepth == 0)
            {
                owner.RetireQueries();
            }
        }
    }
}
