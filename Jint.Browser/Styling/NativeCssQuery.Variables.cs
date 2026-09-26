using Jint.HtmlParser.Css.Selectors;
using Jint.HtmlParser.Css.Values;
using Jint.HtmlParser.Css.Values.References;

namespace Jint.Browser.Styling;

internal sealed partial class NativeCssQuery
{
    // Query-affine layer. Immutable input/programs/models never retain this callback or its cache.
    private sealed class QueryVariables(NativeCssQuery query, State state, SelectorMatchWork matching) : ICssQueryBindingResolver
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
            catch { _bindings.Clear(); query.AbortRead(); throw; }
        }
    }
}
