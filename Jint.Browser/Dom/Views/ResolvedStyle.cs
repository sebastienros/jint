using Jint.Browser.Layout;
using Jint.Browser.Runtime;
using Jint.Browser.Styling;
using Jint.HtmlParser;
using Jint.HtmlParser.Css.Model;
using Jint.HtmlParser.Css.Values;
using Jint.HtmlParser.Css.Values.Math;
using Jint.HtmlParser.Css.Values.Properties;
using Jint.HtmlParser.Css.Values.Transforms;

namespace Jint.Browser.Dom.Views;

// CSSOM §9 resolved values: used geometry belongs to Browser, not property syntax or cascade.
internal static class ResolvedStyle
{
    // Only the transform list contributes: no origin, ancestors or individual transforms.
    // Transforms 2 §2.1 and Transforms 1 §6: HTML view-box/stroke-box use border-box.
    internal static string Transform(NativeCssComputedStyle style, NativeCssProperty property,
        Element element, PageRuntime? runtime)
    {
        style.VerifyRead();
        if (property.Value?.Kind != CssPropertyValueKind.TransformList) return property.Text;
        var list = property.Value.TransformList;
        var work = style.Work;
        double? width = null;
        double? height = null;
        if (CssTransformMatrix.NeedsReferenceBox(list, work))
        {
            if (runtime is null || !ReferenceEquals(runtime.Document, element.OwnerDocument))
                throw Missing("transform-current-document");
            if (element.NamespaceUri != Namespaces.Html) throw Missing("transform-svg-reference-box");
            var box = style.GetPropertyValue("transform-box");
            if (box is "content-box" or "fill-box") throw Missing("transform-content-box");
            if (box is not ("view-box" or "border-box" or "stroke-box")) throw Missing("transform-reference-box");
            style.VerifyRead();
            var sizes = runtime.Layout.MeasureSizes();
            if (!sizes.HasBox(element)) throw Missing("transform-reference-box");
            // These are untransformed synthetic flat dimensions (inherited/flex width and row height),
            // not a claim of full CSS padding/border geometry. Never Place or use FlatBox.Empty.
            var measured = sizes.Measure(element);
            if (!double.IsFinite(measured.Width) || measured.Width < 0 ||
                !double.IsFinite(measured.Height) || measured.Height < 0) throw Missing("transform-reference-box");
            width = measured.Width;
            height = measured.Height;
            style.VerifyRead();
        }
        var result = CssTransformMatrix.Resolve(list, work, width, height);
        style.VerifyRead();
        return result;

        CssIncompleteGrammarException Missing(string dependency) => new("transform", "C6:" + dependency, list.Span);
    }

