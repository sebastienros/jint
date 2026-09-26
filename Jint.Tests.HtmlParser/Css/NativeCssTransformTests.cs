#nullable enable

using System.Globalization;
using Jint.Browser.Styling;
using Jint.HtmlParser;
using Jint.HtmlParser.Css.Media;
using Jint.HtmlParser.Css.Model;
using Jint.HtmlParser.Css.Selectors;
using Jint.HtmlParser.Css.Values;
using Jint.HtmlParser.Css.Values.Properties;
using Jint.HtmlParser.Css.Values.Math;
using Jint.HtmlParser.Css.Values.Transforms;
using Jint.HtmlParser.Css.Values.References;

namespace Jint.Tests.HtmlParser.Css;

public sealed class NativeCssTransformTests
{
    [TestCase("transform", "translate(2em, 10%) translateZ(3rem)", "translate(40px, 10%) translateZ(48px)")]
    [TestCase("transform", "rotate(.25turn) scale(50%, 200%)", "rotate(90deg) scale(0.5, 2)")]
    [TestCase("transform", "translateX(calc(2em - 50%))", "translateX(calc(-50% + 40px))")]
    [TestCase("transform", "perspective(.25px)", "perspective(0.25px)")]
    [TestCase("transform", "perspective(calc(-2px))", "perspective(0px)")]
    [TestCase("transform", "none", "none")]
    [TestCase("translate", "-1in 20% 2pt", "-96px 20% 2.666667px")]
    [TestCase("translate", "calc(-10px + 20%) 0", "calc(20% - 10px)")]
    [TestCase("translate", "calc(2em - 50%) -3vw", "calc(-50% + 40px) -24px")]
    [TestCase("translate", "0 0 0", "0px")]
    [TestCase("rotate", "-.25turn", "-90deg")]
    [TestCase("rotate", "45deg 1 2 3", "1 2 3 45deg")]
    [TestCase("rotate", "0 0 0 45deg", "0 0 0 45deg")]
    [TestCase("rotate", "calc(1 + 1) 0 0 calc(.25turn)", "x 90deg")]
    [TestCase("rotate", "x calc(2em / 1px * 1deg)", "x 40deg")]
    [TestCase("rotate", "0 0 -2 .5turn", "-180deg")]
    [TestCase("rotate", "0deg", "0deg")]
    [TestCase("scale", "-50% 200% 100%", "-0.5 2")]
    [TestCase("scale", "calc(50% * 2)", "1")]
    [TestCase("scale", "calc(-2 * .25) 1 2", "-0.5 1 2")]
    [TestCase("scale", "calc(2em / 1px)", "40")]
    [TestCase("scale", "1 1 1", "1")]
    [TestCase("translate", "none", "none")]
    [TestCase("rotate", "none", "none")]
    [TestCase("scale", "none", "none")]
    public void ComponentsComputeThroughSharedNumericMetricsWithoutChangingIdentity(string name, string declared, string expected)
    {
        var document = Document.CreateHtml();
        var target = document.CreateElement("div");
        var block = CssDeclarationBlock.Parse("font-size:20px;" + name + ":" + declared);
        var work = new CssValueWork(default);
        var query = Query(document, [(target, block)], work);
        var matching = new SelectorMatchWork(document, default);
        var result = query.GetProperty(target, name, ref matching);
        result.Text.Should().Be(expected);
        result.Value!.Kind.Should().Be(declared == "none" ? CssPropertyValueKind.Keyword :
            name == "transform" ? CssPropertyValueKind.TransformList : CssPropertyValueKind.Transform);
        block.GetPropertyValue(name, work).Should().NotBeEmpty();
    }

    [TestCase("transform", "translateX(2ch)", "C6:zero-advance")]
    [TestCase("transform", "scaleX(calc(2ch / 1px))", "C6:zero-advance")]
    [TestCase("translate", "2ch", "C6:zero-advance")]
    [TestCase("translate", "calc(2ch - 50%)", "C6:zero-advance")]
    [TestCase("translate", "2cqw", "C6:container-length")]
    [TestCase("scale", "calc(2ch / 1px)", "C6:zero-advance")]
    [TestCase("rotate", "calc(2ch / 1px) 0 0 45deg", "C6:zero-advance")]
    public void MissingMetricsRemainNamedFailuresAndDoNotBlockUnrelatedReads(string name, string declared, string blocker)
    {
        var document = Document.CreateHtml();
        var target = document.CreateElement("div");
        var block = CssDeclarationBlock.Parse("display:block;" + name + ":" + declared);
        var query = Query(document, [(target, block)], new(default));
        var matching = new SelectorMatchWork(document, default);
        query.GetProperty(target, "display", ref matching).Text.Should().Be("block");
        Action read = () => query.GetProperty(target, name, ref matching);
        read.Should().Throw<CssIncompleteGrammarException>().Which.Blocker.Should().Be(blocker);
    }

