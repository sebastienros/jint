using System.Globalization;
using AngleSharp.Css.Dom;
using AngleSharp.Dom;
using Jint.Browser.Runtime;

namespace Jint.Browser.DevTools;

/// <summary>
/// The identifiers the <c>CSS</c> domain addresses a page's style sheets by, and the commit every
/// attachment hears about.
/// </summary>
/// <remarks>
/// <para>
/// <b>A <c>styleSheetId</c> is a document's, and one table serves every attachment.</b> That is
/// <see cref="DomNodeTracker"/>'s decision for a <c>nodeId</c> and the same one holds here: two clients
/// attached to one page must be told the same identifier for the same sheet, and a sheet dies with the
/// document that owns it, so both maps are emptied on every commit. The counter is process-wide, so an
/// identifier from the document before last resolves to nothing rather than to a sheet of the one that
/// replaced it.
/// </para>
/// <para>
/// <b>Sheets are announced when the domain is asked, not watched.</b> AngleSharp raises no notification
/// when a sheet is added to or removed from a document — <c>IStyleSheetList</c> is a live view over the
/// tree with no event on it — so there is nothing to subscribe to. <see cref="CssDomain"/> reconciles the
/// document's sheets against this table at the four moments a client can tell the difference: when it
/// enables the domain, when a document commits, when it reads a sheet's text, and before it is handed
/// coverage. <c>styleSheetRemoved</c> and <c>styleSheetChanged</c> are absent for the same reason and are
/// stated in <c>DevTools/AGENTS.md</c>.
/// </para>
/// <para>
/// Everything here runs on the page loop.
/// </para>
/// </remarks>
internal sealed class CssStyleSheetTracker
{
    private static int _serial;

    private readonly Dictionary<ICssStyleSheet, string> _ids = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<string, ICssStyleSheet> _byId = new(StringComparer.Ordinal);

    private readonly object _domainGate = new();
    private CssDomain[] _domains = [];

    /// <summary>Registers one attachment's domain, so it hears about a document's sheets.</summary>
    internal void Add(CssDomain domain)
    {
        lock (_domainGate)
        {
            _domains = [.. _domains, domain];
        }
    }

    /// <summary>Stops telling one attachment's domain anything, which detaching does.</summary>
    internal void Remove(CssDomain domain)
    {
        lock (_domainGate)
        {
            _domains = [.. _domains.Where(candidate => !ReferenceEquals(candidate, domain))];
        }
    }

    /// <summary>Throws away a document's identifiers, which a navigation does.</summary>
    internal void DocumentReplaced()
    {
        _ids.Clear();
        _byId.Clear();
    }

    /// <summary>Tells every attachment that a document is parsed and its sheets can be read.</summary>
    internal void DocumentCommitted(PageRuntime runtime)
    {
        foreach (var domain in Volatile.Read(ref _domains))
        {
            domain.DocumentCommitted(runtime);
        }
    }

    /// <summary>The identifier <paramref name="sheet"/> is addressed by in this document, minting one.</summary>
    internal string IdOf(ICssStyleSheet sheet)
    {
        if (_ids.TryGetValue(sheet, out var existing))
        {
            return existing;
        }

        var id = Interlocked.Increment(ref _serial).ToString(CultureInfo.InvariantCulture);
        _ids[sheet] = id;
        _byId[id] = sheet;
        return id;
    }

    /// <summary>Whether <paramref name="sheet"/> has been given an identifier already.</summary>
    internal bool Knows(ICssStyleSheet sheet) => _ids.ContainsKey(sheet);

    /// <summary>The sheet an identifier names, or none.</summary>
    internal ICssStyleSheet? ById(string id) => _byId.GetValueOrDefault(id);

    /// <summary>The identifier <paramref name="sheet"/> already has, or none.</summary>
    internal string? KnownIdOf(ICssStyleSheet sheet) => _ids.GetValueOrDefault(sheet);

    /// <summary>
    /// Every sheet of <paramref name="document"/> a rule can be attributed to, outermost first.
    /// </summary>
    /// <remarks>
    /// The document's own sheets and everything they <c>@import</c>, which is what CSSOM calls the
    /// document's style sheet set plus the sheets hanging off it. The user-agent sheet is not among them and
    /// deliberately gets no identifier: the protocol says a <c>RuleUsage</c>'s style sheet identifier is
    /// absent for user-agent rules, and a client that was handed one would ask for text this cannot give.
    /// </remarks>
    internal static List<ICssStyleSheet> SheetsOf(IDocument document)
    {
        var sheets = new List<ICssStyleSheet>();
        var seen = new HashSet<ICssStyleSheet>(ReferenceEqualityComparer.Instance);

        for (var i = 0; i < document.StyleSheets.Length; i++)
        {
            if (document.StyleSheets[i] is ICssStyleSheet sheet)
            {
                Collect(sheet, sheets, seen);
            }
        }

        return sheets;
    }

    private static void Collect(ICssStyleSheet sheet, List<ICssStyleSheet> sheets, HashSet<ICssStyleSheet> seen)
    {
        if (!seen.Add(sheet))
        {
            return;
        }

        sheets.Add(sheet);

        for (var i = 0; i < sheet.Rules.Length; i++)
        {
            if (sheet.Rules[i] is ICssImportRule { Sheet: { } imported })
            {
                Collect(imported, sheets, seen);
            }
        }
    }
}
