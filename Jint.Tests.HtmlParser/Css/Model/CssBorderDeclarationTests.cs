using Jint.HtmlParser.Css.Model;
using Jint.HtmlParser.Css.Values;
using Jint.HtmlParser.Css.Values.Properties;
using Jint.HtmlParser.Css.Values.References;

namespace Jint.Tests.HtmlParser.Css.Model;

public sealed class CssBorderDeclarationTests
{
    [TestCase("border-width", "0 2em 3rem 2em", "0px 2em 3rem")]
    [TestCase("border-width", "THIN", "thin")]
    [TestCase("border-style", "DOTTED dashed solid double", "dotted dashed solid double")]
    [TestCase("border-color", "red currentColor red currentColor", "red currentcolor")]
    [TestCase("border-inline-width", "2em 3px", "2em 3px")]
    [TestCase("border-block-style", "groove groove", "groove")]
    [TestCase("border-inline-color", "red blue", "red blue")]
    [TestCase("border", "red SOLID 2px", "2px solid red")]
    [TestCase("border", "none", "medium none currentcolor")]
    [TestCase("border-left", "inset", "medium inset currentcolor")]
    [TestCase("border-block", "dashed 0", "0px dashed currentcolor")]
    [TestCase("border-inline-end", "blue", "medium none blue")]
    [TestCase("border-radius", "1px 2px 3px 4px / 5% 6%", "1px 2px 3px 4px / 5% 6%")]
    [TestCase("border-radius", "1em 2em / 1em 2em", "1em 2em")]
    [TestCase("border-top-left-radius", "2px 2px", "2px")]
    [TestCase("border-end-start-radius", "0 50%", "0px 50%")]
    [TestCase("outline", "auto", "medium auto auto")]
    [TestCase("outline", "2px auto", "2px auto auto")]
    [TestCase("outline", "auto solid", "medium solid auto")]
    [TestCase("outline", "auto red", "medium auto red")]
    [TestCase("outline", "auto auto", "medium auto auto")]
    [TestCase("outline", "red dotted 1px", "1px dotted red")]
    [TestCase("outline-color", "auto", "auto")]
    [TestCase("outline-offset", "-2em", "-2em")]
    [TestCase("border-top-width", "calc(-2px)", "calc(-2px)")]
    [TestCase("border-radius", "calc(1em + 10%) / 2rem", "calc(10% + 1em) / 2rem")]
    public void ValuesRoundTripThroughSpecifiedStorage(string name, string input, string expected)
    {
        foreach (var context in new[] { CssDeclarationContext.Style, CssDeclarationContext.Keyframe })
        {
            var block = CssDeclarationBlock.Parse(name + ":" + input, context);
            block.GetPropertyValue(name).Should().Be(expected);
            var text = block.CssText;
            CssDeclarationBlock.Parse(text, context).CssText.Should().Be(text);
            block.RemoveProperty(name).Should().Be(expected);
            block.Count.Should().Be(0);
        }
    }

    [TestCase("border-width", "-1px")]
    [TestCase("border-width", "-1e-999px")]
    [TestCase("border-width", "10%")]
    [TestCase("border-width", "1")]
    [TestCase("border-width", "normal")]
    [TestCase("border-width", "1px 2px 3px 4px 5px")]
    [TestCase("border-inline-width", "1px 2px 3px")]
    [TestCase("border-width", "calc(1px + 0%)")]
    [TestCase("border-width", "calc(0)")]
    [TestCase("border-style", "auto")]
    [TestCase("border-color", "none")]
    [TestCase("border", "solid dotted")]
    [TestCase("border", "red blue")]
    [TestCase("border", "1px 2px")]
    [TestCase("border", "red inherit")]
    [TestCase("border", "1px/2px solid")]
    [TestCase("border-radius", "/ 1px")]
    [TestCase("border-radius", "1px /")]
    [TestCase("border-radius", "1px / 2px / 3px")]
    [TestCase("border-radius", "-1%")]
    [TestCase("border-radius", "1px, 2px")]
    [TestCase("border-radius", "1px 2px 3px 4px 5px")]
    [TestCase("border-top-left-radius", "1px / 2px")]
    [TestCase("border-top-left-radius", "1px 2px 3px")]
    [TestCase("outline", "auto auto red")]
    [TestCase("outline", "hidden red")]
    [TestCase("outline-style", "hidden")]
    [TestCase("outline-color", "invert")]
    [TestCase("outline-offset", "10%")]
    [TestCase("outline-offset", "auto")]
    public void InvalidAssignmentsAreAtomic(string name, string input)
    {
        var block = CssDeclarationBlock.Parse(name + ":initial");
        var text = block.CssText;
        var stamp = block.Stamp;
        CssPropertyParser.Parse(name, input).Status.Should().Be(CssPropertyStatus.Invalid);
        block.SetProperty(name, input);
        block.Stamp.Should().Be(stamp);
        block.CssText.Should().Be(text);
    }

