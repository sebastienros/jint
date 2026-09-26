using Jint.HtmlParser.Css.Model.Syntax;
using Jint.HtmlParser.Css.Values;

namespace Jint.HtmlParser.Css.Model;

// A revision witness only. Root ordering, cascade origin and tree scope belong to the caller.
internal sealed class CssStyleSheetRevisionSnapshot
{
    private static readonly CssStyleSheetRevisionSnapshot Empty = new([]);
    private readonly Entry[] _entries;

    private readonly record struct Entry(CssStyleSheet Sheet, CssMutationStamp Stamp);

    private CssStyleSheetRevisionSnapshot(Entry[] entries) => _entries = entries;
    internal int Count => _entries.Length;

    internal static CssStyleSheetRevisionSnapshot Capture(CssStyleSheet root, CssValueWork work)
    {
        work.CheckCancellation();
        var roots = new[] { new Entry(root, root.Stamp) };
        return CaptureEntries(roots, work);
    }

    internal static CssStyleSheetRevisionSnapshot Capture(IReadOnlyList<CssStyleSheet> roots, CssValueWork work)
    {
        work.CheckCancellation();
        if (roots.Count == 0) return Complete(Empty, work);
        var entries = new Entry[roots.Count];
        for (var i = 0; i < entries.Length; i++)
        {
            var root = roots[i];
            entries[i] = new Entry(root, root.Stamp);
            work.Charge(1);
        }
        return CaptureEntries(entries, work);
    }

    private static CssStyleSheetRevisionSnapshot CaptureEntries(Entry[] roots, CssValueWork work)
    {
        // Common case: retain only the roots, without a traversal collection or identity set.
        // Valid imports form a prefix; a sheet with 10,000 style rules costs one rule inspection.
        foreach (var root in roots)
        {
            work.Charge(1);
            for (var i = 0; i < root.Sheet.Rules.Count; i++)
            {
                var rule = root.Sheet.Rules[i];
                work.Charge(1);
                if (rule is not CssImportRule import) break;
                if (import.StyleSheet is { } child)
                    return CaptureGraph(roots, new Entry(child, child.Stamp), work);
            }
        }
        return Complete(new CssStyleSheetRevisionSnapshot(roots), work);
    }

    private static CssStyleSheetRevisionSnapshot CaptureGraph(Entry[] roots, Entry firstChild, CssValueWork work)
    {
        var seen = new HashSet<CssStyleSheet>(ReferenceEqualityComparer.Instance);
        var entries = new List<Entry>();
        foreach (var root in roots)
        {
            if (seen.Add(root.Sheet)) entries.Add(root);
            work.Charge(1);
        }
        if (seen.Add(firstChild.Sheet)) entries.Add(firstChild);
        // The entry list doubles as an iterative queue. Snapshot a newly discovered sheet before
        // charging work or examining its edges, so a later callback cannot refresh its witness.
        for (var i = 0; i < entries.Count; i++)
        {
            var sheet = entries[i].Sheet;
            work.Charge(1);
            for (var ruleIndex = 0; ruleIndex < sheet.Rules.Count; ruleIndex++)
            {
                var rule = sheet.Rules[ruleIndex];
                work.Charge(1);
                if (rule is not CssImportRule import) break;
                if (import.StyleSheet is { } child && seen.Add(child))
                    entries.Add(new Entry(child, child.Stamp));
            }
        }
        work.Charge(entries.Count);
        var captured = new CssStyleSheetRevisionSnapshot(entries.ToArray());
        return Complete(captured, work);
    }

    private static CssStyleSheetRevisionSnapshot Complete(CssStyleSheetRevisionSnapshot snapshot, CssValueWork work)
    {
        if (!snapshot.IsCurrent(work))
            throw new InvalidOperationException("The stylesheet graph changed while capturing its revisions.");
        return snapshot;
    }

    internal bool IsCurrent(CssValueWork work)
    {
        // Every host callback precedes the first comparison. Verification then polls only the
        // token; calling host code between comparisons could invalidate an already checked sheet.
        work.Charge(_entries.Length);
        work.CheckCancellation();
        var current = true;
        for (var i = 0; i < _entries.Length; i++)
        {
            if ((i & 1023) == 0) work.Token.ThrowIfCancellationRequested();
            var entry = _entries[i];
            if (!entry.Stamp.CanReuse || entry.Sheet.Stamp != entry.Stamp)
            {
                current = false;
                break;
            }
        }
        work.Token.ThrowIfCancellationRequested();
        return current;
    }
}
