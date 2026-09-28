using Jint.HtmlParser;
using Jint.HtmlParser.Css;
using Jint.HtmlParser.Css.Media;
using Jint.HtmlParser.Css.Model;
using Jint.HtmlParser.Css.Model.Syntax;
using Jint.HtmlParser.Css.Selectors;
using Jint.HtmlParser.Css.Serialization;
using Jint.HtmlParser.Css.Values;
using Jint.HtmlParser.Css.Values.Colors;
using Jint.HtmlParser.Css.Values.Math;
using Jint.HtmlParser.Css.Values.Properties;
using Jint.HtmlParser.Css.Values.References;

namespace Jint.Browser.Styling;

internal enum NativeCssOrigin { UserAgent, User, Author }
internal enum NativeCssDisposition { Cascaded, Initial, Inherited, InvalidAtComputedValue }
internal sealed record NativeCssSheet(CssStyleSheet Sheet, NativeCssOrigin Origin, string? NamespaceUri = null);
internal sealed record NativeCssSource(CssStyleRule? Rule, CssDeclarationBlock Block,
    NativeCssOrigin Origin, SelectorSpecificity Specificity, long Order, bool Inline, NativeCssLayer? Layer = null,
    int EncapsulationDepth = 0);
internal sealed record NativeCssProperty(string Name, string Text, CssPropertyValue? Value,
    NativeCssSource? Source, NativeCssDisposition Disposition);

