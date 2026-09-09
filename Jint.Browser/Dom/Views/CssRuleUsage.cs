using System.Runtime.CompilerServices;
using AngleSharp;
using AngleSharp.Css;
using AngleSharp.Css.Dom;
using AngleSharp.Dom;

namespace Jint.Browser.Dom.Views;

/// <summary>
/// Which style rules matched something while a client was watching: the arming switch every cascade
/// computation reads, and nothing else.
/// </summary>
/// <remarks>
/// <para>
/// <b>A page nobody is tracking pays one static read.</b> <see cref="Observe"/> is on the hot path of
/// <see cref="CssCascade.Of"/> and of every step of <see cref="CssCascade.Traversal"/>, which between them
/// answer <c>getComputedStyle</c>, every box the flat model produces and the accessibility tree's hidden
/// verdict. With nothing armed it is a volatile array read and a length test — no allocation, no dictionary,
/// no per-page field to find — and the recording itself is behind a non-inlined call so that the guard is
/// the only thing a caller's code carries. That is the same shape <c>Runtime/PageNetworkRecorder</c> uses
/// for a page with no client attached.
/// </para>
/// <para>
/// <b>The list is process-wide and the tracker filters by document, rather than the other way round.</b> An
/// <see cref="IElement"/> knows its document and nothing above it, and a page's browsing context is rebuilt
/// for every document (<c>Runtime/Parsing/ParserDriver</c>), so there is no per-page key a static seam could
/// look a tracker up by. Two pages tracked at once therefore cost each other one reference comparison per
/// cascade, which is what <see cref="CssRuleUsageTracker.Observe"/> starts with.
/// </para>
/// <para>
/// <b>Nothing here may throw into a cascade.</b> Every caller is running a page's own script or answering a
/// protocol command; an escaping CLR exception would turn a client's coverage request into a broken
/// <c>getComputedStyle</c>. The recording is guarded exactly as <see cref="CssCascade"/> guards the compute.
/// </para>
/// </remarks>
internal static class CssRuleUsage
{
    private static readonly object Gate = new();

    private static CssRuleUsageTracker[] _tracking = [];

    /// <summary>Whether any client is recording rule usage anywhere in this process.</summary>
    internal static bool IsTracking => Volatile.Read(ref _tracking).Length != 0;

    /// <summary>Records the rules that match <paramref name="element"/>, for whoever is tracking.</summary>
    /// <param name="element">The element a cascade is being computed for.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void Observe(IElement element)
    {
        var tracking = Volatile.Read(ref _tracking);
        if (tracking.Length != 0)
        {
            Record(tracking, element);
        }
    }

    /// <summary>Starts delivering to <paramref name="tracker"/>.</summary>
    internal static void Arm(CssRuleUsageTracker tracker)
    {
        lock (Gate)
        {
            _tracking = [.. _tracking, tracker];
        }
    }

