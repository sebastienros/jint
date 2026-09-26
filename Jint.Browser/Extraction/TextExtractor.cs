using System.Text;
using Jint.HtmlParser;
using Jint.Browser.Dom;
using Jint.Browser.Accessibility;

namespace Jint.Browser.Extraction;

/// <summary>
/// HTML's <c>innerText</c> getter, as far as it can be computed without laying the page out.
/// </summary>
/// <remarks>
/// <para>
/// The algorithm is
/// <see href="https://html.spec.whatwg.org/multipage/dom.html#the-innertext-idl-attribute">HTML's rendered
/// text collection steps</see>: text with CSS white-space processing applied, a literal line feed for
/// <c>&lt;br&gt;</c>, tabs between table cells and line feeds between table rows, two required line breaks
/// around a <c>&lt;p&gt;</c> and one around any other block-level box, then the runs of required breaks
/// collapsed to their maximum and trimmed off the ends.
/// </para>
/// <para>
/// Three of its inputs are layout, and this is what replaces them: "being rendered" becomes the hidden
/// verdict <see cref="ElementVisibility"/> computes, "block-level" becomes HTML's suggested rendering
/// (<see cref="HtmlDisplay"/>) rather than a used display, and line wrapping does not happen at all — a
/// paragraph is one line however wide it would have been. So this is the text of the document, not the text
/// of a rendering of it.
/// </para>
/// </remarks>
internal static class TextExtractor
{
    internal static string InnerText(Element element, bool useComputedStyle = true)
        => InnerText(element, useComputedStyle, null, default);

    internal static string InnerText(Element element, bool useComputedStyle, Action<int>? checkpoint, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(element);
        var work = new DomReadWork(checkpoint, token);
        work.Check();
        var visibility = new ElementVisibility(useComputedStyle, work);
        var collector = new Collector(visibility, visibility.CreateTraversal(element.OwnerDocument), work);
        collector.Collect(element);
        return collector.Assemble();
    }

    internal static string InnerText(Document document, bool useComputedStyle = true)
    {
        ArgumentNullException.ThrowIfNull(document);
        var root = DomDocumentElements.Body(document) ?? document.DocumentElement;
        return root is null ? string.Empty : InnerText(root, useComputedStyle);
    }

    private sealed class Collector(ElementVisibility visibility, Dom.Views.CssCascade.Traversal? traversal, DomReadWork work)
    {
        private readonly StringBuilder _builder = new();
        private int _pendingBreaks;
        private bool _pendingSpace;
        private bool _started;

        internal void Collect(Element root)
        {
            // A deferred sibling frame keeps storage proportional to depth, rather than subtree width.
            var pending = new Stack<Frame>();
            pending.Push(new Frame(root, WhiteSpaceMode.Collapse, false, false, null, 0, true, false));
            while (pending.TryPop(out var frame))
            {
                work.Step();
                if (frame.Exit)
                {
                    var element = (Element) frame.Node;
                    if (frame.Display == "table-cell" && NextElement(element) is not null) Separator('\t');
                    else if (frame.Display == "table-row" && !IsLastRow(element)) Separator('\n');
                    Break(frame.Breaks);
                    continue;
                }
                if (frame.Siblings && frame.Node.NextSibling is { } sibling)
                    pending.Push(new Frame(sibling, frame.Mode, false, true, null, 0, false, frame.Hidden));
                switch (frame.Node)
                {
                    case Text text:
                        if (!frame.Hidden) AddText(text, frame.Mode);
                        break;
                    case CDataSection cdata:
                        if (!frame.Hidden) AddText(cdata.Data, frame.Mode);
                        break;
                    case Element element:
                        if (ImplicitRole.IsMetadataContent(element)) break;
                        var reason = visibility.RenderingReasonFor(element, traversal);
                        if (reason is AxIgnoredReason.Hidden or AxIgnoredReason.NotRendered) break;
                        var style = visibility.Style(element, traversal);
                        var hidden = reason == AxIgnoredReason.NotVisible || frame.Hidden
                            && !string.Equals(style.Visibility, "visible", StringComparison.OrdinalIgnoreCase)
                            && !string.Equals(style.Visibility, "initial", StringComparison.OrdinalIgnoreCase);
                        var display = HtmlDisplay.Resolve(element, style.Display);
                        // The queried root supplies context, but its own box contributes no separators.
                        var breaks = hidden || frame.Root ? 0 : element.LocalName == "p" ? 2
                            : HtmlDisplay.IsBlockLevel(display) || display == "table-caption" ? 1 : 0;
                        Break(breaks);
                        if (element.LocalName == "br")
                        {
                            if (!hidden && !frame.Root) Separator('\n');
                            Break(breaks);
                            break;
                        }
                        var mode = WhiteSpaceFor(element, frame.Mode);
                        pending.Push(new Frame(element, mode, true, false, hidden || frame.Root ? null : display, breaks, false, hidden));
                        if (element.FirstChild is { } child) pending.Push(new Frame(child, mode, false, true, null, 0, false, hidden));
                        break;
                }
            }
        }

