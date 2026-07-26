namespace Jint.Tests.Runtime;

/// <summary>
/// A <c>Set</c> compares its values with SameValueZero, which — unlike <c>===</c> — treats
/// <c>NaN</c> as equal to itself. The backing structure keeps both a lookup structure and an
/// ordering list, and both of them, plus every set derived from an existing one, have to use that
/// same rule. Where they disagree the two halves drift apart: a value can be missing from lookups
/// while iteration still yields it.
/// </summary>
public class SetSameValueZeroTests
{
    private static Engine CreateEngine() => new();

    [Fact]
    public void DeleteRemovesNaN()
    {
        var engine = CreateEngine();

        engine.Evaluate("new Set([NaN]).delete(NaN)").AsBoolean().Should().BeTrue();
        engine.Evaluate("var s = new Set([NaN]); s.delete(NaN); s.size").AsNumber().Should().Be(0);
        engine.Evaluate("var s = new Set([NaN]); s.delete(NaN); s.has(NaN)").AsBoolean().Should().BeFalse();
    }

    [Fact]
    public void DeleteLeavesNoStragglerBehindForNaN()
    {
        // the ordering list and the lookup structure must agree afterwards
        var engine = CreateEngine();

        engine.Evaluate("var s = new Set([NaN]); s.delete(NaN); [...s].length").AsNumber().Should().Be(0);
        engine.Evaluate("var s = new Set([NaN]); s.delete(NaN); var n = 0; s.forEach(function () { n++; }); n")
            .AsNumber().Should().Be(0);
        engine.Evaluate("var s = new Set([NaN]); s.delete(NaN); Array.from(s.values()).length").AsNumber().Should().Be(0);
    }

    [Fact]
    public void ReAddingNaNAfterDeleteDoesNotDuplicate()
    {
        var engine = CreateEngine();

        engine.Evaluate("var s = new Set([NaN]); s.delete(NaN); s.add(NaN); s.size").AsNumber().Should().Be(1);
        engine.Evaluate("var s = new Set([NaN]); s.delete(NaN); s.add(NaN); [...s].length").AsNumber().Should().Be(1);
    }

    [Fact]
    public void DeleteRemovesNaNAmongOtherValues()
    {
        var engine = CreateEngine();

        engine.Evaluate("var s = new Set([1, NaN, 2]); s.delete(NaN); s.size").AsNumber().Should().Be(2);
        engine.Evaluate("var s = new Set([1, NaN, 2]); s.delete(NaN); [...s].join(',')").AsString().Should().Be("1,2");
    }

    [Fact]
    public void ADerivedSetKeepsNaNSemantics()
    {
        // a set produced from another one must not fall back to default equality
        var engine = CreateEngine();

        engine.Evaluate("new Set([NaN, 1]).difference(new Set([1])).has(NaN)").AsBoolean().Should().BeTrue();
        engine.Evaluate("new Set([NaN, 1]).symmetricDifference(new Set([1])).has(NaN)").AsBoolean().Should().BeTrue();
        engine.Evaluate("new Set([NaN, 1]).intersection(new Set([NaN])).has(NaN)").AsBoolean().Should().BeTrue();
        engine.Evaluate("new Set([NaN, 1]).union(new Set([NaN])).has(NaN)").AsBoolean().Should().BeTrue();
    }

    [Fact]
    public void ADerivedSetDoesNotDuplicateNaN()
    {
        var engine = CreateEngine();

        engine.Evaluate("var d = new Set([NaN, 1]).difference(new Set([1])); d.add(NaN); d.size")
            .AsNumber().Should().Be(1);
        engine.Evaluate("var d = new Set([NaN, 1]).symmetricDifference(new Set([1])); d.add(NaN); d.size")
            .AsNumber().Should().Be(1);
    }

    [Fact]
    public void ADerivedSetCanDeleteNaN()
    {
        var engine = CreateEngine();

        engine.Evaluate("new Set([NaN, 1]).difference(new Set([1])).delete(NaN)").AsBoolean().Should().BeTrue();
        engine.Evaluate("var d = new Set([NaN, 1]).difference(new Set([1])); d.delete(NaN); d.size")
            .AsNumber().Should().Be(0);
    }

    [Fact]
    public void SubsetAndDisjointChecksHandleNaN()
    {
        var engine = CreateEngine();

        engine.Evaluate("new Set([NaN]).isSubsetOf(new Set([NaN, 1]))").AsBoolean().Should().BeTrue();
        engine.Evaluate("new Set([NaN, 1]).isSupersetOf(new Set([NaN]))").AsBoolean().Should().BeTrue();
        engine.Evaluate("new Set([NaN]).isDisjointFrom(new Set([NaN]))").AsBoolean().Should().BeFalse();
    }

    [Fact]
    public void NegativeZeroIsNormalized()
    {
        // the other SameValueZero special case, which already worked; guards the same code path
        var engine = CreateEngine();

        engine.Evaluate("new Set([-0]).delete(0)").AsBoolean().Should().BeTrue();
        engine.Evaluate("var s = new Set([-0]); s.delete(0); s.size").AsNumber().Should().Be(0);
        engine.Evaluate("new Set([-0, 0]).size").AsNumber().Should().Be(1);
        engine.Evaluate("Object.is([...new Set([-0])][0], 0)").AsBoolean().Should().BeTrue();
    }

    [Fact]
    public void OrdinaryValuesAreUnaffected()
    {
        // control
        var engine = CreateEngine();

        engine.Evaluate("var s = new Set([1, 2]); s.delete(1); s.size").AsNumber().Should().Be(1);
        engine.Evaluate("var s = new Set([1, 2]); s.delete(3)").AsBoolean().Should().BeFalse();
        engine.Evaluate("var s = new Set(['a', 'b']); s.delete('a'); [...s].join(',')").AsString().Should().Be("b");
        engine.Evaluate("new Set([1, 2]).difference(new Set([2])).has(1)").AsBoolean().Should().BeTrue();
        engine.Evaluate("var o = {}; var s = new Set([o]); s.delete(o); s.size").AsNumber().Should().Be(0);
    }

    [Fact]
    public void MapAlreadyHandledNaN()
    {
        // Map uses a different backing structure and was never affected; pinned for contrast
        var engine = CreateEngine();

        engine.Evaluate("new Map([[NaN, 'v']]).delete(NaN)").AsBoolean().Should().BeTrue();
        engine.Evaluate("var m = new Map([[NaN, 'v']]); m.delete(NaN); m.size").AsNumber().Should().Be(0);
        engine.Evaluate("new Map([[NaN, 'v']]).get(NaN)").AsString().Should().Be("v");
    }
}
