using System.Globalization;
using System.Text;
using Jint.HtmlParser;
using Jint.HtmlParser.Css.Selectors;
using Jint.Browser.Dom;
using Jint.Browser.Accessibility;

namespace Jint.Browser.Runtime;

/// <summary>
/// What the <c>target</c> of a click, a fill, a key press or a query names: a CSS selector, a reference an
/// accessibility snapshot printed, or an element found by its role, its label or its text.
/// </summary>
/// <remarks>
/// <para>
/// <b>Several spellings because callers hold different things.</b> A caller in code has a selector, and it
/// is the right thing for it to have. A caller reading a snapshot has a role and a name —
/// <c>- button "Save" [ref=42]</c> — and a CSS selector for that is something it would have to invent. So a
/// target beginning <c>ref=</c> is the identifier the snapshot printed, resolved back through the
/// accessibility tree's own table; <c>role=button[name="Save"]</c> asks the same role and accessible-name
/// computation the snapshot printed from, without a snapshot; <c>label=</c> and <c>text=</c> find a control
/// by its label and an element by its text; and anything else is a selector matched by the native DOM
/// selector adapter.
/// </para>
/// <para>
/// <b>The grammar is Playwright's.</b> A role part is <c>role=<i>role</i></c> followed by any of
/// <c>[name="…"]</c> (a case-insensitive substring; <c>"…"s</c> for an exact, case-sensitive match and
/// <c>"…"i</c> to say the default), <c>[level=<i>n</i>]</c>, <c>[checked]</c> / <c>[checked=false|mixed]</c>,
/// <c>[pressed]</c>, <c>[expanded]</c>, <c>[selected]</c>, <c>[disabled]</c> and <c>[include-hidden]</c>. A
/// <c>text=</c> or <c>label=</c> body is a case-insensitive substring, or with quotes an exact one
/// (<c>"…"i</c> keeps it insensitive). The <c>internal:role=</c>, <c>internal:text=</c> and
/// <c>internal:label=</c> spellings a Playwright locator serializes to are the same parts, <c>css=</c> marks a
/// selector explicitly, and Puppeteer's <c>aria/<i>name</i>[role="…"]</c> is an exact accessible name with an
/// optional role. Parts chain with <c>&gt;&gt;</c>: each part is resolved inside every element the part
/// before it found, so <c>[data-group=type] &gt;&gt; role=checkbox[name="Integration"]</c> is a role query
/// scoped to a region.
/// </para>
/// <para>
/// <b>A plain selector takes the path it always took.</b> A target with no <c>&gt;&gt;</c> and no locator
/// prefix is handed to the selector compiler unchanged, and a first match stops the walk; none of these
/// prefixes is valid CSS, so no selector changes meaning.
/// </para>
/// <para>
/// <b>A reference belongs to one document.</b> The identifiers are the document's, so a navigation ends
/// every reference a snapshot of the document before it printed. Resolving one afterwards answers nothing
/// rather than the wrong element, because the table is keyed on the document that is gone. A role, label or
/// text target has no such table and resolves against whatever document is current.
/// </para>
/// </remarks>
internal sealed class ElementLocator
{
    /// <summary>What a target starts with to be a snapshot reference rather than a selector.</summary>
    internal const string ReferencePrefix = "ref=";

    private const string Chain = ">>";

    private readonly Part[] _parts;

    private ElementLocator(Part[] parts) => _parts = parts;

    /// <summary>The first element <paramref name="target"/> names, or <see langword="null"/>.</summary>
    /// <remarks>
    /// A target the parser or the native selector compiler rejects answers <see langword="null"/> rather
    /// than throwing: a caller asking about an element that is not there and a caller asking wrongly both
    /// want "no", and a target arriving from a protocol client or an agent is input rather than code.
    /// </remarks>
    internal static Element? Find(DomRealm realm, Document? document, string target) => Find(realm, document, target, 0);

    /// <summary>The indexed element <paramref name="target"/> names, or <see langword="null"/>.</summary>
    /// <param name="realm">The page's DOM realm, whose constraints bound a selector match.</param>
    /// <param name="document">The document to search, or <see langword="null"/> when there is none.</param>
    /// <param name="target">The target, in any of the spellings the type describes.</param>
    /// <param name="index">The zero-based match, or a negative offset from the last match.</param>
    internal static Element? Find(DomRealm realm, Document? document, string target, int index)
        => document is null ? null : TryParse(target)?.TryFind(realm, document, index);

