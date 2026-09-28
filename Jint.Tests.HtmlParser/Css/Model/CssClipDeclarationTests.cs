using Jint.HtmlParser;
using Jint.HtmlParser.Css.Model;
using Jint.HtmlParser.Css.Values;
using Jint.HtmlParser.Css.Values.Math;
using Jint.HtmlParser.Css.Values.Properties;
using Jint.HtmlParser.Css.Values.References;

namespace Jint.Tests.HtmlParser.Css.Model;

public sealed class CssClipDeclarationTests
{
    [TestCase("AUTO", "auto")]
    [TestCase("rect(0, 0, 0, 0)", "rect(0px, 0px, 0px, 0px)")]
    [TestCase("rect(0 0 0 0)", "rect(0px, 0px, 0px, 0px)")]
    [TestCase("RECT(auto, -2em, 3rem, -4px)", "rect(auto, -2em, 3rem, -4px)")]
    [TestCase(@"r\65 ct(AUTO/**/ -2em 3rem/**/-4px)", "rect(auto, -2em, 3rem, -4px)")]
    [TestCase("rect(auto auto auto auto)", "rect(auto, auto, auto, auto)")]
    [TestCase("rect(1in, -2.54cm, 72pt, 0)", "rect(96px, -96px, 96px, 0px)")]
    [TestCase("rect(calc(1em + 2px), auto, min(2em, 3rem), calc(-4px))",
        "rect(calc(1em + 2px), auto, min(2em, 3rem), calc(-4px))")]
    public void LegacyRectanglesRoundTripThroughDeclarationStorage(string input, string expected)
    {
        foreach (var context in new[] { CssDeclarationContext.Style, CssDeclarationContext.Keyframe })
        {
            var priority = context == CssDeclarationContext.Style ? "important" : "";
            var suffix = priority.Length == 0 ? "" : " !important";
            var block = CssDeclarationBlock.Parse("clip:" + input + suffix, context);
            block.GetPropertyValue("clip").Should().Be(expected);
            block.GetPropertyPriority("clip").Should().Be(priority);
            block.CssText.Should().Be("clip: " + expected + suffix + ";");
            CssDeclarationBlock.Parse(block.CssText, context).CssText.Should().Be(block.CssText);
            block.RemoveProperty("clip").Should().Be(expected);
            block.Count.Should().Be(0);
        }
    }

    [TestCase("none")]
    [TestCase("rect()")]
    [TestCase("rect(0, 0, 0)")]
    [TestCase("rect(0 0 0 0 0)")]
    [TestCase("rect(0, 0, 0, 0, 0)")]
    [TestCase("rect(0, 0 0, 0)")]
    [TestCase("rect(0 0, 0 0)")]
    [TestCase("rect(0, 0, 0, 0,)")]
    [TestCase("rect(,0, 0, 0, 0)")]
    [TestCase("rect(0,,0,0)")]
    [TestCase("rect(0 / 0 / 0 / 0)")]
    [TestCase("rect(1, 0, 0, 0)")]
    [TestCase("rect(0%, 0, 0, 0)")]
    [TestCase("rect(1fr, 0, 0, 0)")]
    [TestCase("rect(1s, 0, 0, 0)")]
    [TestCase("rect(1deg, 0, 0, 0)")]
    [TestCase("rect(calc(1px + 0%), 0, 0, 0)")]
    [TestCase("rect(calc(0), 0, 0, 0)")]
    [TestCase("rect(calc(1s), 0, 0, 0)")]
    [TestCase("rect(min(1px, 2%), 0, 0, 0)")]
    [TestCase("rect(inherit, 0, 0, 0)")]
    [TestCase("rect(rect(0, 0, 0, 0), 0, 0, 0)")]
    [TestCase("rect(0, 0, 0, 0) border-box")]
    [TestCase("rect(0, 0, 0, 0), rect(0, 0, 0, 0)")]
    [TestCase("auto rect(0, 0, 0, 0)")]
    [TestCase("inset(0)")]
    public void InvalidRectanglesPreserveThePreviousDeclaration(string input)
    {
        var block = CssDeclarationBlock.Parse("clip:rect(auto, 2em, -3px, 0)!important");
        var stamp = block.Stamp;
        var text = block.CssText;
        CssPropertyParser.Parse("clip", input).Status.Should().Be(CssPropertyStatus.Invalid);
        block.SetProperty("clip", input);
        block.Stamp.Should().Be(stamp);
        block.CssText.Should().Be(text);
    }

    [TestCase("initial")]
    [TestCase("inherit")]
    [TestCase("unset")]
    [TestCase("revert")]
    [TestCase("revert-layer")]
    [TestCase("revert-rule")]
    public void WideKeywordsAndAllResetShareLonghandMembership(string keyword)
    {
        CssPropertyParser.Parse("clip", keyword).Value.Serialize().Should().Be(keyword);
        var block = CssDeclarationBlock.Parse("clip:rect(0, 0, 0, 0);all:" + keyword);
        block.GetPropertyValue("clip").Should().Be(keyword);
        block.GetPropertyValue("all").Should().Be(keyword);
        block.RemoveProperty("clip").Should().Be(keyword);
        block.GetPropertyValue("all").Should().BeEmpty();
    }

