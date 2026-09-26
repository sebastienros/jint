using System.Text;
using Jint.HtmlParser.Css.Model.Syntax;
using Jint.HtmlParser.Css.Serialization;
using Jint.HtmlParser.Css.Values;
using Jint.HtmlParser.Css.Values.Properties;
using Jint.HtmlParser.Css.Values.References;

namespace Jint.HtmlParser.Css.Model;

internal sealed partial class CssDeclarationBlock
{
    private RawEntry[] _raw = [];
    private Dictionary<string, List<int>>? _index;
    private readonly Dictionary<string, CssDeclaration?> _resolved = new(StringComparer.Ordinal);
    private readonly Dictionary<string, CssCustomDeclaration?> _customResolved = new(StringComparer.Ordinal);
    private bool _allResolved;
    private const string Invalidated = "The native declaration view was invalidated by mutation.";

    // Retained C1 inputs are immutable. Only completed materialization is memoized.
    private sealed class Materialization(string? source, CssDeclarationSyntax? syntax, int depth, CssDeclaration[]? entries)
    {
        internal readonly string? Source = source;
        internal readonly CssDeclarationSyntax? Syntax = syntax;
        internal readonly int Depth = depth;
        internal CssDeclaration[]? Entries = entries;
    }
    private readonly record struct RawEntry(string Name, Materialization Input);

    private static RawEntry[] Retain(string source, IReadOnlyList<CssDeclarationSyntax> declarations, int depth, CssValueWork work)
    {
        var result = new RawEntry[declarations.Count];
        for (var i = 0; i < result.Length; i++)
        {
            work.Charge(1);
            var syntax = declarations[i];
            work.Charge(syntax.Name.Length);
            var name = CssPropertyEffects.Canonical(CssPropertyRegistry.NormalizeName(syntax.Name, work));
            result[i] = new(name, new(source, syntax, depth, null));
        }
        work.CheckCancellation();
        return result;
    }

    private CssValueWork ResolutionWork(CssValueWork work)
    {
        var stamp = Stamp;
        var raw = _raw;
        return CssValueWork.Guard(work, () =>
        {
            work.CheckCancellation();
            if (Stamp != stamp || !ReferenceEquals(_raw, raw)) throw new InvalidOperationException(Invalidated);
        });
    }

    private Dictionary<string, List<int>> Index(CssValueWork work)
    {
        if (_index is { } cached) return cached;
        var result = new Dictionary<string, List<int>>(StringComparer.Ordinal);
        for (var i = 0; i < _raw.Length; i++)
        {
            work.Charge(1);
            var raw = _raw[i];
            var names = new HashSet<string>(StringComparer.Ordinal) { raw.Name };
            foreach (var name in CssPropertyEffects.Longhands(raw.Name)) { work.Charge(name.Length); names.Add(name); }
            if (CssPropertyRegistry.Find(raw.Name, _context) is { } metadata)
                foreach (var name in metadata.ResetOnlyLonghands) { work.Charge(name.Length); names.Add(name); }
            foreach (var name in names)
            {
                work.Charge(name.Length);
                if (!result.TryGetValue(name, out var indices)) result.Add(name, indices = []);
                indices.Add(i);
            }
        }
        work.CheckCancellation();
        return _index = result;
    }

    // A demand-side dependency probe may inspect pending inputs without materializing their grammar.
    internal bool HasPropertyInput(string name, CssValueWork work)
    {
        work = ResolutionWork(work);
        work.Charge(name.Length);
        var index = Index(work);
        var present = index.ContainsKey(name) || CssPropertyEffects.ResetByAll(name) && index.ContainsKey("all");
        work.CheckCancellation();
        return present;
    }

    internal IReadOnlyList<string> CustomPropertyNames(CssValueWork work)
    {
        work = ResolutionWork(work);
        var names = new List<string>();
        foreach (var name in Index(work).Keys)
        {
            work.Charge(name.Length);
            if (name.StartsWith("--", StringComparison.Ordinal)) names.Add(name);
        }
        work.CheckCancellation();
        return names.AsReadOnly();
    }