    /// <summary>
    /// Parses <paramref name="target"/> once for a caller that resolves it repeatedly, or answers
    /// <see langword="null"/> when it is malformed — the wait's form of <see cref="Find(DomRealm, Document?, string, int)"/>.
    /// </summary>
    internal static ElementLocator? TryParse(string target)
    {
        if (string.IsNullOrWhiteSpace(target))
        {
            return null;
        }

        try
        {
            return Parse(target);
        }
        catch (DomException)
        {
            return null;
        }
    }

    /// <summary>Parses <paramref name="target"/> once, for a caller that resolves it repeatedly.</summary>
    /// <exception cref="DomException">
    /// A <c>SyntaxError</c> when a locator part is malformed. A malformed CSS part throws the same exception,
    /// from the native compiler, when it is first resolved.
    /// </exception>
    internal static ElementLocator Parse(string target)
    {
        ArgumentNullException.ThrowIfNull(target);

        // The common case is a plain selector: it reaches the compiler unchanged, whitespace included.
        if (target.IndexOf(Chain, StringComparison.Ordinal) < 0)
        {
            var single = Classify(target) ?? new CssPart(target);
            return single is FilterPart
                ? throw Invalid(target, "a filter part with nothing before it to filter")
                : new ElementLocator([single]);
        }

        var texts = Split(target);
        var parts = new Part[texts.Count];
        for (var i = 0; i < texts.Count; i++)
        {
            var text = texts[i].Trim();
            if (text.Length == 0)
            {
                throw Invalid(target, "an empty part");
            }

            parts[i] = Classify(text) ?? new CssPart(text);
            if (i == 0 && parts[i] is FilterPart)
            {
                throw Invalid(target, "a filter part with nothing before it to filter");
            }
        }

        return new ElementLocator(parts);
    }

    /// <summary>Every element this target names, in document order.</summary>
    /// <exception cref="DomException">A CSS part is not a valid selector.</exception>
    internal Element[] FindAll(DomRealm realm, Document? document)
    {
        if (document is null)
        {
            return [];
        }

        if (_parts is [CssPart only])
        {
            return DomSelectors.QuerySelectorAll(realm, document, only.Program(realm));
        }

        return Resolve(realm, document, first: false).ToArray();
    }

    /// <summary>The non-trivial resolution: every part in turn, each inside the matches of the one before it.</summary>
    /// <param name="realm">The page's DOM realm, whose constraints bound the walk.</param>
    /// <param name="document">The document to search.</param>
    /// <param name="first">
    /// Whether only the first match in document order is wanted, which lets the last part stop early when it
    /// searches a single scope.
    /// </param>
    private List<Element> Resolve(DomRealm realm, Document document, bool first)
    {
        // A role, label or text walk runs no script, so the page's turn budget and cancellation reach it
        // only through this, once per element: the same two a selector match observes.
        var checkpoint = () =>
        {
            realm.Engine.Constraints.Check();
            realm.CancellationToken.ThrowIfCancellationRequested();
        };

        List<Node> scopes = [document];
        var found = new List<Element>();
        for (var i = 0; i < _parts.Length; i++)
        {
            found = new List<Element>();
            var last = i == _parts.Length - 1;
            _parts[i].Query(new Search(realm, document, scopes, found, first && last && scopes.Count == 1, checkpoint));
            if (scopes.Count > 1 && _parts[i] is not FilterPart)
            {
                found = InDocumentOrder(document, found, checkpoint);
            }

            if (found.Count == 0 || last)
            {
                break;
            }

            // Every match is a scope, nested ones included: a part such as `:scope > li` depends on which
            // element it is asked inside, and InDocumentOrder removes what nested scopes find twice.
            scopes = [.. found];
        }

        return found;
    }

    /// <summary>
    /// The indexed element this target names, or <see langword="null"/> — also when a CSS part is not a
    /// valid selector, for the reason <see cref="Find(DomRealm, Document?, string, int)"/> gives.
    /// </summary>
    internal Element? TryFind(DomRealm realm, Document? document, int index)
    {
        try
        {
            return Find(realm, document, index);
        }
        catch (DomException)
        {
            return null;
        }
    }

