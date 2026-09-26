using Jint.HtmlParser;
using Jint.HtmlParser.Css;
using Jint.HtmlParser.Css.Media;
using Jint.HtmlParser.Css.Model;
using Jint.HtmlParser.Css.Model.Syntax;
using Jint.HtmlParser.Css.Selectors;
using Jint.HtmlParser.Css.Serialization;
using Jint.HtmlParser.Css.Values;
using Jint.HtmlParser.Css.Values.Properties;
using Jint.HtmlParser.Css.Values.References;

namespace Jint.Browser.Styling;

internal enum NativeCssOrigin { UserAgent, User, Author }
internal enum NativeCssDisposition { Cascaded, Initial, Inherited, InvalidAtComputedValue }
internal sealed record NativeCssSheet(CssStyleSheet Sheet, NativeCssOrigin Origin);
internal sealed record NativeCssSource(CssStyleRule? Rule, CssDeclarationBlock Block,
    NativeCssOrigin Origin, SelectorSpecificity Specificity, long Order, bool Inline);
internal sealed record NativeCssProperty(string Name, string Text, CssPropertyValue? Value,
    NativeCssSource? Source, NativeCssDisposition Disposition);

// CSS Cascade 5 §§6–7 and Variables 1 §3. One synchronous, immutable-input query.
// Matching retains specified candidates; only GetProperty and explicit enumeration compute values.
internal sealed class NativeCssQuery
{
    internal const string Invalidated = "The native CSS query was invalidated by mutation.";
    private readonly Document _document;
    private readonly ulong _documentStamp;
    private readonly CssMutationStamp _resourceStamp;
    private readonly NativeCssSheet[] _sheets;
    private readonly CssMutationStamp[] _sheetStamps;
    private readonly Dictionary<Element, (CssDeclarationBlock Block, CssMutationStamp Stamp)> _inline = new();
    private readonly Dictionary<Element, State> _states = new();
    private readonly CssMediaEnvironment _media;
    private readonly SelectorEnvironment _selectors;
    private readonly CssEnvironmentSnapshot _environment;
    private readonly CssValueWork _work;

    internal NativeCssQuery(Document document, IReadOnlyList<NativeCssSheet> sheets,
        IReadOnlyList<(Element Element, CssDeclarationBlock Block)> inline,
        CssMediaEnvironment media, in SelectorEnvironment selectors,
        CssEnvironmentSnapshot environment, CssValueWork work)
    {
        _document = document;
        _documentStamp = document.MutationStamp;
        _resourceStamp = NativeCssStyleSheets.Stamp(document);
        _media = media;
        _selectors = selectors;
        _environment = environment;
        _work = work;
        _sheets = new NativeCssSheet[sheets.Count];
        _sheetStamps = new CssMutationStamp[sheets.Count];
        for (var i = 0; i < sheets.Count; i++)
        {
            work.Charge(1);
            if (!Enum.IsDefined(sheets[i].Origin)) throw new ArgumentException("Invalid CSS origin.", nameof(sheets));
            _sheets[i] = sheets[i];
            _sheetStamps[i] = sheets[i].Sheet.Stamp;
        }
        foreach (var item in inline)
        {
            work.Charge(1);
            if (!ReferenceEquals(item.Element.OwnerDocument, document))
                throw new ArgumentException("Inline style belongs to another document.", nameof(inline));
            _inline.Add(item.Element, (item.Block, item.Block.Stamp));
        }
        Verify();
    }

