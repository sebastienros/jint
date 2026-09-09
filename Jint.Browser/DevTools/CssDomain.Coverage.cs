using AngleSharp.Css.Dom;
using AngleSharp.Dom;
using Jint.Browser.Dom.Views;
using Jint.Browser.Runtime;
using Jint.DevTools;
using Jint.DevTools.Domains;
using Jint.DevTools.Protocol;
using Jint.DevTools.Session;
using ProtocolCss = Jint.DevTools.Protocol.CSS;

namespace Jint.Browser.DevTools;

/// <summary>
/// Rule-usage coverage: which of a page's style rules matched something while a client was watching.
/// </summary>
/// <remarks>
/// <para>
/// <b>This is what <c>page.coverage.startCSSCoverage()</c> is.</b> Puppeteer and Playwright both build it
/// out of exactly five calls — <c>DOM.enable</c>, <c>CSS.enable</c>, <c>CSS.startRuleUsageTracking</c>, the
/// <c>CSS.styleSheetAdded</c> stream with a <c>CSS.getStyleSheetText</c> per sheet, and finally
/// <c>CSS.stopRuleUsageTracking</c> — and then slice each sheet's text with the offsets the last one
/// carries. All five are here; nothing else of the domain's thirty-odd editing commands is.
/// </para>
/// <para>
/// <b>Used means matched during a cascade computation.</b> <see cref="CssRuleUsage"/> is the seam, armed
/// only while a window is open, and every caller of <c>Dom/Views/CssCascade</c> feeds it: the page's own
/// <c>getComputedStyle</c>, every box the flat model measures, the accessibility tree's hidden verdict and
/// this domain's own <c>getComputedStyleForNode</c>. There is no rendering here to recalculate style for,
/// so a document nobody queries computes no cascade — which is why
/// <see cref="StartRuleUsageTrackingAsync"/> and a commit each walk the document once, the way Blink's own
/// <c>startRuleUsageTracking</c> marks every element for style recalculation and runs it before returning.
/// </para>
/// <para>
/// <b>The offsets index the text this domain hands out and no other string.</b>
/// <see cref="CssStyleSheetText"/> serializes a sheet and measures the same serialization, because
/// AngleSharp keeps no source position on a rule; what that costs a client is stated on that type.
/// </para>
/// <para>
/// See <see href="https://chromedevtools.github.io/devtools-protocol/tot/CSS/#method-startRuleUsageTracking"/>,
/// <see href="https://chromedevtools.github.io/devtools-protocol/tot/CSS/#method-takeCoverageDelta"/> and
/// <see href="https://chromedevtools.github.io/devtools-protocol/tot/CSS/#method-stopRuleUsageTracking"/>.
/// </para>
/// </remarks>
internal sealed partial class CssDomain : IDetachableDomain, ITargetObserver
{
    private readonly HashSet<string> _announced = new(StringComparer.Ordinal);

    private CssRuleUsageTracker? _coverage;

    private CssStyleSheetTracker Sheets => _target.Sheets;

    /// <inheritdoc/>
    void IDetachableDomain.Detach()
    {
        Sheets.Remove(this);
        StopTracking();
    }

    /// <inheritdoc/>
    /// <remarks>
    /// A navigation throws every <c>styleSheetId</c> away with the document that minted them — the target
    /// clears the table once, for every attachment — so this forgets what it has announced. The window
    /// itself survives, unbound, because coverage is the client's rather than the document's; the commit
    /// that follows rebinds and sweeps it.
    /// </remarks>
    void ITargetObserver.RuntimeReplaced(TargetRuntime runtime)
    {
        _announced.Clear();
        _coverage?.Rebind(null);
    }

    /// <summary>Announces the new document's sheets, and gives an open window its first look at it.</summary>
    /// <remarks>
    /// Called from <see cref="PageTarget"/> at <c>DocumentParsed</c>, which is the first moment there is a
    /// tree to read sheets off — <c>RuntimeReplaced</c> runs before the parse, where the document has none.
    /// </remarks>
    internal void DocumentCommitted(PageRuntime runtime)
    {
        if (!IsEnabled)
        {
            return;
        }

        foreach (var header in Unannounced(runtime))
        {
            EmitDetached(CSSEvents.StyleSheetAdded(new ProtocolCss.StyleSheetAddedEvent { Header = header }));
        }

        if (_coverage is { } coverage && runtime.Document is { } document)
        {
            coverage.Rebind(document);
            coverage.Sweep();
        }
    }