    [Test]
    public void TransformTuplesUseTheElementsComputedFontAndTheActualRootFont()
    {
        var document = Document.CreateHtml();
        var root = document.CreateElement("html");
        var parent = document.CreateElement("div");
        var child = document.CreateElement("span");
        document.AppendChild(root);
        root.AppendChild(parent);
        parent.AppendChild(child);
        var rootBlock = CssDeclarationBlock.Parse("font-size:12px");
        var parentBlock = CssDeclarationBlock.Parse("font-size:20px");
        var childBlock = CssDeclarationBlock.Parse("translate:2em 10% 3rem;"
            + "scale:calc(2em / 1px);rotate:calc(2em / 1px) 1 0 45deg;"
            + "transform:translate(2em, 10%) translateZ(3rem) scale(calc(2em / 1px))");
        var query = Query(document, [(root, rootBlock), (parent, parentBlock), (child, childBlock)],
            new(default), new NativeCssMetrics { FontSize = 99, RootFontSize = 99 });
        var matching = new SelectorMatchWork(document, default);
        query.GetProperty(child, "translate", ref matching).Text.Should().Be("40px 10% 36px");
        query.GetProperty(child, "scale", ref matching).Text.Should().Be("40");
        query.GetProperty(child, "rotate", ref matching).Text.Should().Be("40 1 0 45deg");
        query.GetProperty(child, "transform", ref matching).Text.Should().Be("translate(40px, 10%) translateZ(36px) scale(40)");
        // A fresh query after an inherited-font mutation recomputes every transform component.
        parentBlock.SetProperty("font-size", "30px");
        query = Query(document, [(root, rootBlock), (parent, parentBlock), (child, childBlock)], new(default));
        query.GetProperty(child, "translate", ref matching).Text.Should().Be("60px 10% 36px");
        query.GetProperty(child, "scale", ref matching).Text.Should().Be("60");
        query.GetProperty(child, "transform", ref matching).Text.Should().Be("translate(60px, 10%) translateZ(36px) scale(60)");
        query.GetProperty(child, "rotate", ref matching).Text.Should().Be("60 1 0 45deg");
    }

    [TestCase("transform", "translate(10px, 20%)", "none")]
    [TestCase("translate", "10px 20%", "none")]
    [TestCase("rotate", "x 45deg", "none")]
    [TestCase("scale", "2", "none")]
    public void ExplicitInheritanceAndInvalidVariableWinnersUseTheNormalCascade(string name, string parentValue, string initial)
    {
        var document = Document.CreateHtml();
        var parent = document.CreateElement("div");
        var child = document.CreateElement("span");
        document.AppendChild(parent);
        parent.AppendChild(child);
        var parentBlock = CssDeclarationBlock.Parse(name + ":" + parentValue);
        foreach (var declaration in new[] { "", name + ":unset", name + ":inherit", name + ":initial",
                     name + ":" + parentValue + ";--bad:bogus;" + name + ":var(--bad) !important" })
        {
            var block = CssDeclarationBlock.Parse(declaration);
            var query = Query(document, [(parent, parentBlock), (child, block)], new(default));
            var matching = new SelectorMatchWork(document, default);
            var result = query.GetProperty(child, name, ref matching);
            result.Text.Should().Be(declaration == name + ":inherit" ? parentValue : initial);
            if (declaration.Contains("var(--bad)", StringComparison.Ordinal))
                result.Disposition.Should().Be(NativeCssDisposition.InvalidAtComputedValue);
        }
    }

