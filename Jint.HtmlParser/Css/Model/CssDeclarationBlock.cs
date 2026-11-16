using CssTextOperations = Jint.HtmlParser.Css.Values.CssText;
using Jint.HtmlParser.Css.Model.Syntax;
using Jint.HtmlParser.Css.Serialization;
using Jint.HtmlParser.Css.Syntax;
using Jint.HtmlParser.Css.Values;
using Jint.HtmlParser.Css.Values.Properties;

namespace Jint.HtmlParser.Css.Model;

// CSSOM declaration order and mutation, with renderless text values rather than typed grammars.
// https://drafts.csswg.org/cssom/#css-declaration-blocks
internal sealed class CssDeclarationBlock
{
    private CssDeclaration[] _entries = [];
    private ulong _version;
    private readonly CssDeclarationContext _context;
    private CssRule? _owner;

    private CssDeclarationBlock(CssDeclarationContext context) => _context = context;
    internal void AttachTo(CssRule owner)
    {
        if (_owner is not null) throw new InvalidOperationException("A declaration block already has an owner.");
        _owner = owner;
    }

    internal static CssDeclarationBlock Parse(string source, CssDeclarationContext context = CssDeclarationContext.Style,
        CssParseOptions? options = null, CancellationToken cancellationToken = default) =>
        Parse(source, context, options, new CssValueWork(cancellationToken), cancellationToken);

    internal static CssDeclarationBlock Parse(string source, CssDeclarationContext context,
        CssParseOptions? options, CssValueWork work, CancellationToken cancellationToken)
    {
        using var parser = new CssSyntaxParser(source, options, cancellationToken, work.CheckCancellation);
        var declarations = parser.ParseDeclarationList();
        return FromDeclarations(source, declarations, context, options?.Limits.MaxNestingDepth ?? 0, work);
    }

    internal static CssDeclarationBlock FromDeclarations(string source, IReadOnlyList<CssDeclarationSyntax> declarations,
        CssDeclarationContext context, int maximumNestingDepth, CssValueWork work)
    {
        // Superseded entries become holes so the winners keep the order of their last insertion.
        var entries = new List<CssDeclaration?>(declarations.Count);
        Dictionary<string, int>? winners = null;
        var winnerCount = 0;
        var insertedNameLength = 0L;
        foreach (var declaration in declarations)
        {
            work.Charge(1);
            var name = CssPropertyRegistry.NormalizeName(declaration.Name, work);
            if (!Accepts(name, context) || context == CssDeclarationContext.FontFace && declaration.IsImportant) continue;
            var span = declaration.ValueSerializationSpan;
            work.Charge(span.Length);
            var value = string.Concat(source.AsSpan(span.Start, span.Length), declaration.ValueTermination);
            if (value.Length == 0 && name.StartsWith("--", StringComparison.Ordinal)) value = " ";
            if (value.Length == 0) continue;
            if (context == CssDeclarationContext.Style)
            {
                if (!CssLayoutValues.Accepts(name, declaration.Value, work)) continue;
                value = CssLayoutValues.NormalizeKeywords(name, value, declaration.Value, work);
            }
            if (context == CssDeclarationContext.FontFace || CssPropertyRegistry.Find(name) is not { Longhands.Count: > 0 })
            {
                Add(Longhand(name, value, declaration.IsImportant, declaration.Span, context, work), work);
                continue;
            }
            foreach (var entry in Expand(name, value, declaration.IsImportant, declaration.Span, context, work))
            {
                Add(entry, work);
            }
        }
        while (insertedNameLength > 0)
        {
            var charge = (int) Math.Min(insertedNameLength, int.MaxValue);
            work.Charge(charge);
            insertedNameLength -= charge;
        }
        var result = new CssDeclaration[winnerCount];
        var count = 0;
        foreach (var entry in entries)
        {
            if (entry is not null) result[count++] = entry;
        }
        work.CheckCancellation();
        return new(context) { _entries = result };

        void Add(CssDeclaration entry, CssValueWork work)
        {
            work.Charge(entry.Name.Length);
            insertedNameLength += entry.Name.Length;
            var previous = FindWinner(entry.Name);
            if (previous >= 0)
            {
                if (entries[previous]!.IsImportant && !entry.IsImportant) return;
                entries[previous] = null;
                winnerCount--;
            }
            if (winners is not null) winners[entry.Name] = entries.Count;
            entries.Add(entry);
            winnerCount++;
        }

        // Linear scans stay cheap for typical blocks; wide ones switch to an index.
        int FindWinner(string name)
        {
            if (winners is null && entries.Count >= 16)
            {
                winners = new Dictionary<string, int>(StringComparer.Ordinal);
                for (var i = 0; i < entries.Count; i++)
                {
                    if (entries[i] is { } winner) winners[winner.Name] = i;
                }
            }
            if (winners is not null) return winners.GetValueOrDefault(name, -1);
            for (var i = entries.Count - 1; i >= 0; i--)
            {
                if (entries[i]?.Name == name) return i;
            }
            return -1;
        }
    }

