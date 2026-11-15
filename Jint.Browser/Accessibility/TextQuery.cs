using System.Text;
using Jint.Browser.Dom;
using Jint.HtmlParser;

namespace Jint.Browser.Accessibility;

/// <summary>
/// A text a <c>text=</c> or <c>label=</c> target asks for, and the two engine-free searches that answer it.
/// </summary>
/// <remarks>
/// <para>
/// <b>The text is the document's, not a rendering's.</b> An element's text is its descendant text in tree
/// order, with <c>script</c>, <c>noscript</c>, <c>style</c>, <c>template</c> and the document's
/// <c>head</c> contributing nothing, and an <c>&lt;input type=button|submit|reset&gt;</c> standing for its
/// <c>value</c> — Playwright's <c>elementText</c>. Both sides are whitespace-flattened before they are
/// compared. Nothing here filters by visibility: a hidden element's text is still text, and the input
/// operations a target feeds refuse an element with no box on their own.
/// </para>
/// <para>
/// <b>A text target names the deepest element</b> whose text matches: an element matches only when none of
/// its child elements matches on its own, so <c>text=Save</c> is the button and not the form around it.
/// </para>
/// <para>
/// Both searches call the caller's checkpoint once per element, which is how a page's turn budget and
/// cancellation reach a walk that runs no script.
/// </para>
/// </remarks>
internal sealed class TextQuery
{
    private char[] _scratch = new char[64];

    private TextQuery(string text, bool exact)
    {
        Text = text;
        Exact = exact;
    }

    /// <summary>The flattened text to look for.</summary>
    internal string Text { get; }

    /// <summary>
    /// Whether the whole flattened text must equal <see cref="Text"/>, case-sensitively; otherwise
    /// <see cref="Text"/> is a case-insensitive substring of it.
    /// </summary>
    internal bool Exact { get; }

    /// <summary>A query for <paramref name="text"/>, or <see langword="null"/> when it flattens to nothing.</summary>
    internal static TextQuery? Create(string text, bool exact)
    {
        var flattened = AccessibleName.Flatten(text);
        return flattened.Length == 0 ? null : new TextQuery(flattened, exact);
    }

