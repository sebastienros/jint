using Jint.HtmlParser;
using Jint.HtmlParser.Css.Conditions;
using Jint.HtmlParser.Css.Media;
using Jint.HtmlParser.Css.Model;
using Jint.HtmlParser.Css.Selectors;
using Jint.HtmlParser.Css.Values.Properties;
using Jint.HtmlParser.Css.Values.References;

namespace Jint.Browser.Styling;

// Values only: the query neither owns nor constructs a renderer or a browser runtime.
internal interface INativeCssContainerMetrics
{
    bool HasBox(Element element, ref SelectorMatchWork matching);
    double Width(Element element, ref SelectorMatchWork matching);
}

internal sealed partial class NativeCssQuery
{
    private INativeCssContainerMetrics? _containerMetrics;
    private Action? _readWitness;

    internal void AttachReadWitness(Action witness)
    {
        if (_readWitness is not null || _states.Count != 0)
            throw new InvalidOperationException("The read witness must be attached once before querying styles.");
        _readWitness = witness;
        Verify();
    }
    private readonly Dictionary<(Element Element, CssContainerRule Rule), CssMediaTruth> _containerConditions = new();
    private readonly Dictionary<Element, double> _containerWidths = new();
    private readonly List<(Element Element, string Kind, object Key)> _activeDependencies = new();
    private bool _aborted;

    internal void AttachContainerMetrics(INativeCssContainerMetrics metrics)
    {
        Verify();
        if (_containerMetrics is not null || _states.Count != 0)
            throw new InvalidOperationException("Container metrics must be attached once before querying styles.");
        _containerMetrics = metrics;
    }

    private DependencyScope EnterDependency(Element element, string kind, object key)
    {
        if (_activeDependencies.Count >= 64) throw new NativeCssDependencyLimitException();
        var dependency = (element, kind, key);
        foreach (var active in _activeDependencies)
        {
            _work.Charge(1);
            if (!ReferenceEquals(active.Element, element) || active.Kind != kind) continue;
            if (ReferenceEquals(active.Key, key) || active.Key is string left && key is string right &&
                CssSubstitutionArguments.Equals(left, right, _work))
            {
                // Relative units and currentColor can add ordinary-property edges to a
                // registered variable cycle. Keep this distinct from a container cycle.
                foreach (var registration in _activeDependencies)
                {
                    _work.Charge(1);
                    if (registration.Kind == "registered-value")
                        throw new CssIncompleteGrammarException((string) registration.Key, "C6:registered-property-cycle", default);
                }
                throw new CssIncompleteGrammarException("container", "C6:container-layout-cycle", default);
            }
        }
        _activeDependencies.Add(dependency);
        return new(this, dependency);
    }

    private readonly struct DependencyScope(NativeCssQuery query, (Element Element, string Kind, object Key) dependency) : IDisposable
    {
        public void Dispose()
        {
            // Removal cannot poll or throw while unwinding an original callback/constraint failure.
            for (var i = query._activeDependencies.Count - 1; i >= 0; i--)
                if (ReferenceEquals(query._activeDependencies[i].Element, dependency.Element) &&
                    ReferenceEquals(query._activeDependencies[i].Key, dependency.Key) &&
                    query._activeDependencies[i].Kind == dependency.Kind)
                { query._activeDependencies.RemoveAt(i); break; }
        }
    }

    private void AbortRead(Exception exception)
    {
        // An unsupported ordinary property does not invalidate completed independent reads.
        // Geometry failures still discard the whole invocation, including nested property failures.
        if (exception is CssIncompleteGrammarException { PropertyName: not "container" } &&
            !_activeDependencies.Exists(static dependency => dependency.Kind is "condition" or "metric"))
            return;
        _aborted = true;
        _containerWidths.Clear();
        _containerConditions.Clear();
        _states.Clear();
    }

    private bool SourceApplies(NativeCssSource source, Element element, string name, ref SelectorMatchWork matching)
        => source.Block.MayAffectProperty(name, _work) && ConditionsApply(source.Rule, element, ref matching);

    private bool ConditionsApply(CssStyleRule? rule, Element element, ref SelectorMatchWork matching)
    {
        // All outer conditions precede inner conditions. Their owning tree scope was matched already.
        var chain = new Stack<CssContainerRule>();
        for (CssRule? owner = rule?.ParentRule; owner is not null; owner = owner.ParentRule)
        {
            _work.Charge(1);
            if (owner is CssContainerRule container) chain.Push(container);
        }
        while (chain.TryPop(out var condition))
            if (ContainerCondition(element, condition, ref matching) != CssMediaTruth.True) return false;
        Verify();
        return true;
    }

    private CssMediaTruth ContainerCondition(Element element, CssContainerRule rule, ref SelectorMatchWork matching)
    {
        Verify();
        if (_containerConditions.TryGetValue((element, rule), out var cached)) return cached;
        using var guard = EnterDependency(element, "condition", rule);
        if (rule.Condition.PendingDependency is { } pending)
            throw new CssIncompleteGrammarException("container", pending, rule.SourceSpan);
        var container = SelectContainer(element, rule, ref matching);
        // No eligible ancestor is spec unknown BEFORE applying not/and/or.
        var result = CssMediaTruth.Unknown;
        if (container is not null)
        {
            var localMatching = matching;
            try
            {
                result = rule.Condition.Evaluate(feature => EvaluateFeature(container, feature, ref localMatching), _work);
            }
            finally { matching = localMatching; }
        }
        matching.VerifyRead();
        Verify();
        _containerConditions.Add((element, rule), result);
        return result;
    }