    /// <summary>Stops delivering to <paramref name="tracker"/>, which is what stopping tracking does.</summary>
    internal static void Disarm(CssRuleUsageTracker tracker)
    {
        lock (Gate)
        {
            _tracking = [.. _tracking.Where(candidate => !ReferenceEquals(candidate, tracker))];
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void Record(CssRuleUsageTracker[] tracking, IElement element)
    {
        foreach (var tracker in tracking)
        {
            tracker.Observe(element);
        }
    }
}

/// <summary>
/// One client's recording window: the author rules that have matched an element since it opened, and the
/// ones it has not been told about yet.
/// </summary>
/// <remarks>
/// <para>
/// <b>The candidate list is AngleSharp's own, and that is the whole point.</b>
/// <c>IWindow.GetStyleCollection(device)</c> is the flattened, ordered, condition-filtered rule list the
/// cascade itself matches against, so a rule inside an <c>@media</c> or <c>@supports</c> whose condition
/// holds is enumerated individually — exactly what a client expects a nested rule's coverage to mean — and
/// one inside a group that does not hold is not enumerated at all, so it can never be reported used. The
/// predicate is <c>ICssStyleRule.TryMatch</c>, the same public member AngleSharp.Css matches with.
/// </para>
/// <para>
/// <b>Why matching rather than reading the cascade's answer.</b> AngleSharp.Css's computed-style pipeline —
/// <c>IElement.ComputeCurrentStyle()</c>, <c>IStyleCollection.ComputeCascadedStyle</c> and their siblings —
/// answers an <c>ICssStyleDeclaration</c>, and neither it nor the <c>ICssProperty</c> objects in it carry
/// the rule a declaration came from. There is no public seam anywhere in the assembly that reports which
/// rules matched an element, so rule identity cannot be recovered from a cascade that has already run: the
/// matching has to be done again. It is done over the *unused* candidates only, so the work shrinks towards
/// nothing as the window goes on, and it computes no values at all — no device conversion, no custom
/// property resolution, no inheritance — which is the expensive half of a cascade.
/// </para>
/// <para>
/// <b>Used means used once.</b> A rule that matched in one query and not in the next is used; the recorded
/// set is never revisited, which is why a matched rule leaves the candidate list for good.
/// </para>
/// <para>
/// Everything here runs on the page loop, because every caller does.
/// </para>
/// </remarks>
internal sealed class CssRuleUsageTracker
{
    private readonly HashSet<ICssStyleRule> _used = new(ReferenceEqualityComparer.Instance);
    private readonly List<ICssStyleRule> _pending = [];
    private readonly List<ICssStyleRule> _candidates = [];
    private readonly HashSet<ICssStyleSheet> _reportable = new(ReferenceEqualityComparer.Instance);

    private readonly Predicate<ICssStyleRule> _recorded;

    private IDocument? _document;
    private IStyleSheet[] _sheets = [];
    private int _ruleCount = -1;

    internal CssRuleUsageTracker()
    {
        _recorded = _used.Contains;
    }

    /// <summary>The document whose cascades this window is about, or none until it is bound.</summary>
    internal IDocument? Document => _document;

    /// <summary>Points the window at <paramref name="document"/>, forgetting the candidates of the last.</summary>
    /// <remarks>
    /// The recorded set is deliberately kept: Chrome's coverage is the agent's rather than the document's,
    /// and a client that navigates in the middle of a window is told about both documents' rules when it
    /// takes its next delta. What is dropped is the candidate list, because its rules belong to sheets that
    /// are gone.
    /// </remarks>
    internal void Rebind(IDocument? document)
    {
        _document = document;
        _candidates.Clear();
        _reportable.Clear();
        _sheets = [];
        _ruleCount = -1;
    }

    /// <summary>
    /// Records every rule that matches something in the bound document right now.
    /// </summary>
    /// <remarks>
    /// This is Blink's forced recalculation, in the one form this engine can give it. Chrome's
    /// <c>startRuleUsageTracking</c> does not merely arm a counter: it marks every element of every document
    /// for style recalculation and runs it, so that coverage describes the page as it stands rather than
    /// only what happens to be restyled afterwards. Nothing renders here, so a document that is never
    /// queried computes no cascade at all and a window opened over it would be empty; this walks the
    /// document once instead, which costs selector matching and no value computation.
    /// </remarks>
    internal void Sweep()
    {
        if (_document is not { } document)
        {
            return;
        }

        foreach (var element in document.All)
        {
            Observe(element);
        }
    }

    /// <summary>Records the rules matching <paramref name="element"/>, if it belongs to this window.</summary>
    internal void Observe(IElement element)
    {
        if (_document is null || !ReferenceEquals(element.Owner, _document))
        {
            return;
        }

        try
        {
            Refresh();

            var matched = false;
            foreach (var rule in _candidates)
            {
                if (rule.TryMatch(element, null, out _))
                {
                    _used.Add(rule);
                    _pending.Add(rule);
                    matched = true;
                }
            }

            if (matched)
            {
                _candidates.RemoveAll(_recorded);
            }
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or NullReferenceException)
        {
            // The same three AngleSharp.Css raises out of a cascade it cannot compute, and for the same
            // reason they are swallowed there: a client's coverage request may not break the page's own
            // getComputedStyle. A window that cannot match reports less, and reports nothing wrong.
        }
    }

    /// <summary>The rules recorded since the last take, oldest first, emptying the pending list.</summary>
    internal ICssStyleRule[] TakeDelta()
    {
        var delta = _pending.ToArray();
        _pending.Clear();
        return delta;
    }

    /// <summary>Rebuilds the candidate list when the document's sheets have moved under it.</summary>
    /// <remarks>
    /// The check is the sheet list's identity plus the number of top-level rules in it, which catches a
    /// sheet appearing or disappearing and a rule inserted at the top level of one. A rule inserted into an
    /// <c>@media</c> block of a sheet that was already there is not caught until something else moves; there
    /// is no CSSOM mutation signal in AngleSharp to hang a precise invalidation on, and re-flattening every
    /// sheet for every element would make each cascade quadratic in the page's rules.
    /// </remarks>
    private void Refresh()
    {
        var sheets = _document!.StyleSheets;
        var count = 0;
        for (var i = 0; i < sheets.Length; i++)
        {
            count += (sheets[i] as ICssStyleSheet)?.Rules.Length ?? 0;
        }

        if (count == _ruleCount && Same(sheets))
        {
            return;
        }

        _sheets = [.. sheets];
        _ruleCount = count;
        _candidates.Clear();
        _reportable.Clear();

        foreach (var sheet in _sheets)
        {
            if (sheet is ICssStyleSheet css)
            {
                Reportable(css);
            }
        }

        if (Collection() is not { } styles)
        {
            return;
        }

        foreach (var rule in styles)
        {
            if (!_used.Contains(rule) && rule.Owner is { } owner && _reportable.Contains(owner))
            {
                _candidates.Add(rule);
            }
        }
    }

    /// <summary>The sheets a rule may be attributed to: the document's, and everything they import.</summary>
    /// <remarks>
    /// The user-agent sheet is excluded by construction — it is not among the document's — which is what the
    /// protocol asks for: a <c>RuleUsage</c>'s style sheet identifier is "absent for user agent stylesheet
    /// and user-specified stylesheet rules", and there is no identifier to give one.
    /// </remarks>
    private void Reportable(ICssStyleSheet sheet)
    {
        if (!_reportable.Add(sheet))
        {
            return;
        }

        for (var i = 0; i < sheet.Rules.Length; i++)
        {
            if (sheet.Rules[i] is ICssImportRule { Sheet: { } imported })
            {
                Reportable(imported);
            }
        }
    }

    private IStyleCollection? Collection()
    {
        if (_document?.DefaultView is not { } window)
        {
            return null;
        }

        var device = _document.Context.GetService<IRenderDevice>() ?? new DefaultRenderDevice();
        return window.GetStyleCollection(device);
    }

    private bool Same(IStyleSheetList sheets)
    {
        if (sheets.Length != _sheets.Length)
        {
            return false;
        }

        for (var i = 0; i < _sheets.Length; i++)
        {
            if (!ReferenceEquals(sheets[i], _sheets[i]))
            {
                return false;
            }
        }

        return true;
    }
}