        private WhiteSpaceMode WhiteSpaceFor(Element element, WhiteSpaceMode inherited)
        {
            // CSS Text §4.1: use the actual computed longhand, so a descendant normal overrides pre.
            if (visibility.WhiteSpaceCollapse(element, traversal) is { } value)
                return value switch
                {
                    "collapse" => WhiteSpaceMode.Collapse,
                    "preserve" or "break-spaces" => WhiteSpaceMode.Preserve,
                    "preserve-breaks" => WhiteSpaceMode.PreserveBreaks,
                    "preserve-spaces" => WhiteSpaceMode.PreserveSpaces,
                    _ => throw new NotSupportedException("innerText does not support computed white-space-collapse: " + value)
                };
            // The engine-free fallback uses HTML's suggested rendering without inventing CSS values.
            return HtmlDisplay.PreservesWhitespace(element, null) ? WhiteSpaceMode.Preserve : inherited;
        }

        private void AddText(Text text, WhiteSpaceMode mode)
        {
            var first = true;
            for (var i = 0; i < text.DataLength; i++)
            {
                work.Step();
                var c = text.DataAt(i);
                if (c == '\r')
                {
                    c = '\n';
                    if (i + 1 < text.DataLength)
                    {
                        work.Step();
                        if (text.DataAt(i + 1) == '\n') i++;
                    }
                }
                AddCharacter(c, mode, ref first);
            }
        }

        private void AddText(string text, WhiteSpaceMode mode)
        {
            var first = true;
            for (var i = 0; i < text.Length; i++)
            {
                work.Step();
                var c = text[i];
                if (c == '\r')
                {
                    c = '\n';
                    if (i + 1 < text.Length)
                    {
                        work.Step();
                        if (text[i + 1] == '\n') i++;
                    }
                }
                AddCharacter(c, mode, ref first);
            }
        }

        private void AddCharacter(char c, WhiteSpaceMode mode, ref bool first)
        {
            if (mode == WhiteSpaceMode.PreserveSpaces && c is '\t' or '\n' or '\f') c = ' ';
            var collapse = mode is WhiteSpaceMode.Collapse or WhiteSpaceMode.PreserveBreaks;
            if (mode == WhiteSpaceMode.PreserveBreaks && c == '\n')
            {
                _pendingSpace = false;
                Prefix();
            }
            else if (collapse && c is ' ' or '\t' or '\n' or '\f')
            {
                _pendingSpace = true;
                return;
            }
            else if (collapse || first) Prefix();
            _builder.Append(c);
            _started = true;
            _pendingSpace = false;
            first = false;
        }

        private void Prefix()
        {
            if (_pendingBreaks > 0)
            {
                _builder.Append('\n', _pendingBreaks);
                _pendingBreaks = 0;
                _pendingSpace = false;
            }
            if (_pendingSpace && _started && _builder.Length > 0 && _builder[^1] is not ('\n' or '\t')) _builder.Append(' ');
        }

        private void Separator(char c)
        {
            Prefix();
            _builder.Append(c);
            _started = true;
            _pendingSpace = false;
        }

        private void Break(int count)
        {
            if (count == 0) return;
            if (_started) _pendingBreaks = Math.Max(_pendingBreaks, count);
            _pendingSpace = false;
        }

        internal string Assemble()
        {
            work.Check();
            var result = _builder.ToString();
            work.Check();
            return result;
        }

        private Element? NextElement(Node node)
        {
            for (var sibling = node.NextSibling; sibling is not null; sibling = sibling.NextSibling)
            {
                work.Step();
                if (sibling is Element element) return element;
            }
            return null;
        }

        private bool IsLastRow(Element row)
        {
            if (NextElement(row) is not null) return false;
            work.Step();
            if (row.ParentNode is not Element group || group.LocalName is not ("thead" or "tbody" or "tfoot")) return true;
            for (var sibling = NextElement(group); sibling is not null; sibling = NextElement(sibling))
            {
                if (sibling.LocalName is not ("thead" or "tbody" or "tfoot")) continue;
                for (var child = sibling.FirstChild; child is not null; child = child.NextSibling)
                {
                    work.Step();
                    if (child is Element { LocalName: "tr" }) return false;
                }
            }
            return true;
        }

        private enum WhiteSpaceMode { Collapse, Preserve, PreserveBreaks, PreserveSpaces }

        private readonly record struct Frame(Node Node, WhiteSpaceMode Mode, bool Exit, bool Siblings, string? Display, int Breaks, bool Root, bool Hidden);
    }
}