    // https://drafts.csswg.org/cssom/#resolved-values
    // The computed query remains layout-free. Only the Browser declaration enters this adapter.
    internal static string ValueOf(string name, NativeCssComputedStyle style, Element element, PageRuntime? runtime)
    {
        style.VerifyRead();
        name = CssPropertyRegistry.NormalizeName(name, style.Work);
        if (name == "transform") return Transform(style, style.GetNormalizedProperty(name), element, runtime);
        var dimensions = name is "width" or "height";
        var minimum = name is "min-width" or "min-height";
        var edge = name is "margin-top" or "margin-right" or "margin-bottom" or "margin-left" or
            "padding-top" or "padding-right" or "padding-bottom" or "padding-left";
        if (!dimensions && !minimum && !edge) return Computed();

        NativeCssProperty? computed = null;
        if (!dimensions)
        {
            computed = style.GetNormalizedProperty(name);
            if (minimum && computed.Text != "auto") return Finish(computed.Text);
            if (edge && computed.Value is { Kind: CssPropertyValueKind.Numeric, Numeric.Kind: not CssNumericKind.Percentage })
                return Finish(computed.Text); // Absolute edges need neither display nor geometry.
            if (edge && computed.Value is { Kind: CssPropertyValueKind.Math } calculation &&
                !NeedsPercentageBasis(calculation, style.Work)) return Finish(computed.Text);
        }

        var current = runtime is not null && ReferenceEquals(runtime.Document, element.OwnerDocument) && Connected(element, style.Work);
        var display = style.GetPropertyValue("display");
        var applicable = display is not ("none" or "contents") &&
            (display != "inline" || IsSupportedReplacedInline(element));
        // Do not compute authored dimensions only to throw them away. The flat row policy
        // does not consume them, whereas horizontal flex sizing requests its own dependencies.
        if (dimensions && current && applicable)
        {
            if (element.NamespaceUri != Namespaces.Html) throw Missing(name, "svg-used-size");
            return Measure(sizes => sizes.HasBox(element)
                ? Pixels(name == "width" ? sizes.Width(element) : sizes.Height(element), style.Work)
                : Computed());
        }

        var property = computed ?? style.GetNormalizedProperty(name);
        if (minimum && property.Text == "auto")
        {
            // https://drafts.csswg.org/css-sizing-3/#min-width
            // No CSS box resolves auto to zero. Ordinary CSS2 boxes do so with auto aspect ratio;
            // flex/grid automatic minima and a potentially authored ratio need their own producer.
            if (!current || display is "none" or "contents") return Finish("0px");
            if (element.NamespaceUri != Namespaces.Html) throw Missing(name, "svg-automatic-minimum");
            return Measure(sizes =>
            {
                if (!sizes.HasBox(element)) return "0px";
                if (IsFlexOrGridItem()) throw Missing(name, "automatic-minimum");
                if (style.HasPropertyInput("aspect-ratio"))
                    throw new CssIncompleteGrammarException(name, "V2:aspect-ratio", property.Value?.Span ?? default);
                return "0px";
            });
        }
        if (!edge || !current || display is "none" or "contents") return Finish(property.Text);
        if (property.Text == "auto")
        {
            if (IsFlexOrGridItem() || Positioned()) throw Missing(name, "auto-margin-used-value");
            return Finish("0px"); // Explicit margin-free normal-flow flat policy.
        }
        if (property.Value is not { Kind: CssPropertyValueKind.Numeric or CssPropertyValueKind.Math } value)
            return Finish(property.Text);
        var needsBasis = NeedsPercentageBasis(value, style.Work);
        if (!needsBasis) return Finish(property.Text);
        if (element.NamespaceUri != Namespaces.Html) throw Missing(name, "svg-containing-block");
        if (Positioned()) throw Missing(name, "positioned-containing-block");
        // writing-mode is still pending: absence has its horizontal initial semantics, while
        // an authored candidate is an explicit dependency, never a guessed vertical basis.
        for (Element? ancestor = element; ancestor is not null; ancestor = ancestor.ParentNode as Element)
        {
            style.Work.Charge(1);
            var ancestorStyle = style.For(ancestor);
            if (ancestorStyle.HasPropertyInput("writing-mode")) throw Missing(name, "writing-mode");
            if (ancestor.NamespaceUri != Namespaces.Html) throw Missing(name, "svg-containing-block");
            if (ancestorStyle.GetPropertyValue("position") != "static") throw Missing(name, "positioned-containing-block");
        }
        return Measure(sizes =>
        {
            if (sizes.ContainingInlineWidth(element) is not { } basis) throw Missing(name, "containing-block-inline-size");
            var number = UsedLength(name, value, basis, style.Work);
            if (name.StartsWith("padding-", StringComparison.Ordinal)) number = System.Math.Max(0, number);
            return Pixels(number, style.Work);
        });

        bool Positioned() => style.GetPropertyValue("position") != "static";
        bool IsFlexOrGridItem()
        {
            for (var parent = element.ParentNode as Element; parent is not null; parent = parent.ParentNode as Element)
            {
                style.Work.Charge(1);
                var parentDisplay = style.For(parent).GetPropertyValue("display");
                if (parentDisplay == "contents") continue;
                return parentDisplay is "flex" or "inline-flex" or "grid" or "inline-grid";
            }
            return false;
        }
        string Computed() => Finish(style.GetNormalizedProperty(name).Text);
        string Finish(string result)
        {
            style.VerifyRead();
            return result;
        }
        string Measure(Func<FlatLayout.SizeQuery, string> read)
        {
            style.VerifyRead();
            var media = runtime!.Media;
            var document = runtime.Document;
            var sizes = runtime.Layout.MeasureSizes();
            var revision = runtime.Layout.Version;
            Verify();
            var result = read(sizes);
            Verify();
            return result;

            void Verify()
            {
                style.VerifyRead();
                if (!ReferenceEquals(runtime.Document, document) || runtime.Media != media || runtime.Layout.Version != revision)
                    throw new InvalidOperationException(NativeCssQuery.Invalidated);
            }
        }
    }

