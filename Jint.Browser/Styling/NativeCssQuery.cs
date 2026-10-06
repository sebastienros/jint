using Jint.HtmlParser;
using Jint.HtmlParser.Css;
using Jint.HtmlParser.Css.Media;
using Jint.HtmlParser.Css.Model;
using Jint.HtmlParser.Css.Model.Syntax;
using Jint.HtmlParser.Css.Selectors;
using Jint.HtmlParser.Css.Values;
using Jint.HtmlParser.Css.Values.Properties;

namespace Jint.Browser.Styling;

internal enum NativeCssOrigin { UserAgent, User, Author }
internal enum NativeCssDisposition { Cascaded, Initial, Inherited, InvalidAtComputedValue }
internal sealed record NativeCssSheet(CssStyleSheet Sheet, NativeCssOrigin Origin, string? NamespaceUri = null);
internal sealed record NativeCssSource(CssStyleRule? Rule, CssDeclarationBlock Block,
    NativeCssOrigin Origin, SelectorSpecificity Specificity, long Order, bool Inline, NativeCssLayer? Layer = null,
    int EncapsulationDepth = 0);
internal sealed record NativeCssProperty(string Name, string Text, NativeCssSource? Source, NativeCssDisposition Disposition);

// CSS Cascade 5 §§6-7. One synchronous query, with text values and iterative inheritance.
internal sealed partial class NativeCssQuery
{
    internal const string Invalidated = "The native CSS query was invalidated by mutation.";
    private readonly NativeCssQueryDiagnostics.QueryRecord? _diagnostics;
    internal NativeCssQueryDiagnostics.QueryRecord? Diagnostics => _diagnostics;
    private readonly Document _document;
    private readonly ulong _documentStamp;
    private readonly CssMutationStamp _resourceStamp;
    private readonly NativeCssSheet[] _sheets;
    private readonly CssStyleSheetRevisionSnapshot _sheetRevisions;
    private readonly Dictionary<Element, (CssDeclarationBlock Block, CssMutationStamp Stamp)> _inline = new();
    private readonly Dictionary<Element, State> _states = new();
    private readonly CssMediaEnvironment _media;
    private readonly SelectorEnvironment _selectors;
    private readonly CssValueWork _work;
    private readonly bool _readInlineAttributes;
    private Action? _readWitness;
    private bool _aborted;
    private List<(CssStyleRule Rule, NativeCssOrigin Origin, long Order, string? NamespaceUri, NativeCssLayer Layer)>? _rules;

    internal NativeCssQuery(Document document, IReadOnlyList<NativeCssSheet> sheets,
        IReadOnlyList<(Element Element, CssDeclarationBlock Block)> inline,
        CssMediaEnvironment media, in SelectorEnvironment selectors, CssValueWork work,
        bool readInlineAttributes = false, NativeCssQueryDiagnostics? diagnostics = null)
    {
        _document = document;
        _documentStamp = document.MutationStamp;
        _resourceStamp = NativeCssStyleSheets.Stamp(document);
        _media = media;
        _selectors = selectors;
        _work = work;
        _readInlineAttributes = readInlineAttributes;
        _sheets = new NativeCssSheet[sheets.Count];
        var roots = new CssStyleSheet[sheets.Count];
        for (var i = 0; i < sheets.Count; i++)
        {
            work.Charge(1);
            if (!Enum.IsDefined(sheets[i].Origin)) throw new ArgumentException("Invalid CSS origin.", nameof(sheets));
            _sheets[i] = sheets[i];
            roots[i] = sheets[i].Sheet;
            NativeCssParsing.PrepareImports(roots[i], work);
        }
        _sheetRevisions = CssStyleSheetRevisionSnapshot.Capture(roots, work);
        foreach (var item in inline)
        {
            work.Charge(1);
            if (!ReferenceEquals(item.Element.OwnerDocument, document))
                throw new ArgumentException("Inline style belongs to another document.", nameof(inline));
            _inline.Add(item.Element, (item.Block, item.Block.Stamp));
        }
        Verify();
        _diagnostics = diagnostics?.QueryStarted();
    }

    internal CssValueWork Work => _work;

    internal void AttachReadWitness(Action witness)
    {
        if (_readWitness is not null || _states.Count != 0)
            throw new InvalidOperationException("The read witness must be attached once before querying styles.");
        _readWitness = witness;
        Verify();
    }

