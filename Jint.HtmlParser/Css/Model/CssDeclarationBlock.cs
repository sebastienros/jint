using CssTextOperations = Jint.HtmlParser.Css.Values.CssText;
using System.Text;
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
        var entries = new List<CssDeclaration>();
        var winners = new Dictionary<string, CssDeclaration>(StringComparer.Ordinal);
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
            foreach (var entry in Expand(name, value, declaration.IsImportant, declaration.Span, context, work))
            {
                work.Charge(entry.Name.Length);
                if (winners.TryGetValue(entry.Name, out var previous) && previous.IsImportant && !entry.IsImportant) continue;
                winners[entry.Name] = entry;
                entries.Add(entry);
            }
        }
        var result = new List<CssDeclaration>();
        foreach (var entry in entries)
        {
            work.Charge(entry.Name.Length);
            if (ReferenceEquals(winners[entry.Name], entry)) result.Add(entry);
        }
        work.CheckCancellation();
        return new(context) { _entries = result.ToArray() };
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
            ResolveProperty(name, work)?.Value ?? "";
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
        var replacement = Expand(name, text, !string.IsNullOrEmpty(priority), default, _context, work);
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
        if (context == CssDeclarationContext.FontFace) return [new(name, text, important, span)];
        if (CssPropertyRegistry.Find(name) is not { Longhands.Count: > 0 } metadata)
            return [new(name, NormalizeValue(name, text, work), important, span)];
        var parts = CssTextOperations.Split(text, work);
        if (parts.Length == 0) return [];
        string[] values;
        if (parts.Length == 1 && CssTextOperations.IsWide(parts[0])) values = Enumerable.Repeat(parts[0], metadata.Longhands.Count).ToArray();
        else if (name == "flex")
        {
            values = text switch
            {
                "none" => ["0", "0", "auto"],
                "auto" => ["1", "1", "auto"],
                _ => parts.Length == 1 && !char.IsDigit(parts[0][0]) ? ["1", "1", parts[0]] :
                    [parts[0], parts.Length > 1 ? parts[1] : "1", parts.Length > 2 ? parts[2] : "0%"]
            };
        }
        else if (name == "flex-flow") values = parts[0] is "wrap" or "nowrap" or "wrap-reverse"
            ? [parts.Length > 1 ? parts[1] : "row", parts[0]]
            : [parts[0], parts.Length > 1 ? parts[1] : "nowrap"];
        else if (metadata.Longhands.Count == 4)
            values = [parts[0], parts.Length > 1 ? parts[1] : parts[0], parts.Length > 2 ? parts[2] : parts[0],
                parts.Length > 3 ? parts[3] : parts.Length > 1 ? parts[1] : parts[0]];
        else values = [parts[0], parts.Length > 1 ? parts[1] : parts[0]];
        var result = new CssDeclaration[metadata.Longhands.Count];
        for (var i = 0; i < result.Length; i++)
        {
            work.Charge(1);
            result[i] = new(metadata.Longhands[i], NormalizeValue(metadata.Longhands[i], values[i], work), important, span);
        }
        return result;
    }

    private static string NormalizeValue(string name, string text, CssValueWork work)
    {
        work.Charge(text.Length);
        if (name.StartsWith("--", StringComparison.Ordinal)) return text;
        if (text.StartsWith('.') && text.Length > 1 && char.IsAsciiDigit(text[1])) return "0" + text;
        if (text.StartsWith("-.", StringComparison.Ordinal) && text.Length > 2 && char.IsAsciiDigit(text[2])) return "-0" + text[1..];
        if (text == "0" && (name is "width" or "height" or "top" or "right" or "bottom" or "left" or "flex-basis" ||
            name.StartsWith("margin-", StringComparison.Ordinal) || name.StartsWith("padding-", StringComparison.Ordinal) ||
            name.EndsWith("-width", StringComparison.Ordinal) || name.EndsWith("-height", StringComparison.Ordinal) ||
            name.EndsWith("-gap", StringComparison.Ordinal))) return "0px";
        return text;
    }

    internal static string ShorthandValue(CssDeclaration[] entries, CssPropertyMetadata shorthand, CssValueWork work)
    {
        var values = new string[shorthand.Longhands.Count];
        bool? important = null;
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
            values[i] = found.Value;
        }
        work.Charge(values.Sum(static value => value.Length));
        if (values.Any(CssTextOperations.IsWide) && values.Any(value => value != values[0])) return "";
        if (values.All(value => value == values[0]) && CssTextOperations.IsWide(values[0])) return values[0];
        var count = values.Length;
        if (count == 4 && values[3] == values[1]) count--;
        if (count == 3 && shorthand.Name != "flex" && values[2] == values[0]) count--;
        if (count == 2 && shorthand.Name != "flex-flow" && values[1] == values[0]) count--;
        return string.Join(" ", values, 0, count);
    }

    internal string SerializeSource(CssValueWork work) => Serialize(work);
    internal string Serialize(CssValueWork work)
    {
        work = ReadWork(work);
        work.CheckCancellation();
        var builder = new StringBuilder();
        var written = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in _entries)
        {
            work.Charge(1);
            if (written.Contains(entry.Name)) continue;
            var name = entry.Name;
            var value = entry.Value;
            if (_context == CssDeclarationContext.Style)
                foreach (var metadata in CssPropertyRegistry.Shorthands)
                {
                    work.Charge(1);
                    if (!metadata.Longhands.Contains(name) || metadata.Longhands.Any(written.Contains)) continue;
                    var shorthand = ShorthandValue(_entries, metadata, work);
                    if (shorthand.Length == 0) continue;
                    name = metadata.Name;
                    value = shorthand;
                    foreach (var longhand in metadata.Longhands) { work.Charge(1); written.Add(longhand); }
                    break;
                }
            written.Add(name);
            if (builder.Length != 0) builder.Append(' ');
            builder.Append(CssSyntaxSerializer.SerializeIdentifier(name, work)).Append(": ");
            work.Charge(value.Length);
            if (value != " ") builder.Append(value);
            if (entry.IsImportant) builder.Append(" !important");
            builder.Append(';');
        }
        work.CheckCancellation();
        return builder.ToString();
    }
}

internal sealed record CssDeclaration(string Name, string Value, bool IsImportant, CssSourceSpan Span);
