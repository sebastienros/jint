using System.Text;
using Jint.HtmlParser.Css.Model.Syntax;
using Jint.HtmlParser.Css.Serialization;
using Jint.HtmlParser.Css.Syntax;
using Jint.HtmlParser.Css.Values;
using Jint.HtmlParser.Css.Values.Properties;
using Jint.HtmlParser.Css.Values.References;

namespace Jint.HtmlParser.Css.Model;

// CSSOM §6.4.3 specified order, §6.6 declaration blocks and §6.6.1 mutations.
// https://drafts.csswg.org/cssom/#css-declaration-blocks
internal sealed partial class CssDeclarationBlock
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
        var result = ParseUnresolved(source, context, options, work, cancellationToken);
        result.ResolveAll(work);
        return result;
    }

    internal static CssDeclarationBlock ParseUnresolved(string source, CssDeclarationContext context,
        CssParseOptions? options, CssValueWork work, CancellationToken cancellationToken)
    {
        var syntax = new CssSyntaxParser(source, options, cancellationToken, work.CheckCancellation).ParseDeclarationList();
        return FromDeclarations(source, syntax, context, options?.Limits.MaxNestingDepth ?? 0,
            work);
    }

    // A sheet/rule builder retains its immutable C1 declarations/source and shares invocation work.
    // No syntax editor or second stylesheet model is retained.
    internal static CssDeclarationBlock FromDeclarations(string source, IReadOnlyList<CssDeclarationSyntax> declarations,
        CssDeclarationContext context, int maximumNestingDepth, CssValueWork work)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(declarations);
        ArgumentNullException.ThrowIfNull(work);
        if (!Enum.IsDefined(context)) throw new ArgumentOutOfRangeException(nameof(context));
        ArgumentOutOfRangeException.ThrowIfNegative(maximumNestingDepth);
        var result = new CssDeclarationBlock(context);
        result._raw = Retain(source, declarations, maximumNestingDepth, work);
        work.CheckCancellation();
        return result;
    }

    internal int Count => ResolveAll(new CssValueWork(default)).Length;
    internal string GetPropertyName(int index) => ResolveAll(new CssValueWork(default))[index].Name;
    internal CssDeclaration GetDeclaration(int index) => ResolveAll(new CssValueWork(default))[index];
    internal CssMutationStamp Stamp => new(_version);
    internal string CssText => Serialize(new CssValueWork(default));

    internal string GetPropertyValue(string name) => GetPropertyValue(name, new CssValueWork(default));

    internal string GetPropertyValue(string name, CssValueWork work)
    {
        work.CheckCancellation();
        name = CssPropertyRegistry.NormalizeName(name, work);
        if (Shorthand(name) is { } shorthand) return ShorthandValue(ResolveShorthand(shorthand, work), shorthand, work);
        var entry = ResolveProperty(name, work);
        work.CheckCancellation();
        return entry is null || entry.PendingShorthand is not null ? "" : EntryValue(entry, work);
    }

    internal string GetPropertyPriority(string name) => GetPropertyPriority(name, new CssValueWork(default));

    internal string GetPropertyPriority(string name, CssValueWork work)
    {
        work.CheckCancellation();
        name = CssPropertyRegistry.NormalizeName(name, work);
        if (Shorthand(name) is { } shorthand)
        {
            foreach (var longhand in shorthand.Longhands)
            {
                work.Charge(1);
                if (ResolveProperty(longhand, work) is not { IsImportant: true }) return "";
            }
            work.CheckCancellation();
            return "important";
        }
        var entry = ResolveProperty(name, work);
        work.CheckCancellation();
        return entry is { IsImportant: true } ? "important" : "";
    }

    internal void SetProperty(string name, string value, string? priority = null,
        CssParseOptions? options = null, CancellationToken cancellationToken = default) =>
        SetProperty(name, value, priority, options, new CssValueWork(cancellationToken), cancellationToken);

    internal void SetProperty(string name, string value, string? priority, CssParseOptions? options,
        CssValueWork work, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(work);
        work.CheckCancellation();
        work.Charge(name.Length);
        name = CssPropertyRegistry.NormalizeName(name, work);
        if (value.Length == 0)
        {
            RemoveProperty(name, work);
            return;
        }
        // CSSOM precedence: reject priority before parsing a nonempty value (including pending grammars).
        if (!string.IsNullOrEmpty(priority) && !CssAscii.EqualsIgnoreCase(priority, "important")) return;
        if (_context == CssDeclarationContext.Keyframe && !string.IsNullOrEmpty(priority)) return;
        var parser = new CssSyntaxParser(value, options, cancellationToken, work.CheckCancellation);
        var components = parser.ParseComponentValues();
        var input = CssReferenceInput.FromComponents(value, components, options?.Limits.MaxNestingDepth ?? 0,
            new CssSourceSpan(0, value.Length), work,
            parser.TrimLexicalBoundaryWhitespace(0, value.Length, components),
            parser.ValueTermination(components, new CssSourceSpan(0, value.Length), work));
        var result = CssPropertyParser.Parse(name, input, _context, work);
        RequireCompleted(name, result, new CssSourceSpan(0, value.Length));
        if (result.Status is not (CssPropertyStatus.Valid or CssPropertyStatus.Deferred)) return;
        var replacement = new List<CssDeclaration>();
        Install(replacement, name, result.Value, !string.IsNullOrEmpty(priority),
            new CssSourceSpan(0, value.Length), work,
            LexicalText(result.Value, value, parser.TrimLexicalBoundaryWhitespace(0, value.Length, components), work),
            parser.ValueTermination(components, new CssSourceSpan(0, value.Length), work));
        CommitTarget(name, replacement, work);
    }

    internal string RemoveProperty(string name) => RemoveProperty(name, new CssValueWork(default));

    internal string RemoveProperty(string name, CssValueWork work)
    {
        work.CheckCancellation();
        work.Charge(name.Length);
        name = CssPropertyRegistry.NormalizeName(name, work);
        if (CssPropertyParser.NameFailure(name, _context) is { } failure)
            RequireCompleted(name, failure, default);
        var oldValue = GetPropertyValue(name, work);
        CommitTarget(name, [], work, remove: true);
        return oldValue;
    }

    internal void ReplaceText(string source, CssParseOptions? options = null,
        CancellationToken cancellationToken = default)
        => ReplaceText(source, options, new CssValueWork(cancellationToken), cancellationToken);

    internal void ReplaceText(string source, CssParseOptions? options, CssValueWork work,
        CancellationToken cancellationToken)
    {
        var syntax = new CssSyntaxParser(source, options, cancellationToken, work.CheckCancellation).ParseDeclarationList();
        ReplaceDeclarations(source, syntax, options?.Limits.MaxNestingDepth ?? 0, work);
    }

    internal void ReplaceDeclarations(string source, IReadOnlyList<CssDeclarationSyntax> declarations,
        int maximumNestingDepth, CssValueWork work)
    {
        var replacement = FromDeclarations(source, declarations, _context, maximumNestingDepth, work);
        var entries = replacement.ResolveAll(work);
        work.CheckCancellation();
        _entries = entries;
        _raw = replacement._raw;
        _index = null;
        _resolved.Clear();
        _customResolved.Clear();
        _allResolved = true;
        CssMutationStamp.Advance(ref _version);
        _owner?.Changed();
    }

    private static CssDeclaration[] Build(string source, IReadOnlyList<CssDeclarationSyntax> declarations,
        CssDeclarationContext context, int depth, CssValueWork work)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(declarations);
        ArgumentNullException.ThrowIfNull(work);
        ArgumentOutOfRangeException.ThrowIfNegative(depth);
        work.CheckCancellation();
        var entries = new List<CssDeclaration>();
        var winners = new Dictionary<string, CssDeclaration>(new DeclarationNameComparer(work));
        for (var i = 0; i < declarations.Count; i++)
        {
            work.Charge(1);
            var declaration = declarations[i];
            if (context == CssDeclarationContext.Keyframe && declaration.IsImportant) continue;
            work.Charge(declaration.Name.Length);
            var name = CssPropertyRegistry.NormalizeName(declaration.Name, work);
            var input = CssReferenceInput.FromComponents(source, declaration.Value, depth,
                declaration.ValueSourceSpan, work, declaration.ValueSerializationSpan, declaration.ValueTermination);
            var result = CssPropertyParser.Parse(name, input, context, work);
            RequireCompleted(name, result, declaration.Span);
            if (result.Status is CssPropertyStatus.Valid or CssPropertyStatus.Deferred)
                Install(entries, name, result.Value, declaration.IsImportant, declaration.Span, work,
                    LexicalText(result.Value, source, declaration.ValueSerializationSpan, work),
                    declaration.ValueTermination, winners);
        }
        var ordered = new List<CssDeclaration>(winners.Count);
        foreach (var entry in entries)
        {
            work.Charge(entry.Name.Length);
            if (ReferenceEquals(winners[entry.Name], entry)) ordered.Add(entry);
        }
        work.CheckCancellation();
        var resultEntries = ordered.ToArray();
        work.Charge(resultEntries.Length);
        work.CheckCancellation();
        return resultEntries;
    }

    private static void RequireCompleted(string name, CssPropertyResult result, CssSourceSpan span)
    {
        if (result.Status == CssPropertyStatus.UnimplementedGrammar)
            throw new CssIncompleteGrammarException(name, result.Blocker!, span);
    }

    private static void Install(List<CssDeclaration> entries, string name, CssPropertyValue value,
        bool important, CssSourceSpan span, CssValueWork work, string? lexicalText = null,
        string termination = "", Dictionary<string, CssDeclaration>? winners = null)
    {
        var shorthand = Shorthand(name);
        if (shorthand is null)
        {
            InstallEntry(entries, new CssDeclaration(name, value, important, span, null, lexicalText, termination), work, winners);
            return;
        }
        // Variables 1 §3.2: one pending identity shared by all longhands until substitution.
        var pending = value.Kind == CssPropertyValueKind.Deferred
            ? new CssPendingShorthand(name, value, lexicalText!, termination) : null;
        for (var i = 0; i < shorthand.Longhands.Count; i++)
        {
            work.Charge(1);
            var expanded = pending is not null ? value : value.Kind == CssPropertyValueKind.Shorthand
                ? value.Components[i] : CssPropertyValue.Keyword(
                    i == 1 ? value.SecondKeyword ?? value.Text : value.Text, value.Span);
            InstallEntry(entries, new CssDeclaration(shorthand.Longhands[i], expanded, important, span, pending), work, winners);
        }
    }

    private static void InstallEntry(List<CssDeclaration> entries, CssDeclaration entry, CssValueWork work,
        Dictionary<string, CssDeclaration>? winners)
    {
        if (winners is not null)
        {
            work.Charge(entry.Name.Length);
            work.CheckCancellation();
            if (winners.TryGetValue(entry.Name, out var prior) && prior.IsImportant && !entry.IsImportant) return;
            winners[entry.Name] = entry;
            entries.Add(entry);
            work.CheckCancellation();
            return;
        }
        for (var i = 0; i < entries.Count; i++)
        {
            work.Charge(1);
            if (!CssSubstitutionArguments.Equals(entries[i].Name, entry.Name, work)) continue;
            entries[i] = entry;
            return;
        }
        entries.Add(entry);
    }

    private static CssDeclaration? Find(CssDeclaration[] entries, string name, CssValueWork work)
    {
        foreach (var entry in entries)
        {
            work.Charge(1);
            if (CssSubstitutionArguments.Equals(entry.Name, name, work)) return entry;
        }
        return null;
    }

    private static CssPropertyMetadata? Shorthand(string name) =>
        CssPropertyRegistry.Find(name, CssDeclarationContext.Style) is { Longhands.Count: > 0 } entry ? entry : null;

    internal static CssDeclaration[] ExpandValue(string name, CssPropertyValue value, CssValueWork work)
    {
        work.CheckCancellation();
        var entries = new List<CssDeclaration>();
        Install(entries, name, value, false, value.Span, work, value.Text, "");
        work.CheckCancellation();
        return entries.ToArray();
    }

    internal static string ShorthandValue(CssDeclaration[] entries, CssPropertyMetadata shorthand, CssValueWork work)
    {
        var first = Find(entries, shorthand.Longhands[0], work);
        if (first is null) return "";
        var values = new string[shorthand.Longhands.Count];
        var pending = first.PendingShorthand;
        for (var i = 0; i < values.Length; i++)
        {
            work.Charge(1);
            var entry = Find(entries, shorthand.Longhands[i], work);
            if (entry is null || entry.IsImportant != first.IsImportant) return "";
            if (pending is not null)
            {
                if (!ReferenceEquals(pending, entry.PendingShorthand) || pending.Name != shorthand.Name) return "";
            }
            else if (entry.PendingShorthand is not null || entry.Value.Kind == CssPropertyValueKind.Deferred) return "";
            else values[i] = entry.Value.Serialize();
        }
        if (pending is not null) return CompleteLexicalValue(pending.LexicalSpecifiedText, pending.Termination, work);
        var allEqual = true;
        var anyWide = false;
        for (var i = 0; i < values.Length; i++)
        {
            work.Charge(values[i].Length);
            allEqual &= CssSubstitutionArguments.Equals(values[i], values[0], work);
            anyWide |= IsWide(values[i]);
        }
        if (anyWide) return allEqual ? values[0] : "";
        if (shorthand.Grammar == CssPropertyGrammar.WhiteSpace)
            return CssWhiteSpacePropertyParser.Serialize(values[0], values[1], values[2], work);
        if (shorthand.Grammar == CssPropertyGrammar.TextAlign)
            return CssTextAlignPropertyParser.Serialize(values[0], values[1], work);
        work.CheckCancellation();
        // Pair shorthands copy their first value when omitted. Flex-flow and flex do not.
        var text = allEqual && shorthand.Grammar is CssPropertyGrammar.Overflow or CssPropertyGrammar.PlaceItems or CssPropertyGrammar.PlaceSelf
            ? values[0] : string.Join(" ", values);
        work.Charge(text.Length);
        work.CheckCancellation();
        return text;
    }

    private static bool IsWide(string value) => value is "initial" or "inherit" or "unset" or "revert" or "revert-layer" or "revert-rule";

    private static string EntryValue(CssDeclaration entry, CssValueWork work) =>
        CompleteLexicalValue(entry.LexicalSpecifiedText ?? entry.Value.Serialize(),
            entry.Value.Kind is CssPropertyValueKind.Custom or CssPropertyValueKind.Deferred ? entry.Termination : "", work);

    private static string CompleteLexicalValue(string value, string termination, CssValueWork work)
    {
        if (termination.Length == 0) return value;
        work.CheckCancellation();
        var result = string.Concat(value, termination);
        work.Charge(value.Length);
        work.Charge(termination.Length);
        work.CheckCancellation();
        return result;
    }

    // Variables 1 §4.1: preserve custom lexical representation (including comments). The C1
    // source range strips importance and only boundary whitespace tokens.
    private static string? LexicalText(CssPropertyValue value, string source,
        CssSourceSpan span, CssValueWork work)
    {
        if (value.Kind is not (CssPropertyValueKind.Custom or CssPropertyValueKind.Deferred)) return null;
        var start = span.Start;
        var end = checked(start + span.Length);
        if (start < 0 || end > source.Length) throw new ArgumentOutOfRangeException(nameof(span));
        work.CheckCancellation();
        var text = end == start ? " " : source.Substring(start, end - start);
        work.Charge(text.Length);
        work.CheckCancellation();
        return text;
    }

    internal string Serialize(CssValueWork work)
    {
        work.CheckCancellation();
        ResolveAll(work);
        var builder = new StringBuilder();
        var shorthandValues = new Dictionary<string, string>(StringComparer.Ordinal);
        var written = new HashSet<string>(StringComparer.Ordinal);
        foreach (var metadata in CssPropertyRegistry.Completed.Values)
        {
            work.Charge(1);
            if (metadata.Longhands.Count != 0)
                shorthandValues.Add(metadata.Name, ShorthandValue(_entries, metadata, work));
        }
        foreach (var entry in _entries)
        {
            work.Charge(1);
            var name = entry.Name;
            var value = entry.PendingShorthand is null ? EntryValue(entry, work) : "";
            var skip = false;
            foreach (var pair in shorthandValues)
            {
                work.Charge(1);
                if (pair.Value.Length == 0 || !CssPropertyRegistry.Completed[pair.Key].Longhands.Contains(name)) continue;
                if (!written.Add(pair.Key)) { skip = true; break; }
                name = pair.Key;
                value = pair.Value;
                break;
            }
            if (skip || value.Length == 0) continue;
            if (builder.Length != 0) builder.Append(' ');
            builder.Append(CssSyntaxSerializer.SerializeIdentifier(name, work)).Append(": ");
            work.CheckCancellation();
            if (value != " ") builder.Append(value);
            work.Charge(value.Length);
            if (entry.IsImportant) builder.Append(" !important");
            builder.Append(';');
        }
        work.CheckCancellation();
        var text = builder.ToString();
        work.Charge(text.Length);
        work.CheckCancellation();
        return text;
    }

    private sealed class DeclarationNameComparer(CssValueWork work) : IEqualityComparer<string>
    {
        public bool Equals(string? left, string? right) =>
            left is not null && right is not null && CssSubstitutionArguments.Equals(left, right, work);

        public int GetHashCode(string value) => unchecked((int) CssSubstitutionArguments.Hash(value, work));
    }
}

internal sealed record CssDeclaration(string Name, CssPropertyValue Value, bool IsImportant,
    CssSourceSpan Span, CssPendingShorthand? PendingShorthand, string? LexicalSpecifiedText = null, string Termination = "");

internal sealed record CssPendingShorthand(string Name, CssPropertyValue Value, string LexicalSpecifiedText, string Termination);

internal sealed class CssIncompleteGrammarException : NotSupportedException
{
    internal CssIncompleteGrammarException(string propertyName, string blocker, CssSourceSpan span)
        : base("Unimplemented CSS grammar: " + blocker)
    { PropertyName = propertyName; Blocker = blocker; Span = span; }

    internal string PropertyName { get; }
    internal string Blocker { get; }
    internal CssSourceSpan Span { get; }
}