    private CssDeclaration[] Materialize(RawEntry raw, CssValueWork work)
    {
        work.Charge(1);
        if (raw.Input.Entries is { } cached) return cached;
        var input = raw.Input;
        var entries = Build(input.Source!, [input.Syntax!], _context, input.Depth, work);
        work.CheckCancellation();
        input.Entries = entries;
        return entries;
    }

    // CSS Cascade 5 §6.1: only declarations correlated with this property's effect are validated.
    internal CssDeclaration? ResolveProperty(string name, CssValueWork work)
    {
        work = ResolutionWork(work);
        name = CssPropertyEffects.Canonical(CssPropertyRegistry.NormalizeName(name, work));
        work.Charge(name.Length);
        if (_resolved.TryGetValue(name, out var cached)) return cached;
        var index = Index(work);
        index.TryGetValue(name, out var direct);
        List<int>? all = null;
        if (name != "all" && CssPropertyEffects.ResetByAll(name)) index.TryGetValue("all", out all);
        var a = 0;
        var b = 0;
        CssDeclaration? winner = null;
        while (a < (direct?.Count ?? 0) || b < (all?.Count ?? 0))
        {
            work.Charge(1);
            var next = b == (all?.Count ?? 0) || a < (direct?.Count ?? 0) && direct![a] < all![b]
                ? direct![a++] : all![b++];
            var raw = _raw[next];
            foreach (var entry in Materialize(raw, work))
            {
                work.Charge(1);
                if (!CssSubstitutionArguments.Equals(entry.Name, name, work)) continue;
                if (winner is null || !winner.IsImportant || entry.IsImportant) winner = entry;
            }
        }
        work.CheckCancellation();
        _resolved.Add(name, winner);
        return winner;
    }

    private CssDeclaration[] ResolveShorthand(CssPropertyMetadata shorthand, CssValueWork work)
    {
        var result = new List<CssDeclaration>(shorthand.Longhands.Count);
        foreach (var name in shorthand.Longhands)
        {
            work.Charge(1);
            if (ResolveProperty(name, work) is { } entry) result.Add(entry);
        }
        work.CheckCancellation();
        return result.ToArray();
    }

    // Snapshot construction retains a pending binding; only reaching that binding refuses its feature.
    // This result never represents pending syntax as a successful CssPropertyValue.
    internal CssCustomDeclaration? ResolveCustomProperty(string name, CssValueWork work)
    {
        work = ResolutionWork(work);
        work.Charge(name.Length);
        if (!name.StartsWith("--", StringComparison.Ordinal)) throw new ArgumentException("A custom property is required.", nameof(name));
        if (_customResolved.TryGetValue(name, out var cached)) return cached;
        CssCustomDeclaration? winner = null;
        if (Index(work).TryGetValue(name, out var indices))
            foreach (var index in indices)
            {
                work.Charge(1);
                var raw = _raw[index];
                CssCustomDeclaration candidate;
                if (raw.Input.Syntax is { } syntax)
                {
                    if (_context == CssDeclarationContext.Keyframe && syntax.IsImportant) continue;
                    var input = CssReferenceInput.FromComponents(raw.Input.Source!, syntax.Value, raw.Input.Depth,
                        syntax.ValueSourceSpan, work, syntax.ValueSerializationSpan, syntax.ValueTermination);
                    var parsed = CssPropertyParser.Parse(name, input, _context, work);
                    if (parsed.Status == CssPropertyStatus.UnimplementedGrammar)
                        candidate = new(CssSubstitutionBinding.Pending(name, parsed.Blocker!), null, syntax.IsImportant);
                    else if (parsed.Status is CssPropertyStatus.Valid or CssPropertyStatus.Deferred)
                    {
                        candidate = FromValue(parsed.Value!, syntax.IsImportant);
                    }
                    else continue;
                }
                else
                {
                    var entry = raw.Input.Entries![0];
                    candidate = FromValue(entry.Value, entry.IsImportant);
                }
                if (winner is null || !winner.IsImportant || candidate.IsImportant) winner = candidate;
            }
        work.CheckCancellation();
        _customResolved.Add(name, winner);
        return winner;

        CssCustomDeclaration FromValue(CssPropertyValue value, bool important) => value.Kind == CssPropertyValueKind.Custom
            ? new(CssSubstitutionBinding.Specified(name, value.References.Input, false), null, important)
            : new(CssSubstitutionBinding.Invalid(name, false), value.Text, important);
    }