    internal NativeCssProperty GetProperty(Element element, string name, ref SelectorMatchWork matching) =>
        GetNormalizedProperty(element, CssPropertyRegistry.NormalizeName(name, _work), ref matching);

    internal NativeCssProperty GetNormalizedProperty(Element element, string name, ref SelectorMatchWork matching)
    {
        try
        {
            Verify();
            var result = ReadProperty(element, name, ref matching);
            matching.VerifyRead();
            Verify();
            return result;
        }
        catch { _aborted = true; _states.Clear(); throw; }
    }

    internal bool HasPropertyInput(Element element, string name, ref SelectorMatchWork matching)
    {
        var state = StateOf(element, ref matching);
        foreach (var source in state.Sources)
        {
            _work.Charge(1);
            if (source.Block.HasPropertyInput(name, _work)) return true;
        }
        return false;
    }

    private NativeCssProperty ReadProperty(Element element, string name, ref SelectorMatchWork matching)
    {
        var custom = name.StartsWith("--", StringComparison.Ordinal);
        var metadata = CssPropertyRegistry.Find(name);
        if (!custom && metadata is null) return new(name, "", null, NativeCssDisposition.Initial);
        if (metadata is { Longhands.Count: > 0 })
        {
            var entries = new CssDeclaration[metadata.Longhands.Count];
            for (var i = 0; i < entries.Length; i++)
            {
                _work.Charge(1);
                var property = GetProperty(element, metadata.Longhands[i], ref matching);
                entries[i] = new(property.Name, property.Text, false, default);
            }
            return new(name, CssDeclarationBlock.ShorthandValue(entries, metadata, _work), null, NativeCssDisposition.Cascaded);
        }
        if (name == "display") WarmParents(element, ref matching);
        var pending = new Stack<(State State, NativeCssSource? Source, NativeCssDisposition Disposition)>();
        var current = element;
        NativeCssProperty result;
        while (true)
        {
            _work.Charge(1);
            var state = StateOf(current, ref matching);
            if (state.Computed.TryGetValue(name, out result!))
            {
                _diagnostics?.CacheHit(current, name);
                break;
            }
            var candidate = Winner(state, name);
            var text = candidate?.Declaration.Value;
            var disposition = NativeCssDisposition.Cascaded;
            if (!custom && text is not null && text.Contains("var(", StringComparison.Ordinal))
            {
                text = Substitute(current, text, ref matching);
                if (text is not null) text = CssLayoutValues.AcceptsText(name, text, _work, out var normalized) ? normalized : null;
                if (text is null) disposition = NativeCssDisposition.InvalidAtComputedValue;
            }
            var inherited = custom || metadata!.Inherited;
            var inherit = text is null ? inherited : !custom && (text == "inherit" || text == "unset" && inherited);
            if (inherit && InheritanceParent(current) is { } parent)
            {
                pending.Push((state, candidate?.Source, disposition == NativeCssDisposition.InvalidAtComputedValue
                    ? disposition : NativeCssDisposition.Inherited));
                current = parent;
                continue;
            }
            if (text is null || inherit || !custom && text is "initial" or "unset")
            {
                text = custom ? "" : metadata!.InitialValue;
                if (disposition != NativeCssDisposition.InvalidAtComputedValue) disposition = NativeCssDisposition.Initial;
            }
            if (name == "display") text = Display(current, text, ref matching);
            result = new(name, text, candidate?.Source, disposition);
            state.Computed.Add(name, result);
            _diagnostics?.ComputedPublished(current, name);
            break;
        }
        while (pending.TryPop(out var item))
        {
            _work.Charge(1);
            result = result with
            {
                Text = name == "display" ? Display(item.State.Element, result.Text, ref matching) : result.Text,
                Source = item.Source,
                Disposition = item.Disposition
            };
            item.State.Computed.Add(name, result);
            _diagnostics?.ComputedPublished(item.State.Element, name);
        }
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
        var own = StateOf(element, ref matching);
        if (own.Enumeration is { } cached) return cached;
        var names = new HashSet<string>(new Names(_work));
        foreach (var entry in CssPropertyRegistry.Completed.Values)
        {
            _work.Charge(1);
            if (entry.Longhands.Count == 0) names.Add(entry.Name);
        }
        for (Element? current = element; current is not null; current = InheritanceParent(current))
        {
            _work.Charge(1);
            foreach (var source in StateOf(current, ref matching).Sources)
                foreach (var name in source.Block.CustomPropertyNames(_work))
                {
                    _work.Charge(1);
                    names.Add(name);
                }
        }
        var sorted = names.ToList();
        sorted.Sort((left, right) =>
        {
            _work.Charge(System.Math.Min(left.Length, right.Length));
            return string.CompareOrdinal(left, right);
        });
        var result = new List<NativeCssProperty>();
        foreach (var name in sorted)
        {
            _work.Charge(1);
            result.Add(GetProperty(element, name, ref matching));
        }
        Verify();
        return own.Enumeration = result.AsReadOnly();
    }

