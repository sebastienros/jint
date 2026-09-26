using Jint.HtmlParser;
using Jint.HtmlParser.Css.Selectors;
using Jint.HtmlParser.Css.Values.Properties;

namespace Jint.Browser.Styling;

internal sealed partial class NativeCssQuery
{
    // Parent dependencies are warmed iteratively, including explicit inheritance of coupled properties.
    private void WarmParents(Element element, string property, ref SelectorMatchWork matching)
    {
        var pending = new Stack<Element>();
        var parent = InheritanceParent(element);
        while (parent is not null && !StateOf(parent, ref matching).Computed.ContainsKey(property))
        {
            _work.Charge(1);
            pending.Push(parent);
            parent = InheritanceParent(parent);
        }
        while (pending.TryPop(out parent)) GetProperty(parent, property, ref matching);
    }

    // CSS Overflow 3 §3.1: visible/clip compute jointly with the other axis.
    private NativeCssProperty Overflow(Element element, string name, ref SelectorMatchWork matching)
    {
        var state = StateOf(element, ref matching);
        if (state.Computed.TryGetValue(name, out var cached)) return cached;
        WarmParents(element, name, ref matching);
        var x = GetPropertyCore(element, "overflow-x", ref matching, adjust: false);
        var y = GetPropertyCore(element, "overflow-y", ref matching, adjust: false);
        state.Computed.Add("overflow-x", Adjust(x, y));
        state.Computed.Add("overflow-y", Adjust(y, x));
        matching.VerifyRead();
        Verify();
        return state.Computed[name];

        static NativeCssProperty Adjust(NativeCssProperty axis, NativeCssProperty other)
        {
            if (axis.Text is not ("visible" or "clip") || other.Text is "visible" or "clip") return axis;
            var text = axis.Text == "visible" ? "auto" : "hidden";
            return axis with { Text = text, Value = CssPropertyValue.Keyword(text, axis.Value!.Span) };
        }
    }

    // CSS Display 3 §2.7: root, out-of-flow and flex/grid item boxes blockify their display type.
    private CssPropertyValue Display(Element element, CssPropertyValue value, ref SelectorMatchWork matching)
    {
        if (value.Text == "none") return value;
        var root = element.ParentNode is Document;
        var parent = InheritanceParent(element);
        var positioned = GetProperty(element, "position", ref matching).Text is "absolute" or "fixed";
        string? parentDisplay = null;
        while (parent is not null)
        {
            _work.Charge(1);
            parentDisplay = GetProperty(parent, "display", ref matching).Text;
            if (parentDisplay != "contents") break;
            parent = InheritanceParent(parent);
        }
        var item = parentDisplay is "flex" or "grid";
        if (!root && !positioned && !item)
        {
            if (parentDisplay is not ("ruby" or "block ruby")) return value;
            var inline = value.Text switch
            {
                "block" => "inline",
                "flow-root" => "inline-block",
                "flex" => "inline-flex",
                "grid" => "inline-grid",
                "table" => "inline-table",
                "block ruby" => "ruby",
                "list-item" => "inline list-item",
                "flow-root list-item" => "inline flow-root list-item",
                _ => value.Text
            };
            return CssPropertyValue.Keyword(inline, value.Span);
        }
        var text = value.Text switch
        {
            "contents" => root ? "block" : "contents",
            "inline" or "run-in" or "ruby" or "block ruby" => "block",
            "inline-block" or "run-in flow-root" => "block",
            "inline-flex" => "flex",
            "inline-grid" => "grid",
            "inline-table" => "table",
            "inline list-item" or "run-in list-item" => "list-item",
            "inline flow-root list-item" or "run-in flow-root list-item" => "flow-root list-item",
            "table-row-group" or "table-header-group" or "table-footer-group" or "table-row" or "table-cell" or
                "table-column-group" or "table-column" or "table-caption" or "ruby-base" or "ruby-text" or
                "ruby-base-container" or "ruby-text-container" => "block",
            _ => value.Text
        };
        return CssPropertyValue.Keyword(text, value.Span);
    }
}
