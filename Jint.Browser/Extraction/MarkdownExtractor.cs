using System.Globalization;
using System.Text;
using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using Jint.Browser.Accessibility;

namespace Jint.Browser.Extraction;

/// <summary>
/// Renders a document or an element as CommonMark, for a reader whose budget is tokens rather than pixels.
/// </summary>
/// <remarks>
/// <para>
/// The output is CommonMark with GFM pipe tables. Three choices are worth knowing at the call site: a
/// <c>&lt;br&gt;</c> becomes a backslash hard break rather than two trailing spaces, so no line ends in
/// invisible white space; a definition list becomes a bold term followed by its definitions; and a
/// <c>&lt;details&gt;</c> becomes its summary in bold followed by its body, open or not.
/// </para>
/// <para>
/// What is skipped: <c>&lt;script&gt;</c>, <c>&lt;style&gt;</c>, <c>&lt;template&gt;</c>,
/// <c>&lt;noscript&gt;</c> and everything the hidden rules of <see cref="ElementVisibility"/> exclude.
/// </para>
/// </remarks>
internal static class MarkdownExtractor
{
    /// <summary>What a result cut short by <see cref="MarkdownOptions.MaxLength"/> ends with.</summary>
    internal const string TruncationMarker = "\n\n[truncated]";

    /// <summary>Renders <paramref name="document"/> as CommonMark.</summary>
    internal static string ToMarkdown(IDocument document, MarkdownOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(document);

        options ??= MarkdownOptions.Default;
        var root = options.MainContentOnly ? MainContentOf(document) : null;
        root ??= document.Body ?? document.DocumentElement;

        return root is null ? string.Empty : ToMarkdown(root, options);
    }

    /// <summary>Renders <paramref name="element"/> and its descendants as CommonMark.</summary>
    internal static string ToMarkdown(IElement element, MarkdownOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(element);

        options ??= MarkdownOptions.Default;
        var writer = new Writer(options, new ElementVisibility(options.UseComputedStyle));
        var body = writer.Blocks(element);
        return Truncate(Normalize(body), options.MaxLength);
    }

    private static IElement? MainContentOf(IDocument document) =>
        document.QuerySelector("main")
        ?? document.QuerySelector("[role=main]")
        ?? document.QuerySelector("article");

    /// <summary>
    /// Drops the blank lines at both ends and caps any run of them at one, which is all a block separator
    /// ever needs.
    /// </summary>
    /// <remarks>
    /// A line that is not blank is emitted as it stands, trailing white space included: inside a fenced code
    /// block that white space is content, and trimming it here would silently rewrite the page's own text.
    /// </remarks>
    private static string Normalize(string text)
    {
        var lines = text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        var builder = new StringBuilder(text.Length);
        var blanks = 0;
        var started = false;

        foreach (var line in lines)
        {
            if (line.AsSpan().Trim().IsEmpty)
            {
                blanks++;
                continue;
            }

            if (started)
            {
                builder.Append('\n', Math.Min(blanks, 1) + 1);
            }

            builder.Append(line);
            blanks = 0;
            started = true;
        }

        return builder.ToString();
    }

    private static string Truncate(string text, int maxLength)
    {
        if (maxLength <= 0 || text.Length <= maxLength)
        {
            return text;
        }

        var budget = Math.Max(0, maxLength - TruncationMarker.Length);
        var cut = budget;
        while (cut > 0 && !char.IsWhiteSpace(text[cut]))
        {
            cut--;
        }

        if (cut == 0)
        {
            cut = budget;
        }

        return string.Concat(text.AsSpan(0, cut).TrimEnd(), TruncationMarker);
    }

    private sealed class Writer
    {
        private readonly MarkdownOptions _options;
        private readonly ElementVisibility _visibility;

        internal Writer(MarkdownOptions options, ElementVisibility visibility)
        {
            _options = options;
            _visibility = visibility;
        }

        /// <summary>Renders an element's children as a sequence of blocks separated by a blank line.</summary>
        internal string Blocks(IElement element)
        {
            var blocks = new List<string>();
            var inline = new StringBuilder();

            foreach (var child in element.ChildNodes)
            {
                if (child is IElement childElement)
                {
                    if (Skip(childElement))
                    {
                        continue;
                    }

                    if (IsBlock(childElement))
                    {
                        Flush(blocks, inline);
                        var block = Block(childElement);
                        if (block.Length > 0)
                        {
                            blocks.Add(block);
                        }

                        continue;
                    }
                }

                inline.Append(Inline(child));
            }

            Flush(blocks, inline);
            return string.Join("\n\n", blocks);

            static void Flush(List<string> blocks, StringBuilder inline)
            {
                var text = Collapse(inline.ToString());
                inline.Clear();

                if (text.Length > 0)
                {
                    blocks.Add(text);
                }
            }
        }

