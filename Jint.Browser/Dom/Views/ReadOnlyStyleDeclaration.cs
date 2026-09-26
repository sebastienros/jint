using Jint.Browser.Runtime;
using Jint.Browser.Styling;
using Jint.HtmlParser;
using Jint.Runtime;
using Jint.WebApi.DomException;

namespace Jint.Browser.Dom.Views;

// CSSOM §6.6.1: a live declaration with its computed and read-only flags set.
// Each script read creates a fresh native query; no cached query crosses DOM/CSSOM writes.
internal sealed class ReadOnlyStyleDeclaration : NativeCssDeclaration
{
    private readonly PageRuntime _runtime;
    private readonly Element _element;

    internal ReadOnlyStyleDeclaration(PageRuntime runtime, Element element, NativeCssComputedStyle? computed = null)
    {
        _runtime = runtime;
        _element = element;
    }

    internal override string Item(int index) => Current()[index];
    internal override Jint.HtmlParser.Css.Model.CssRule? ParentRule => null;
    internal override int Length => Current().Length;
    internal override string CssText
    {
        get
        {
            _runtime.Engine.Constraints.Check();
            return "";
        }
        set => Refuse("cssText");
    }

    internal override string GetPropertyValue(string propertyName)
    {
        var property = Current().GetProperty(propertyName);
        var document = _element.OwnerDocument;
        var host = document is null ? null : NativeCssStyleSheets.RealmOf(document);
        var current = host is null ? null : PageRuntime.FindBrowsingContext(host.Engine, document);
        return property.Source is null && property.Text == "auto" && property.Name is "width" or "height" && current is not null
            ? ResolvedStyle.ValueOf(property.Name, _element, current) ?? property.Text : property.Text;
    }
    internal NativeCssProperty GetProperty(string propertyName) => Current().GetProperty(propertyName);
    internal override string GetPropertyPriority(string propertyName) => Current().GetPropertyPriority(propertyName);
    internal IReadOnlyList<NativeCssProperty> Enumerate() => Current().Enumerate();
    internal override void SetProperty(string name, string value, string? priority = null) => Refuse("setProperty");
    internal override string RemoveProperty(string name)
    {
        Refuse("removeProperty");
        return "";
    }

    private NativeCssComputedStyle Current()
    {
        var document = _element.OwnerDocument ?? throw new ArgumentException("Element needs a document.");
        var realm = NativeCssStyleSheets.RealmOf(document) ?? _runtime.Dom.RealmOfDocument(document);
        NativeCssStyleSheets.Associate(realm, document);
        return CssCascade.Traversal.For(document)!.Of(_element);
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
