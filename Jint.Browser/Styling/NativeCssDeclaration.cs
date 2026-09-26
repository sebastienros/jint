using Jint.HtmlParser.Css.Model;

namespace Jint.Browser.Styling;

// CSSOM §6.6.1. One receiver vocabulary for specified and computed declaration bindings.
internal abstract class NativeCssDeclaration
{
    internal abstract string CssText { get; set; }
    internal abstract int Length { get; }
    internal abstract CssRule? ParentRule { get; }
    internal abstract string Item(int index);
    internal abstract string GetPropertyValue(string name);
    internal abstract string GetPropertyPriority(string name);
    internal abstract void SetProperty(string name, string value, string? priority = null);
    internal abstract string RemoveProperty(string name);
    internal void Update(string value) => CssText = value;
}