    // Explicit whole-block CSSOM reads may require every retained value grammar.
    internal CssDeclaration[] ResolveAll(CssValueWork work)
    {
        work = ResolutionWork(work);
        work.CheckCancellation();
        if (_allResolved) return _entries;
        var entries = new List<CssDeclaration>();
        var winners = new Dictionary<string, CssDeclaration>(new DeclarationNameComparer(work));
        foreach (var raw in _raw)
        {
            foreach (var entry in Materialize(raw, work))
            {
                work.Charge(1);
                InstallEntry(entries, entry, work, winners);
            }
        }
        var ordered = new List<CssDeclaration>(winners.Count);
        foreach (var entry in entries)
        {
            work.Charge(entry.Name.Length);
            if (ReferenceEquals(winners[entry.Name], entry)) ordered.Add(entry);
        }
        var resolved = ordered.ToArray();
        work.Charge(resolved.Length);
        work.CheckCancellation();
        _entries = resolved;
        _allResolved = true;
        return resolved;
    }

    internal CssDeclarationBlock Copy(CssValueWork work)
    {
        work = ResolutionWork(work);
        work.CheckCancellation();
        var result = new CssDeclarationBlock(_context) { _raw = (RawEntry[]) _raw.Clone() };
        work.Charge(_raw.Length);
        work.CheckCancellation();
        return result;
    }

    private void CommitTarget(string name, List<CssDeclaration> declarations, CssValueWork work, bool remove = false)
    {
        work = ResolutionWork(work);
        var targets = new HashSet<string>(StringComparer.Ordinal) { name };
        foreach (var longhand in CssPropertyEffects.Longhands(name)) { work.Charge(longhand.Length); targets.Add(longhand); }
        var prior = new Dictionary<string, CssDeclaration?>(StringComparer.Ordinal);
        foreach (var target in targets)
        {
            work.Charge(target.Length);
            prior.Add(target, ResolveProperty(target, work));
        }
        var incoming = new Dictionary<string, CssDeclaration>(StringComparer.Ordinal);
        foreach (var entry in declarations) { work.Charge(entry.Name.Length); incoming.Add(entry.Name, entry); }
        var replacement = new List<RawEntry>(_raw.Length + declarations.Count);
        var inserted = new HashSet<string>(StringComparer.Ordinal);
        var affected = false;
        foreach (var raw in _raw)
        {
            work.Charge(1);
            var correlated = targets.Contains(raw.Name);
            foreach (var longhand in CssPropertyEffects.Longhands(raw.Name))
            {
                work.Charge(longhand.Length);
                correlated |= targets.Contains(longhand);
            }
            if (CssPropertyEffects.AffectsAll(raw.Name))
                foreach (var target in targets)
                {
                    work.Charge(target.Length);
                    correlated |= CssPropertyEffects.ResetByAll(target);
                }
            if (!correlated) { replacement.Add(raw); continue; }
            foreach (var entry in Materialize(raw, work))
            {
                work.Charge(entry.Name.Length);
                if (!targets.Contains(entry.Name)) { Add(entry); continue; }
                affected = true;
                if (ReferenceEquals(prior[entry.Name], entry) && incoming.TryGetValue(entry.Name, out var changed))
                {
                    Add(changed);
                    inserted.Add(entry.Name);
                }
            }
        }
        foreach (var entry in declarations)
        {
            work.Charge(entry.Name.Length);
            if (!inserted.Contains(entry.Name)) Add(entry);
        }
        if (remove && !affected) return;
        var next = replacement.ToArray();
        work.Charge(next.Length);
        work.CheckCancellation();
        _raw = next;
        _index = null;
        _resolved.Clear();
        _customResolved.Clear();
        _entries = [];
        _allResolved = false;
        CssMutationStamp.Advance(ref _version);
        _owner?.Changed();

        void Add(CssDeclaration entry) => replacement.Add(new(entry.Name, new(null, null, 0, [entry])));
    }

