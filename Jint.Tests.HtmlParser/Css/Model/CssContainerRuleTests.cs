#nullable enable
using Jint.HtmlParser;
using Jint.HtmlParser.Css.Conditions;
using Jint.HtmlParser.Css.Media;
using Jint.HtmlParser.Css.Model;
using Jint.HtmlParser.Css.Values;
using Jint.HtmlParser.Css.Syntax;
using Jint.HtmlParser.Css.Values.Properties;

namespace Jint.Tests.HtmlParser.Css.Model;

public sealed class CssContainerRuleTests
{
    [TestCase("(width < 380px)", "Less", 380)]
    [TestCase("(width <= 400px)", "LessEqual", 400)]
    [TestCase("(width = 1in)", "Equal", 96)]
    [TestCase("(width > 72pt)", "Greater", 96)]
    [TestCase("(width >= 6pc)", "GreaterEqual", 96)]
    [TestCase("(380px > width)", "Less", 380)]
    [TestCase("(400px >= width)", "LessEqual", 400)]
    [TestCase("(96px = WIDTH)", "Equal", 96)]
    [TestCase("(96px < width)", "Greater", 96)]
    [TestCase("(96px <= width)", "GreaterEqual", 96)]
    [TestCase("(width < -1px)", "Less", -1)]
    [TestCase("(width >= 0)", "GreaterEqual", 0)]
    [TestCase("(width </**/= 400px)", "LessEqual", 400)]
    [TestCase(@"(w\69 dth < 380px)", "Less", 380)]
    public void RangeOperandsRetainTypedComparisonsAndAbsoluteLengths(string query, string comparison, double pixels)
    {
        var rule = (CssContainerRule) CssStyleSheet.Parse("@container panel " + query + " {a{}}").Rules[0];
        var feature = rule.Condition.Instructions.Single().Feature!;
        feature.Axis.Should().Be(CssContainerAxis.Width);
        feature.Comparison.ToString().Should().Be(comparison);
        feature.Pixels.Should().Be(pixels);
        feature.Dependency.Should().BeNull();
        var reparsed = (CssContainerRule) CssStyleSheet.Parse(rule.CssText).Rules[0];
        reparsed.Condition.Instructions.Single().Feature.Should().Be(feature);
    }

    [TestCase("(100px < inline-size <= 400px)", "Greater", "LessEqual", 100, 400)]
    [TestCase("(400px > inline-size >= 100px)", "Less", "GreaterEqual", 400, 100)]
    public void ChainedRangesAreTwoConjoinedFeatures(string query, string first, string second,
        double lower, double upper)
    {
        var rule = (CssContainerRule) CssStyleSheet.Parse("@container " + query + " {a{}}").Rules[0];
        rule.ContainerQuery.Should().Be(query);
        rule.Condition.Instructions.Select(instruction => instruction.Operation)
            .Should().Equal(CssMediaOperation.Feature, CssMediaOperation.Feature, CssMediaOperation.And);
        rule.Condition.Instructions[0].Feature.Should().Be(new CssContainerFeature(CssContainerAxis.InlineSize,
            Enum.Parse<CssMediaComparison>(first), lower));
        rule.Condition.Instructions[1].Feature.Should().Be(new CssContainerFeature(CssContainerAxis.InlineSize,
            Enum.Parse<CssMediaComparison>(second), upper));
        rule.Condition.Evaluate(_ => CssMediaTruth.Unknown, new CssValueWork(default)).Should().Be(CssMediaTruth.Unknown);
    }

    [TestCase("(width < = 400px)")]
    [TestCase("(width >\n= 400px)")]
    [TestCase("(width == 400px)")]
    [TestCase("(width => 400px)")]
    [TestCase("(400px = width = 400px)")]
    [TestCase("(100px < width > 400px)")]
    [TestCase("(width < 100px < 400px)")]
    [TestCase("(0 < width < 100px < 400px)")]
    [TestCase("(width <)")]
    [TestCase("(< width)")]
    [TestCase("(min-width < 400px)")]
    [TestCase("(400px > max-width)")]
    [TestCase("(future-width < 400px)")]
    [TestCase("(width < 1)")]
    [TestCase("(width < 1e-9999)")]
    [TestCase("(width < 50%)")]
    [TestCase("(width < 1s)")]
    [TestCase("(width < 1unknown)")]
    [TestCase("(width < auto)")]
    [TestCase("(0 < width < 1s)")]
    public void InvalidRangesRemainGeneralEnclosedUnknownIncludingUnderNot(string query)
    {
        var rule = (CssContainerRule) CssStyleSheet.Parse("@container not " + query + " {a{}}").Rules[0];
        rule.Condition.Instructions.Should().NotContain(instruction => instruction.Operation == CssMediaOperation.Feature);
        rule.Condition.Evaluate(_ => throw new InvalidOperationException("No metric should be requested."),
            new CssValueWork(default)).Should().Be(CssMediaTruth.Unknown);
        var reparsed = (CssContainerRule) CssStyleSheet.Parse(rule.CssText).Rules[0];
        reparsed.Condition.Instructions.Should().Equal(rule.Condition.Instructions);
    }

    [TestCase("(height < 10px)", "C6:container-metric:height")]
    [TestCase("(10px <= block-size)", "C6:container-metric:block-size")]
    [TestCase("(width < 1em)", "C6:container-length-unit:em")]
    [TestCase("(1cqw < width)", "C6:container-length-unit:cqw")]
    [TestCase("(width < calc(1px + 1em))", "C6:container-feature-syntax:width")]
    [TestCase("(calc(1px + 1em) < width)", "C6:container-feature-syntax:width")]
    public void RangesPreserveUnimplementedMetricAndValueDependencies(string query, string dependency)
    {
        var rule = (CssContainerRule) CssStyleSheet.Parse("@container " + query + " {a{}}").Rules[0];
        rule.Condition.Instructions.Single().Feature!.Dependency.Should().Be(dependency);
    }

