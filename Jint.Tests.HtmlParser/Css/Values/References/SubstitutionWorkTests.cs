#nullable enable
using Jint.HtmlParser;
using Jint.HtmlParser.Css;
using Jint.HtmlParser.Css.Values;
using Jint.HtmlParser.Css.Values.References;

namespace Jint.Tests.HtmlParser.Css.Values.References;

[TestFixture]
public sealed class SubstitutionWorkTests
{
    [Test]
    public void ExactLexicalOccurrenceCeilingAndOnePastAreDistinct()
    {
        var exact = string.Concat(Enumerable.Repeat("x ", 32_768));
        var result = SubstitutionFixture.Resolve(exact);
        result.Value.TokenCount.Should().Be(65_536);
        SubstitutionFixture.Resolve(exact + "x").Kind.Should().Be(CssSubstitutionResultKind.GuaranteedInvalid);
        var eof = "foo(" + string.Concat(Enumerable.Repeat("x ", 32_767));
        SubstitutionFixture.Resolve(eof).Value.TokenCount.Should().Be(65_536);
        SubstitutionFixture.Resolve(eof + "x").Kind.Should().Be(CssSubstitutionResultKind.GuaranteedInvalid);
    }

    [Test]
    public void ExactSpellingCeilingIncludesGiantTokensAndSyntheticClosers()
    {
        var exact = new string('x', 1_048_576);
        SubstitutionFixture.Resolve(exact).Value.SpellingLength.Should().Be(1_048_576);
        SubstitutionFixture.Resolve(exact + "x").Kind.Should().Be(CssSubstitutionResultKind.GuaranteedInvalid);
        var eof = "f(" + new string('x', 1_048_573);
        SubstitutionFixture.Resolve(eof).Value.SpellingLength.Should().Be(1_048_576);
        SubstitutionFixture.Resolve(eof + "x").Kind.Should().Be(CssSubstitutionResultKind.GuaranteedInvalid);
    }

    [Test]
    public void SharedFanOutCountsEveryOccurrenceAndOutsideFallbackCanRecover()
    {
        var bindings = FanOut(17);
        SubstitutionFixture.Resolve("var(--16)", bindings).Value.TokenCount.Should().Be(65_536);
        SubstitutionFixture.Resolve("var(--17)", bindings).Kind.Should().Be(CssSubstitutionResultKind.GuaranteedInvalid);
        SubstitutionFixture.Identifier(SubstitutionFixture.Resolve("var(--17,red)", bindings)).Should().Be("red");
        SubstitutionFixture.Identifier(SubstitutionFixture.Resolve("var(--present,var(--17))",
            [.. bindings, SubstitutionFixture.Specified("--present", "blue")])).Should().Be("blue");
    }

    [Test]
    public void OversizeDeviceReplacementAndLiteralRootAreInvalidButUnusedFallbackIsLazy()
    {
        var work = new CssValueWork(default);
        var huge = new string('x', 1_048_577);
        var device = CssEnvironmentBinding.Create("device", [], SubstitutionFixture.Input(huge), work);
        SubstitutionFixture.Identifier(SubstitutionFixture.ResolveWith("env(device,red)", [], true, [device]))
            .Should().Be("red");
        SubstitutionFixture.Identifier(SubstitutionFixture.Resolve("var(--present," + huge + ")",
            SubstitutionFixture.Specified("--present", "blue"))).Should().Be("blue");
        SubstitutionFixture.Identifier(SubstitutionFixture.Resolve("var(...var(--name)," + huge + ")",
            SubstitutionFixture.Specified("--name", "--present"),
            SubstitutionFixture.Specified("--present", "blue"))).Should().Be("blue");
    }

