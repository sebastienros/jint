using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using AngleSharp;
using AngleSharp.Css;
using AngleSharp.Css.Dom;
using AngleSharp.Css.Parser;
using AngleSharp.Dom;

namespace Jint.Browser.Dom;

/// <summary>
/// Adapts Selectors §5.1's empty namespace prefix to AngleSharp's public predicate factory. Parsing,
/// nested selector evaluation and tree traversal remain native; the added predicate reads the existing
/// element's namespace without altering the DOM or its identity.
/// </summary>
internal static class DomSelectors
{
    private static readonly ConditionalWeakTable<IBrowsingContext, Adapter> _adapters = new();

    internal static IElement? QuerySelector(INode root, string text)
    {
        var selector = Adapt(root, text);
        return selector is null
            ? ((IParentNode) root).QuerySelector(text)
            : selector.MatchAny(root.ChildNodes.OfType<IElement>(), Scope(root));
    }

    internal static IHtmlCollection<IElement> QuerySelectorAll(INode root, string text)
    {
        var selector = Adapt(root, text);
        return selector is null
            ? ((IParentNode) root).QuerySelectorAll(text)
            : root.ChildNodes.QuerySelectorAll(new ScopedSelector(selector, Scope(root)));
    }

    internal static bool Matches(IElement element, string text)
        => Adapt(element, text) is { } selector ? selector.Match(element, element) : element.Matches(text);

    internal static IElement? Closest(IElement element, string text)
    {
        if (Adapt(element, text) is not { } selector)
        {
            return element.Closest(text);
        }

        for (var candidate = element; candidate is not null; candidate = candidate.ParentElement)
        {
            if (selector.Match(candidate, element))
            {
                return candidate;
            }
        }

        return null;
    }

    private static IElement? Scope(INode root)
        => root as IElement ?? (root as IDocument)?.DocumentElement ?? (root as IShadowRoot)?.Host;

    private static ISelector? Adapt(INode root, string text)
    {
        if (!text.Contains('|'))
        {
            return null;
        }

        var context = root.Owner?.Context ?? ((IDocument) root).Context;
        if (_adapters.TryGetValue(context, out var adapter) && adapter.TryGetCached(text, out var cached))
        {
            return cached;
        }
        if (FindNames(text) is not { } names)
        {
            return null;
        }

        return _adapters.GetValue(context, static context => new Adapter(context)).Parse(text, names);
    }

    private sealed class Adapter(IBrowsingContext context)
    {
        private const int MaxCachedSelectors = 256;
        private readonly Dictionary<string, ISelector> _selectors = new(StringComparer.Ordinal);
        private readonly object _gate = new();

        internal bool TryGetCached(string text, out ISelector? selector)
        {
            lock (_gate)
            {
                return _selectors.TryGetValue(text, out selector);
            }
        }

        internal ISelector Parse(string text, Names names)
        {
            lock (_gate)
            {
                if (_selectors.TryGetValue(text, out var cached))
                {
                    return cached;
                }

                var factory = new PredicateFactory(context.GetFactory<IPseudoClassSelectorFactory>());
                // Only parser factories are shared, preserving the page's existing selector predicates.
                // No service is installed into the document and stylesheet parsing is unchanged.
                var configuration = Configuration.Default
                    .WithOnly(context.GetFactory<IAttributeSelectorFactory>())
                    .WithOnly<IPseudoClassSelectorFactory>(factory)
                    .WithOnly(context.GetFactory<IPseudoElementSelectorFactory>());
                var parser = BrowsingContext.New(configuration).GetService<ICssSelectorParser>()
                    ?? throw new InvalidOperationException("AngleSharp did not provide a selector parser.");

                // The native parser validates the original grammar and supplies the decoded names of
                // every pseudo-class it encounters, including unknown names in forgiving lists. A
                // private marker must not accidentally turn one of those into a supported extension.
                if (parser.ParseSelector(text) is null)
                {
                    throw new DomException(DomError.Syntax);
                }

                var suffix = 0;
                var builder = new StringBuilder(text.Length);
                var copied = 0;
                foreach (var range in names.Ranges)
                {
                    string marker;
                    do
                    {
                        marker = "-jint-empty-namespace-" + suffix.ToString(CultureInfo.InvariantCulture);
                        suffix++;
                    }
                    while (factory.Names.Contains(marker));

                    var type = text[range.NameStart..range.End];
                    string? localName = null;
                    if (type != "*")
                    {
                        // Let the same native tokenizer decode the name. A no-namespace local name is
                        // case-sensitive even in an HTML document, unlike AngleSharp's TypeSelector.
                        var visitor = new TypeNameVisitor();
                        (parser.ParseSelector(type) ?? throw new DomException(DomError.Syntax)).Accept(visitor);
                        localName = visitor.Name ?? throw new DomException(DomError.Syntax);
                    }
                    factory.Markers.Add(marker, new EmptyNamespaceSelector(localName));
                    builder.Append(text, copied, range.Start - copied);
                    builder.Append(':').Append(marker);
                    copied = range.End;
                }
                builder.Append(text, copied, text.Length - copied);

                var selector = parser.ParseSelector(builder.ToString()) ?? throw new DomException(DomError.Syntax);
                // The compiled native selector captures only predicates, not this transient parser or
                // its factory. Bound the per-context cache as AngleSharp bounds its own selector cache.
                if (_selectors.Count == MaxCachedSelectors)
                {
                    _selectors.Clear();
                }
                _selectors.Add(text, selector);
                return selector;
            }
        }
    }

    private sealed class PredicateFactory(IPseudoClassSelectorFactory original) : IPseudoClassSelectorFactory
    {
        internal Dictionary<string, ISelector> Markers { get; } = new(StringComparer.Ordinal);