        private string Block(IElement element)
        {
            switch (element.LocalName)
            {
                case "h1":
                case "h2":
                case "h3":
                case "h4":
                case "h5":
                case "h6":
                    {
                        var level = element.LocalName[1] - '0';
                        var text = InlineOf(element);
                        return text.Length == 0 ? string.Empty : new string('#', level) + " " + text;
                    }

                case "p":
                    return InlineOf(element);

                case "pre":
                    return CodeBlock(element);

                case "blockquote":
                    return Prefix(Blocks(element), "> ", "> ");

                case "ul":
                case "menu":
                    return List(element, ordered: false);

                case "ol":
                    return List(element, ordered: true);

                case "dl":
                    return DefinitionList(element);

                case "table":
                    return Table(element);

                case "hr":
                    return "---";

                case "details":
                    return Details(element);

                case "figure":
                    return Blocks(element);

                case "li":
                    // A list item reached outside a list still renders as one.
                    return "- " + Prefix(Blocks(element), string.Empty, "  ");

                default:
                    return Blocks(element);
            }
        }

        private string InlineOf(IElement element)
        {
            var builder = new StringBuilder();
            foreach (var child in element.ChildNodes)
            {
                builder.Append(Inline(child));
            }

            return Collapse(builder.ToString());
        }

        private string Inline(INode node)
        {
            if (node is IText text)
            {
                return Escape(text.Data);
            }

            if (node is not IElement element || Skip(element))
            {
                return string.Empty;
            }

            switch (element.LocalName)
            {
                case "br":
                    return "\\\n";

                case "a":
                    {
                        var content = InlineOf(element);
                        var href = element is IHtmlAnchorElement anchor && anchor.HasAttribute("href") ? anchor.Href : null;
                        if (string.IsNullOrEmpty(href))
                        {
                            return content;
                        }

                        return content.Length == 0 ? $"<{href}>" : $"[{content}]({Link(href)})";
                    }

                case "img":
                    {
                        var alt = element.GetAttribute("alt") ?? string.Empty;

                        // An explicitly empty alt is HTML's way of saying the image is decoration, so it is
                        // not content and does not belong in a rendering whose whole purpose is content.
                        if (alt.Length == 0 && element.HasAttribute("alt"))
                        {
                            return string.Empty;
                        }

                        if (!_options.IncludeImages)
                        {
                            return Escape(alt);
                        }

                        var source = element is IHtmlImageElement image && image.HasAttribute("src") ? image.Source : element.GetAttribute("src");
                        return string.IsNullOrEmpty(source) ? Escape(alt) : $"![{Escape(alt)}]({Link(source)})";
                    }

                case "strong":
                case "b":
                    return Wrap(InlineOf(element), "**");

                case "em":
                case "i":
                    return Wrap(InlineOf(element), "*");

                case "del":
                case "s":
                    return Wrap(InlineOf(element), "~~");

                case "code":
                case "kbd":
                case "samp":
                    return CodeSpan(Collapse(element.TextContent));

                default:
                    return IsBlock(element) ? " " + Collapse(Block(element).Replace('\n', ' ')) + " " : InlineOf(element);
            }
        }

        /// <summary>
        /// Wraps <paramref name="content"/> in a code span whose delimiter is longer than any backtick run
        /// inside it, which is how CommonMark quotes a backtick — doubling one would not.
        /// </summary>
        private static string CodeSpan(string content)
        {
            if (content.Length == 0)
            {
                return string.Empty;
            }

            var longest = 0;
            var run = 0;
            foreach (var c in content)
            {
                run = c == '`' ? run + 1 : 0;
                longest = Math.Max(longest, run);
            }

            var fence = new string('`', longest + 1);
            var pad = content.StartsWith('`') || content.EndsWith('`') ? " " : string.Empty;
            return fence + pad + content + pad + fence;
        }