    /// <summary>Whether <paramref name="candidate"/>, once flattened, satisfies this query.</summary>
    internal bool Matches(ReadOnlySpan<char> candidate)
    {
        // Flattened into a reused buffer: a text walk asks this of every element's whole subtree text.
        if (_scratch.Length < candidate.Length)
        {
            _scratch = new char[Math.Max(candidate.Length, _scratch.Length * 2)];
        }

        var length = 0;
        var pendingSpace = false;
        foreach (var c in candidate)
        {
            if (char.IsWhiteSpace(c))
            {
                pendingSpace = length > 0;
                continue;
            }

            if (pendingSpace)
            {
                _scratch[length++] = ' ';
                pendingSpace = false;
            }

            _scratch[length++] = c;
        }

        var flattened = _scratch.AsSpan(0, length);
        return Exact
            ? flattened.SequenceEqual(Text.AsSpan())
            : flattened.Contains(Text.AsSpan(), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Appends, in document order, the deepest elements under <paramref name="scope"/> whose text matches.
    /// </summary>
    /// <param name="scope">The document or element to search under; an element contributes its descendants.</param>
    /// <param name="output">Where the matches are appended.</param>
    /// <param name="first">Whether the walk stops at the first match.</param>
    /// <param name="checkpoint">Called once per element visited.</param>
    internal void ByText(Node scope, List<Element> output, bool first, Action? checkpoint)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(output);

        var walk = new TextWalk(this, output, first, checkpoint);
        foreach (var child in scope.ChildNodes)
        {
            if (child is Element element && walk.Visit(element).Stop)
            {
                return;
            }
        }
    }

    /// <summary>
    /// Appends, in document order, every element under one of <paramref name="scopes"/> one of whose labels
    /// matches.
    /// </summary>
    /// <remarks>
    /// <para>
    /// An element's labels are the elements its <c>aria-labelledby</c> names, else its <c>aria-label</c>,
    /// else the <c>&lt;label&gt;</c> elements HTML associates with it — Playwright's <c>getElementLabels</c>,
    /// and the sources accname reads before content for the same element.
    /// </para>
    /// <para>
    /// <b>The labels are read once.</b> Every <c>&lt;label&gt;</c> of the document is matched against the
    /// text first, and only a matching one asks <see cref="HtmlLabelAssociation.ControlFor"/> which control it
    /// labels, so a form with many controls costs one pass rather than one per control. A label may sit
    /// outside the scope its control is in.
    /// </para>
    /// </remarks>
    internal void ByLabel(Document document, IReadOnlyList<Node> scopes, List<Element> output, bool first,
        Action? checkpoint, Action<int>? readCheckpoint, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(scopes);
        ArgumentNullException.ThrowIfNull(output);

        HashSet<Element>? labelled = null;
        foreach (var label in ContentDom.Descendants(document))
        {
            checkpoint?.Invoke();
            if (label is { NamespaceUri: Namespaces.Html, LocalName: "label" }
                && Matches(ElementText(label))
                && HtmlLabelAssociation.ControlFor(label, readCheckpoint, token) is { } control)
            {
                (labelled ??= []).Add(control);
            }
        }

        foreach (var scope in scopes)
        {
            foreach (var element in ContentDom.Descendants(scope))
            {
                checkpoint?.Invoke();
                if (Labelled(element, labelled))
                {
                    output.Add(element);
                    if (first)
                    {
                        return;
                    }
                }
            }
        }
    }

    /// <summary>The text an element contributes to a text match, the way <see cref="ByText"/> reads it.</summary>
    internal static string ElementText(Element element)
    {
        var builder = new StringBuilder();
        Append(element, builder);
        return builder.ToString();

        static void Append(Element element, StringBuilder builder)
        {
            if (IsSkipped(element))
            {
                return;
            }

            if (IsButtonInput(element))
            {
                builder.Append(element.GetAttribute("value"));
                return;
            }

            foreach (var child in element.ChildNodes)
            {
                switch (child)
                {
                    case Text text:
                        builder.Append(text.Data);
                        break;
                    case CDataSection cdata:
                        builder.Append(cdata.Data);
                        break;
                    case Element childElement:
                        Append(childElement, builder);
                        break;
                }
            }
        }
    }

    private bool Labelled(Element element, HashSet<Element>? labelled)
    {
        var labelledBy = AccessibleName.References(element, "aria-labelledby");
        if (labelledBy.Count > 0)
        {
            foreach (var label in labelledBy)
            {
                if (Matches(label is Element labelElement ? ElementText(labelElement) : ContentDom.TextContent(label)))
                {
                    return true;
                }
            }

            return false;
        }

        var ariaLabel = element.GetAttribute("aria-label");
        if (!string.IsNullOrWhiteSpace(ariaLabel))
        {
            return Matches(ariaLabel);
        }

        return labelled?.Contains(element) == true;
    }

    private static bool IsSkipped(Element element) =>
        element.NamespaceUri == Namespaces.Html
        && element.LocalName is "script" or "noscript" or "style" or "template" or "head";

    private static bool IsButtonInput(Element element) =>
        element is { NamespaceUri: Namespaces.Html, LocalName: "input" }
        && ContentDom.InputType(element) is "button" or "submit" or "reset";

    /// <summary>
    /// One bottom-up text walk. Every element's text is a range of a single buffer the walk appends to in
    /// tree order, so a deep tree's text is written once rather than once per level.
    /// </summary>
    private sealed class TextWalk(TextQuery query, List<Element> output, bool first, Action? checkpoint)
    {
        private char[] _buffer = new char[256];
        private int _length;

        /// <summary>Visits one element, answering whether it matched and whether the walk is over.</summary>
        internal (bool Matched, bool Stop) Visit(Element element)
        {
            checkpoint?.Invoke();
            if (IsSkipped(element))
            {
                return (false, false);
            }

            // A match is inserted where the element starts, so a parent decided after its subtree still lands
            // in document order ahead of anything its subtree contributed.
            var position = output.Count;
            var start = _length;
            var childMatched = false;
            if (IsButtonInput(element))
            {
                Append(element.GetAttribute("value"));
            }
            else
            {
                foreach (var child in element.ChildNodes)
                {
                    switch (child)
                    {
                        case Text text:
                            Append(text.Data);
                            break;
                        case CDataSection cdata:
                            Append(cdata.Data);
                            break;
                        case Element childElement:
                            var (matched, stop) = Visit(childElement);
                            if (stop)
                            {
                                return (true, true);
                            }

                            childMatched |= matched;
                            break;
                    }
                }
            }

            // A substring a child's text already contains is in this element's text too.
            var matches = (childMatched && !query.Exact) || query.Matches(_buffer.AsSpan(start, _length - start));
            if (matches && !childMatched)
            {
                output.Insert(position, element);

                // No element before this one in document order can still match on its own: only an ancestor
                // is decided later, and an ancestor holds this match as a child. So the first match found is
                // the first in document order.
                if (first)
                {
                    return (true, true);
                }
            }

            return (matches, false);
        }

        private void Append(string? text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            if (_buffer.Length - _length < text.Length)
            {
                Array.Resize(ref _buffer, Math.Max(_buffer.Length * 2, _length + text.Length));
            }

            text.CopyTo(0, _buffer, _length, text.Length);
            _length += text.Length;
        }
    }
}