    [TestCase("scale(0.0000004) translateX(10000000px)")]
    [TestCase("scale(0.00004%) translateX(10000000px)")]
    [TestCase("scale(calc(0.0000004)) translateX(10000000px)")]
    [TestCase("scale(calc(0.00004%)) translateX(10000000px)")]
    [TestCase("scale(calc(0.00000002em / 1px)) translateX(10000000px)")]
    public void ComputedTransformComponentsRetainPrecisionBeyondTheirDisplayText(string source)
    {
        var document = Document.CreateHtml();
        var target = document.CreateElement("div");
        var block = CssDeclarationBlock.Parse("font-size:20px;transform:" + source);
        var work = new CssValueWork(default);
        var query = Query(document, [(target, block)], work);
        var matching = new SelectorMatchWork(document, default);
        var property = query.GetProperty(target, "transform", ref matching);
        property.Text.Should().Be("scale(0) translateX(10000000px)");
        var list = property.Value!.TransformList;
        var atom = list[0].Arguments[0].Numeric;
        CssMathNumbers.ParseFinite(atom.Number, atom.Unit, work).Should().BeApproximately(0.0000004, 1e-21);
        CssTransformMatrix.Resolve(list, work).Should().Be("matrix(0, 0, 0, 0, 4, 0)");
    }

    [TestCase("1e-323", 1e308, 1e30)]
    [TestCase("1e308", 1e-323, 1e30)]
    [TestCase("1e308", 100, 1e-308)]
    public void ComputedPercentageProductsKeepRepresentableResultsWithFiniteAndSubnormalBases(
        string percentage, double basis, double scale)
    {
        var document = Document.CreateHtml();
        var target = document.CreateElement("div");
        var block = CssDeclarationBlock.Parse("transform:scale(" + scale.ToString("R", CultureInfo.InvariantCulture)
            + ") translateX(" + percentage + "%)");
        var work = new CssValueWork(default);
        var query = Query(document, [(target, block)], work);
        var matching = new SelectorMatchWork(document, default);
        var list = query.GetProperty(target, "transform", ref matching).Value!.TransformList;
        var result = CssTransformMatrix.Resolve(list, work, basis, 80);
        var translation = double.Parse(result.Split(',')[4], CultureInfo.InvariantCulture);
        // This independent ordering keeps these rows' intermediate products representable.
        var percent = double.Parse(percentage, CultureInfo.InvariantCulture);
        var expected = percentage == "1e308" && basis == 100 ? percent * (basis / 100) * scale :
            percent * basis / 100 * scale;
        translation.Should().BeApproximately(expected, System.Math.Abs(expected) * 1e-14);
        translation.Should().NotBe(0);
    }

    [Test]
    public void ComputedTinyLengthsSurviveLaterScaleMultiplication()
    {
        var document = Document.CreateHtml();
        var target = document.CreateElement("div");
        var block = CssDeclarationBlock.Parse("transform:scale(10000000) translateX(0.0000004px)");
        var work = new CssValueWork(default);
        var query = Query(document, [(target, block)], work);
        var matching = new SelectorMatchWork(document, default);
        var property = query.GetProperty(target, "transform", ref matching);
        property.Text.Should().Be("scale(10000000) translateX(0px)");
        CssTransformMatrix.Resolve(property.Value!.TransformList, work).Should().Be("matrix(10000000, 0, 0, 10000000, 4, 0)");
    }

    [Test]
    public void LongTransformMathComputationPollsWorkCancellation()
    {
        var document = Document.CreateHtml();
        var target = document.CreateElement("div");
        var source = "font-size:20px;translate:hypot(" + string.Join(", ", Enumerable.Repeat("1em, 1px", 4096)) + ")";
        var block = CssDeclarationBlock.Parse(source);
        using var cancellation = new CancellationTokenSource();
        var armed = false;
        var polls = 0;
        var work = new CssValueWork(cancellation.Token, () => { if (armed && ++polls == 8) cancellation.Cancel(); });
        var query = Query(document, [(target, block)], work);
        var matching = new SelectorMatchWork(document, cancellation.Token);
        armed = true;
        Action read = () => query.GetProperty(target, "translate", ref matching);
        read.Should().Throw<OperationCanceledException>();
    }

    private static NativeCssQuery Query(Document document,
        IReadOnlyList<(Element Element, CssDeclarationBlock Block)> inline, CssValueWork work,
        NativeCssMetrics? metrics = null) =>
        new(document, [], inline, new CssMediaEnvironment { Width = 800, Height = 600 }, new(document, null, null, null),
            CssEnvironmentSnapshot.Create([], work), work, metrics);
}