    private State StateOf(Element element, ref SelectorMatchWork matching)
    {
        Verify();
        if (!ReferenceEquals(element.OwnerDocument, _document)) throw new ArgumentException("Element belongs to another document.");
        if (_states.TryGetValue(element, out var cached)) return cached;
        matching.Observe(element);
        var state = new State(element, _work);
        _rules ??= BuildRules();
        var depth = 0;
        for (var root = element.TreeShadowRoot; root is not null; root = root.Host.TreeShadowRoot)
        {
            _work.Charge(1);
            depth++;
        }
        _index ??= new RuleIndex(_rules, _work);
        // Matching may re-enter this query for another element; that call then allocates its own buffer.
        var candidates = _candidateBuffer ?? [];
        _candidateBuffer = null;
        _index.Collect(element, candidates, _work);
        foreach (var position in candidates)
        {
            _work.Charge(1);
            var (rule, origin, order, namespaceUri, layer) = _rules[position];
            if (namespaceUri is not null && element.NamespaceUri != namespaceUri) continue;
            var scope = (rule.ParentStyleSheet?.Attachment.OwnerNode ??
                rule.ParentStyleSheet?.EffectiveOwnerNode(_work))?.TreeShadowRoot;
            var hostRule = ReferenceEquals(scope?.Host, element);
            if (origin == NativeCssOrigin.Author && !hostRule && !ReferenceEquals(scope, element.TreeShadowRoot)) continue;
            _diagnostics?.RuleAttempted(element, rule);
            if (rule.TryMatch(element, out var specificity, null, _selectors, ref matching, scope))
            {
                _diagnostics?.RuleMatched(element, rule);
                state.Matches.Add(rule);
                state.Sources.Add(new(rule, rule.Style, origin, specificity, order, false, layer, depth + (hostRule ? 1 : 0)));
            }
        }
        _candidateBuffer = candidates;
        if (_readInlineAttributes && !_inline.ContainsKey(element))
            for (uint i = 0; i < (uint) element.AttributeCount; i++)
            {
                _work.Charge(1);
                var attribute = element.GetAttributeAt(i)!;
                if (attribute.NamespaceUri is not null || !CssText.Equals(attribute.LocalName, "style", _work)) continue;
                var block = NativeCssStyleSheets.InlineOf(element, _work);
                Verify();
                _inline.Add(element, (block, block.Stamp));
                break;
            }
        if (_inline.TryGetValue(element, out var inline))
            state.Sources.Add(new(null, inline.Block, NativeCssOrigin.Author, default, _rules.Count, true,
                EncapsulationDepth: depth));
        matching.VerifyRead();
        Verify();
        _states.Add(element, state);
        _diagnostics?.StatePublished(element);
        return state;
    }

    private Candidate? Winner(State state, string name)
    {
        List<Candidate>? candidates = null;
        foreach (var source in state.Sources)
        {
            _work.Charge(1);
            if (source.Block.ResolveProperty(name, _work) is { } declaration) (candidates ??= []).Add(new(declaration, source));
        }
        if (candidates is null) return null;
        if (candidates.Count > 1) candidates.Sort((left, right) => { _work.Charge(1); return Compare(right, left); });
        var excludedOrigin = int.MaxValue;
        HashSet<CssDeclarationBlock>? excludedRules = null;
        List<NativeCssSource>? excludedLayers = null;
        foreach (var candidate in candidates)
        {
            _work.Charge(1);
            var origin = (int) candidate.Source.Origin;
            if (origin >= excludedOrigin || excludedRules?.Contains(candidate.Source.Block) == true || LayerExcluded(candidate.Source, excludedLayers)) continue;
            if (name.StartsWith("--", StringComparison.Ordinal)) return candidate;
            switch (candidate.Declaration.Value)
            {
                case "revert":
                    excludedOrigin = System.Math.Min(excludedOrigin, origin);
                    continue;
                case "revert-rule":
                    (excludedRules ??= []).Add(candidate.Source.Block);
                    continue;
                case "revert-layer":
                    (excludedLayers ??= []).Add(candidate.Source);
                    continue;
            }
            return candidate;
        }
        return null;
    }