    private static bool Accepts(string name, CssDeclarationContext context) =>
        context == CssDeclarationContext.FontFace
            ? name is "font-family" or "src" or "font-style" or "font-weight" or "font-stretch" or "font-width" or
                "font-display" or "unicode-range" or "font-feature-settings" or "font-variation-settings" or
                "font-named-instance" or "font-language-override" or "ascent-override" or "descent-override" or
                "line-gap-override" or "size-adjust"
            : name.StartsWith("--", StringComparison.Ordinal) || CssPropertyRegistry.Find(name) is not null;

    internal int Count => _entries.Length;
    internal string GetPropertyName(int index) => _entries[index].Name;
    internal CssDeclaration GetDeclaration(int index) => _entries[index];
    internal CssMutationStamp Stamp => new(_version);
    internal string CssText => Serialize(new CssValueWork(default));

    internal CssDeclaration[] ResolveAll(CssValueWork work)
    {
        work = ReadWork(work);
        work.Charge(_entries.Length);
        work.CheckCancellation();
        return _entries;
    }

    internal CssDeclaration? ResolveProperty(string name, CssValueWork work)
    {
        work = ReadWork(work);
        work.CheckCancellation();
        foreach (var entry in _entries)
        {
            work.Charge(1);
            if (CssTextOperations.Equals(entry.Name, name, work)) return entry;
        }
        work.CheckCancellation();
        return null;
    }

    internal bool HasPropertyInput(string name, CssValueWork work) => ResolveProperty(name, work) is not null;

    internal IEnumerable<string> CustomPropertyNames(CssValueWork work)
    {
        work = ReadWork(work);
        foreach (var entry in _entries)
        {
            work.Charge(1);
            if (entry.Name.StartsWith("--", StringComparison.Ordinal)) yield return entry.Name;
        }
    }

    internal string GetPropertyValue(string name) => GetPropertyValue(name, new CssValueWork(default));
    internal string GetPropertyValue(string name, CssValueWork work)
    {
        work = ReadWork(work);
        name = CssPropertyRegistry.NormalizeName(name, work);
        var value = Shorthand(name) is { } shorthand ? ShorthandValue(_entries, shorthand, work) :
            ResolveProperty(name, work) is { PendingShorthand: null } declaration ? declaration.Value : "";
        work.CheckCancellation();
        return value;
    }

    internal string GetPropertyPriority(string name) => GetPropertyPriority(name, new CssValueWork(default));
    internal string GetPropertyPriority(string name, CssValueWork work)
    {
        work = ReadWork(work);
        name = CssPropertyRegistry.NormalizeName(name, work);
        if (Shorthand(name) is { } shorthand)
        {
            foreach (var longhand in shorthand.Longhands)
            {
                work.Charge(1);
                if (ResolveProperty(longhand, work) is not { IsImportant: true }) return "";
            }
            return "important";
        }
        return ResolveProperty(name, work) is { IsImportant: true } ? "important" : "";
    }

    internal void SetProperty(string name, string value, string? priority = null,
        CssParseOptions? options = null, CancellationToken cancellationToken = default) =>
        SetProperty(name, value, priority, options, new CssValueWork(cancellationToken), cancellationToken);