    internal NativeCssProperty GetProperty(Element element, string name, ref SelectorMatchWork matching)
    {
        Verify();
        matching.Observe(element);
        name = CssPropertyRegistry.NormalizeName(name, _work);
        if (name.StartsWith("--", StringComparison.Ordinal)) return Custom(element, name, ref matching);
        var metadata = CssPropertyRegistry.Find(name, CssDeclarationContext.Style);
        if (metadata is null)
        {
            var failure = CssPropertyParser.NameFailure(name, CssDeclarationContext.Style);
            if (failure is { Status: CssPropertyStatus.UnimplementedGrammar })
                throw new CssIncompleteGrammarException(name, failure.Value.Blocker!, default);
            return new(name, "", null, null, NativeCssDisposition.Initial);
        }
        if (metadata.Longhands.Count != 0)
        {
            var entries = new CssDeclaration[metadata.Longhands.Count];
            for (var i = 0; i < entries.Length; i++)
            {
                _work.Charge(1);
                var property = GetProperty(element, metadata.Longhands[i], ref matching);
                entries[i] = new(property.Name, property.Value!, false, default, null);
            }
            var text = CssDeclarationBlock.ShorthandValue(entries, metadata, _work);
            Verify();
            return new(name, text, null, null, NativeCssDisposition.Cascaded);
        }

        // Inheritance is iterative even for arbitrarily deep native trees.
        var pending = new Stack<(State State, NativeCssSource? Source, NativeCssDisposition Disposition)>();
        var current = element;
        NativeCssProperty result;
        while (true)
        {
            _work.Charge(1);
            var state = StateOf(current, ref matching);
            if (state.Computed.TryGetValue(name, out result!)) break;
            var candidate = Winner(state, name);
            var value = candidate?.Declaration.Value;
            var disposition = NativeCssDisposition.Cascaded;
            if (value?.Kind == CssPropertyValueKind.Deferred)
            {
                value = Substitute(state, candidate!, name, ref matching);
                if (value is null) disposition = NativeCssDisposition.InvalidAtComputedValue;
            }
            var inherit = value is null ? metadata.Inherited : value.Kind == CssPropertyValueKind.Keyword &&
                (value.Text == "inherit" || value.Text == "unset" && metadata.Inherited);
            if (inherit && current.ParentNode is Element parent)
            {
                pending.Push((state, candidate?.Source, disposition == NativeCssDisposition.InvalidAtComputedValue
                    ? disposition : NativeCssDisposition.Inherited));
                matching.Observe(parent);
                current = parent;
                continue;
            }
            if (value is null || value.Kind == CssPropertyValueKind.Keyword && value.Text is "initial" or "inherit" or "unset")
            {
                value = CssPropertyParser.Parse(name, metadata.InitialValue).Value;
                if (disposition != NativeCssDisposition.InvalidAtComputedValue) disposition = NativeCssDisposition.Initial;
            }
            result = new(name, value.Serialize(), value, candidate?.Source, disposition);
            state.Computed.Add(name, result);
            break;
        }
        while (pending.TryPop(out var item))
        {
            _work.Charge(1);
            result = result with { Source = item.Source, Disposition = item.Disposition };
            item.State.Computed.Add(name, result);
        }
        matching.VerifyRead();
        Verify();
        return result;
    }

    internal IReadOnlyList<CssStyleRule> MatchedRules(Element element, ref SelectorMatchWork matching)
    {
        var state = StateOf(element, ref matching);
        Verify();
        return state.Matches.AsReadOnly();
    }

    internal IReadOnlyList<NativeCssProperty> Enumerate(Element element, ref SelectorMatchWork matching)
    {
        var result = new List<NativeCssProperty>();
        foreach (var entry in CssPropertyRegistry.Completed.Values)
        {
            _work.Charge(1);
            if (entry.Longhands.Count == 0) result.Add(GetProperty(element, entry.Name, ref matching));
        }
        Variables(StateOf(element, ref matching), ref matching);
        var names = new HashSet<string>(new Names(_work));
        for (var state = StateOf(element, ref matching); state is not null; state = state.Parent)
            foreach (var name in state.Candidates.Keys)
            {
                _work.Charge(1);
                if (name.StartsWith("--", StringComparison.Ordinal)) names.Add(name);
            }
        foreach (var name in names) result.Add(Custom(element, name, ref matching));
        Verify();
        return result.AsReadOnly();
    }

    private State StateOf(Element element, ref SelectorMatchWork matching)
    {
        Verify();
        if (!ReferenceEquals(element.OwnerDocument, _document)) throw new ArgumentException("Element belongs to another document.");
        if (_states.TryGetValue(element, out var cached)) return cached;
        matching.Observe(element);
        var state = new State(element, _work);
        long order = 0;
        foreach (var input in _sheets)
            foreach (var rule in input.Sheet.ApplicableStyleRules(_media, _work))
            {
                _work.Charge(1);
                if (rule.TryMatch(element, out var specificity, null, _selectors, ref matching))
                {
                    state.Matches.Add(rule);
                    Add(state, rule.Style, new(rule, rule.Style, input.Origin, specificity, order, false));
                }
                order++;
            }
        if (_inline.TryGetValue(element, out var inline))
            Add(state, inline.Block, new(null, inline.Block, NativeCssOrigin.Author, default, order, true));
        foreach (var candidates in state.Candidates.Values)
            candidates.Sort((left, right) => { _work.Charge(1); return Compare(right, left); });
        matching.VerifyRead();
        Verify();
        _states.Add(element, state);
        return state;
    }