// CSS Cascade 5 §§6–7 and Variables 1 §3. One synchronous, immutable-input query.
// Matching retains specified candidates; only GetProperty and explicit enumeration compute values.
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
    private readonly Dictionary<string, CssPropertyValue> _initialValues = new(StringComparer.Ordinal);
    private readonly CssMediaEnvironment _media;
    private readonly NativeCssMetrics _metrics;
    private readonly NativeCssSystemColors? _systemColors;
    private readonly SelectorEnvironment _selectors;
    private readonly CssEnvironmentSnapshot _environment;
    private readonly CssValueWork _work;
    private readonly bool _readInlineAttributes;
    private readonly NativeCssUrlResolver? _resolveUrl;
    private List<(CssStyleRule Rule, NativeCssOrigin Origin, long Order, string? NamespaceUri, NativeCssLayer Layer)>? _rules;

    internal NativeCssQuery(Document document, IReadOnlyList<NativeCssSheet> sheets,
        IReadOnlyList<(Element Element, CssDeclarationBlock Block)> inline,
        CssMediaEnvironment media, in SelectorEnvironment selectors,
        CssEnvironmentSnapshot environment, CssValueWork work, NativeCssMetrics? metrics = null,
        bool readInlineAttributes = false, NativeCssSystemColors? systemColors = null,
        NativeCssQueryDiagnostics? diagnostics = null, NativeCssUrlResolver? resolveUrl = null)
    {
        _document = document;
        _documentStamp = document.MutationStamp;
        _resourceStamp = NativeCssStyleSheets.Stamp(document);
        _media = media;
        _metrics = metrics ?? new NativeCssMetrics();
        _systemColors = systemColors;
        _selectors = selectors;
        _environment = environment;
        _work = work;
        _readInlineAttributes = readInlineAttributes;
        _resolveUrl = resolveUrl;
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

    internal NativeCssProperty GetProperty(Element element, string name, ref SelectorMatchWork matching)
        => GetPropertyCore(element, name, ref matching, adjust: true);

    internal NativeCssProperty GetNormalizedProperty(Element element, string name, ref SelectorMatchWork matching)
        => GetPropertyCore(element, name, ref matching, adjust: true, normalize: false);

    internal bool HasPropertyInput(Element element, string name, ref SelectorMatchWork matching)
    {
        using var guard = EnterDependency(element, "property-input", name);
        try { return HasPropertyInputCore(element, name, ref matching); }
        catch (Exception exception) { AbortRead(exception); throw; }
    }

    private bool HasPropertyInputCore(Element element, string name, ref SelectorMatchWork matching)
    {
        var state = StateOf(element, ref matching);
        var present = false;
        foreach (var source in state.Sources)
        {
            _work.Charge(1);
            present |= source.Block.HasPropertyInput(name, _work) && ConditionsApply(source.Rule, element, ref matching);
        }
        matching.VerifyRead();
        Verify();
        return present;
    }

    private NativeCssProperty GetPropertyCore(Element element, string name, ref SelectorMatchWork matching, bool adjust, bool normalize = true)
    {
        if (normalize) name = CssPropertyRegistry.NormalizeName(name, _work);
        using var guard = EnterDependency(element, adjust ? "property" : "unadjusted-property", name);
        try { return GetPropertyValueCore(element, name, ref matching, adjust); }
        catch (Exception exception) { AbortRead(exception); throw; }
    }

    private NativeCssProperty GetPropertyValueCore(Element element, string name, ref SelectorMatchWork matching, bool adjust)
    {
        Verify();
        matching.Observe(element);
        if (name.StartsWith("--", StringComparison.Ordinal)) return Custom(element, name, ref matching);
        // Resolved longhands never serialize as one CSS-wide reset.
        if (name == "all") return new(name, "", null, null, NativeCssDisposition.Cascaded);
        var metadata = CssPropertyRegistry.Find(name, CssDeclarationContext.Style);
        if (metadata is null)
        {
            var failure = CssPropertyParser.NameFailure(name, CssDeclarationContext.Style);
            if (failure is { Status: CssPropertyStatus.UnimplementedGrammar })
                throw new CssIncompleteGrammarException(name, failure.Value.Blocker!, default);
            Verify();
            return new(name, "", null, null, NativeCssDisposition.Initial);
        }
        if (metadata.Longhands.Count != 0)
        {
            var entries = new CssDeclaration[metadata.Longhands.Count];
            for (var i = 0; i < entries.Length; i++)
            {
                _work.Charge(1);
                if (metadata.ResetOnlyLonghands.Contains(metadata.Longhands[i]))
                {
                    if (!BorderImageResetIsInitial(element, metadata.Longhands[i], ref matching))
                        return new(name, "", null, null, NativeCssDisposition.Cascaded);
                    entries[i] = new(metadata.Longhands[i], CssPropertyValue.Keyword("initial", default), false, default, null);
                    continue;
                }
                var property = GetProperty(element, metadata.Longhands[i], ref matching);
                var value = property.Value!;
                // Color 4 resolved values: this shorthand exposes the element's actual color,
                // while its longhand keeps currentColor for explicit inheritance into another element.
                if (value.Kind == CssPropertyValueKind.Color &&
                    value.Color.Kind == CssColorKind.CurrentColor)
                    value = GetProperty(element, "color", ref matching).Value!;
                if (IsLineWidth(property.Name) && property.Text == "0px")
                    value = Number(property.Name, new CssMathNumeric(0, CssNumericKind.Dimension, CssUnit.Px, value.Span));
                entries[i] = new(property.Name, value, false, default, null);
            }
            var text = CssDeclarationBlock.ShorthandValue(entries, metadata, _work);
            Verify();
            return new(name, text, null, null, NativeCssDisposition.Cascaded);
        }

        if (CssBorderPropertyParser.IsLogical(name))
            return GetProperty(element, PhysicalBorderName(element, name, ref matching), ref matching) with { Name = name };
        if (adjust && name is "overflow-x" or "overflow-y")
            return Overflow(element, name, ref matching);
        if (adjust && name == "display") WarmParents(element, name, ref matching);

        // Inheritance is iterative even for arbitrarily deep native trees.
        var pending = new Stack<(State State, NativeCssSource? Source, NativeCssDisposition Disposition, CssPropertyValue? DependentValue)>();
        var current = element;
        NativeCssProperty result;
        while (true)
        {
            _work.Charge(1);
            var state = StateOf(current, ref matching);
            if (!ReferenceEquals(current, element) && state.Computed.TryGetValue(name, out result!))
            {
                _diagnostics?.CacheHit(current, name);
                break;
            }
            if ((adjust ? state.Computed : state.Unadjusted).TryGetValue(name, out result!))
            {
                _diagnostics?.CacheHit(current, name);
                break;
            }
            var candidate = Winner(state, name, ref matching, substitute: true);
            var value = candidate is { WasSubstituted: true } ? candidate.Resolved : candidate?.Declaration.Value;
            var disposition = NativeCssDisposition.Cascaded;
            if (candidate is { WasSubstituted: true, Resolved: null })
                disposition = NativeCssDisposition.InvalidAtComputedValue;
            var inherit = value is null ? metadata.Inherited : value.Kind == CssPropertyValueKind.Keyword &&
                (value.Text == "inherit" || value.Text == "unset" && metadata.Inherited);
            inherit |= name == "color" && value is { Kind: CssPropertyValueKind.Color } &&
                value.Color.Kind == CssColorKind.CurrentColor;
            var parent = InheritanceParent(current);
            var relativeWeight = name == "font-weight" && value is { Kind: CssPropertyValueKind.Keyword, Text: "bolder" or "lighter" }
                ? value : null;
            var relativeSize = name == "font-size" && value is not null && (FontDependencies(value, true) & 1) != 0
                ? value : null;
            var relativeAlignment = name is "text-align-all" or "text-align-last" &&
                value is { Kind: CssPropertyValueKind.Keyword, Text: "match-parent" } ? value : null;
            if ((relativeWeight ?? relativeSize ?? relativeAlignment) is { } dependent && parent is not null)
            {
                // Fonts 4 §§2.2.1/2.5. Follow the actual computed parent dependency iteratively.
                pending.Push((state, candidate?.Source, disposition, dependent));
                matching.Observe(parent);
                current = parent;
                continue;
            }
            if (inherit && parent is not null)
            {
                pending.Push((state, candidate?.Source, disposition == NativeCssDisposition.InvalidAtComputedValue
                    ? disposition : NativeCssDisposition.Inherited, null));
                matching.Observe(parent);
                current = parent;
                continue;
            }
            if (value is null || inherit && parent is null ||
                value.Kind == CssPropertyValueKind.Keyword && value.Text is "initial" or "inherit" or "unset")
            {
                if (!_initialValues.TryGetValue(name, out value))
                {
                    value = CssPropertyParser.Parse(name, metadata.InitialValue).Value;
                    _initialValues.Add(name, value);
                }
                if (disposition != NativeCssDisposition.InvalidAtComputedValue) disposition = NativeCssDisposition.Initial;
            }
            if (relativeWeight is not null) value = RelativeFontWeight(relativeWeight.Text, 400, relativeWeight.Span);
            if (relativeAlignment is not null) value = CssPropertyValue.Keyword("start", relativeAlignment.Span);
            value = value.Kind switch
            {
                CssPropertyValueKind.Color => ComputeColor(current, name, value, ref matching),
                CssPropertyValueKind.PaintServer => ComputePaint(current, name, value, candidate?.Source, ref matching),
                CssPropertyValueKind.Url => ComputeUrl(name, value, candidate?.Source),
                CssPropertyValueKind.ImageList => ComputeImages(name, value, candidate?.Source),
                _ => ComputeForElement(current, name, value, ref matching)
            };
            if (adjust && name == "display") value = Display(current, value, ref matching);
            result = new(name, ColorText(current, name, value, ref matching), value, candidate?.Source, disposition);
            (adjust ? state.Computed : state.Unadjusted).Add(name, result);
            _diagnostics?.ComputedPublished(current, name);
            break;
        }
        while (pending.TryPop(out var item))
        {
            _work.Charge(1);
            var value = adjust && name == "display" ? Display(item.State.Element, result.Value!, ref matching) : result.Value!;
            if (item.DependentValue is { } relative)
            {
                if (name is "text-align-all" or "text-align-last")
                    value = MatchParentAlignment(item.State.Element, value, relative.Span, ref matching);
                else
                {
                    var basis = CssMathNumbers.ParseFinite(value.Numeric.Number, value.Numeric.Unit, _work);
                    value = name == "font-size" ? ComputeFontSize(item.State.Element, relative, basis, ref matching)
                        : RelativeFontWeight(relative.Text, basis, relative.Span);
                }
            }
            result = result with
            {
                Text = ColorText(item.State.Element, name, value, ref matching),
                Value = value,
                Source = item.Source,
                Disposition = item.Disposition
            };
            (adjust ? item.State.Computed : item.State.Unadjusted).Add(name, result);
            _diagnostics?.ComputedPublished(item.State.Element, name);
        }
        matching.VerifyRead();
        Verify();
        return result;
    }

    // CSS Color 4: contextual currentColor survives inheritance outside the color property.
    // CSSOM resolves that retained dependency for each element's returned color text.
    private string ColorText(Element element, string name, CssPropertyValue value, ref SelectorMatchWork matching)
    {
        if (IsLineWidth(name) && GetProperty(element, name[..^5] + "style", ref matching).Text is "none" or "hidden")
            return "0px";
        if (name != "color" && value.Kind == CssPropertyValueKind.Color && value.Color.Kind == CssColorKind.CurrentColor)
            return GetProperty(element, "color", ref matching).Text;
        if (value.Kind == CssPropertyValueKind.PaintServer &&
            value.PaintFallback is { Kind: CssPropertyValueKind.Color } fallback && fallback.Color.Kind == CssColorKind.CurrentColor)
            return CssPropertyValue.PaintServer(value.PaintUrl, GetProperty(element, "color", ref matching).Value,
                value.Span, _work, value.PaintUsesSrc).Text;
        return value.Serialize();
    }

    internal IReadOnlyList<CssStyleRule> MatchedRules(Element element, ref SelectorMatchWork matching)
    {
        try
        {
            var state = StateOf(element, ref matching);
            Verify();
            var result = new List<CssStyleRule>();
            foreach (var rule in state.Matches)
                if (ConditionsApply(rule, element, ref matching)) result.Add(rule);
            Verify();
            return result.AsReadOnly();
        }
        catch (Exception exception) { AbortRead(exception); throw; }
    }

    internal IReadOnlyList<NativeCssProperty> Enumerate(Element element, ref SelectorMatchWork matching)
    {
        try { return EnumerateCore(element, ref matching); }
        catch (Exception exception) { AbortRead(exception); throw; }
    }

    private IReadOnlyList<NativeCssProperty> EnumerateCore(Element element, ref SelectorMatchWork matching)
    {
        var own = StateOf(element, ref matching);
        if (own.Enumeration is { } cached) return cached;
        var result = new List<NativeCssProperty>();
        var ordinary = new List<string>();
        foreach (var entry in CssPropertyRegistry.Completed.Values)
        {
            _work.Charge(1);
            if (entry.Longhands.Count == 0) ordinary.Add(entry.Name);
        }
        ordinary.Sort(CompareNames);
        foreach (var name in ordinary) result.Add(GetProperty(element, name, ref matching));
        Variables(own, ref matching);
        var names = new HashSet<string>(new Names(_work));
        foreach (var name in _registrations!.Keys) { _work.Charge(1); names.Add(name); }
        for (var state = own; state is not null; state = state.Parent)
            foreach (var name in CustomNames(state))
            {
                _work.Charge(1);
                if (name.StartsWith("--", StringComparison.Ordinal)) names.Add(name);
            }
        var custom = names.ToList();
        _work.Charge(custom.Count);
        custom.Sort(CompareNames);
        foreach (var name in custom)
        {
            var property = Custom(element, name, ref matching);
            if (property.Disposition != NativeCssDisposition.InvalidAtComputedValue) result.Add(property);
        }
        Verify();
        return own.Enumeration = result.AsReadOnly();
    }

    private int CompareNames(string left, string right)
    {
        var length = System.Math.Min(left.Length, right.Length);
        for (var i = 0; i < length; i++)
        {
            _work.Charge(1);
            var result = left[i].CompareTo(right[i]);
            if (result != 0) return result;
        }
        return left.Length.CompareTo(right.Length);
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
        foreach (var (rule, origin, order, namespaceUri, layer) in _rules)
        {
            _work.Charge(1);
            if (namespaceUri is not null && element.NamespaceUri != namespaceUri) continue;
            var scope = (rule.ParentStyleSheet?.Attachment.OwnerNode ??
                rule.ParentStyleSheet?.EffectiveOwnerNode(_work))?.TreeShadowRoot;
            var hostRule = ReferenceEquals(scope?.Host, element);
            if (origin == NativeCssOrigin.Author && !hostRule && !ReferenceEquals(scope, element.TreeShadowRoot))
                continue;
            _diagnostics?.RuleAttempted(element, rule);
            if (rule.TryMatch(element, out var specificity, null, _selectors, ref matching, scope))
            {
                _diagnostics?.RuleMatched(element, rule);
                state.Matches.Add(rule);
                Add(state, new(rule, rule.Style, origin, specificity, order, false, layer, depth + (hostRule ? 1 : 0)));
            }
        }
        if (_readInlineAttributes && !_inline.ContainsKey(element))
            for (uint i = 0; i < (uint) element.AttributeCount; i++)
            {
                _work.Charge(1);
                var attribute = element.GetAttributeAt(i)!;
                if (attribute.NamespaceUri is not null || !CssSubstitutionArguments.Equals(attribute.LocalName, "style", _work)) continue;
                var block = NativeCssStyleSheets.InlineOf(element, _work);
                Verify();
                _inline.Add(element, (block, block.Stamp));
                break;
            }
        if (_inline.TryGetValue(element, out var inline))
            Add(state, new(null, inline.Block, NativeCssOrigin.Author, default, _rules.Count, true,
                EncapsulationDepth: depth));
        matching.VerifyRead();
        Verify();
        _states.Add(element, state);
        _diagnostics?.StatePublished(element);
        return state;
    }

    private static void Add(State state, NativeCssSource source)
    {
        state.Sources.Add(source);
    }

    private List<Candidate> Candidates(State state, string name, ref SelectorMatchWork matching)
    {
        if (state.Candidates.TryGetValue(name, out var cached)) return cached;
        var candidates = new List<Candidate>();
        var logicalGroup = CssBorderPropertyParser.LogicalGroup(name);
        foreach (var source in state.Sources)
        {
            _work.Charge(1);
            if (SourceApplies(source, state.Element, name, ref matching) &&
                source.Block.ResolveProperty(name, _work) is { } declaration)
                candidates.Add(new(declaration, source, DeclarationOrder: logicalGroup.Count == 0 ? 0 :
                    source.Block.DeclarationOrder(declaration, _work)));
            foreach (var logical in logicalGroup)
            {
                _work.Charge(1);
                if (!SourceApplies(source, state.Element, logical, ref matching) ||
                    source.Block.ResolveProperty(logical, _work) is not { } mapped ||
                    PhysicalBorderName(state.Element, logical, ref matching) != name) continue;
                candidates.Add(new(mapped, source, DeclarationOrder: source.Block.DeclarationOrder(mapped, _work)));
            }
        }
        candidates.Sort((left, right) => { _work.Charge(1); return Compare(right, left); });
        Verify();
        state.Candidates.Add(name, candidates);
        return candidates;
    }

    private HashSet<string> CustomNames(State state)
    {
        var result = new HashSet<string>(new Names(_work));
        foreach (var source in state.Sources)
            foreach (var name in source.Block.CustomPropertyNames(_work))
            {
                _work.Charge(name.Length);
                result.Add(name);
            }
        return result;
    }

    private static int Compare(Candidate left, Candidate right)
    {
        var comparison = Compare(left.Declaration.IsImportant, left.Source, right.Declaration.IsImportant, right.Source);
        return comparison != 0 ? comparison : left.DeclarationOrder.CompareTo(right.DeclarationOrder);
    }

    private static int Compare(bool leftImportant, NativeCssSource left, bool rightImportant, NativeCssSource right)
    {
        var a = leftImportant ? 5 - (int) left.Origin : (int) left.Origin;
        var b = rightImportant ? 5 - (int) right.Origin : (int) right.Origin;
        var comparison = a.CompareTo(b);
        if (comparison != 0) return comparison;
        // CSS Cascade 5: outer contexts win normally; inner contexts win !important.
        // https://drafts.csswg.org/css-cascade-5/#cascade-context
        comparison = left.EncapsulationDepth.CompareTo(right.EncapsulationDepth);
        if (comparison != 0) return leftImportant ? comparison : -comparison;
        comparison = left.Inline.CompareTo(right.Inline);
        if (comparison != 0) return comparison;
        comparison = (left.Layer?.Rank ?? int.MaxValue).CompareTo(right.Layer?.Rank ?? int.MaxValue);
        if (comparison != 0) return leftImportant ? -comparison : comparison;
        comparison = left.Specificity.CompareTo(right.Specificity);
        return comparison != 0 ? comparison : left.Order.CompareTo(right.Order);
    }

    private (CssCustomDeclaration Declaration, NativeCssSource Source)? CustomWinner(State state, string name, ref SelectorMatchWork matching)
    {
        var candidates = new List<(CssCustomDeclaration Declaration, NativeCssSource Source)>();
        foreach (var source in state.Sources)
        {
            _work.Charge(1);
            if (SourceApplies(source, state.Element, name, ref matching) &&
                source.Block.ResolveCustomProperty(name, _work) is { } declaration) candidates.Add((declaration, source));
        }
        candidates.Sort((left, right) =>
        {
            _work.Charge(1);
            return Compare(right.Declaration.IsImportant, right.Source, left.Declaration.IsImportant, left.Source);
        });
        var excludedOrigins = new bool[3];
        var excludedRules = new HashSet<CssDeclarationBlock>();
        List<NativeCssSource>? excludedLayers = null;
        foreach (var candidate in candidates)
        {
            _work.Charge(1);
            var origin = (int) candidate.Source.Origin;
            if (excludedOrigins[origin] || excludedRules.Contains(candidate.Source.Block) || LayerExcluded(candidate.Source, excludedLayers)) continue;
            switch (candidate.Declaration.WideKeyword)
            {
                case "revert":
                    for (var i = origin; i < excludedOrigins.Length; i++) excludedOrigins[i] = true;
                    continue;
                case "revert-rule": excludedRules.Add(candidate.Source.Block); continue;
                case "revert-layer":
                    (excludedLayers ??= []).Add(candidate.Source);
                    continue;
            }
            return candidate;
        }
        return null;
    }

    private Candidate? Winner(State state, string name, ref SelectorMatchWork matching, bool substitute = false)
    {
        var candidates = Candidates(state, name, ref matching);
        var excludedOrigins = new bool[3];
        var excludedRules = new HashSet<CssDeclarationBlock>();
        List<NativeCssSource>? excludedLayers = null;
        foreach (var candidate in candidates)
        {
            _work.Charge(1);
            var origin = (int) candidate.Source.Origin;
            if (excludedOrigins[origin] || excludedRules.Contains(candidate.Source.Block) || LayerExcluded(candidate.Source, excludedLayers)) continue;
            var value = candidate.Declaration.Value;
            var deferred = substitute && value.Kind == CssPropertyValueKind.Deferred;
            if (deferred) value = Substitute(state, candidate, name, ref matching);
            if (value?.Kind == CssPropertyValueKind.Keyword && value.Text == "revert")
            {
                for (var i = origin; i < excludedOrigins.Length; i++) excludedOrigins[i] = true;
                continue;
            }
            if (value?.Kind == CssPropertyValueKind.Keyword && value.Text == "revert-rule")
            {
                excludedRules.Add(candidate.Source.Block);
                continue;
            }
            if (value?.Kind == CssPropertyValueKind.Keyword && value.Text == "revert-layer")
            {
                (excludedLayers ??= []).Add(candidate.Source);
                continue;
            }
            return deferred ? candidate with { Resolved = value, WasSubstituted = true } : candidate;
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
            if (InheritanceParent(current.Element) is not { } parent) break;
            current.Parent ??= StateOf(parent, ref matching);
            current = current.Parent;
        }
        var inherited = current.Variables;
        while (pending.TryPop(out current))
        {
            _work.Charge(1);
            // Copies share the existing observation/work cell; no per-name cadence reset.
            matching.EnsureCell();
            inherited = CssSubstitutionSnapshot.CreateQueryLayer(inherited, new QueryVariables(this, current, matching));
            matching.VerifyRead();
            Verify();
            current.Variables = inherited;
        }
        return state.Variables!;
    }

    private NativeCssProperty Custom(Element element, string name, ref SelectorMatchWork matching)
    {
        var state = StateOf(element, ref matching);
        if (state.Computed.TryGetValue(name, out var cached))
        {
            _diagnostics?.CacheHit(element, name);
            return cached;
        }
        var snapshot = Variables(state, ref matching);
        var text = "";
        var disposition = NativeCssDisposition.InvalidAtComputedValue;
        if (snapshot.TryGet(name, _work, out var binding))
        {
            var result = ResolveCustomBinding(binding);
            if (result.Kind == CssSubstitutionResultKind.PendingFeature)
                throw new CssIncompleteGrammarException(name, "C6:" + result.PendingFeature, default);
            if (result.Kind == CssSubstitutionResultKind.Tokens)
            {
                text = result.Value.SerializeCustomProperty(_work);
                disposition = NativeCssDisposition.Cascaded;
            }
        }
        var property = new NativeCssProperty(name, text, null, CustomWinner(state, name, ref matching)?.Source, disposition);
        Verify();
        state.Computed.Add(name, property);
        _diagnostics?.ComputedPublished(element, name);
        return property;
    }

    private CssPropertyValue? Substitute(State state, Candidate candidate, string name, ref SelectorMatchWork matching)
    {
        var declaration = candidate.Declaration;
        name = declaration.Name;
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

    internal CssValueWork Work => _work;

    internal void Verify()
    {
        if (_aborted) throw new InvalidOperationException("The native CSS read context was aborted.");
        var inlineCount = _inline.Count;
        _work.Charge(inlineCount);
        // The graph's final host checkpoint precedes every witness comparison. A later callback
        // could mutate a sheet or native document that an earlier comparison already accepted.
        var current = _sheetRevisions.IsCurrent(_work);
        if (!current || _inline.Count != inlineCount || !_resourceStamp.CanReuse || NativeCssStyleSheets.Stamp(_document) != _resourceStamp ||
            _documentStamp == ulong.MaxValue || _document.MutationStamp != _documentStamp)
            throw new InvalidOperationException(Invalidated);
        VerifyControlFactsSeed();
        var compared = 0;
        foreach (var inline in _inline.Values)
        {
            if ((compared++ & 1023) == 0) _work.Token.ThrowIfCancellationRequested();
            if (!inline.Stamp.CanReuse || inline.Block.Stamp != inline.Stamp)
                throw new InvalidOperationException(Invalidated);
        }
        _work.Token.ThrowIfCancellationRequested();
        _readWitness?.Invoke();
    }

    private void VerifyControlFactsSeed()
    {
        if (_selectors.ControlFactsFactory is not { } factory) return;
        if (!ReferenceEquals(_selectors.Document, _document) || _selectors.ControlFactsContext is null ||
            _selectors.ControlFactsRevision == ulong.MaxValue ||
            factory.ReadRevision(_selectors.ControlFactsContext, _document) != _selectors.ControlFactsRevision)
            throw new InvalidOperationException(Invalidated);
    }

    // CSS Scoping 1: inheritance follows the flat tree; selector rules retain their tree scope.
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

    private sealed record Candidate(CssDeclaration Declaration, NativeCssSource Source,
        CssPropertyValue? Resolved = null, bool WasSubstituted = false, int DeclarationOrder = 0);
    private sealed class State(Element element, CssValueWork work)
    {
        internal Element Element { get; } = element;
        internal State? Parent;
        internal bool InlinifiesChildren;
        internal CssSubstitutionSnapshot? Variables;
        internal IReadOnlyList<NativeCssProperty>? Enumeration;
        internal Dictionary<string, List<Candidate>> Candidates { get; } = new(new Names(work));
        internal Dictionary<string, NativeCssProperty> Computed { get; } = new(new Names(work));
        internal Dictionary<string, NativeCssProperty> Unadjusted { get; } = new(new Names(work));
        internal Dictionary<CssPendingShorthand, CssDeclaration[]?> Shorthands { get; } = new(ReferenceEqualityComparer.Instance);
        internal List<CssStyleRule> Matches { get; } = [];
        internal List<NativeCssSource> Sources { get; } = [];
    }
    private sealed class Names(CssValueWork work) : IEqualityComparer<string>
    {
        public bool Equals(string? x, string? y) => ReferenceEquals(x, y) || x is not null && y is not null &&
            CssSubstitutionArguments.Equals(x, y, work);
        public int GetHashCode(string value) => unchecked((int) CssSubstitutionArguments.Hash(value, work));
    }
}