    private Element? SelectContainer(Element subject, CssContainerRule rule, ref SelectorMatchWork matching)
    {
        foreach (var instruction in rule.Condition.Instructions)
        {
            _work.Charge(1);
            if (instruction.Operation == CssMediaOperation.Unknown || instruction.Feature is { Axis: CssContainerAxis.Unknown, Dependency: null })
                return null;
        }
        for (var ancestor = InheritanceParent(subject); ancestor is not null; ancestor = InheritanceParent(ancestor))
        {
            _work.Charge(1);
            matching.Observe(ancestor);
            if (rule.ContainerName.Length != 0)
            {
                var names = GetProperty(ancestor, "container-name", ref matching).Value!;
                var matches = false;
                if (names.Kind == CssPropertyValueKind.IdentifierList)
                    foreach (var name in names.Identifiers)
                    {
                        _work.Charge(name.Length);
                        matches |= CssSubstitutionArguments.Equals(name, rule.ContainerName, _work);
                    }
                if (!matches) continue;
            }
            var type = GetProperty(ancestor, "container-type", ref matching).Text;
            var size = type is "size" or "size scroll-state";
            var inline = size || type is "inline-size" or "inline-size scroll-state";
            var eligible = true;
            foreach (var instruction in rule.Condition.Instructions)
            {
                _work.Charge(1);
                if (instruction.Feature is not { } feature) continue;
                switch (feature.Axis)
                {
                    case CssContainerAxis.Width:
                    case CssContainerAxis.Height:
                    case CssContainerAxis.InlineSize:
                    case CssContainerAxis.BlockSize:
                    case CssContainerAxis.Both:
                        if (!inline) { eligible = false; break; }
                        if (size) break;
                        var horizontal = GetProperty(ancestor, "writing-mode", ref matching).Text == "horizontal-tb";
                        eligible &= feature.Axis == CssContainerAxis.InlineSize ||
                            feature.Axis == CssContainerAxis.Width && horizontal || feature.Axis == CssContainerAxis.Height && !horizontal;
                        break;
                    case CssContainerAxis.ScrollState:
                        eligible &= type is "scroll-state" or "inline-size scroll-state" or "size scroll-state";
                        break;
                }
                if (!eligible) break;
            }
            if (eligible) return ancestor;
        }
        Verify();
        return null;
    }

    private CssMediaTruth EvaluateFeature(Element container, CssContainerFeature feature, ref SelectorMatchWork matching)
    {
        if (feature.Dependency is { } dependency)
            throw new CssIncompleteGrammarException("container", dependency, default);
        if (feature.Axis is not (CssContainerAxis.Width or CssContainerAxis.InlineSize)) return CssMediaTruth.Unknown;
        if (GetProperty(container, "writing-mode", ref matching).Text != "horizontal-tb")
            throw new CssIncompleteGrammarException("container", "C6:container-writing-mode", default);
        if (_containerMetrics is null)
            throw new CssIncompleteGrammarException("container", "C6:container-layout-metric", default);
        using var guard = EnterDependency(container, "metric", "width");
        // The finite box provider cannot represent flat-tree geometry in a shadow-containing tree.
        for (Element? ancestor = container; ancestor is not null; ancestor = ancestor.ParentNode as Element)
        {
            _work.Charge(1);
            if (ancestor.TreeShadowRoot is not null || ancestor.AttachedShadowRoot is not null)
                throw new CssIncompleteGrammarException("container", "C6:container-flat-tree-metric", default);
        }
        Verify();
        if (!_containerMetrics.HasBox(container, ref matching))
        {
            matching.VerifyRead();
            Verify();
            return CssMediaTruth.Unknown;
        }
        matching.VerifyRead();
        Verify();
        if (!_containerWidths.TryGetValue(container, out var width))
        {
            width = _containerMetrics.Width(container, ref matching);
            matching.VerifyRead();
            Verify();
            if (!double.IsFinite(width) || width < 0)
                throw new CssIncompleteGrammarException("container", "C6:container-layout-metric", default);
            _containerWidths.Add(container, width);
        }
        var matches = feature.Comparison switch
        {
            CssMediaComparison.Boolean => width != 0,
            CssMediaComparison.Equal => width == feature.Pixels,
            CssMediaComparison.Less => width < feature.Pixels,
            CssMediaComparison.LessEqual => width <= feature.Pixels,
            CssMediaComparison.Greater => width > feature.Pixels,
            CssMediaComparison.GreaterEqual => width >= feature.Pixels,
            _ => false
        };
        return matches ? CssMediaTruth.True : CssMediaTruth.False;
    }
}

internal sealed class NativeCssDependencyLimitException() : InvalidOperationException("Native CSS read limit container-dependency-depth of 64 was exceeded.");