    internal void SetProperty(string name, string value, string? priority, CssParseOptions? options,
        CssValueWork work, CancellationToken cancellationToken = default)
    {
        work = ReadWork(work);
        name = CssPropertyRegistry.NormalizeName(name, work);
        if (value.Length == 0) { RemoveProperty(name, work); return; }
        if (!Accepts(name, _context) || !string.IsNullOrEmpty(priority) &&
            !CssAscii.EqualsIgnoreCase(priority, "important")) return;
        using var parser = new CssSyntaxParser(value, options, cancellationToken, work.CheckCancellation);
        var values = parser.ParseComponentValues();
        foreach (var component in values)
        {
            work.Charge(1);
            if (component.Kind == CssComponentKind.Token &&
                (component.Token.Kind is CssTokenKind.Semicolon or CssTokenKind.BadString or CssTokenKind.BadUrl ||
                component.Token.Kind == CssTokenKind.Delim && component.Token.Delimiter == '!')) return;
        }
        var span = parser.TrimLexicalBoundaryWhitespace(0, value.Length, values);
        var text = string.Concat(value.AsSpan(span.Start, span.Length),
            parser.ValueTermination(values, new CssSourceSpan(0, value.Length), work));
        if (text.Length == 0) text = name.StartsWith("--", StringComparison.Ordinal) ? " " : "";
        if (text.Length == 0) return;
        if (_context == CssDeclarationContext.Style)
        {
            if (!CssLayoutValues.Accepts(name, values, work)) return;
            text = CssLayoutValues.NormalizeKeywords(name, text, values, work);
        }
        var replacement = Expand(name, text, !string.IsNullOrEmpty(priority), default, _context, work);
        if (replacement.Length == 0) return;
        var entries = new List<CssDeclaration>(_entries);
        foreach (var entry in replacement)
        {
            var index = -1;
            for (var i = 0; i < entries.Count; i++)
            {
                work.Charge(1);
                if (CssTextOperations.Equals(entries[i].Name, entry.Name, work)) { index = i; break; }
            }
            if (index < 0) entries.Add(entry);
            else entries[index] = entry;
        }
        Commit(entries.ToArray(), work);
    }

    internal string RemoveProperty(string name) => RemoveProperty(name, new CssValueWork(default));
    internal string RemoveProperty(string name, CssValueWork work)
    {
        work = ReadWork(work);
        name = CssPropertyRegistry.NormalizeName(name, work);
        var old = GetPropertyValue(name, work);
        var targets = Shorthand(name)?.Longhands ?? [name];
        var entries = new List<CssDeclaration>();
        foreach (var entry in _entries)
        {
            work.Charge(1);
            if (!targets.Contains(entry.Name)) entries.Add(entry);
        }
        if (entries.Count != _entries.Length) Commit(entries.ToArray(), work);
        return old;
    }

    internal void ReplaceText(string source, CssParseOptions? options = null, CancellationToken cancellationToken = default) =>
        ReplaceText(source, options, new CssValueWork(cancellationToken), cancellationToken);
    internal void ReplaceText(string source, CssParseOptions? options, CssValueWork work, CancellationToken cancellationToken)
    {
        work = ReadWork(work);
        Commit(Parse(source, _context, options, work, cancellationToken)._entries, work);
    }

    internal void ReplaceDeclarations(string source, IReadOnlyList<CssDeclarationSyntax> declarations,
        int maximumNestingDepth, CssValueWork work)
    {
        work = ReadWork(work);
        Commit(FromDeclarations(source, declarations, _context, maximumNestingDepth, work)._entries, work);
    }

    private CssValueWork ReadWork(CssValueWork work)
    {
        var stamp = Stamp;
        return CssValueWork.Guard(work, () =>
        {
            work.CheckCancellation();
            if (Stamp != stamp) throw new InvalidOperationException("The CSS declaration changed during the operation.");
        });
    }

    private void Commit(CssDeclaration[] entries, CssValueWork work)
    {
        work.CheckCancellation();
        _entries = entries;
        CssMutationStamp.Advance(ref _version);
        _owner?.Changed();
    }

    internal CssDeclarationBlock Copy(CssValueWork work)
    {
        work.CheckCancellation();
        return new(_context) { _entries = _entries };
    }

    private CssPropertyMetadata? Shorthand(string name) =>
        _context == CssDeclarationContext.Style && CssPropertyRegistry.Find(name) is { Longhands.Count: > 0 } entry ? entry : null;

    private static CssDeclaration[] Expand(string name, string text, bool important, CssSourceSpan span,
        CssDeclarationContext context, CssValueWork work)
    {
        if (context == CssDeclarationContext.FontFace || CssPropertyRegistry.Find(name) is not { Longhands.Count: > 0 } metadata)
            return [Longhand(name, text, important, span, context, work)];
        var result = new CssDeclaration[metadata.Longhands.Count];
        // CSS Variables 1 §3.2: a shorthand with var() cannot be split before substitution; every longhand
        // keeps the shorthand's text until the cascade substitutes and expands it.
        if (text.Contains("var(", StringComparison.Ordinal))
        {
            for (var i = 0; i < result.Length; i++)
            {
                work.Charge(1);
                result[i] = new(metadata.Longhands[i], text, important, span, name);
            }
            return result;
        }
        if (ExpandValues(metadata, text, work) is not { } values) return [];
        for (var i = 0; i < result.Length; i++)
        {
            work.Charge(1);
            result[i] = new(metadata.Longhands[i], NormalizeValue(metadata.Longhands[i], values[i], work), important, span);
        }
        return result;
    }