    /// <summary>
    /// https://chromedevtools.github.io/devtools-protocol/tot/CSS/#method-getStyleSheetText — one sheet's
    /// text, which is the string a coverage offset indexes.
    /// </summary>
    protected override async ValueTask<ProtocolCss.GetStyleSheetTextResponse> GetStyleSheetTextAsync(
        ProtocolCss.GetStyleSheetTextRequest parameters,
        CommandContext context)
    {
        await AnnounceAsync(context).ConfigureAwait(false);

        var sheet = Sheets.ById(parameters.StyleSheetId)
            ?? Throw.ServerError<ICssStyleSheet>("No style sheet with given id found");

        return new ProtocolCss.GetStyleSheetTextResponse { Text = CssStyleSheetText.Of(sheet).Text };
    }

    /// <summary>
    /// https://chromedevtools.github.io/devtools-protocol/tot/CSS/#method-startRuleUsageTracking — opens a
    /// window, and walks the document once so that it describes the page as it stands.
    /// </summary>
    /// <remarks>
    /// The protocol gives this command no return values, so the reply is empty: the timestamp a client
    /// reads a window's start from is the one <c>takeCoverageDelta</c> answers with. Starting a second time
    /// replaces the window rather than failing, which is what re-arming a coverage run means.
    /// </remarks>
    protected override async ValueTask<EmptyResult> StartRuleUsageTrackingAsync(
        EmptyParameters parameters,
        CommandContext context)
    {
        await AnnounceAsync(context).ConfigureAwait(false);

        StopTracking();

        var coverage = new CssRuleUsageTracker();
        coverage.Rebind(Document());
        _coverage = coverage;
        CssRuleUsage.Arm(coverage);
        coverage.Sweep();

        return EmptyResult.Instance;
    }

    /// <summary>
    /// https://chromedevtools.github.io/devtools-protocol/tot/CSS/#method-takeCoverageDelta — the rules
    /// that became used since the last one.
    /// </summary>
    protected override async ValueTask<ProtocolCss.TakeCoverageDeltaResponse> TakeCoverageDeltaAsync(
        EmptyParameters parameters,
        CommandContext context)
    {
        var coverage = Tracking();
        await AnnounceAsync(context).ConfigureAwait(false);

        return new ProtocolCss.TakeCoverageDeltaResponse
        {
            Coverage = Report(coverage.TakeDelta()),
            Timestamp = DevToolsTarget.UnixMilliseconds() / 1000d,
        };
    }

    /// <summary>
    /// https://chromedevtools.github.io/devtools-protocol/tot/CSS/#method-stopRuleUsageTracking — the rest
    /// of the window, and the seam disarmed.
    /// </summary>
    /// <remarks>
    /// Chrome's own implementation is a <c>takeCoverageDelta</c> followed by turning the tracker off, so a
    /// client that took a delta and then stopped is told about the rules used in between and about nothing
    /// twice.
    /// </remarks>
    protected override async ValueTask<ProtocolCss.StopRuleUsageTrackingResponse> StopRuleUsageTrackingAsync(
        EmptyParameters parameters,
        CommandContext context)
    {
        var coverage = Tracking();
        await AnnounceAsync(context).ConfigureAwait(false);

        var usage = Report(coverage.TakeDelta());
        StopTracking();

        return new ProtocolCss.StopRuleUsageTrackingResponse { RuleUsage = usage };
    }

    /// <summary>The open window, or the refusal a client that never opened one gets.</summary>
    private CssRuleUsageTracker Tracking()
        => _coverage ?? Throw.ServerError<CssRuleUsageTracker>(
            "CSS rule usage tracking is not enabled",
            "no window is open on this attachment: send CSS.startRuleUsageTracking before asking for coverage");

    private void StopTracking()
    {
        if (_coverage is { } coverage)
        {
            CssRuleUsage.Disarm(coverage);
            _coverage = null;
        }
    }