    [Test]
    public void RectanglesKeepTypedEdgesAndLengthOnlyMathDependencies()
    {
        var value = CssPropertyParser.Parse("clip", "rect(auto, -2em, calc(3rem + 4px), 0)").Value;
        value.Kind.Should().Be(CssPropertyValueKind.ClipRectangle);
        value.Components.Should().HaveCount(4);
        value.Components[0].Kind.Should().Be(CssPropertyValueKind.Keyword);
        value.Components[1].Numeric.Unit.Should().Be(CssUnit.Em);
        value.Components[1].Numeric.Number.Sign.Should().Be(-1);
        value.Components[2].Kind.Should().Be(CssPropertyValueKind.Math);
        value.Components[2].Math.Context.Expected.Should().Be(CssMathProduction.Length);
        value.Components[2].Math.Context.Percentages.Should().Be(CssMathPercentageMode.Forbidden);
        value.Components[2].Math.Context.Range.Lower.Should().BeNull();
        value.Components[3].Numeric.Unit.Should().Be(CssUnit.Px);
        var metadata = CssPropertyRegistry.Find("clip", CssDeclarationContext.Style)!;
        metadata.InitialValue.Should().Be("auto");
        metadata.Inherited.Should().BeFalse();
        metadata.Longhands.Should().BeEmpty();
        metadata.Aliases.Should().BeEmpty();
        CssPropertyRegistry.Completed["all"].Longhands.Should().Contain("clip");
        CssPropertyParser.Parse("clip", "auto", CssDeclarationContext.FontFace).Status.Should().Be(CssPropertyStatus.UnsupportedProperty);
    }

    [TestCase("var(--clip)")]
    [TestCase("rect(var(--edges))")]
    [TestCase("rect(0, var(--right), auto, 0)")]
    [TestCase("rect(env(clip-top), 0, 0, 0)")]
    public void SubstitutionDefersTheWholeDeclaration(string source)
    {
        CssPropertyParser.Parse("clip", source).Status.Should().Be(CssPropertyStatus.Deferred);
        var block = CssDeclarationBlock.Parse("clip:" + source + "!important");
        block.GetPropertyValue("clip").Should().Be(source);
        block.GetPropertyPriority("clip").Should().Be("important");
    }

    [Test]
    public void PriorityAndKeyframeRestrictionsUseTheExistingDeclarationRules()
    {
        var block = CssDeclarationBlock.Parse("clip:rect(0,0,0,0)!important;clip:auto");
        block.GetPropertyValue("clip").Should().Be("rect(0px, 0px, 0px, 0px)");
        block.SetProperty("clip", "auto");
        block.CssText.Should().Be("clip: auto;");
        block = CssDeclarationBlock.Parse("clip:auto;clip:rect(0,0,0,0)!important", CssDeclarationContext.Keyframe);
        block.CssText.Should().Be("clip: auto;");
        var stamp = block.Stamp;
        block.SetProperty("clip", "rect(0,0,0,0)", "important");
        block.Stamp.Should().Be(stamp);
    }

    [Test]
    public void RectangleEnvelopeCountsTowardsTheMathNestingLimit()
    {
        var input = CssReferenceInput.Parse("rect(calc(1em + 2px), auto, 0, 0)", null, default);
        var work = new CssValueWork(default);
        var parts = CssPropertyParser.Significant(input.Components, work);
        CssClipPropertyParser.Parse(parts, 2, work).Status.Should().Be(CssPropertyStatus.Valid);
        Action parse = () => CssClipPropertyParser.Parse(parts, 1, work);
        var exception = parse.Should().Throw<ParseLimitException>().Which;
        exception.Kind.Should().Be(ParseLimitKind.NestingDepth);
        exception.Observed.Should().Be(2);
    }

    [Test]
    public void CancellationPreservesStorageAndInterruptsTypedEdgeParsing()
    {
        var block = CssDeclarationBlock.Parse("clip:auto;color:red");
        var stamp = block.Stamp;
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Action set = () => block.SetProperty("clip", "rect(0, 0, 0, 0)", cancellationToken: cancellation.Token);
        set.Should().Throw<OperationCanceledException>();
        Action replace = () => block.ReplaceText("color:blue;clip:rect(0, 0, 0, 0)", cancellationToken: cancellation.Token);
        replace.Should().Throw<OperationCanceledException>();
        block.Stamp.Should().Be(stamp);
        block.CssText.Should().Be("clip: auto; color: red;");

        var input = CssReferenceInput.Parse("rect(1em, 2rem, calc(3em + 4px), auto)", null, default);
        var parts = CssPropertyParser.Significant(input.Components, new CssValueWork(default));
        using var duringParse = new CancellationTokenSource();
        var polls = 0;
        var work = new CssValueWork(duringParse.Token, () => { if (++polls == 3) duringParse.Cancel(); });
        Action parse = () => CssClipPropertyParser.Parse(parts, input.MaxNestingDepth, work);
        parse.Should().Throw<OperationCanceledException>();
        polls.Should().Be(3);
    }
}