    /// <summary>The indexed element this target names, or <see langword="null"/>.</summary>
    /// <exception cref="DomException">A CSS part is not a valid selector.</exception>
    internal Element? Find(DomRealm realm, Document? document, int index)
    {
        if (document is null)
        {
            return null;
        }

        if (index == 0)
        {
            // The first match is what nearly every caller wants, and QuerySelector — or a walk told to stop —
            // stops at it rather than building a collection the caller reads one element of.
            return _parts is [CssPart only]
                ? DomSelectors.QuerySelector(realm, document, only.Program(realm))
                : Resolve(realm, document, first: true) is [var head, ..] ? head : null;
        }

        var elements = FindAll(realm, document);
        var resolved = index >= 0 ? index : elements.Length + index;
        return (uint) resolved < (uint) elements.Length ? elements[resolved] : null;
    }

    private static Part? Classify(string text)
    {
        if (TryStrip(text, "css=", out var css))
        {
            return new CssPart(css);
        }

        if (TryStrip(text, ReferencePrefix, out var reference))
        {
            return int.TryParse(reference.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var id)
                ? new ReferencePart(id)
                : throw Invalid(text, "a reference that is not a number");
        }

        if (TryStrip(text, "role=", out var role) || TryStrip(text, "internal:role=", out role))
        {
            return new RolePart(ParseRole(text, role));
        }

        if (TryStrip(text, "text=", out var body) || TryStrip(text, "internal:text=", out body))
        {
            return new TextPart(ParseText(text, body), label: false);
        }

        if (TryStrip(text, "label=", out body) || TryStrip(text, "internal:label=", out body))
        {
            return new TextPart(ParseText(text, body), label: true);
        }

        if (TryStrip(text, "aria/", out var aria))
        {
            return new RolePart(ParseAria(text, aria));
        }

        if (TryStrip(text, "nth=", out body) || TryStrip(text, "internal:nth=", out body))
        {
            return int.TryParse(body.Trim(), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var nth)
                ? new NthPart(nth)
                : throw Invalid(text, "an nth that is not an integer");
        }

        if (TryStrip(text, "visible=", out body) || TryStrip(text, "internal:visible=", out body))
        {
            return body.Trim() switch
            {
                "true" => new VisiblePart(visible: true),
                "false" => new VisiblePart(visible: false),
                _ => throw Invalid(text, "a visible that is not true or false"),
            };
        }

        if (TryStrip(text, "has-text=", out body) || TryStrip(text, "internal:has-text=", out body))
        {
            return new HasTextPart(ParseHasText(text, body), has: true);
        }

        if (TryStrip(text, "has-not-text=", out body) || TryStrip(text, "internal:has-not-text=", out body))
        {
            return new HasTextPart(ParseHasText(text, body), has: false);
        }

        return null;
    }

    private static bool TryStrip(string text, string prefix, out string rest)
    {
        var trimmed = text.AsSpan().TrimStart();
        if (trimmed.StartsWith(prefix, StringComparison.Ordinal))
        {
            rest = trimmed[prefix.Length..].ToString();
            return true;
        }

        rest = string.Empty;
        return false;
    }

    /// <summary>Splits a target at every <c>&gt;&gt;</c> outside a quoted string.</summary>
    /// <remarks>
    /// Playwright's splitter: a backslash escapes the next character anywhere, and a quote opens a string
    /// wherever it appears, so an unescaped apostrophe in an unquoted <c>text=</c> body holds every later
    /// <c>&gt;&gt;</c> inside the part; quote such a body.
    /// </remarks>
    private static List<string> Split(string target)
    {
        var parts = new List<string>();
        var start = 0;
        char quote = '\0';
        for (var i = 0; i < target.Length; i++)
        {
            var c = target[i];
            if (c == '\\')
            {
                i++;
            }
            else if (quote != '\0')
            {
                if (c == quote)
                {
                    quote = '\0';
                }
            }
            else if (c is '"' or '\'')
            {
                quote = c;
            }
            else if (c == '>' && i + 1 < target.Length && target[i + 1] == '>')
            {
                parts.Add(target[start..i]);
                start = i + 2;
                i++;
            }
        }

        parts.Add(target[start..]);
        return parts;
    }

