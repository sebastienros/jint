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
internal sealed class CssDeclarationBlock
{
    private CssDeclaration[] _entries = [];
    private ulong _version;
    private readonly CssDeclarationContext _context;

    private CssDeclarationBlock(CssDeclarationContext context) => _context = context;

    internal static CssDeclarationBlock Parse(string source, CssDeclarationContext context = CssDeclarationContext.Style,
        CssParseOptions? options = null, CancellationToken cancellationToken = default)
    {
        var syntax = new CssSyntaxParser(source, options, cancellationToken).ParseDeclarationList();
        return FromDeclarations(source, syntax, context, options?.Limits.MaxNestingDepth ?? 0,
            new CssValueWork(cancellationToken));
    }

    // A sheet/rule builder shares its C1 parse and work state. No syntax editor or sheet is retained.
    internal static CssDeclarationBlock FromDeclarations(string source, IReadOnlyList<CssDeclarationSyntax> declarations,
        CssDeclarationContext context, int maximumNestingDepth, CssValueWork work)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(declarations);
        ArgumentNullException.ThrowIfNull(work);
        if (!Enum.IsDefined(context)) throw new ArgumentOutOfRangeException(nameof(context));
        ArgumentOutOfRangeException.ThrowIfNegative(maximumNestingDepth);
        var result = new CssDeclarationBlock(context);
        result._entries = Build(source, declarations, context, maximumNestingDepth, work);
        work.CheckCancellation();
        return result;
    }

    internal int Count => _entries.Length;
    internal string GetPropertyName(int index) => _entries[index].Name;
    internal CssDeclaration GetDeclaration(int index) => _entries[index];
    internal CssMutationStamp Stamp => new(_version);
    internal string CssText => Serialize(new CssValueWork(default));

    internal string GetPropertyValue(string name) => GetPropertyValue(name, new CssValueWork(default));

    internal string GetPropertyValue(string name, CssValueWork work)
    {
        work.CheckCancellation();
        name = CssPropertyRegistry.NormalizeName(name, work);
        if (name == "overflow") return OverflowValue(_entries, work);
        var entry = Find(_entries, name, work);
        work.CheckCancellation();
        return entry is null || entry.PendingShorthand is not null ? "" : EntryValue(entry, work);
    }

    internal string GetPropertyPriority(string name) => GetPropertyPriority(name, new CssValueWork(default));

    internal string GetPropertyPriority(string name, CssValueWork work)
    {
        work.CheckCancellation();
        name = CssPropertyRegistry.NormalizeName(name, work);
        if (name == "overflow")
        {
            var x = Find(_entries, "overflow-x", work);
            var y = Find(_entries, "overflow-y", work);
            work.CheckCancellation();
            return x is { IsImportant: true } && y is { IsImportant: true } ? "important" : "";
        }
        var entry = Find(_entries, name, work);
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
        var parser = new CssSyntaxParser(value, options, cancellationToken);
        var components = parser.ParseComponentValues();
        var input = CssReferenceInput.FromComponents(value, components, options?.Limits.MaxNestingDepth ?? 0,
            new CssSourceSpan(0, value.Length), work);
        var result = CssPropertyParser.Parse(name, input, _context, work);
        RequireCompleted(name, result, new CssSourceSpan(0, value.Length));
        if (result.Status is not (CssPropertyStatus.Valid or CssPropertyStatus.Deferred)) return;
        var replacement = CopyEntries(work);
        Install(replacement, name, result.Value, !string.IsNullOrEmpty(priority),
            new CssSourceSpan(0, value.Length), work,
            LexicalText(result.Value, value, parser.TrimLexicalBoundaryWhitespace(0, value.Length, components), work),
            parser.ValueTermination(components, new CssSourceSpan(0, value.Length), work));
        Commit(replacement, work);
    }

    internal string RemoveProperty(string name) => RemoveProperty(name, new CssValueWork(default));

    internal string RemoveProperty(string name, CssValueWork work)
    {
        work.CheckCancellation();
        work.Charge(name.Length);
        name = CssPropertyRegistry.NormalizeName(name, work);
        if (CssPropertyParser.NameFailure(name, _context) is { } failure)
            RequireCompleted(name, failure, default);
        var oldValue = name == "overflow" ? OverflowValue(_entries, work) :
            Find(_entries, name, work) is { } entryValue && entryValue.PendingShorthand is null
                ? EntryValue(entryValue, work) : "";
        var replacement = new List<CssDeclaration>(_entries.Length);
        var removed = false;
        foreach (var entry in _entries)
        {
            work.Charge(1);
            if (CssSubstitutionArguments.Equals(entry.Name, name, work) || name == "overflow" && entry.Name is "overflow-x" or "overflow-y")
                removed = true;
            else replacement.Add(entry);
        }
        if (removed) Commit(replacement, work);
        return oldValue;
    }

    internal void ReplaceText(string source, CssParseOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var syntax = new CssSyntaxParser(source, options, cancellationToken).ParseDeclarationList();
        ReplaceDeclarations(source, syntax, options?.Limits.MaxNestingDepth ?? 0, new CssValueWork(cancellationToken));
    }

    internal void ReplaceDeclarations(string source, IReadOnlyList<CssDeclarationSyntax> declarations,
        int maximumNestingDepth, CssValueWork work)
    {
        var entries = Build(source, declarations, _context, maximumNestingDepth, work);
        work.CheckCancellation();
        _entries = entries;
        CssMutationStamp.Advance(ref _version);
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
                declaration.ValueSourceSpan, work);
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
        if (name != "overflow")
        {
            InstallEntry(entries, new CssDeclaration(name, value, important, span, null, lexicalText, termination), work, winners);
            return;
        }
        // Variables 1 §3.2: keep the shorthand's parsed value, shared by its pending longhands.
        // https://drafts.csswg.org/css-variables-1/#variables-in-shorthands
        var pending = value.Kind == CssPropertyValueKind.Deferred
            ? new CssPendingShorthand(name, value, lexicalText!, termination) : null;
        var x = pending is not null ? value : CssPropertyValue.Keyword(value.Text, value.Span);
        var y = pending is not null ? value : CssPropertyValue.Keyword(value.SecondKeyword ?? value.Text, value.Span);
        InstallEntry(entries, new CssDeclaration("overflow-x", x, important, span, pending), work, winners);
        InstallEntry(entries, new CssDeclaration("overflow-y", y, important, span, pending), work, winners);
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

    private List<CssDeclaration> CopyEntries(CssValueWork work)
    {
        var result = new List<CssDeclaration>(_entries.Length);
        foreach (var entry in _entries) { work.Charge(1); result.Add(entry); }
        work.CheckCancellation();
        return result;
    }

    private void Commit(List<CssDeclaration> entries, CssValueWork work)
    {
        work.CheckCancellation();
        var replacement = entries.ToArray();
        work.Charge(replacement.Length);
        work.CheckCancellation();
        _entries = replacement;
        CssMutationStamp.Advance(ref _version);
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

    private static string OverflowValue(CssDeclaration[] entries, CssValueWork work)
    {
        var x = Find(entries, "overflow-x", work);
        var y = Find(entries, "overflow-y", work);
        if (x is null || y is null || x.IsImportant != y.IsImportant) return "";
        if (x.PendingShorthand is { } pending)
            return ReferenceEquals(pending, y.PendingShorthand)
                ? CompleteLexicalValue(pending.LexicalSpecifiedText, pending.Termination, work) : "";
        if (y.PendingShorthand is not null || x.Value.Kind == CssPropertyValueKind.Deferred ||
            y.Value.Kind == CssPropertyValueKind.Deferred) return "";
        var first = x.Value.Serialize();
        var second = y.Value.Serialize();
        if (first == second) return first;
        // A CSS-wide keyword cannot be combined with another keyword in a shorthand.
        return IsWide(first) || IsWide(second) ? "" : first + " " + second;
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
        var builder = new StringBuilder();
        var overflow = OverflowValue(_entries, work);
        var overflowWritten = false;
        foreach (var entry in _entries)
        {
            work.Charge(1);
            var name = entry.Name;
            string value;
            if (name is "overflow-x" or "overflow-y" && overflow.Length != 0)
            {
                if (overflowWritten) continue;
                overflowWritten = true;
                name = "overflow";
                value = overflow;
            }
            else value = entry.PendingShorthand is null ? EntryValue(entry, work) : "";
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
