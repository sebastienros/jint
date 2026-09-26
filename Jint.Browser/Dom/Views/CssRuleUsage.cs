using System.Runtime.CompilerServices;
using Jint.Browser.Styling;
using Jint.HtmlParser;
using Jint.HtmlParser.Css.Model;
using Jint.HtmlParser.Css.Model.Syntax;
using Jint.HtmlParser.Css.Values;

namespace Jint.Browser.Dom.Views;

// Coverage records the same native rules the cascade matched, independently of winning declarations.
internal static class CssRuleUsage
{
    private static readonly object Gate = new();
    private static CssRuleUsageTracker[] _tracking = [];
    internal static bool IsTracking => Volatile.Read(ref _tracking).Length != 0;
    internal static bool IsTrackingDocument(Document? document)
    {
        foreach (var tracker in Volatile.Read(ref _tracking))
            if (ReferenceEquals(tracker.Document, document)) return true;
        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void Observe(Element element, IReadOnlyList<CssStyleRule> matches)
    {
        var tracking = Volatile.Read(ref _tracking);
        if (tracking.Length != 0) Record(tracking, element, matches);
    }
    internal static void Arm(CssRuleUsageTracker tracker)
    {
        lock (Gate) _tracking = [.. _tracking, tracker];
    }
    internal static void Disarm(CssRuleUsageTracker tracker)
    {
        lock (Gate) _tracking = [.. _tracking.Where(candidate => !ReferenceEquals(candidate, tracker))];
    }
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void Record(CssRuleUsageTracker[] tracking, Element element, IReadOnlyList<CssStyleRule> matches)
    {
        foreach (var tracker in tracking) tracker.Observe(element, matches);
    }
}

internal sealed class CssRuleUsageTracker
{
    private readonly HashSet<CssStyleRule> _used = new(ReferenceEqualityComparer.Instance);
    private readonly List<CssStyleRule> _pending = [];
    private readonly HashSet<CssStyleSheet> _reportable = new(ReferenceEqualityComparer.Instance);
    private Document? _document;
    private ulong _documentStamp;
    private CssMutationStamp _resourceStamp;
    private bool _refreshed;
    internal Document? Document => _document;

    internal void Rebind(Document? document)
    {
        _document = document;
        _reportable.Clear();
        _refreshed = false;
    }

    internal void Sweep()
    {
        if (_document is not { } document || CssCascade.Traversal.For(document) is not { } traversal) return;
        var realm = NativeCssStyleSheets.RealmOf(document)!;
        var work = new CssValueWork(realm.CancellationToken, realm.Engine.Constraints.Check);
        var pending = new Stack<Node>();
        pending.Push(document);
        while (pending.TryPop(out var node))
        {
            work.Charge(1);
            if (node is Element element) Observe(element, traversal.Of(element).MatchedRules());
            for (var child = node.LastChild; child is not null; child = child.PreviousSibling)
            {
                work.Charge(1);
                pending.Push(child);
            }
        }
        work.CheckCancellation();
    }

    internal void Observe(Element element, IReadOnlyList<CssStyleRule> matches)
    {
        if (_document is null || !ReferenceEquals(element.OwnerDocument, _document)) return;
        var realm = NativeCssStyleSheets.RealmOf(_document);
        if (realm is null) return;
        var work = new CssValueWork(realm.CancellationToken, realm.Engine.Constraints.Check);
        var stamp = NativeCssStyleSheets.Stamp(_document);
        if (!_refreshed || !_resourceStamp.CanReuse || _resourceStamp != stamp || _documentStamp != _document.MutationStamp)
        {
            _reportable.Clear();
            foreach (var sheet in NativeCssStyleSheets.Get(_document, work))
            {
                work.Charge(1);
                if (sheet.Origin == NativeCssOrigin.Author) _reportable.Add(sheet.Sheet);
            }
            _resourceStamp = NativeCssStyleSheets.Stamp(_document);
            _documentStamp = _document.MutationStamp;
            _refreshed = true;
        }
        foreach (var rule in matches)
        {
            work.Charge(1);
            if (rule.ParentStyleSheet is { } owner && _reportable.Contains(owner) && _used.Add(rule))
                _pending.Add(rule);
        }
        work.CheckCancellation();
    }

    internal CssStyleRule[] TakeDelta()
    {
        var result = _pending.ToArray();
        _pending.Clear();
        return result;
    }
}