    private static AxRoleQuery ParseRole(string part, string body)
    {
        var reader = new Reader(part, body);
        var role = reader.ReadWhile(static c => char.IsAsciiLetter(c) || c == '-').ToLowerInvariant();
        if (role.Length == 0)
        {
            throw Invalid(part, "a role part with no role");
        }

        var query = new AxRoleQuery { Role = role };
        foreach (var (name, value, flag) in reader.ReadAttributes())
        {
            query = name switch
            {
                "name" => NameCriterion(part, query, value, flag),
                "checked" => query with { Checked = Tristate(part, name, value, flag) },
                "pressed" => query with { Pressed = Tristate(part, name, value, flag) },
                "disabled" => query with { Disabled = Boolean(part, name, value, flag) },
                "expanded" => query with { Expanded = Boolean(part, name, value, flag) },
                "selected" => query with { Selected = Boolean(part, name, value, flag) },
                "include-hidden" => query with { IncludeHidden = Boolean(part, name, value, flag) },
                "level" => query with { Level = Level(part, value, flag) },
                _ => throw Invalid(part, $"an unknown attribute '{name}'"),
            };
        }

        return query;
    }

    /// <summary>Puppeteer's <c>aria/</c> query: an exact accessible name, and a role when one is given.</summary>
    private static AxRoleQuery ParseAria(string part, string body)
    {
        var bracket = body.IndexOf('[');
        var name = AccessibleName.Flatten(bracket < 0 ? body : body[..bracket]);
        var query = new AxRoleQuery { Name = name.Length == 0 ? null : name, NameExact = true };
        if (bracket >= 0)
        {
            var reader = new Reader(part, body[bracket..]);
            foreach (var (attribute, value, flag) in reader.ReadAttributes())
            {
                if (value is not { Quoted: true } || flag is not null)
                {
                    throw Invalid(part, $"an aria attribute '{attribute}' that is not a quoted string");
                }

                query = attribute switch
                {
                    "role" => query with { Role = value.Value.Text.Trim().ToLowerInvariant() },
                    "name" => query with { Name = AccessibleName.Flatten(value.Value.Text) },
                    _ => throw Invalid(part, $"an unknown aria attribute '{attribute}'"),
                };
            }
        }

        return query.Name is null && query.Role is null ? throw Invalid(part, "an aria query with neither name nor role") : query;
    }

    private static TextQuery ParseText(string part, string body)
    {
        var text = body.Trim();
        var exact = false;
        if (text.Length >= 2 && text[0] is '"' or '\'')
        {
            var reader = new Reader(part, text);
            var value = reader.ReadQuoted();
            var flag = reader.ReadFlag();
            if (!reader.AtEnd)
            {
                throw Invalid(part, "text after a quoted string");
            }

            text = value;
            exact = flag != 'i';
        }

        return TextQuery.Create(text, exact) ?? throw Invalid(part, "an empty text");
    }

    /// <summary>
    /// A <c>has-text=</c> body: a case-insensitive substring, quoted or not, and the whole text exactly with a
    /// quoted string and Playwright's <c>s</c> flag.
    /// </summary>
    private static TextQuery ParseHasText(string part, string body)
    {
        var text = body.Trim();
        var exact = false;
        if (text.Length >= 2 && text[0] is '"' or '\'')
        {
            var reader = new Reader(part, text);
            var value = reader.ReadQuoted();
            var flag = reader.ReadFlag();
            if (!reader.AtEnd)
            {
                throw Invalid(part, "text after a quoted string");
            }

            text = value;
            exact = flag == 's';
        }

        return TextQuery.Create(text, exact) ?? throw Invalid(part, "an empty text");
    }

    private static AxRoleQuery NameCriterion(string part, AxRoleQuery query, Value? value, char? flag)
    {
        if (value is not { } name)
        {
            throw Invalid(part, "a name attribute with no value");
        }

        return query with { Name = AccessibleName.Flatten(name.Text), NameExact = flag == 's' };
    }

    private static string Tristate(string part, string name, Value? value, char? flag)
    {
        if (flag is not null)
        {
            throw Invalid(part, $"a case flag on '{name}'");
        }

        return value?.Text.ToLowerInvariant() switch
        {
            null or "true" => "true",
            "false" => "false",
            "mixed" => "mixed",
            _ => throw Invalid(part, $"a '{name}' that is not true, false or mixed"),
        };
    }