    /// <summary>
    /// One longhand of a shorthand whose var() references the cascade has substituted, or null when the
    /// substituted text is not a valid value of the shorthand (invalid at computed-value time).
    /// </summary>
    internal static string? PendingLonghandValue(string shorthand, string longhand, string substituted, CssValueWork work)
    {
        if (CssPropertyRegistry.Find(shorthand) is not { Longhands.Count: > 0 } metadata ||
            !CssLayoutValues.AcceptsText(shorthand, substituted, work, out var normalized) ||
            ExpandValues(metadata, normalized, work) is not { } values) return null;
        for (var i = 0; i < values.Length; i++)
        {
            work.Charge(1);
            if (metadata.Longhands[i] == longhand) return NormalizeValue(longhand, values[i], work);
        }
        return null;
    }

    private static string[]? ExpandValues(CssPropertyMetadata metadata, string text, CssValueWork work)
    {
        var name = metadata.Name;
        var parts = CssTextOperations.Split(text, work);
        if (parts.Length == 0) return null;
        if (parts.Length == 1 && CssTextOperations.IsWide(parts[0])) return Enumerable.Repeat(parts[0], metadata.Longhands.Count).ToArray();
        if (CssBoxShorthands.Handles(name)) return CssBoxShorthands.Expand(name, text, work);
        if (CssMotionShorthands.Handles(name)) return CssMotionShorthands.Expand(name, text, work);
        if (name == "flex")
        {
            return text switch
            {
                "none" => ["0", "0", "auto"],
                "auto" => ["1", "1", "auto"],
                _ => ExpandFlex(parts),
            };
        }
        if (name == "flex-flow") return parts[0] is "wrap" or "nowrap" or "wrap-reverse"
            ? [parts.Length > 1 ? parts[1] : "row", parts[0]]
            : [parts[0], parts.Length > 1 ? parts[1] : "nowrap"];
        if (metadata.Longhands.Count == 4)
        {
            if (parts.Length > 4) return null;
            return [parts[0], parts.Length > 1 ? parts[1] : parts[0], parts.Length > 2 ? parts[2] : parts[0],
                parts.Length > 3 ? parts[3] : parts.Length > 1 ? parts[1] : parts[0]];
        }
        return [parts[0], parts.Length > 1 ? parts[1] : parts[0]];
    }

    /// <summary>
    /// https://drafts.csswg.org/css-flexbox/#flex-property — <c>[ &lt;'flex-grow'&gt; &lt;'flex-shrink'&gt;? ||
    /// &lt;'flex-basis'&gt; ]</c>: the factors are the numbers, the first growth and the next shrinkage, and the
    /// basis is the one other part, before or after them. An omitted shrinkage is 1, an omitted basis 0% and,
    /// when only a basis is given, the growth is 1. "A unitless zero that is not already preceded by two flex
    /// factors must be interpreted as a flex factor." So <c>0 auto</c> is <c>0 1 auto</c>, not a shrinkage of
    /// <c>auto</c>.
    /// </summary>
    private static string[] ExpandFlex(string[] parts)
    {
        string? grow = null, shrink = null, basis = null;
        foreach (var part in parts)
        {
            var isFactor = IsFlexFactor(part) && (part != "0" || shrink is null);
            if (isFactor && grow is null && basis is null) grow = part;
            else if (isFactor && grow is not null && shrink is null && basis is null) shrink = part;
            else if (isFactor && grow is null) grow = part;
            else if (isFactor && shrink is null) shrink = part;
            else basis ??= part;
        }

        return grow is null
            ? ["1", "1", basis ?? "0%"]
            : [grow, shrink ?? "1", basis ?? "0%"];
    }

    private static bool IsFlexFactor(string part) =>
        part.Length > 0 && double.TryParse(part, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out _);

    private static CssDeclaration Longhand(string name, string text, bool important, CssSourceSpan span,
        CssDeclarationContext context, CssValueWork work) =>
        new(name, context == CssDeclarationContext.FontFace ? text : NormalizeValue(name, text, work), important, span);

    private static string NormalizeValue(string name, string text, CssValueWork work)
    {
        work.Charge(text.Length);
        if (name.StartsWith("--", StringComparison.Ordinal)) return text;
        var zeroed = CssTextOperations.WithLeadingZero(text);
        if (!ReferenceEquals(zeroed, text)) return zeroed;
        if (text == "0" && (name is "width" or "height" or "top" or "right" or "bottom" or "left" or "flex-basis" ||
            name.StartsWith("margin-", StringComparison.Ordinal) || name.StartsWith("padding-", StringComparison.Ordinal) ||
            name.EndsWith("-width", StringComparison.Ordinal) || name.EndsWith("-height", StringComparison.Ordinal) ||
            name.EndsWith("-gap", StringComparison.Ordinal))) return "0px";
        return text;
    }

