using Jint.HtmlParser;
using Jint.HtmlParser.Css.Model;
using Jint.HtmlParser.Css.Selectors;
using Jint.HtmlParser.Css.Values;
using Jint.HtmlParser.Css.Values.Math;
using Jint.HtmlParser.Css.Values.Properties;
using Jint.HtmlParser.Css.Values.References;

namespace Jint.Browser.Styling;

internal sealed partial class NativeCssQuery
{
    // Query-affine layer. Immutable input/programs/models never retain this callback or its cache.
    private sealed class QueryVariables(NativeCssQuery query, State state, SelectorMatchWork matching) :
        ICssQueryBindingResolver, ICssQueryBindingComputer
    {
        private SelectorMatchWork _matching = matching;
        private readonly Dictionary<string, CssSubstitutionBinding?> _bindings = new(new Names(query._work));

        public bool TryResolve(string name, CssValueWork work, out CssSubstitutionBinding binding)
        {
            try
            {
                if (!ReferenceEquals(work, query._work))
                    throw new InvalidOperationException("A query-bound variable layer requires its invocation work.");
                _matching.VerifyRead();
                query.Verify();
                using var guard = query.EnterDependency(state.Element, "variable-binding", name);
                if (!_bindings.TryGetValue(name, out var selected))
                {
                    var candidate = query.CustomWinner(state, name, ref _matching);
                    selected = candidate is null || candidate.Value.Declaration.WideKeyword is "inherit" or "unset"
                        ? null : candidate.Value.Declaration.Binding;
                    if (query.Registration(name) is { } registration)
                    {
                        var keyword = candidate?.Declaration.WideKeyword;
                        var inherit = keyword == "inherit" || (candidate is null || keyword == "unset") && registration.Inherits;
                        if (inherit)
                            selected = query.InheritanceParent(state.Element) is null ? InitialBinding(registration) : null;
                        else if (candidate is null || keyword is "initial" or "unset")
                            selected = InitialBinding(registration);
                    }
                    // initial is a local guaranteed-invalid binding; it shadows the parent.
                    _matching.VerifyRead();
                    query.Verify();
                    _bindings.Add(name, selected);
                }
                _matching.VerifyRead();
                query.Verify();
                binding = selected ?? default;
                return selected is not null;
            }
            catch (Exception exception) { _bindings.Clear(); query.AbortRead(exception); throw; }
        }

        public bool RequiresComputation(string name, CssValueWork work) => query.Registration(name) is not null;

        public CssSubstitutionResult Compute(string name, CssSubstitutionResult value, CssValueWork work)
        {
            _matching.VerifyRead();
            query.Verify();
            var registration = query.Registration(name)!;
            using var guard = query.EnterDependency(state.Element, "registered-value", name);
            var computed = value.Kind == CssSubstitutionResultKind.Tokens
                ? query.ComputeRegistered(state.Element, registration, value, ref _matching) : value;
            if (computed.Kind == CssSubstitutionResultKind.GuaranteedInvalid)
            {
                if (registration.Inherits && state.Parent is { } parent &&
                    parent.Variables!.TryGet(name, work, out var inherited))
                    computed = query.ResolveCustomBinding(inherited);
                else if (registration.Initial is { } initial)
                    computed = query.ComputeRegistered(state.Element, registration,
                        Literal(initial, work), ref _matching);
            }
            _matching.VerifyRead();
            query.Verify();
            return computed;
        }

        private static CssSubstitutionBinding InitialBinding(CssPropertyRule rule) => rule.Initial is { } input
            ? CssSubstitutionBinding.Specified(rule.Name, input, false)
            : CssSubstitutionBinding.Invalid(rule.Name, false);
    }

    private Dictionary<string, CssPropertyRule>? _registrations;

    private CssPropertyRule? Registration(string name)
    {
        _rules ??= BuildRules();
        _work.Charge(name.Length);
        return _registrations!.GetValueOrDefault(name);
    }

    private static CssSubstitutionResult Literal(CssReferenceInput input, CssValueWork work) =>
        CssSubstitutionResult.Tokens(CssSubstitutedValue.Create(CssSegment.FromInput(input, work), input.MaxNestingDepth, work));

    private CssSubstitutionResult ResolveCustomBinding(CssSubstitutionBinding binding) => binding.Kind switch
    {
        CssSubstitutionBindingKind.Specified => CssSubstitutionExecutor.Resolve(binding.Input, binding.Scope!, _environment,
            new(binding.Name, CssReferenceUse.CustomPropertyValue, true), _work),
        CssSubstitutionBindingKind.Computed => CssSubstitutionResult.Tokens(binding.Value),
        CssSubstitutionBindingKind.Pending => CssSubstitutionResult.Pending(binding.PendingFeature),
        _ => CssSubstitutionResult.Invalid()
    };

    private CssSubstitutionResult ComputeRegistered(Element element, CssPropertyRule registration,
        CssSubstitutionResult result, ref SelectorMatchWork matching)
    {
        if (registration.Syntax.IsUniversal) return result;
        var value = registration.Syntax.Match(result.Value.AsReferenceInput(_work), _work);
        if (value is null) return CssSubstitutionResult.Invalid();
        var builder = new System.Text.StringBuilder();
        foreach (var component in value.Values)
        {
            _work.Charge(1);
            var computed = component.Kind == CssPropertyValueKind.Color
                ? ComputeColor(element, registration.Name, component, ref matching)
                : ComputeForElement(element, registration.Name, component, ref matching);
            if (value.Type == CssRegisteredType.Integer && computed.Kind == CssPropertyValueKind.Numeric)
                computed = Number("z-index", new CssMathNumeric(
                    CssMathNumbers.ParseFinite(computed.Numeric.Number, computed.Numeric.Unit, _work),
                    CssNumericKind.Number, CssUnit.None, computed.Span));
            if (value.Type == CssRegisteredType.Resolution && computed.Kind == CssPropertyValueKind.Numeric)
                computed = Number(registration.Name, new CssMathNumeric(
                    System.Math.Max(0, CssMathNumbers.ParseFinite(computed.Numeric.Number, computed.Numeric.Unit, _work)),
                    CssNumericKind.Dimension, CssUnit.Dppx, computed.Span));
            if (builder.Length != 0) builder.Append(value.Separator);
            var text = ColorText(element, registration.Name, computed, ref matching);
            _work.Charge(text.Length);
            builder.Append(text);
        }
        return Literal(CssRegisteredSyntax.ParseInput(builder.ToString(), _work), _work);
    }
}