        private static string CodeBlock(IElement element)
        {
            var code = element.QuerySelector("code");
            var language = LanguageOf(code ?? element);
            var content = (code ?? element).TextContent.Replace("\r\n", "\n", StringComparison.Ordinal).TrimEnd('\n');

            var fence = "```";
            while (content.Contains(fence, StringComparison.Ordinal))
            {
                fence += "`";
            }

            return fence + language + "\n" + content + "\n" + fence;
        }

        private static string LanguageOf(IElement element)
        {
            foreach (var token in element.ClassList)
            {
                if (token.StartsWith("language-", StringComparison.Ordinal))
                {
                    return token["language-".Length..];
                }

                if (token.StartsWith("lang-", StringComparison.Ordinal))
                {
                    return token["lang-".Length..];
                }
            }

            return string.Empty;
        }

        private string List(IElement element, bool ordered)
        {
            var number = ordered && element is IHtmlOrderedListElement { Start: var start } ? start : 1;
            var lines = new List<string>();

            foreach (var child in element.Children)
            {
                if (!string.Equals(child.LocalName, "li", StringComparison.Ordinal) || Skip(child))
                {
                    continue;
                }

                var marker = ordered ? number.ToString(CultureInfo.InvariantCulture) + ". " : "- ";
                var body = Blocks(child);
                lines.Add(Prefix(body, marker, new string(' ', marker.Length)));
                number++;
            }

            return string.Join("\n", lines);
        }

        private string DefinitionList(IElement element)
        {
            var lines = new List<string>();
            foreach (var child in element.Children)
            {
                if (Skip(child))
                {
                    continue;
                }

                if (string.Equals(child.LocalName, "dt", StringComparison.Ordinal))
                {
                    var term = InlineOf(child);
                    if (term.Length > 0)
                    {
                        lines.Add("**" + term + "**\\");
                    }
                }
                else if (string.Equals(child.LocalName, "dd", StringComparison.Ordinal))
                {
                    var definition = Blocks(child);
                    if (definition.Length > 0)
                    {
                        lines.Add(Prefix(definition, "  ", "  "));
                    }
                }
            }

            return string.Join("\n", lines);
        }

        private string Details(IElement element)
        {
            var parts = new List<string>();
            foreach (var child in element.Children)
            {
                if (Skip(child))
                {
                    continue;
                }

                if (string.Equals(child.LocalName, "summary", StringComparison.Ordinal))
                {
                    var summary = InlineOf(child);
                    if (summary.Length > 0)
                    {
                        parts.Add("**" + summary + "**");
                    }
                }
                else if (IsBlock(child))
                {
                    var block = Block(child);
                    if (block.Length > 0)
                    {
                        parts.Add(block);
                    }
                }
                else
                {
                    var inline = Inline(child);
                    if (inline.Trim().Length > 0)
                    {
                        parts.Add(Collapse(inline));
                    }
                }
            }

            return string.Join("\n\n", parts);
        }

        private string Table(IElement element)
        {
            var rows = new List<List<string>>();
            var headerIndex = -1;

            foreach (var row in RowsOf(element))
            {
                if (Skip(row))
                {
                    continue;
                }

                var cells = new List<string>();
                var isHeader = true;

                foreach (var cell in row.Children)
                {
                    if (cell.LocalName is not ("td" or "th") || Skip(cell))
                    {
                        continue;
                    }

                    isHeader &= string.Equals(cell.LocalName, "th", StringComparison.Ordinal);
                    cells.Add(Collapse(InlineOf(cell)).Replace("|", "\\|", StringComparison.Ordinal));
                }

                if (cells.Count == 0)
                {
                    continue;
                }

                if (isHeader && headerIndex < 0)
                {
                    headerIndex = rows.Count;
                }

                rows.Add(cells);
            }

            if (rows.Count == 0)
            {
                return string.Empty;
            }

            var width = rows.Max(static row => row.Count);
            var builder = new StringBuilder();

            var header = headerIndex >= 0 ? rows[headerIndex] : [];
            builder.Append(Row(header, width)).Append('\n');
            builder.Append("| ").Append(string.Join(" | ", Enumerable.Repeat("---", width))).Append(" |");

            for (var i = 0; i < rows.Count; i++)
            {
                if (i == headerIndex)
                {
                    continue;
                }

                builder.Append('\n').Append(Row(rows[i], width));
            }

            var caption = CaptionOf(element);
            if (caption is not null && !Skip(caption))
            {
                var text = InlineOf(caption);
                if (text.Length > 0)
                {
                    return "**" + text + "**\n\n" + builder;
                }
            }

            return builder.ToString();

            static string Row(IReadOnlyList<string> cells, int width)
            {
                var builder = new StringBuilder("|");
                for (var i = 0; i < width; i++)
                {
                    builder.Append(' ').Append(i < cells.Count ? cells[i] : string.Empty).Append(" |");
                }

                return builder.ToString();
            }
        }