    private static bool Boolean(string part, string name, Value? value, char? flag)
    {
        if (flag is not null)
        {
            throw Invalid(part, $"a case flag on '{name}'");
        }

        return value?.Text.ToLowerInvariant() switch
        {
            null or "true" => true,
            "false" => false,
            _ => throw Invalid(part, $"a '{name}' that is not true or false"),
        };
    }

    private static int Level(string part, Value? value, char? flag)
    {
        return flag is null && value is { } level
            && int.TryParse(level.Text, NumberStyles.None, CultureInfo.InvariantCulture, out var number) && number > 0
            ? number
            : throw Invalid(part, "a level that is not a positive integer");
    }

    private static DomException Invalid(string target, string what)
        => new("SyntaxError", $"'{target}' is not a valid target: it has {what}.");

    /// <summary>Restores document order, and removes duplicates, after several scopes were searched.</summary>
    private static List<Element> InDocumentOrder(Document document, List<Element> found, Action checkpoint)
    {
        if (found.Count < 2)
        {
            return found;
        }

        var members = new HashSet<Element>(found);
        var ordered = new List<Element>(members.Count);
        foreach (var element in ContentDom.Descendants(document))
        {
            checkpoint();
            if (members.Contains(element))
            {
                ordered.Add(element);
                if (ordered.Count == members.Count)
                {
                    break;
                }
            }
        }

        return ordered;
    }

