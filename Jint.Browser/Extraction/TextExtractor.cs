using System.Text;
using AngleSharp.Dom;
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
    /// <summary>Returns the rendered text of <paramref name="element"/>.</summary>
    internal static string InnerText(IElement element, bool useComputedStyle = true)
    {
        ArgumentNullException.ThrowIfNull(element);

        var collector = new Collector(new ElementVisibility(useComputedStyle));
        collector.Collect(element, preserveWhitespace: false);
        return collector.Assemble();
    }

    /// <summary>Returns the rendered text of <paramref name="document"/>'s body.</summary>
    internal static string InnerText(IDocument document, bool useComputedStyle = true)
    {
        ArgumentNullException.ThrowIfNull(document);

        var root = document.Body ?? document.DocumentElement;
        return root is null ? string.Empty : InnerText(root, useComputedStyle);
    }

    private sealed class Collector
    {
        private readonly ElementVisibility _visibility;
        private readonly List<Item> _items = [];

        internal Collector(ElementVisibility visibility) => _visibility = visibility;

        internal void Collect(INode node, bool preserveWhitespace)
        {
            switch (node)
            {
                case IText text:
                    _items.Add(Item.Content(text.Data, preserveWhitespace));
                    return;

                case IElement element:
                    CollectElement(element, preserveWhitespace);
                    return;
            }
        }

        private void CollectElement(IElement element, bool inheritedPreserve)
        {
            if (ImplicitRole.IsMetadataContent(element) || _visibility.RenderingReasonFor(element) != AxIgnoredReason.None)
            {
                return;
            }

            var (declaredDisplay, _) = _visibility.Style(element);
            var display = HtmlDisplay.Resolve(element, declaredDisplay);
            var breaks = string.Equals(element.LocalName, "p", StringComparison.Ordinal) ? 2
                : HtmlDisplay.IsBlockLevel(display) || string.Equals(display, "table-caption", StringComparison.Ordinal) ? 1
                : 0;

            if (breaks > 0)
            {
                _items.Add(Item.Break(breaks));
            }

            if (string.Equals(element.LocalName, "br", StringComparison.Ordinal))
            {
                _items.Add(Item.Separator("\n"));
            }
            else
            {
                var preserve = inheritedPreserve || HtmlDisplay.PreservesWhitespace(element, _visibility.WhiteSpace(element));
                foreach (var child in element.ChildNodes)
                {
                    Collect(child, preserve);
                }

                if (string.Equals(display, "table-cell", StringComparison.Ordinal) && element.NextElementSibling is not null)
                {
                    _items.Add(Item.Separator("\t"));
                }
                else if (string.Equals(display, "table-row", StringComparison.Ordinal) && !IsLastRow(element))
                {
                    _items.Add(Item.Separator("\n"));
                }
            }

            if (breaks > 0)
            {
                _items.Add(Item.Break(breaks));
            }
        }

        internal string Assemble()
        {
            var builder = new StringBuilder();
            var pendingBreaks = 0;
            var pendingSpace = false;
            var started = false;

            foreach (var item in _items)
            {
                if (item.Breaks > 0)
                {
                    if (started)
                    {
                        pendingBreaks = Math.Max(pendingBreaks, item.Breaks);
                    }

                    pendingSpace = false;
                    continue;
                }

                var text = item.Text!;
                if (text.Length == 0)
                {
                    continue;
                }

                string core;
                var leadingSpace = false;
                var trailingSpace = false;

                if (item.Preserve)
                {
                    core = text;
                }
                else
                {
                    var collapsed = Collapse(text);
                    if (collapsed.Length == 0)
                    {
                        continue;
                    }

                    if (collapsed == " ")
                    {
                        pendingSpace |= started;
                        continue;
                    }

                    leadingSpace = collapsed[0] == ' ';
                    trailingSpace = collapsed[^1] == ' ';
                    core = collapsed.Trim(' ');
                }

                if (pendingBreaks > 0)
                {
                    builder.Append('\n', pendingBreaks);
                    pendingBreaks = 0;
                    pendingSpace = false;
                    leadingSpace = false;
                }

                if ((pendingSpace || leadingSpace) && started && builder.Length > 0 && builder[^1] is not ('\n' or '\t'))
                {
                    builder.Append(' ');
                }

                builder.Append(core);
                started = true;
                pendingSpace = trailingSpace;
            }

            return builder.ToString();
        }

        private static bool IsLastRow(IElement row)
        {
            if (row.NextElementSibling is not null)
            {
                return false;
            }

            var group = row.ParentElement;
            if (group is null || group.LocalName is not ("thead" or "tbody" or "tfoot"))
            {
                return true;
            }

            for (var sibling = group.NextElementSibling; sibling is not null; sibling = sibling.NextElementSibling)
            {
                if (sibling.LocalName is "thead" or "tbody" or "tfoot" && sibling.QuerySelector("tr") is not null)
                {
                    return false;
                }
            }

            return true;
        }

        private static string Collapse(string text)
        {
            var builder = new StringBuilder(text.Length);
            var space = false;

            foreach (var c in text)
            {
                if (c is ' ' or '\t' or '\n' or '\r' or '\f')
                {
                    space = true;
                    continue;
                }

                if (space)
                {
                    builder.Append(' ');
                    space = false;
                }

                builder.Append(c);
            }

            if (space)
            {
                builder.Append(' ');
            }

            return builder.ToString();
        }

        private readonly record struct Item(string? Text, int Breaks, bool Preserve)
        {
            internal static Item Content(string text, bool preserve) => new(text, 0, preserve);

            internal static Item Separator(string text) => new(text, 0, Preserve: true);

            internal static Item Break(int count) => new(null, count, Preserve: false);
        }
    }
}