    [Test]
    public void RangeRuleSerializationRangesAndMutationKeepRealOwnership()
    {
        var sheet = CssStyleSheet.Parse("@container panel (100px <= width < 400px) { a { opacity:.5 } }");
        var rule = (CssContainerRule) sheet.Rules[0];
        rule.ConditionText.Should().Be("panel (100px <= width < 400px)");
        rule.CssText.Should().Be("@container panel (100px <= width < 400px) {\na { opacity: 0.5; }\n}");
        var child = (CssStyleRule) rule.Rules[0];
        child.ParentRule.Should().BeSameAs(rule);
        child.ParentStyleSheet.Should().BeSameAs(sheet);
        var snapshot = sheet.SerializeWithRanges();
        foreach (var entry in snapshot.Ranges)
            snapshot.Text[entry.Value.Start..entry.Value.End].Should().Be(entry.Key.CssText);
        var stamp = sheet.Stamp;
        Assert.Throws<DomException>(() => rule.InsertRule("@import 'no.css';", 0))!.Name.Should().Be("HierarchyRequestError");
        sheet.Stamp.Should().Be(stamp);
        child.Style.SetProperty("opacity", ".75");
        sheet.Stamp.Should().NotBe(stamp);
        sheet.DeleteRule(0);
        rule.ParentStyleSheet.Should().BeNull();
        stamp = sheet.Stamp;
        child.Style.SetProperty("opacity", ".25");
        sheet.Stamp.Should().Be(stamp);
        CssStyleSheet.Parse(rule.CssText).Rules[0].CssText.Should().Be(rule.CssText);
    }

    [Test]
    public void RangeNumericWorkIsCancellableBeforeProgramPublication()
    {
        var source = "(width < " + new string('0', 100_000) + "1px)";
        var values = new CssSyntaxParser(source, null, default).ParseComponentValues();
        var parts = CssPropertyParser.Significant(values, new CssValueWork(default));
        using var cancellation = new CancellationTokenSource();
        var checkpoints = 0;
        var work = new CssValueWork(cancellation.Token, () =>
        {
            if (++checkpoints == 20) cancellation.Cancel();
        });
        var program = new List<CssContainerInstruction>();
        Assert.Throws<OperationCanceledException>(() => CssContainerParser.TryParseCondition(parts, program, work));
        checkpoints.Should().Be(20);
        program.Should().BeEmpty();
    }

    [TestCase("(min-width)")]
    [TestCase("not (max-inline-size)")]
    [TestCase("(min-width) or (width:10px)")]
    public void PrefixedBooleanFeaturesStayUnknownRatherThanBecomingNonzeroTests(string condition)
    {
        var rule = (CssContainerRule) CssStyleSheet.Parse("@container " + condition + " {a{}}").Rules[0];
        rule.Condition.Instructions.Should().Contain(instruction => instruction.Operation == CssMediaOperation.Unknown);
        rule.Condition.Instructions.Where(instruction => instruction.Feature is not null)
            .Should().OnlyContain(instruction => instruction.Feature!.Comparison != CssMediaComparison.Boolean);
    }

    [Test]
    public void ConditionListsKeepTypedBranchesAndChildrenWithANamedDependency()
    {
        var sheet = CssStyleSheet.Parse("@container first (width:10px), second not (inline-size:20px) {a{opacity:.5}}");
        var rule = (CssContainerRule) sheet.Rules[0];
        rule.Rules.Count.Should().Be(1);
        rule.ContainerName.Should().Be("");
        rule.ContainerQuery.Should().Be("");
        rule.ConditionText.Should().Be("first (width:10px), second not (inline-size:20px)");
        rule.Conditions.Select(branch => branch.Name).Should().Equal("first", "second");
        rule.Condition.PendingDependency.Should().Be("C6:container-condition-list");
        Assert.Throws<CssIncompleteGrammarException>(() => rule.Condition.Evaluate(_ => CssMediaTruth.True, new CssValueWork(default)))!
            .Blocker.Should().Be("C6:container-condition-list");
    }
    [Test]
    public void WideConditionCancellationInterruptsTaskExpansionBeforeEvaluatingOperands()
    {
        var source = string.Join(" or ", Enumerable.Repeat("(width:1px)", 8192));
        var values = new CssSyntaxParser(source, null, default).ParseComponentValues();
        var parts = CssPropertyParser.Significant(values, new CssValueWork(default));
        using var cancellation = new CancellationTokenSource();
        var checkpoints = 0;
        var work = new CssValueWork(cancellation.Token, () =>
        {
            // The frame and join validation consume exactly 8192 units (two polls).
            // The third poll occurs inside expansion, before any queued operand executes.
            if (++checkpoints == 3) cancellation.Cancel();
        });
        var program = new List<CssContainerInstruction>();
        Assert.Throws<OperationCanceledException>(() => CssContainerParser.TryParseCondition(parts, program, work));
        checkpoints.Should().Be(3);
        program.Should().BeEmpty();
    }

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
        Assert.Throws<ParseLimitException>(() => CssStyleSheet.Parse("@container " + condition + " {}",
            new CssParseOptions { Limits = new ParseLimits { MaxNestingDepth = 64 } }))!.Kind.Should().Be(ParseLimitKind.NestingDepth);
        CssPropertyRegistry.Completed["writing-mode"].Inherited.Should().BeTrue();
        CssPropertyRegistry.Completed["writing-mode"].InitialValue.Should().Be("horizontal-tb");
    }
}
