#nullable enable
using Jint.HtmlParser;
using Jint.HtmlParser.Css.Conditions;
using Jint.HtmlParser.Css.Media;
using Jint.HtmlParser.Css.Model;
using Jint.HtmlParser.Css.Values;
using Jint.HtmlParser.Css.Values.Properties;

namespace Jint.Tests.HtmlParser.Css.Model;

public sealed class CssContainerRuleTests
{
    [Test]
    public void NamedRuleOwnsLiveOrderedChildrenAndRetainsTypedCondition()
    {
        var sheet = CssStyleSheet.Parse("@container swagger-ui (max-width:550px) { a {flex-direction:column} b {font-size:12px} }");
        var rule = (CssContainerRule) sheet.Rules[0];
        rule.Type.Should().Be(CssRuleType.Container);
        rule.ContainerName.Should().Be("swagger-ui");
        rule.ContainerQuery.Should().Be("(max-width:550px)");
        rule.Rules.Select(child => ((CssStyleRule) child).SelectorText).Should().Equal("a", "b");
        rule.Rules[0].ParentRule.Should().BeSameAs(rule);
        rule.Rules[0].ParentStyleSheet.Should().BeSameAs(sheet);
        rule.Condition.Instructions[0].Feature!.Axis.Should().Be(CssContainerAxis.Width);
        rule.Condition.Instructions[0].Feature!.Pixels.Should().Be(550);
        var children = rule.Rules;
        rule.InsertRule("c {}", 1);
        children.Select(child => ((CssStyleRule) child).SelectorText).Should().Equal("a", "c", "b");
        rule.DeleteRule(1);
        children.Count.Should().Be(2);
    }

    [TestCase("not (width:1px)")]
    [TestCase("(width:1px) and (inline-size:2px)")]
    [TestCase("(width:1px) or (inline-size:2px)")]
    public void UnknownFeaturesStayUnknownThroughBooleanOperations(string condition)
    {
        var rule = (CssContainerRule) CssStyleSheet.Parse("@container " + condition + " {a{}}").Rules[0];
        rule.Condition.Evaluate(_ => CssMediaTruth.Unknown, new CssValueWork(default)).Should().Be(CssMediaTruth.Unknown);
    }

    [TestCase("(height:1px)", "C6:container-metric:height")]
    [TestCase("(width:1em)", "C6:container-length-unit:em")]
    [TestCase("scroll-state(stuck:top)", "C6:container-scroll-state")]
    public void UnsupportedMetricsAreDemandTimeDependencies(string query, string dependency)
    {
        var rule = (CssContainerRule) CssStyleSheet.Parse("@container " + query + " {a{}}").Rules[0];
        rule.Condition.Instructions[0].Feature!.Dependency.Should().Be(dependency);
    }

    [TestCase("swagger-ui / inline-size", "swagger-ui", "inline-size")]
    [TestCase("first second / size", "first second", "size")]
    [TestCase("none", "none", "normal")]
    [TestCase("name / scroll-state inline-size", "name", "inline-size scroll-state")]
    public void ShorthandResetsBothLonghandsAndSerializesNames(string input, string name, string type)
    {
        var block = CssDeclarationBlock.Parse("container:" + input);
        block.GetPropertyValue("container-name").Should().Be(name);
        block.GetPropertyValue("container-type").Should().Be(type);
        block.SetProperty("container", "next");
        block.GetPropertyValue("container-type").Should().Be("normal");
    }

    [TestCase("and")]
    [TestCase("name none")]
    [TestCase("name / normal size")]
    [TestCase("name / size size")]
    [TestCase("name / inline-size / size")]
    public void InvalidShorthandPreservesExistingDeclarationAndStamp(string input)
    {
        var block = CssDeclarationBlock.Parse("container:keep / size");
        var stamp = block.Stamp;
        block.SetProperty("container", input);
        block.GetPropertyValue("container").Should().Be("keep / size");
        block.Stamp.Should().Be(stamp);
    }

    [Test]
    public void GrammarCancellationAndFiniteConditionDepthPropagate()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Assert.Throws<OperationCanceledException>(() => CssPropertyParser.Parse("container-name", "name", cancellationToken: cancellation.Token));
        var condition = new string('(', 66) + "width:1px" + new string(')', 66);
        Assert.Throws<ParseLimitException>(() => CssStyleSheet.Parse("@container " + condition + " {}"))!.Kind.Should().Be(ParseLimitKind.NestingDepth);
        CssPropertyRegistry.Completed["writing-mode"].Inherited.Should().BeTrue();
        CssPropertyRegistry.Completed["writing-mode"].InitialValue.Should().Be("horizontal-tb");
    }
}
