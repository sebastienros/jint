#nullable enable
using Jint.HtmlParser.Css;
using Jint.HtmlParser.Css.Values;
using Jint.HtmlParser.Css.Values.References;

namespace Jint.Tests.HtmlParser.Css.Values.References;

[TestFixture]
public sealed class SubstitutionOwnershipTests
{
    [Test]
    public void SnapshotsOwnArraysAndRejectCanonicalDuplicatesAfterCallerMutation()
    {
        var work = new CssValueWork(default);
        var supplied = new[] { SubstitutionFixture.Specified("--x", "blue") };
        var snapshot = CssSubstitutionSnapshot.Create(supplied, work);
        supplied[0] = SubstitutionFixture.Specified("--x", "red");
        snapshot.TryGet("--x", work, out var binding).Should().BeTrue();
        binding.Input.Source.Should().Be("blue");
        Assert.Throws<ArgumentException>(() => CssSubstitutionSnapshot.Create([binding, supplied[0]], work));
        Assert.Throws<ArgumentException>(() => CssSubstitutionSnapshot.Create([default], work));

        var indices = new[] { "+00012", "-0" };
        var device = CssEnvironmentBinding.Create("segment", indices, SubstitutionFixture.Input("blue"), work);
        indices[0] = "13";
        var duplicate = CssEnvironmentBinding.Create("segment", ["12", "0"], SubstitutionFixture.Input("red"), work);
        Assert.Throws<ArgumentException>(() => CssEnvironmentSnapshot.Create([device, duplicate], work));
        var devices = new[] { device };
        var environment = CssEnvironmentSnapshot.Create(devices, work);
        devices[0] = duplicate;
        environment.TryGet("segment", ["12", "0"], work, out var value).Should().BeTrue();
        value!.Source.Should().Be("blue");
    }

    [Test]
    public void RepeatedInsertionsAndIdenticalOffsetsKeepDistinctProjectionOrigins()
    {
        var first = SubstitutionFixture.Input("red");
        var second = SubstitutionFixture.Input("blue");
        var source = SubstitutionFixture.Input("var(--a) var(--b) var(--a)");
        var work = new CssValueWork(default);
        var result = CssSubstitutionExecutor.Resolve(source,
            CssSubstitutionSnapshot.Create([CssSubstitutionBinding.Specified("--a", first, false),
                CssSubstitutionBinding.Specified("--b", second, false)], work),
            CssEnvironmentSnapshot.Create([], work), new CssSubstitutionContext("color", CssReferenceUse.PropertyValue, true), work);
        var origins = result.Value.OriginsFor(new CssSourceSpan(0, result.Value.SpellingLength));
        origins.Count.Should().Be(5);
        origins[0].Source.Should().BeSameAs(first);
        origins[2].Source.Should().BeSameAs(second);
        origins[4].Source.Should().BeSameAs(first);
        origins[0].SourceSpan.Start.Should().Be(0);
        origins[2].SourceSpan.Start.Should().Be(0);
        origins[4].ProjectionSpan.Start.Should().BeGreaterThan(origins[0].ProjectionSpan.Start);
        first = SubstitutionFixture.Input("green");
        origins[0].Source.Source.Should().Be("red");
        result.Value.OriginsFor(new CssSourceSpan(0, 0)).Count.Should().Be(0);
        result.Value.OriginsFor(new CssSourceSpan(result.Value.SpellingLength, 0)).Count.Should().Be(0);
        Assert.Throws<ArgumentOutOfRangeException>(() => result.Value.OriginsFor(new CssSourceSpan(-1, 1)));
        Assert.Throws<ArgumentOutOfRangeException>(() => _ = origins[origins.Count]);
        SubstitutionFixture.Resolve("").Value.OriginsFor(default).Count.Should().Be(0);
    }

    [Test]
    public void SegmentFactoriesCopyInputsAndComputedValuesRetainImmutableMetrics()
    {
        var work = new CssValueWork(default);
        var red = CssSegment.FromInput(SubstitutionFixture.Input("red"), work);
        var green = CssSegment.FromInput(SubstitutionFixture.Input("green"), work);
        var children = new[] { red, red };
        var concat = CssSegment.Concat(children, work);
        children[0] = green;
        concat.Children[0].Source!.Source.Should().Be("red");
        var containerInput = SubstitutionFixture.Input("foo(red)");
        var container = CssSegment.Container(containerInput, containerInput.Components[0], children, work);
        children[0] = red;
        container.Children[0].Source!.Source.Should().Be("green");
        var value = CssSubstitutedValue.Create(container, 0, work);
        value.Components[0].Values[0].Token.Text.Should().Be("green");
        value.SpellingLength.Should().Be(13);
        value.TokenCount.Should().Be(4);
    }

    [Test]
    public void EofClosersAndEscapedOpenersHaveRealSourceOrigins()
    {
        var input = SubstitutionFixture.Input("f\\6f o(red");
        var result = SubstitutionFixture.Resolve(input.Source);
        var origins = result.Value.OriginsFor(new CssSourceSpan(0, result.Value.SpellingLength));
        origins.Count.Should().Be(3);
        origins[0].SourceSpan.Length.Should().Be(7);
        origins[2].IsSyntheticCloser.Should().BeTrue();
        origins[2].SourceSpan.Start.Should().Be(input.Source.Length);
        origins[2].SourceSpan.Length.Should().Be(0);
        result.Value.SpellingLength.Should().Be(input.Source.Length + 1);
    }
}