    [Test]
    public void AliasCallDepthIsIndependentOfMaterializedComponentDepth()
    {
        var bindings = new CssSubstitutionBinding[2_001];
        bindings[0] = SubstitutionFixture.Specified("--0", "blue");
        for (var i = 1; i < bindings.Length; i++)
            bindings[i] = SubstitutionFixture.Specified("--" + i, "var(--" + (i - 1) + ")");
        var work = new CssValueWork(default);
        var custom = CssSubstitutionSnapshot.Create(bindings, work);
        var environment = CssEnvironmentSnapshot.Create([], work);
        var input = CssReferenceInput.Parse("var(--2000)",
            new CssParseOptions { Limits = new ParseLimits { MaxNestingDepth = 1 } }, default);
        SubstitutionFixture.Identifier(CssSubstitutionExecutor.Resolve(input, custom, environment,
            new CssSubstitutionContext("color", CssReferenceUse.PropertyValue, true), work)).Should().Be("blue");
        var nested = CssReferenceInput.Parse("outer(var(--nested))",
            new CssParseOptions { Limits = new ParseLimits { MaxNestingDepth = 2 } }, default);
        var nestedCustom = CssSubstitutionSnapshot.Create([SubstitutionFixture.Specified("--nested", "foo(bar(red))")], work);
        var error = Assert.Throws<ParseLimitException>(() => CssSubstitutionExecutor.Resolve(nested,
            nestedCustom, environment, new CssSubstitutionContext("color", CssReferenceUse.PropertyValue, true), work));
        error!.Kind.Should().Be(ParseLimitKind.NestingDepth);
        error.Observed.Should().Be(3);
    }

    [Test]
    public void RepeatedSharedEarlyExpansionStopsBeforeAnExponentialFlatten()
    {
        var checkpoints = 0;
        var result = SubstitutionFixture.ResolveWith("var(...var(--16)...var(--16),red)", FanOut(16), true,
            work: new CssValueWork(default, () => checkpoints++));
        result.Kind.Should().Be(CssSubstitutionResultKind.GuaranteedInvalid);
        checkpoints.Should().BeLessThan(2_000);
    }

    [Test]
    public void EnvironmentFactoryWorkIsLinearForOneNameAndManyDistinctIndices()
    {
        static int Count(int count)
        {
            var bindings = new CssEnvironmentBinding[count];
            var input = SubstitutionFixture.Input("red");
            for (var i = 0; i < count; i++)
                bindings[i] = CssEnvironmentBinding.Create("segment", [i.ToString(System.Globalization.CultureInfo.InvariantCulture)],
                    input, new CssValueWork(default));
            var checks = 0;
            CssEnvironmentSnapshot.Create(bindings, new CssValueWork(default, () => checks++));
            return checks;
        }
        var small = Count(1_000);
        Count(2_000).Should().BeLessThan(small * 3);
    }

    [Test]
    public void LongHashScansIntermediateExpansionAndFinalPublicationPollCancellation()
    {
        var longName = "--" + new string('x', 100_000);
        using var hashCancellation = new CancellationTokenSource();
        var hashChecks = 0;
        Assert.Throws<OperationCanceledException>(() => CssSubstitutionSnapshot.Create(
            [SubstitutionFixture.Specified(longName, "red")], new CssValueWork(hashCancellation.Token, () =>
            {
                if (++hashChecks == 8) hashCancellation.Cancel();
            })));

        var bindings = FanOut(12);
        var work = new CssValueWork(default);
        var custom = CssSubstitutionSnapshot.Create(bindings, work);
        var environment = CssEnvironmentSnapshot.Create([], work);
        var input = SubstitutionFixture.Input("foo(var(--12))");
        var context = new CssSubstitutionContext("width", CssReferenceUse.PropertyValue, true);
        var total = 0;
        CssSubstitutionExecutor.Resolve(input, custom, environment, context, new CssValueWork(default, () => total++));
        foreach (var target in new[] { total / 3, total - 1, total })
        {
            using var cancellation = new CancellationTokenSource();
            var current = 0;
            Assert.Throws<OperationCanceledException>(() => CssSubstitutionExecutor.Resolve(input, custom, environment,
                context, new CssValueWork(cancellation.Token, () =>
                {
                    if (++current == target) cancellation.Cancel();
                })));
        }
    }

    [Test]
    public void DoubledWideSourcesHaveLinearCheckpointGrowth()
    {
        static int Count(int repeats)
        {
            var count = 0;
            SubstitutionFixture.ResolveWith(string.Concat(Enumerable.Repeat("foo(var(--x)) ", repeats)),
                [SubstitutionFixture.Specified("--x", "red")], true,
                work: new CssValueWork(default, () => count++));
            return count;
        }
        Count(512).Should().BeLessThan(Count(256) * 3);
    }

    private static CssSubstitutionBinding[] FanOut(int last)
    {
        var result = new CssSubstitutionBinding[last + 1];
        result[0] = SubstitutionFixture.Specified("--0", "x");
        for (var i = 1; i <= last; i++)
            result[i] = SubstitutionFixture.Specified("--" + i,
                "var(--" + (i - 1) + ")var(--" + (i - 1) + ")");
        return result;
    }
}