    [Test]
    public void EveryNewPropertyHasMetadataWideKeywordsAndPendingSubstitution()
    {
        foreach (var entry in CssPropertyRegistry.Completed.Values.Where(entry =>
            entry.Name.StartsWith("border-", StringComparison.Ordinal) || entry.Name is "border" or "outline" ||
            entry.Name.StartsWith("outline-", StringComparison.Ordinal)))
        {
            entry.Inherited.Should().BeFalse();
            CssPropertyParser.Parse(entry.Name, entry.InitialValue).Status.Should().Be(CssPropertyStatus.Valid);
            foreach (var keyword in new[] { "initial", "inherit", "unset", "revert", "revert-layer", "revert-rule" })
            {
                var block = CssDeclarationBlock.Parse(entry.Name + ":" + keyword + "!important");
                block.GetPropertyValue(entry.Name).Should().Be(keyword);
                block.GetPropertyPriority(entry.Name).Should().Be("important");
                block = CssDeclarationBlock.Parse("all:" + keyword);
                block.GetPropertyValue(entry.Name).Should().Be(keyword);
            }
            var pending = CssDeclarationBlock.Parse(entry.Name + ":var(--value)!important");
            pending.GetPropertyValue(entry.Name).Should().Be("var(--value)");
            if (entry.Longhands.Count > 0)
            {
                pending.GetPropertyValue(entry.Longhands[0]).Should().BeEmpty();
                pending.RemoveProperty(entry.Longhands[0]);
                pending.GetPropertyValue(entry.Name).Should().BeEmpty();
            }
            CssPropertyParser.Parse(entry.Name, "initial", CssDeclarationContext.FontFace).Status
                .Should().Be(CssPropertyStatus.UnsupportedProperty);
        }
    }

    [Test]
    public void BorderResetsImagesButNotRadiiOrOutlineOffset()
    {
        var block = CssDeclarationBlock.Parse("border-radius:5px;outline-offset:2px;border:solid red;outline:dashed");
        block.GetPropertyValue("border").Should().Be("medium solid red");
        foreach (var name in CssBorderPropertyParser.ImageReset) block.GetPropertyValue(name).Should().Be("initial");
        block.GetPropertyValue("border-radius").Should().Be("5px");
        block.GetPropertyValue("outline-offset").Should().Be("2px");
        block.SetProperty("border-image-source", "inherit");
        block.GetPropertyValue("border").Should().BeEmpty();
        block.SetProperty("border", "2px solid blue", "important");
        block.GetPropertyPriority("border-image-source").Should().Be("important");
        block.SetProperty("border-left-color", "red");
        block.GetPropertyValue("border").Should().BeEmpty();
        block.GetPropertyPriority("border").Should().BeEmpty();
        var reparsed = CssDeclarationBlock.Parse(block.CssText);
        foreach (var name in CssPropertyRegistry.Completed["border"].Longhands)
        {
            reparsed.GetPropertyValue(name).Should().Be(block.GetPropertyValue(name));
            reparsed.GetPropertyPriority(name).Should().Be(block.GetPropertyPriority(name));
        }
    }

    [Test]
    public void LogicalInterleavingSurvivesSerializationAndSetterOrdering()
    {
        var block = CssDeclarationBlock.Parse("border-top-width:1px;border-inline-start-width:2px;" +
            "border-right-width:1px;border-bottom-width:1px;border-left-width:1px");
        block.CssText.Should().Be("border-top-width: 1px; border-inline-start-width: 2px; " +
            "border-right-width: 1px; border-bottom-width: 1px; border-left-width: 1px;");
        block.GetPropertyValue("border-width").Should().BeEmpty();
        block.SetProperty("border-top-width", "3px");
        block.CssText.Should().Be("border-inline-start-width: 2px; border-width: 3px 1px 1px;");
        block = CssDeclarationBlock.Parse("border-inline-start-width:2px;border-left-width:1px;opacity:.5");
        block.SetProperty("border-left-width", "3px");
        block.CssText.Should().Be("border-inline-start-width: 2px; border-left-width: 3px; opacity: 0.5;");
    }

    [Test]
    public void RadiusComponentsAndNegativeMathStayTyped()
    {
        var radius = CssPropertyParser.Parse("border-start-start-radius", "2em calc(10% + 1rem)").Value;
        radius.Kind.Should().Be(CssPropertyValueKind.Radius);
        radius.Components[0].Numeric.Unit.Should().Be(CssUnit.Em);
        radius.Components[1].Math.Context.Range.Lower.Should().Be(0);
        var width = CssPropertyParser.Parse("border-width", "calc(-1em)").Value.Components[0];
        width.Kind.Should().Be(CssPropertyValueKind.Math);
        width.Math.Context.Percentages.Should().Be(Jint.HtmlParser.Css.Values.Math.CssMathPercentageMode.Forbidden);
    }

    [Test]
    public void PriorityAndCancellationPreserveAtomicDeclarations()
    {
        var block = CssDeclarationBlock.Parse("border:solid red!important;border-width:9px");
        block.GetPropertyValue("border-width").Should().Be("medium");
        block.SetProperty("border-width", "1px 2px");
        block.GetPropertyPriority("border-width").Should().BeEmpty();
        var stamp = block.Stamp;
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Action write = () => block.SetProperty("border-radius", "1em 2em / 3em 4em", cancellationToken: cancellation.Token);
        write.Should().Throw<OperationCanceledException>();
        block.Stamp.Should().Be(stamp);
        var keyframe = CssDeclarationBlock.Parse("border:solid;border:dashed!important", CssDeclarationContext.Keyframe);
        keyframe.GetPropertyValue("border").Should().Be("medium solid currentcolor");
    }

    [TestCase("border-width", "1em calc(2rem + 3px)")]
    [TestCase("border-radius", "1em 2rem / calc(3em + 4px) 20%")]
    [TestCase("outline", "1em solid rgb(1 2 3)")]
    public void CancellationInterruptsTypedBorderParsing(string name, string text)
    {
        var input = CssReferenceInput.Parse(text, null, default);
        var parts = CssPropertyParser.Significant(input.Components, new CssValueWork(default));
        using var cancellation = new CancellationTokenSource();
        var polls = 0;
        var work = new CssValueWork(cancellation.Token, () => { if (++polls == 3) cancellation.Cancel(); });
        Action parse = () => CssBorderPropertyParser.Parse(CssPropertyRegistry.Completed[name], parts, input.MaxNestingDepth, work);
        parse.Should().Throw<OperationCanceledException>();
        polls.Should().Be(3);
    }
}