    // Internal publication preserves unrelated syntax. This is not a successful CSSOM value read.
    internal string SerializeSource(CssValueWork work)
    {
        work = ResolutionWork(work);
        var builder = new StringBuilder();
        var literals = new Dictionary<string, CssDeclaration>(new DeclarationNameComparer(work));
        var pendingValues = new Dictionary<CssPendingShorthand, string>(ReferenceEqualityComparer.Instance);
        var writtenPending = new HashSet<CssPendingShorthand>(ReferenceEqualityComparer.Instance);
        foreach (var raw in _raw)
        {
            work.Charge(1);
            if (raw.Input.Syntax is not null) continue;
            foreach (var entry in Materialize(raw, work))
            {
                work.Charge(entry.Name.Length + 1);
                if (!literals.TryGetValue(entry.Name, out var prior) || !prior.IsImportant || entry.IsImportant)
                    literals[entry.Name] = entry;
                if (entry.PendingShorthand is { } pending) pendingValues.TryAdd(pending, "");
            }
        }
        foreach (var pending in pendingValues.Keys.ToArray())
        {
            work.Charge(1);
            var longhands = CssPropertyRegistry.Completed[pending.Name].Longhands;
            bool? important = null;
            var complete = true;
            foreach (var name in longhands)
            {
                work.Charge(name.Length + 1);
                if (!literals.TryGetValue(name, out var entry) || !ReferenceEquals(pending, entry.PendingShorthand) ||
                    important is { } priority && entry.IsImportant != priority)
                {
                    complete = false;
                    break;
                }
                important = entry.IsImportant;
            }
            if (complete) pendingValues[pending] = CompleteLexicalValue(pending.LexicalSpecifiedText, pending.Termination, work);
        }
        foreach (var raw in _raw)
        {
            work.Charge(1);
            if (raw.Input.Syntax is { } syntax)
            {
                work.Charge(syntax.ValueSerializationSpan.Length);
                work.CheckCancellation();
                var value = raw.Input.Source!.Substring(syntax.ValueSerializationSpan.Start, syntax.ValueSerializationSpan.Length);
                Write(raw.Name, value, syntax.IsImportant, syntax.ValueTermination);
            }
            else
                foreach (var entry in Materialize(raw, work))
                {
                    work.Charge(1);
                    if (entry.PendingShorthand is { } pending)
                    {
                        var value = pendingValues[pending];
                        if (value.Length != 0 && writtenPending.Add(pending)) Write(pending.Name, value, entry.IsImportant, "");
                    }
                    else Write(entry.Name, EntryValue(entry, work), entry.IsImportant, "");
                }
        }
        work.CheckCancellation();
        var text = builder.ToString();
        work.Charge(text.Length);
        work.CheckCancellation();
        return text;

        void Write(string name, string value, bool important, string termination)
        {
            if (builder.Length != 0) builder.Append(' ');
            builder.Append(CssSyntaxSerializer.SerializeIdentifier(name, work)).Append(": ");
            work.Charge(value.Length);
            builder.Append(value).Append(termination);
            if (important) builder.Append(" !important");
            builder.Append(';');
        }
    }
}

internal sealed record CssCustomDeclaration(CssSubstitutionBinding Binding, string? WideKeyword, bool IsImportant);
