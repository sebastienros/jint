using AngleSharp.Dom;
using AngleSharp.Html.Dom;

namespace Jint.Browser.Dom;

/// <summary>https://html.spec.whatwg.org/multipage/scripting.html#the-template-element: cloning steps.</summary>
internal static class DomTemplateCloning
{
    /// <summary>A shallow template copy has no content children; correct the detached native copy.</summary>
    internal static void ClearShallowContent(INode copy)
    {
        if (copy is IHtmlTemplateElement template)
        {
            while (template.Content.FirstChild is { } child)
            {
                template.Content.RemoveChild(child);
            }
        }
    }
}
