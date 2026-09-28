using Jint.HtmlParser;
using Jint.HtmlParser.Css.Selectors;

namespace Jint.Browser.Styling;

internal sealed partial class NativeCssQuery
{
    private void WarmParents(Element element, ref SelectorMatchWork matching)
    {
        var pending = new Stack<Element>();
        var parent = InheritanceParent(element);
        while (parent is not null && !StateOf(parent, ref matching).Computed.ContainsKey("display"))
        {
            _work.Charge(1);
            pending.Push(parent);
            parent = InheritanceParent(parent);
        }
        while (pending.TryPop(out parent))
        {
            _work.Charge(1);
            GetProperty(parent, "display", ref matching);
        }
    }

    // CSS Display 3 §2.7: preserve the box kinds used by renderless visibility and flex placement.
    private string Display(Element element, string value, ref SelectorMatchWork matching)
    {
        var root = element.ParentNode is Document;
        if (value == "none" || value == "contents" && !root) return value;
        var parent = InheritanceParent(element);
        string? parentDisplay = null;
        while (parent is not null)
        {
            _work.Charge(1);
            parentDisplay = GetProperty(parent, "display", ref matching).Text;
            if (parentDisplay != "contents") break;
            parent = InheritanceParent(parent);
        }
        var positioned = GetProperty(element, "position", ref matching).Text is "absolute" or "fixed";
        if (!root && !positioned && parentDisplay is not ("flex" or "grid" or "inline-flex" or "inline-grid")) return value;
        return value switch
        {
            "contents" or "inline" or "inline-block" or "run-in" => "block",
            "inline-flex" => "flex",
            "inline-grid" => "grid",
            "inline-table" => "table",
            _ => value
        };
    }
}