    private static int Compare(Candidate left, Candidate right)
    {
        var a = left.Declaration.IsImportant ? 5 - (int) left.Source.Origin : (int) left.Source.Origin;
        var b = right.Declaration.IsImportant ? 5 - (int) right.Source.Origin : (int) right.Source.Origin;
        var comparison = a.CompareTo(b);
        if (comparison != 0) return comparison;
        var important = left.Declaration.IsImportant;
        comparison = left.Source.EncapsulationDepth.CompareTo(right.Source.EncapsulationDepth);
        if (comparison != 0) return important ? comparison : -comparison;
        comparison = left.Source.Inline.CompareTo(right.Source.Inline);
        if (comparison != 0) return comparison;
        comparison = (left.Source.Layer?.Rank ?? int.MaxValue).CompareTo(right.Source.Layer?.Rank ?? int.MaxValue);
        if (comparison != 0) return important ? -comparison : comparison;
        comparison = left.Source.Specificity.CompareTo(right.Source.Specificity);
        return comparison != 0 ? comparison : left.Source.Order.CompareTo(right.Source.Order);
    }

    internal void Verify()
    {
        if (_aborted) throw new InvalidOperationException("The native CSS read context was aborted.");
        if (!InputsAreCurrent()) throw new InvalidOperationException(Invalidated);
        _readWitness?.Invoke();
    }

    // Lets a caller reuse a whole query between separate reads: false, never a throw, when anything
    // the query captured has moved. The caller owns the check of its own read witness.
    internal bool IsReusable() => !_aborted && InputsAreCurrent();

    private bool InputsAreCurrent()
    {
        var inlineCount = _inline.Count;
        _work.Charge(inlineCount);
        var current = _sheetRevisions.IsCurrent(_work);
        if (!current || _inline.Count != inlineCount || !_resourceStamp.CanReuse || NativeCssStyleSheets.Stamp(_document) != _resourceStamp ||
            _documentStamp == ulong.MaxValue || _document.MutationStamp != _documentStamp)
            return false;
        if (_selectors.ControlFactsFactory is { } factory &&
            (!ReferenceEquals(_selectors.Document, _document) || _selectors.ControlFactsContext is null ||
            _selectors.ControlFactsRevision == ulong.MaxValue ||
            factory.ReadRevision(_selectors.ControlFactsContext, _document) != _selectors.ControlFactsRevision))
            return false;
        var compared = 0;
        foreach (var inline in _inline.Values)
        {
            if ((compared++ & 1023) == 0) _work.Token.ThrowIfCancellationRequested();
            if (!inline.Stamp.CanReuse || inline.Block.Stamp != inline.Stamp) return false;
        }
        _work.Token.ThrowIfCancellationRequested();
        return true;
    }

    private Element? InheritanceParent(Element element)
    {
        _work.Charge(1);
        if (SlotAssignment.FindSlot(element, false, _work.Charge, _work.Token) is { } slot) return slot;
        return element.ParentNode switch
        {
            ShadowRoot root => root.Host,
            Element { AttachedShadowRoot: null } parent => parent,
            _ => null
        };
    }

    private sealed record Candidate(CssDeclaration Declaration, NativeCssSource Source);
    private sealed class State(Element element, CssValueWork work)
    {
        internal Element Element { get; } = element;
        internal IReadOnlyList<NativeCssProperty>? Enumeration;
        internal Dictionary<string, NativeCssProperty> Computed { get; } = new(new Names(work));
        internal List<CssStyleRule> Matches { get; } = [];
        internal List<NativeCssSource> Sources { get; } = [];
    }
    private sealed class Names(CssValueWork work) : IEqualityComparer<string>
    {
        public bool Equals(string? x, string? y) => ReferenceEquals(x, y) || x is not null && y is not null && CssText.Equals(x, y, work);
        public int GetHashCode(string value) => unchecked((int) CssText.Hash(value, work));
    }
}