    /// <summary>Turns recorded rules into the protocol's usage entries, dropping what cannot be placed.</summary>
    /// <remarks>
    /// A rule is dropped when its sheet has no identifier — which is how a user-agent rule is left out,
    /// the protocol having no way to name that sheet — and when the sheet's serialization gives it no range
    /// of its own. Everything reported is reported <c>used: true</c>: the protocol's own description of
    /// both commands is "the rules that were used", and a window only ever records a rule when it matched.
    /// </remarks>
    private ProtocolCss.RuleUsage[] Report(ICssStyleRule[] rules)
    {
        if (rules.Length == 0)
        {
            return [];
        }

        var texts = new Dictionary<ICssStyleSheet, CssStyleSheetText>(ReferenceEqualityComparer.Instance);
        var usage = new List<ProtocolCss.RuleUsage>(rules.Length);

        foreach (var rule in rules)
        {
            if (rule.Owner is not { } sheet || Sheets.KnownIdOf(sheet) is not { } id)
            {
                continue;
            }

            if (!texts.TryGetValue(sheet, out var text))
            {
                text = CssStyleSheetText.Of(sheet);
                texts[sheet] = text;
            }

            if (!text.TryRangeOf(rule, out var range))
            {
                continue;
            }

            usage.Add(new ProtocolCss.RuleUsage
            {
                StyleSheetId = id,
                StartOffset = range.Start,
                EndOffset = range.End,
                Used = true,
            });
        }

        return [.. usage];
    }

    /// <summary>Announces the sheets this attachment has not been told about, before the reply it precedes.</summary>
    private async ValueTask AnnounceAsync(CommandContext context)
    {
        if (!IsEnabled || PageRuntime.Find(_target.Runtime.Engine) is not { } runtime)
        {
            return;
        }

        foreach (var header in Unannounced(runtime))
        {
            await EmitAsync(
                CSSEvents.StyleSheetAdded(new ProtocolCss.StyleSheetAddedEvent { Header = header }),
                context.CancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>The headers for every sheet of the current document this attachment has not heard about.</summary>
    private List<ProtocolCss.CSSStyleSheetHeader> Unannounced(PageRuntime runtime)
    {
        var headers = new List<ProtocolCss.CSSStyleSheetHeader>();
        if (runtime.Document is not { } document)
        {
            return headers;
        }

        foreach (var sheet in CssStyleSheetTracker.SheetsOf(document))
        {
            var id = Sheets.IdOf(sheet);
            if (_announced.Add(id))
            {
                headers.Add(Header(sheet, id, runtime.DocumentUrl));
            }
        }

        return headers;
    }

    /// <summary>What a client is told about one sheet.</summary>
    /// <remarks>
    /// <para>
    /// <b>An inline sheet's <c>sourceURL</c> is the document's</b>, which is Chrome's answer and not a
    /// convenience: Puppeteer's and Playwright's CSS coverage both drop a header whose <c>sourceURL</c> is
    /// empty, so a <c>&lt;style&gt;</c> block reported with none would be invisible to the very clients
    /// this is for.
    /// </para>
    /// <para>
    /// <b>The position fields describe this domain's text rather than the document's.</b> A sheet's text
    /// here is its own serialization, so it starts at line zero, column zero; Chrome reports where a
    /// <c>&lt;style&gt;</c> element's content sits inside the page's markup, which is a source position
    /// nothing in AngleSharp records. <c>isMutable</c> and <c>isConstructed</c> are false because there is
    /// no editing command and no <c>new CSSStyleSheet()</c> in this binding.
    /// </para>
    /// </remarks>
    private ProtocolCss.CSSStyleSheetHeader Header(ICssStyleSheet sheet, string id, string documentUrl)
    {
        var text = CssStyleSheetText.Of(sheet).Text;
        var lines = 0;
        var lastBreak = -1;
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] == '\n')
            {
                lines++;
                lastBreak = i;
            }
        }

        return new ProtocolCss.CSSStyleSheetHeader
        {
            StyleSheetId = id,
            FrameId = _target.FrameId,
            SourceURL = string.IsNullOrEmpty(sheet.Href) ? documentUrl : sheet.Href,
            Origin = ProtocolCss.StyleSheetOriginValues.Regular,
            Title = sheet.Title ?? "",
            OwnerNode = sheet.OwnerNode is { } owner ? _target.Nodes.BackendIdOf(owner) : null,
            Disabled = sheet.IsDisabled,
            IsInline = string.IsNullOrEmpty(sheet.Href),
            IsMutable = false,
            IsConstructed = false,
            StartLine = 0,
            StartColumn = 0,
            Length = text.Length,
            EndLine = lines,
            EndColumn = text.Length - lastBreak - 1,
        };
    }

    /// <summary>The document a window is about, or Chrome's own refusal when there is none yet.</summary>
    private IDocument Document()
        => PageRuntime.Find(_target.Runtime.Engine)?.Document
        ?? Throw.ServerError<IDocument>("Document is not available");
}