    // HTML replaced elements represented by the existing synthetic renderer. This classification
    // deliberately excludes ordinary inline text boxes and does not promise intrinsic image metrics.
    private static bool IsSupportedReplacedInline(Element element) => element.NamespaceUri == Namespaces.Html &&
        element.LocalName is "img" or "video" or "canvas" or "iframe" or "embed" or "object" or "input" or "textarea" or "select";

    private static bool Connected(Node node, CssValueWork work)
    {
        while (true)
        {
            work.Charge(1);
            if (node.ParentNode is { } parent) node = parent;
            else if (node is ShadowRoot shadow) node = shadow.Host;
            else return node is Document;
        }
    }

    private static bool NeedsPercentageBasis(CssPropertyValue value, CssValueWork work)
    {
        if (value.Kind == CssPropertyValueKind.Numeric) return value.Numeric.Kind == CssNumericKind.Percentage;
        for (var i = 0; i < value.Math.NodeCount; i++)
        {
            work.Charge(1);
            var node = value.Math.GetNode(i);
            if (node.Kind == CssMathNodeKind.Numeric && node.Numeric.Kind == CssNumericKind.Percentage) return true;
        }
        return false;
    }

    private static double UsedLength(string name, CssPropertyValue value, double basis, CssValueWork work)
    {
        CssMathNumeric numeric;
        if (value.Kind == CssPropertyValueKind.Numeric)
        {
            var atom = value.Numeric;
            numeric = Convert(new(CssMathNumbers.ParseFinite(atom.Number, atom.Unit, work), atom.Kind, atom.Unit, atom.Span));
        }
        else
        {
            var math = value.Math;
            var builder = new CssMathBuilder(work);
            var mapped = new int[math.NodeCount];
            for (var i = math.NodeCount - 1; i >= 0; i--)
            {
                work.Charge(1);
                var node = math.GetNode(i);
                var children = new List<int>(node.ChildCount);
                for (var j = 0; j < node.ChildCount; j++)
                {
                    work.Charge(1);
                    children.Add(mapped[math.GetChild(node.ChildStart + j)]);
                }
                var type = node.Type;
                mapped[i] = builder.Add(node.Kind, new(type.Length, type.Angle, type.Time, type.Frequency, type.Resolution, type.Flex),
                    node.Span, node.Kind == CssMathNodeKind.Numeric ? Convert(node.Numeric) : default, children,
                    node.Kind == CssMathNodeKind.Round ? node.RoundingStrategy : CssRoundingStrategy.Nearest);
            }
            var simplified = CssMathSimplifier.Freeze(builder, mapped[math.RootIndex], math.Context, math.Span, work);
            var root = simplified.GetNode(simplified.RootIndex);
            if (root.Kind != CssMathNodeKind.Numeric) throw Missing(name, "unresolved-edge-calculation");
            numeric = root.Numeric;
        }
        if (numeric.Kind != CssNumericKind.Dimension || numeric.Unit != CssUnit.Px || !double.IsFinite(numeric.Value))
            throw Missing(name, "edge-used-length");
        return CssMathNumbers.NormalizeTopLevel(numeric.Value);

        CssMathNumeric Convert(CssMathNumeric input) => input.Kind == CssNumericKind.Percentage
            ? new(CssMathNumbers.ScaleProduct(input.Value, basis, 100), CssNumericKind.Dimension, CssUnit.Px, input.Span) : input;
    }

    private static string Pixels(double value, CssValueWork work)
    {
        if (!double.IsFinite(value)) throw Missing("resolved-value", "nonfinite-used-length");
        return CssMathSerializer.SerializeFiniteNumber(value, work) + "px";
    }

    private static CssIncompleteGrammarException Missing(string name, string dependency) => new(name, "C6:" + dependency, default);
}