    private void Add(State state, CssDeclarationBlock block, NativeCssSource source)
    {
        for (var i = 0; i < block.Count; i++)
        {
            _work.Charge(1);
            var declaration = block.GetDeclaration(i);
            if (!state.Candidates.TryGetValue(declaration.Name, out var candidates))
                state.Candidates.Add(declaration.Name, candidates = []);
            candidates.Add(new(declaration, source));
        }
    }

    private static int Compare(Candidate left, Candidate right)
    {
        var a = left.Declaration.IsImportant ? 5 - (int) left.Source.Origin : (int) left.Source.Origin;
        var b = right.Declaration.IsImportant ? 5 - (int) right.Source.Origin : (int) right.Source.Origin;
        var comparison = a.CompareTo(b);
        if (comparison != 0) return comparison;
        comparison = left.Source.Inline.CompareTo(right.Source.Inline);
        if (comparison != 0) return comparison;
        comparison = left.Source.Specificity.CompareTo(right.Source.Specificity);
        return comparison != 0 ? comparison : left.Source.Order.CompareTo(right.Source.Order);
    }

    private Candidate? Winner(State state, string name)
    {
        if (!state.Candidates.TryGetValue(name, out var candidates)) return null;
        var excludedOrigins = new bool[3];
        var excludedRules = new HashSet<CssDeclarationBlock>();
        foreach (var candidate in candidates)
        {
            _work.Charge(1);
            var origin = (int) candidate.Source.Origin;
            if (excludedOrigins[origin] || excludedRules.Contains(candidate.Source.Block)) continue;
            var value = candidate.Declaration.Value;
            if (value.Kind == CssPropertyValueKind.Keyword && value.Text == "revert")
            {
                for (var i = origin; i < excludedOrigins.Length; i++) excludedOrigins[i] = true;
                continue;
            }
            if (value.Kind == CssPropertyValueKind.Keyword && value.Text == "revert-rule")
            {
                excludedRules.Add(candidate.Source.Block);
                continue;
            }
            if (value.Kind == CssPropertyValueKind.Keyword && value.Text == "revert-layer")
                throw new CssIncompleteGrammarException(name, "C6:revert-layer", value.Span);
            return candidate;
        }
        return null;
    }

    private CssSubstitutionSnapshot Variables(State state, ref SelectorMatchWork matching)
    {
        var pending = new Stack<State>();
        var current = state;
        while (current.Variables is null)
        {
            _work.Charge(1);
            pending.Push(current);
            if (current.Element.ParentNode is not Element parent) break;
            current.Parent ??= StateOf(parent, ref matching);
            current = current.Parent;
        }
        var inherited = current.Variables;
        while (pending.TryPop(out current))
        {
            var bindings = new List<CssSubstitutionBinding>();
            foreach (var name in current.Candidates.Keys)
            {
                _work.Charge(1);
                if (!name.StartsWith("--", StringComparison.Ordinal)) continue;
                var candidate = Winner(current, name);
                if (candidate is null) continue;
                var value = candidate.Declaration.Value;
                if (value.Kind == CssPropertyValueKind.Custom)
                    bindings.Add(CssSubstitutionBinding.Specified(name, value.References.Input, false));
                else if (value.Text == "initial") bindings.Add(CssSubstitutionBinding.Invalid(name, false));
                // inherit and unset retain the parent's defining scope.
            }
            inherited = CssSubstitutionSnapshot.CreateLayer(bindings.ToArray(), inherited, _work);
            current.Variables = inherited;
        }
        return state.Variables!;
    }

