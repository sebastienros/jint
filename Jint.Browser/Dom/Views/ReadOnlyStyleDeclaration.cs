using Jint.Browser.Runtime;
using Jint.Browser.Styling;
using Jint.HtmlParser;
using Jint.Runtime;
using Jint.WebApi.DomException;

namespace Jint.Browser.Dom.Views;

// CSSOM §6.6.1: a live declaration with its computed and read-only flags set.
// Each script read creates a fresh native query; no cached query crosses DOM/CSSOM writes.
internal sealed class ReadOnlyStyleDeclaration
{
    private readonly PageRuntime _runtime;
    private readonly Element _element;

    internal ReadOnlyStyleDeclaration(PageRuntime runtime, Element element, NativeCssComputedStyle? computed = null)
    {
        _runtime = runtime;
        _element = element;
    }

    internal string this[int index] => Current()[index];
    internal string this[string name] => GetPropertyValue(name);
    internal int Length => Current().Length;
    internal string CssText
    {
        get
        {
            _runtime.Engine.Constraints.Check();
            return "";
        }
        set => Refuse("cssText");
    }

    internal string GetPropertyValue(string propertyName)
    {
        var property = Current().GetProperty(propertyName);
        return property.Source is null && property.Text == "auto" && property.Name is "width" or "height"
            ? ResolvedStyle.ValueOf(property.Name, _element, _runtime) ?? property.Text : property.Text;
    }
    internal NativeCssProperty GetProperty(string propertyName) => Current().GetProperty(propertyName);
    internal string GetPropertyPriority(string propertyName) => Current().GetPropertyPriority(propertyName);
    internal IReadOnlyList<NativeCssProperty> Enumerate() => Current().Enumerate();
    internal void SetProperty(string name, string value, string? priority = null) => Refuse("setProperty");
    internal void Update(string value) => Refuse("cssText");
    internal string RemoveProperty(string name)
    {
        Refuse("removeProperty");
        return "";
    }

    private NativeCssComputedStyle Current()
    {
        var document = _element.OwnerDocument ?? throw new ArgumentException("Element needs a document.");
        var realm = _runtime.Dom.RealmOfDocument(document);
        NativeCssStyleSheets.Associate(realm, document);
        var input = NativeCssStyleSheets.CreateQuery(document, realm);
        return new(input.Query, _element, input.Matching);
    }

    private void Refuse(string member)
    {
        var engine = _runtime.Engine;
        var error = engine._mainRealm.Intrinsics.DomException.CreateException(
            DomExceptionNames.NoModificationAllowed,
            "Failed to execute '" + member + "' on 'CSSStyleDeclaration': These styles are computed, and therefore read-only.");
        Throw.JavaScriptException(engine, error, engine.GetLastSyntaxElement()?.Location ?? default);
    }
}