    // computed: the entries are computed values, whose comma-separated lists repeat to the shorthand's layer
    // count instead of leaving it without a serialization.
    internal static string ShorthandValue(CssDeclaration[] entries, CssPropertyMetadata shorthand, CssValueWork work,
        bool computed = false)
    {
        var values = new string[shorthand.Longhands.Count];
        bool? important = null;
        var pending = false;
        for (var i = 0; i < values.Length; i++)
        {
            CssDeclaration? found = null;
            foreach (var entry in entries)
            {
                work.Charge(1);
                if (entry.Name == shorthand.Longhands[i]) { found = entry; break; }
            }
            if (found is null || important is not null && found.IsImportant != important) return "";
            important = found.IsImportant;
            // A pending value serializes only through the shorthand that declared it, and only whole.
            if (found.PendingShorthand is not null || pending)
            {
                if (found.PendingShorthand != shorthand.Name || i > 0 && !pending || i > 0 && found.Value != values[0]) return "";
                pending = true;
            }
            values[i] = found.Value;
        }
        if (pending) return values[0];
        work.Charge(values.Sum(static value => value.Length));
        if (values.Any(CssTextOperations.IsWide) && values.Any(value => value != values[0])) return "";
        if (values.All(value => value == values[0]) && CssTextOperations.IsWide(values[0])) return values[0];
        if (CssBoxShorthands.Handles(shorthand.Name)) return CssBoxShorthands.Serialize(shorthand.Name, values, work);
        if (CssMotionShorthands.Handles(shorthand.Name)) return CssMotionShorthands.Serialize(shorthand.Name, values, computed, work);
        var count = values.Length;
        if (count == 4 && values[3] == values[1]) count--;
        if (count == 3 && shorthand.Name != "flex" && values[2] == values[0]) count--;
        if (count == 2 && shorthand.Name != "flex-flow" && values[1] == values[0]) count--;
        return string.Join(" ", values, 0, count);
    }

    internal string SerializeSource(CssValueWork work) => Serialize(work);
    internal string Serialize(CssValueWork work)
    {
        var builder = new ValueStringBuilder(stackalloc char[256]);
        try
        {
            AppendTo(ref builder, work);
            return builder.ToString();
        }
        finally
        {
            builder.Dispose();
        }
    }

    // Appends the serialization and returns the number of characters written.
    internal int AppendTo(ref ValueStringBuilder builder, CssValueWork work)
    {
        work = ReadWork(work);
        work.CheckCancellation();
        var start = builder.Length;
        // Declaration names are unique, so only longhands folded into a shorthand repeat.
        HashSet<string>? folded = null;
        foreach (var entry in _entries)
        {
            work.Charge(1);
            if (folded?.Contains(entry.Name) == true) continue;
            var name = entry.Name;
            var value = entry.Value;
            if (_context == CssDeclarationContext.Style)
            {
                foreach (var metadata in CssPropertyRegistry.ShorthandsCovering(name))
                {
                    work.Charge(1);
                    if (folded is not null && AnyFolded(metadata.Longhands, folded)) continue;
                    var shorthand = ShorthandValue(_entries, metadata, work);
                    if (shorthand.Length == 0) continue;
                    name = metadata.Name;
                    value = shorthand;
                    folded ??= new HashSet<string>(StringComparer.Ordinal);
                    foreach (var longhand in metadata.Longhands) { work.Charge(1); folded.Add(longhand); }
                    break;
                }
            }
            if (builder.Length != start) builder.Append(' ');
            builder.Append(CssSyntaxSerializer.SerializeIdentifier(name, work));
            builder.Append(": ");
            work.Charge(value.Length);
            if (value != " ") builder.Append(value);
            if (entry.IsImportant) builder.Append(" !important");
            builder.Append(';');
        }
        work.CheckCancellation();
        return builder.Length - start;

        static bool AnyFolded(IReadOnlyList<string> longhands, HashSet<string> folded)
        {
            foreach (var longhand in longhands)
            {
                if (folded.Contains(longhand)) return true;
            }
            return false;
        }
    }
}

// PendingShorthand names the shorthand whose unsubstituted var() text this longhand holds as its Value.
internal sealed record CssDeclaration(string Name, string Value, bool IsImportant, CssSourceSpan Span,
    string? PendingShorthand = null);