    private static bool IsInside(Node node, Node ancestor)
    {
        for (var parent = node.ParentNode; parent is not null; parent = parent.ParentNode)
        {
            if (ReferenceEquals(parent, ancestor))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>What one part is asked: where to look, where to put what it finds, and whether one is enough.</summary>
    private readonly record struct Search(DomRealm Realm, Document Document, List<Node> Scopes, List<Element> Output,
        bool First, Action Checkpoint);

    private abstract class Part
    {
        /// <summary>Appends the elements this part finds under each scope, each scope's in document order.</summary>
        internal abstract void Query(Search search);
    }

    private sealed class CssPart(string selector) : Part
    {
        private CompiledSelector? _program;

        internal CompiledSelector Program(DomRealm realm) => _program ??= DomSelectors.Compile(realm, selector);

        internal override void Query(Search search)
        {
            var program = Program(search.Realm);
            foreach (var scope in search.Scopes)
            {
                if (search.First)
                {
                    if (DomSelectors.QuerySelector(search.Realm, scope, program) is { } match)
                    {
                        search.Output.Add(match);
                    }
                }
                else
                {
                    search.Output.AddRange(DomSelectors.QuerySelectorAll(search.Realm, scope, program));
                }
            }
        }
    }

    private sealed class ReferencePart(int id) : Part
    {
        internal override void Query(Search search)
        {
            if (AccessibilityTree.ElementFor(search.Document, id) is not { } element)
            {
                return;
            }

            foreach (var scope in search.Scopes)
            {
                if (scope is Document || IsInside(element, scope))
                {
                    search.Output.Add(element);
                    return;
                }
            }
        }
    }

    private sealed class RolePart(AxRoleQuery query) : Part
    {
        internal override void Query(Search search)
            => AccessibilityTree.Query(search.Document, search.Scopes, query, search.Output, search.First, search.Checkpoint);
    }

    private sealed class TextPart(TextQuery query, bool label) : Part
    {
        internal override void Query(Search search)
        {
            if (label)
            {
                query.ByLabel(search.Document, search.Scopes, search.Output, search.First, search.Checkpoint,
                    search.Realm.NativeReadCheckpoint, search.Realm.CancellationToken);
                return;
            }

            foreach (var scope in search.Scopes)
            {
                query.ByText(scope, search.Output, search.First, search.Checkpoint);
            }
        }
    }

    /// <summary>
    /// A part that narrows the matches of the part before it rather than searching inside them: Playwright's
    /// <c>nth=</c>, <c>visible=</c> and <c>internal:has-text=</c>, which a locator's <c>First</c>, <c>Nth</c>,
    /// <c>Filter</c> and <c>visible=true</c> serialize to.
    /// </summary>
    /// <remarks>
    /// Its scopes are the previous part's matches in document order, and what it keeps stays in that order.
    /// </remarks>
    private abstract class FilterPart : Part
    {
        internal override void Query(Search search)
        {
            foreach (var scope in search.Scopes)
            {
                search.Checkpoint();
                if (scope is Element element && Keeps(search, element))
                {
                    search.Output.Add(element);
                }
            }
        }

        protected virtual bool Keeps(Search search, Element element) => true;
    }

    /// <summary><c>nth=<i>n</i></c>: the zero-based match, or a negative offset from the last.</summary>
    private sealed class NthPart(int index) : FilterPart
    {
        internal override void Query(Search search) => Pick(search);

        private void Pick(Search search)
        {
            var resolved = index >= 0 ? index : search.Scopes.Count + index;
            if ((uint) resolved < (uint) search.Scopes.Count && search.Scopes[resolved] is Element element)
            {
                search.Output.Add(element);
            }
        }
    }

    /// <summary><c>visible=true|false</c>: the matches that do, or do not, pass <c>checkVisibility</c>.</summary>
    private sealed class VisiblePart(bool visible) : FilterPart
    {
        protected override bool Keeps(Search search, Element element)
            => Layout.LayoutMembers.IsCheckVisible(search.Realm, element, checkOpacity: false, checkVisibility: true) == visible;
    }

    /// <summary><c>has-text=</c> / <c>has-not-text=</c>: the matches whose text does, or does not, contain it.</summary>
    private sealed class HasTextPart(TextQuery query, bool has) : FilterPart
    {
        protected override bool Keeps(Search search, Element element)
            => query.Matches(TextQuery.ElementText(element)) == has;
    }

    private readonly record struct Value(string Text, bool Quoted);

    /// <summary>A cursor over one locator part's attribute list.</summary>
    private sealed class Reader(string part, string text)
    {
        private int _position;

        internal bool AtEnd
        {
            get
            {
                SkipSpaces();
                return _position >= text.Length;
            }
        }

        internal string ReadWhile(Func<char, bool> accept)
        {
            SkipSpaces();
            var start = _position;
            while (_position < text.Length && accept(text[_position]))
            {
                _position++;
            }

            return text[start.._position];
        }

        /// <summary>Reads <c>[name]</c>, <c>[name=value]</c> and <c>[name="value"flag]</c> to the end of the part.</summary>
        internal IEnumerable<(string Name, Value? Value, char? Flag)> ReadAttributes()
        {
            while (!AtEnd)
            {
                Expect('[');
                var name = ReadWhile(static c => char.IsAsciiLetter(c) || c == '-').ToLowerInvariant();
                if (name.Length == 0)
                {
                    throw Invalid(part, "an attribute with no name");
                }

                Value? value = null;
                char? flag = null;
                SkipSpaces();
                if (Peek() == '=')
                {
                    _position++;
                    SkipSpaces();
                    if (Peek() is '"' or '\'')
                    {
                        value = new Value(ReadQuoted(), Quoted: true);
                        flag = ReadFlag();
                    }
                    else
                    {
                        var bare = ReadWhile(static c => c != ']').Trim();
                        value = bare.Length == 0 ? throw Invalid(part, $"an attribute '{name}' with an empty value") : new Value(bare, Quoted: false);
                    }
                }

                Expect(']');
                yield return (name, value, flag);
            }
        }

        /// <summary>Reads a string in double or single quotes, in which a backslash escapes the next character.</summary>
        internal string ReadQuoted()
        {
            SkipSpaces();
            var quote = Peek();
            _position++;
            var builder = new StringBuilder();
            while (_position < text.Length)
            {
                var c = text[_position++];
                if (c == quote)
                {
                    return builder.ToString();
                }

                if (c == '\\' && _position < text.Length)
                {
                    c = text[_position++];
                }

                builder.Append(c);
            }

            throw Invalid(part, "an unterminated string");
        }

        /// <summary>Reads the <c>i</c> or <c>s</c> case flag that may follow a closing quote.</summary>
        internal char? ReadFlag()
        {
            SkipSpaces();
            if (Peek() is 'i' or 'I' or 's' or 'S')
            {
                return char.ToLowerInvariant(text[_position++]);
            }

            return null;
        }

        private char Peek() => _position < text.Length ? text[_position] : '\0';

        private void Expect(char expected)
        {
            SkipSpaces();
            if (Peek() != expected)
            {
                throw Invalid(part, $"'{(_position < text.Length ? text[_position].ToString() : "the end")}' where '{expected}' was expected");
            }

            _position++;
        }

        private void SkipSpaces()
        {
            while (_position < text.Length && char.IsWhiteSpace(text[_position]))
            {
                _position++;
            }
        }
    }
}