        /// <summary>
        /// The rows of this table and no other: its own <c>tr</c> children and those of its own row groups.
        /// </summary>
        /// <remarks>
        /// A descendant selector would pull a nested table's rows into this one, which is not a shape a
        /// pipe table has any way to express.
        /// </remarks>
        private static IEnumerable<IElement> RowsOf(IElement table)
        {
            foreach (var child in table.Children)
            {
                if (string.Equals(child.LocalName, "tr", StringComparison.Ordinal))
                {
                    yield return child;
                }
                else if (child.LocalName is "thead" or "tbody" or "tfoot")
                {
                    foreach (var row in child.Children)
                    {
                        if (string.Equals(row.LocalName, "tr", StringComparison.Ordinal))
                        {
                            yield return row;
                        }
                    }
                }
            }
        }

        /// <summary>This table's own caption, which is a child rather than any descendant.</summary>
        private static IElement? CaptionOf(IElement table)
        {
            foreach (var child in table.Children)
            {
                if (string.Equals(child.LocalName, "caption", StringComparison.Ordinal))
                {
                    return child;
                }
            }

            return null;
        }

        private bool Skip(IElement element) =>
            ImplicitRole.IsMetadataContent(element)
            || string.Equals(element.LocalName, "noscript", StringComparison.Ordinal)
            || _visibility.RenderingReasonFor(element) != AxIgnoredReason.None;

        private bool IsBlock(IElement element) =>
            HtmlDisplay.IsBlockLevel(HtmlDisplay.Resolve(element, _visibility.Style(element).Display))
            || element.LocalName is "table" or "dl" or "details";

        private static string Wrap(string content, string marker)
        {
            var trimmed = content.Trim();
            if (trimmed.Length == 0)
            {
                return content;
            }

            var lead = content.StartsWith(' ') ? " " : string.Empty;
            var tail = content.EndsWith(' ') ? " " : string.Empty;
            return lead + marker + trimmed + marker + tail;
        }

        private static string Prefix(string text, string first, string rest)
        {
            if (text.Length == 0)
            {
                return first.TrimEnd();
            }

            var lines = text.Split('\n');
            var builder = new StringBuilder();

            for (var i = 0; i < lines.Length; i++)
            {
                if (i > 0)
                {
                    builder.Append('\n');
                }

                var prefix = i == 0 ? first : rest;
                builder.Append(lines[i].Length == 0 ? prefix.TrimEnd() : prefix + lines[i]);
            }

            return builder.ToString();
        }

        private static string Collapse(string text)
        {
            var builder = new StringBuilder(text.Length);
            var space = false;

            foreach (var c in text)
            {
                // The only line feed an inline run may carry is the one a <br> wrote, and it always follows
                // the backslash that makes it a CommonMark hard break. Every other one is white space.
                if (c is '\n' && builder.Length > 0 && builder[^1] == '\\')
                {
                    builder.Append('\n');
                    space = false;
                    continue;
                }

                if (char.IsWhiteSpace(c))
                {
                    space = builder.Length > 0 && builder[^1] != '\n';
                    continue;
                }

                if (space)
                {
                    builder.Append(' ');
                    space = false;
                }

                builder.Append(c);
            }

            return builder.ToString().TrimEnd();
        }

        private static string Link(string url) => url.Contains(' ', StringComparison.Ordinal) ? "<" + url + ">" : url;

        private static string Escape(string text)
        {
            var builder = new StringBuilder(text.Length);

            foreach (var c in text)
            {
                switch (c)
                {
                    case '\\':
                    case '`':
                    case '*':
                    case '[':
                    case ']':
                    case '<':
                        builder.Append('\\').Append(c);
                        break;

                    case '_':
                        // Intra-word underscores are not emphasis in CommonMark, so only a boundary one needs
                        // escaping; escaping every one would turn snake_case into noise.
                        if (builder.Length == 0 || char.IsWhiteSpace(builder[^1]))
                        {
                            builder.Append('\\');
                        }

                        builder.Append(c);
                        break;

                    default:
                        builder.Append(c);
                        break;
                }
            }

            return builder.ToString();
        }
    }
}
