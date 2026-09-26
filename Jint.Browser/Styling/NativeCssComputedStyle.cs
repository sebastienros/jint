using Jint.HtmlParser;
using Jint.HtmlParser.Css.Selectors;

namespace Jint.Browser.Styling;

// A read-only view for one synchronous Browser query. A live script wrapper creates a fresh
// query on each read; traversal callers share this view only while its snapshots are current.
internal sealed class NativeCssComputedStyle(NativeCssQuery query, Element element, SelectorMatchWork matching, Action? witness = null)
{
    private SelectorMatchWork _matching = matching;
    internal Jint.HtmlParser.Css.Values.CssValueWork Work => query.Work;
    internal void VerifyRead()
    {
        _matching.VerifyRead();
        query.Verify();
        witness?.Invoke();
    }
    internal Element Element { get; } = element;
    internal string GetPropertyValue(string name) => query.GetProperty(Element, name, ref _matching).Text;
    internal NativeCssProperty GetProperty(string name) => query.GetProperty(Element, name, ref _matching);
    internal NativeCssProperty GetNormalizedProperty(string name) => query.GetNormalizedProperty(Element, name, ref _matching);
    internal bool HasPropertyInput(string name) => query.HasPropertyInput(Element, name, ref _matching);
    internal NativeCssComputedStyle For(Element target) => new(query, target, _matching, witness);
    internal string GetPropertyPriority(string name)
    {
        query.GetProperty(Element, name, ref _matching);
        return "";
    }
    // CSSOM's computed flag makes cssText empty, even though individual values are readable.
    internal string CssText
    {
        get
        {
            _matching.VerifyRead();
            return "";
        }
    }
    internal int Length => Enumerate().Count;
    internal string this[int index]
    {
        get
        {
            var properties = Enumerate();
            return (uint) index < (uint) properties.Count ? properties[index].Name : "";
        }
    }
    internal IReadOnlyList<NativeCssProperty> Enumerate() => query.Enumerate(Element, ref _matching);
    internal IReadOnlyList<Jint.HtmlParser.Css.Model.CssStyleRule> MatchedRules() =>
        query.MatchedRules(Element, ref _matching);
}