    private NativeCssProperty Custom(Element element, string name, ref SelectorMatchWork matching)
    {
        var state = StateOf(element, ref matching);
        if (state.Computed.TryGetValue(name, out var cached)) return cached;
        var snapshot = Variables(state, ref matching);
        var text = "";
        if (snapshot.TryGet(name, _work, out var binding) && binding.Kind == CssSubstitutionBindingKind.Specified)
        {
            var result = CssSubstitutionExecutor.Resolve(binding.Input, binding.Scope!, _environment,
                new(name, CssReferenceUse.CustomPropertyValue, true), _work);
            if (result.Kind == CssSubstitutionResultKind.PendingFeature)
                throw new CssIncompleteGrammarException(name, "C6:" + result.PendingFeature, default);
            if (result.Kind == CssSubstitutionResultKind.Tokens)
                text = CssSyntaxSerializer.SerializeComponents(result.Value.Components, _work);
        }
        var property = new NativeCssProperty(name, text, null, Winner(state, name)?.Source, NativeCssDisposition.Cascaded);
        Verify();
        state.Computed.Add(name, property);
        return property;
    }

    private CssPropertyValue? Substitute(State state, Candidate candidate, string name, ref SelectorMatchWork matching)
    {
        var declaration = candidate.Declaration;
        var pending = declaration.PendingShorthand;
        var requested = pending?.Name ?? name;
        if (pending is not null && state.Shorthands.TryGetValue(pending, out var cached))
            return cached is null ? null : Find(cached, name);
        var result = CssSubstitutionExecutor.Resolve(declaration.Value.References.Input,
            Variables(state, ref matching), _environment,
            new(requested, CssReferenceUse.PropertyValue, true), _work);
        if (result.Kind == CssSubstitutionResultKind.PendingFeature)
            throw new CssIncompleteGrammarException(requested, "C6:" + result.PendingFeature, default);
        CssPropertyValue? value = null;
        if (result.Kind == CssSubstitutionResultKind.Tokens)
        {
            var parsed = CssPropertyParser.Parse(requested, result.Value.AsReferenceInput(_work),
                CssDeclarationContext.Style, _work);
            if (parsed.Status == CssPropertyStatus.UnimplementedGrammar)
                throw new CssIncompleteGrammarException(requested, parsed.Blocker!, default);
            if (parsed.Status == CssPropertyStatus.Valid) value = parsed.Value;
        }
        if (pending is null) return value;
        var expanded = value is null ? null : CssDeclarationBlock.ExpandValue(requested, value, _work);
        state.Shorthands.Add(pending, expanded);
        return expanded is null ? null : Find(expanded, name);
    }

    private CssPropertyValue? Find(CssDeclaration[] declarations, string name)
    {
        foreach (var declaration in declarations)
        {
            _work.Charge(1);
            if (CssSubstitutionArguments.Equals(declaration.Name, name, _work)) return declaration.Value;
        }
        return null;
    }

    private void Verify()
    {
        _work.CheckCancellation();
        if (!_resourceStamp.CanReuse || NativeCssStyleSheets.Stamp(_document) != _resourceStamp ||
            _documentStamp == ulong.MaxValue || _document.MutationStamp != _documentStamp)
            throw new InvalidOperationException(Invalidated);
        for (var i = 0; i < _sheets.Length; i++)
        {
            _work.Charge(1);
            if (!_sheetStamps[i].CanReuse || _sheets[i].Sheet.Stamp != _sheetStamps[i])
                throw new InvalidOperationException(Invalidated);
        }
        foreach (var inline in _inline.Values)
        {
            _work.Charge(1);
            if (!inline.Stamp.CanReuse || inline.Block.Stamp != inline.Stamp)
                throw new InvalidOperationException(Invalidated);
        }
    }

    private sealed record Candidate(CssDeclaration Declaration, NativeCssSource Source);
    private sealed class State(Element element, CssValueWork work)
    {
        internal Element Element { get; } = element;
        internal State? Parent;
        internal CssSubstitutionSnapshot? Variables;
        internal Dictionary<string, List<Candidate>> Candidates { get; } = new(new Names(work));
        internal Dictionary<string, NativeCssProperty> Computed { get; } = new(new Names(work));
        internal Dictionary<CssPendingShorthand, CssDeclaration[]?> Shorthands { get; } = new(ReferenceEqualityComparer.Instance);
        internal List<CssStyleRule> Matches { get; } = [];
    }
    private sealed class Names(CssValueWork work) : IEqualityComparer<string>
    {
        public bool Equals(string? x, string? y) => ReferenceEquals(x, y) || x is not null && y is not null &&
            CssSubstitutionArguments.Equals(x, y, work);
        public int GetHashCode(string value) => unchecked((int) CssSubstitutionArguments.Hash(value, work));
    }
}
