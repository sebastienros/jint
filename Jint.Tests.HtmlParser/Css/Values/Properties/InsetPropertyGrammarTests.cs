#nullable enable

using Jint.HtmlParser.Css.Values;
using Jint.HtmlParser.Css.Values.Properties;
using Jint.HtmlParser.Css.Values.References;

namespace Jint.Tests.HtmlParser.Css.Values.Properties;

public sealed class InsetPropertyGrammarTests
{
    private static readonly string[] Sides = ["top", "right", "bottom", "left"];

    [TestCase("AUTO", "auto")]
    [TestCase("-9999em", "-9999em")]
    [TestCase("-25%", "-25%")]
    [TestCase("25%", "25%")]
    [TestCase("-2px", "-2px")]
    [TestCase("0", "0px")]
    [TestCase("-0", "0px")]
    [TestCase("calc(25% - 2em)", "calc(25% - 2em)")]
    [TestCase("calc(-2px)", "calc(-2px)")]
    public void PhysicalInsetsAcceptSignedTypedValues(string source, string expected)
    {
        foreach (var side in Sides)
        foreach (var context in new[] { CssDeclarationContext.Style, CssDeclarationContext.Keyframe })
        {
            var result = CssPropertyParser.Parse(side, source, context);
            result.Status.Should().Be(CssPropertyStatus.Valid);
            result.Value.Serialize().Should().Be(expected);
            CssPropertyParser.Parse(side, expected, context).Value.Serialize().Should().Be(expected);
        }
    }

    [TestCase("stretch")]
    [TestCase("none")]
    [TestCase("2")]
    [TestCase("-2")]
    [TestCase("1px 2px")]
    [TestCase("auto, 2px")]
    [TestCase("calc(1s)")]
    [TestCase("1fr")]
    public void InvalidValuesRemainInvalid(string source)
    {
        foreach (var side in Sides)
            CssPropertyParser.Parse(side, source).Status.Should().Be(CssPropertyStatus.Invalid);
    }

    [TestCase("anchor(left)", "inset:anchor")]
    [TestCase("anchor-size(width)", "inset:anchor-size")]
    [TestCase("calc(anchor(left) + 2px)", "inset:anchor")]
    [TestCase("calc(25% - anchor-size(width))", "inset:anchor-size")]
    public void AnchorExtensionsStayNamedPendingInsideMath(string source, string blocker)
    {
        foreach (var side in Sides)
        {
            var result = CssPropertyParser.Parse(side, source);
            result.Status.Should().Be(CssPropertyStatus.UnimplementedGrammar);
            result.Blocker.Should().Be(blocker);
        }
    }

    [Test]
    public void InitialMetadataWideKeywordsReferencesAndLogicalBoundariesAreShared()
    {
        foreach (var side in Sides)
        {
            CssPropertyRegistry.Completed[side].InitialValue.Should().Be("auto");
            CssPropertyRegistry.Completed[side].Inherited.Should().BeFalse();
            foreach (var wide in new[] { "initial", "inherit", "unset", "revert", "revert-layer", "revert-rule" })
                CssPropertyParser.Parse(side, wide).Value.Text.Should().Be(wide);
            CssPropertyParser.Parse(side, "var(--inset)").Status.Should().Be(CssPropertyStatus.Deferred);
            CssPropertyParser.Parse(side, "env(inset-offset)").Status.Should().Be(CssPropertyStatus.Deferred);
        }
        foreach (var pending in new[] { "inset", "inset-block", "inset-inline", "inset-block-start", "inset-inline-end" })
        {
            var result = CssPropertyParser.Parse(pending, "0");
            result.Status.Should().Be(CssPropertyStatus.UnimplementedGrammar);
            result.Blocker.Should().Be("V2:" + pending);
        }
    }

    [Test]
    public void AnAlreadyTokenizedCalculationPollsTheFamilyWalk()
    {
        var input = CssReferenceInput.Parse("calc(" + string.Join(" + ", Enumerable.Repeat("1px", 8192)) + ")", null, default);
        var parts = CssPropertyParser.Significant(input.Components, new CssValueWork(default));
        using var cancellation = new CancellationTokenSource();
        var polls = 0;
        var work = new CssValueWork(cancellation.Token, () => { if (++polls == 2) cancellation.Cancel(); });
        Action parse = () => CssInsetPropertyParser.Parse(parts, input.MaxNestingDepth, work);
        parse.Should().Throw<OperationCanceledException>();
        polls.Should().Be(2);
    }
}
