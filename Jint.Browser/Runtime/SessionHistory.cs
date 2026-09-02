using Jint.WebApi.StructuredClone;

namespace Jint.Browser.Runtime;

/// <summary>
/// One entry of a page's session history: where it was, what state a script attached, and which document it
/// belongs to.
/// </summary>
/// <remarks>
/// The state is a <see cref="SerializationRecord"/> rather than a <c>JsValue</c>, which is what lets it
/// survive the engine it was created on: a navigation builds a new engine, and a <c>history.state</c> read
/// afterwards has to answer a value belonging to <i>that</i> one. It is deserialized as a shared record, so
/// going back to the same entry twice answers two structurally equal objects rather than one aliased graph.
/// </remarks>
internal sealed class HistoryEntry
{
    internal HistoryEntry(string url, SerializationRecord? state, long documentId)
    {
        Url = url;
        State = state;
        DocumentId = documentId;
    }

    /// <summary>The URL this entry restores.</summary>
    internal string Url { get; set; }

    /// <summary>The serialized <c>history.state</c>, or <see langword="null"/> for none.</summary>
    internal SerializationRecord? State { get; set; }

    /// <summary>
    /// Which loaded document this entry belongs to: entries sharing one are reachable without a fetch.
    /// </summary>
    /// <remarks>
    /// It is what makes <c>pushState</c> and a fragment navigation same-document traversals — the shape every
    /// client-side router is built out of — while a traversal across the boundary re-fetches. A browser would
    /// restore the old document from its back/forward cache instead; there is no such cache here, so going
    /// back to a previous document loads it again and its scripts run again. That is a real divergence and
    /// the one a page notices: a form's typed-in values are gone, exactly as they are in a browser whose
    /// bfcache declined the page.
    /// </remarks>
    internal long DocumentId { get; set; }
}

/// <summary>
/// A page's session history: the entries it can travel to, and where in them it currently is.
/// </summary>
/// <remarks>
/// <b>Touched on the page loop thread only.</b> Every member is reached either from a <c>history</c> member
/// a script called or from the navigator, both of which run there.
/// </remarks>
internal sealed class SessionHistory
{
    private readonly List<HistoryEntry> _entries = [];
    private long _nextDocumentId;
    private int _index = -1;

    /// <summary>How many entries a script can see — <c>history.length</c>.</summary>
    internal int Length => _entries.Count;

    /// <summary>Where the page currently is, or <c>-1</c> before the first document.</summary>
    internal int Index => _index;

    /// <summary>The entry the page is showing, or <see langword="null"/> before the first document.</summary>
    internal HistoryEntry? Current => _index >= 0 && _index < _entries.Count ? _entries[_index] : null;

    /// <summary>The identifier the document currently loaded was given.</summary>
    internal long CurrentDocumentId => Current?.DocumentId ?? -1;

    /// <summary>Takes the next document identifier; one per document actually loaded.</summary>
    internal long NextDocumentId() => _nextDocumentId++;

    /// <summary>
    /// Adds an entry for a document that has just loaded, dropping everything ahead of the current one.
    /// </summary>
    /// <remarks>
    /// https://html.spec.whatwg.org/multipage/browsing-the-web.html#url-and-history-update-steps: a push
    /// truncates the forward list, which is why a page that goes back and then follows a link cannot go
    /// forward to what it left.
    /// </remarks>
    internal void Push(string url, long documentId)
    {
        Truncate();
        _entries.Add(new HistoryEntry(url, state: null, documentId));
        _index = _entries.Count - 1;
    }

    /// <summary>Replaces the current entry, which is what <c>location.replace</c> and a redirect do.</summary>
    internal void Replace(string url, long documentId)
    {
        if (Current is null)
        {
            Push(url, documentId);
            return;
        }

        _entries[_index] = new HistoryEntry(url, state: null, documentId);
    }

    /// <summary>
    /// <c>history.pushState</c>: a new entry for the document already loaded, so travelling to it needs no
    /// fetch.
    /// </summary>
    internal void PushState(string url, SerializationRecord? state)
    {
        var documentId = CurrentDocumentId;
        Truncate();
        _entries.Add(new HistoryEntry(url, state, documentId));
        _index = _entries.Count - 1;
    }

    /// <summary><c>history.replaceState</c>: the current entry's URL and state, and nothing else.</summary>
    internal void ReplaceState(string url, SerializationRecord? state)
    {
        if (Current is not { } current)
        {
            PushState(url, state);
            return;
        }

        current.Url = url;
        current.State = state;
    }

    /// <summary>
    /// The entry <paramref name="delta"/> steps away, or <see langword="null"/> when there is none — which
    /// is what makes <c>history.back()</c> on the first entry do nothing at all rather than fail.
    /// </summary>
    internal HistoryEntry? Peek(int delta, out int targetIndex)
    {
        targetIndex = _index + delta;
        return targetIndex >= 0 && targetIndex < _entries.Count ? _entries[targetIndex] : null;
    }

    /// <summary>The entry at <paramref name="index"/>, or <see langword="null"/> when there is none.</summary>
    internal HistoryEntry? At(int index) => index >= 0 && index < _entries.Count ? _entries[index] : null;

    /// <summary>Moves to <paramref name="index"/>, which a caller has already checked.</summary>
    internal void MoveTo(int index) => _index = index;

    /// <summary>
    /// Points every entry of one document at another — what a cross-document traversal does once the
    /// document has been loaded again.
    /// </summary>
    /// <remarks>
    /// It is the cluster and not only the entry, because a router's <c>pushState</c> entries all belong to
    /// one document: rebinding only the one travelled to would make every step among its siblings a reload.
    /// </remarks>
    internal void Rebind(long from, long to)
    {
        foreach (var entry in _entries)
        {
            if (entry.DocumentId == from)
            {
                entry.DocumentId = to;
            }
        }
    }

    /// <summary>Points the current entry at a new URL without adding one — a fragment navigation's own move.</summary>
    internal void UpdateCurrentUrl(string url)
    {
        if (Current is { } current)
        {
            current.Url = url;
        }
    }

    private void Truncate()
    {
        if (_index >= 0 && _index + 1 < _entries.Count)
        {
            _entries.RemoveRange(_index + 1, _entries.Count - _index - 1);
        }
    }
}