        internal HashSet<string> Names { get; } = new(StringComparer.OrdinalIgnoreCase);

        public ISelector? Create(string name)
        {
            Names.Add(name);
            return Markers.TryGetValue(name, out var selector) ? selector : original.Create(name);
        }
    }

    private sealed class EmptyNamespaceSelector(string? localName) : ISelector
    {
        public string Text => "|" + (localName is null ? "*" : CssUtilities.Escape(localName));

        public Priority Specificity => localName is null ? Priority.Zero : Priority.OneTag;

        public bool Match(IElement element, IElement? scope)
            => DomNamespaces.Of(element) is null
               && (localName is null || string.Equals(localName, element.LocalName, StringComparison.Ordinal));

        public void Accept(ISelectorVisitor visitor) => visitor.Type(Text);
    }

    private sealed class TypeNameVisitor : ISelectorVisitor
    {
        internal string? Name { get; private set; }

        public void Type(string name) => Name = name;

        public void Attribute(string name, string op, string? value) => throw new DomException(DomError.Syntax);
        public void Id(string value) => throw new DomException(DomError.Syntax);
        public void Child(string name, int step, int offset, ISelector selector) => throw new DomException(DomError.Syntax);
        public void Class(string value) => throw new DomException(DomError.Syntax);
        public void PseudoClass(string value) => throw new DomException(DomError.Syntax);
        public void PseudoElement(string value) => throw new DomException(DomError.Syntax);
        public void List(IEnumerable<ISelector> selectors) => throw new DomException(DomError.Syntax);
        public void Combinator(IEnumerable<ISelector> selectors, IEnumerable<string> symbols) => throw new DomException(DomError.Syntax);
        public void Many(IEnumerable<ISelector> selectors) => throw new DomException(DomError.Syntax);
    }

    private sealed class ScopedSelector(ISelector selector, IElement? scope) : ISelector
    {
        public string Text => selector.Text;

        public Priority Specificity => selector.Specificity;

        public bool Match(IElement element, IElement? ignored) => selector.Match(element, scope);

        public void Accept(ISelectorVisitor visitor) => selector.Accept(visitor);
    }

    private sealed class Names
    {
        internal List<NameRange> Ranges { get; } = [];
    }

    private readonly record struct NameRange(int Start, int NameStart, int End);

    /// <summary>
    /// Finds namespace type tokens, not pipe characters in strings, attributes, comments or escapes.
    /// This is a lexical adapter only: the native parser validates both the original and adapted strings.
    /// </summary>
    private static Names? FindNames(string text)
    {
        Names? names = null;
        var attributeDepth = 0;
        var previousName = false;
        var previousStar = false;
        var previousPipe = false;
        for (var i = 0; i < text.Length;)
        {
            var c = text[i];
            if (c is '\'' or '"')
            {
                var quote = c;
                i++;
                while (i < text.Length && text[i] != quote)
                {
                    i = text[i] == '\\' ? EscapeEnd(text, i) : i + 1;
                }
                i = Math.Min(i + 1, text.Length);
                previousName = previousStar = previousPipe = false;
                continue;
            }

            if (c == '/' && i + 1 < text.Length && text[i + 1] == '*')
            {
                i = CommentEnd(text, i);
                continue;
            }

            if (c == '[')
            {
                attributeDepth++;
            }
            else if (c == ']')
            {
                attributeDepth--;
            }
            else if (attributeDepth == 0)
            {
                if (c == '|' && !previousName && !previousStar
                    && !previousPipe
                    && (i + 1 >= text.Length || text[i + 1] != '|'))
                {
                    var start = SkipComments(text, i + 1);
                    var end = start < text.Length && text[start] == '*' ? start + 1 : NameEnd(text, start);
                    if (end != start)
                    {
                        (names ??= new Names()).Ranges.Add(new NameRange(i, start, end));
                        i = end;
                        previousName = true;
                        previousStar = previousPipe = false;
                        continue;
                    }
                }
            }

            var nameEnd = NameEnd(text, i);
            if (nameEnd != i)
            {
                i = nameEnd;
                previousName = true;
                previousStar = previousPipe = false;
            }
            else
            {
                previousName = false;
                previousStar = c == '*';
                previousPipe = c == '|';
                i++;
            }
        }

        return names;
    }

    private static int SkipComments(string text, int index)
    {
        while (index + 1 < text.Length && text[index] == '/' && text[index + 1] == '*')
        {
            index = CommentEnd(text, index);
        }
        return index;
    }

    private static int CommentEnd(string text, int start)
    {
        var end = text.IndexOf("*/", start + 2, StringComparison.Ordinal);
        return end < 0 ? text.Length : end + 2;
    }

    private static int NameEnd(string text, int start)
    {
        var end = start;
        while (end < text.Length)
        {
            var c = text[end];
            if (c == '\\')
            {
                end = EscapeEnd(text, end);
            }
            else if (char.IsAsciiLetterOrDigit(c) || c is '_' or '-' || c >= 0x80)
            {
                end++;
            }
            else
            {
                break;
            }
        }
        return end;
    }

    private static int EscapeEnd(string text, int start)
    {
        var end = Math.Min(start + 1, text.Length);
        if (end == text.Length)
        {
            return end;
        }
        if (!char.IsAsciiHexDigit(text[end]))
        {
            return end + 1;
        }
        var digits = 0;
        while (end < text.Length && digits < 6 && char.IsAsciiHexDigit(text[end]))
        {
            end++;
            digits++;
        }
        if (end < text.Length && text[end] is '\t' or '\n' or '\r' or '\f' or ' ')
        {
            var cr = text[end++] == '\r';
            if (cr && end < text.Length && text[end] == '\n')
            {
                end++;
            }
        }
        return end;
    }

}
