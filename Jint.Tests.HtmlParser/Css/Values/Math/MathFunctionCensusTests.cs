using Jint.HtmlParser.Css.Values.Math;

namespace Jint.Tests.HtmlParser.Css.Values.Math;

[TestFixture]
public sealed class MathFunctionCensusTests
{
    private static readonly string[] First = ["calc", "min", "max", "clamp"];
    private static readonly string[] Second = ["round", "mod", "rem"];
    private static readonly string[] Third =
    [
        "sin", "cos", "tan", "asin", "acos", "atan", "atan2", "pow", "sqrt", "hypot", "log", "exp", "abs", "sign"
    ];

    [Test]
    public void EveryValuesFourFunctionHasOneNamedStage()
    {
        (First.Length + Second.Length + Third.Length).Should().Be(21);
        var names = First.Concat(Second).Concat(Third).ToArray();
        names.Distinct(StringComparer.Ordinal).Count().Should().Be(21);
        Enum.GetValues<CssMathFunction>().Length.Should().Be(22);
        foreach (var name in names)
            CssMathParser.Recognize(name).Should().NotBe(CssMathFunction.None, name);
    }

    [Test]
    public void PendingFunctionsStayExplicitEvenWhenNested()
    {
        foreach (var name in Second.Concat(Third))
        {
            var result = MathTest.Parse($"calc(1 + {name}(2))", MathTest.Number);
            result.Status.Should().Be(CssMathParseStatus.RequiresLaterGrammar, name);
            result.PendingFunction.Should().Be(CssMathParser.Recognize(name));
            Assert.Throws<InvalidOperationException>(() => _ = result.Value);
        }
    }

    [Test]
    public void FirstPendingFunctionKeepsItsOriginalSpan()
    {
        var result = MathTest.Parse("calc(round(1) + sin(2))", MathTest.Number);
        result.Status.Should().Be(CssMathParseStatus.RequiresLaterGrammar);
        result.PendingFunction.Should().Be(CssMathFunction.Round);
        result.Span.Start.Should().Be(5);
    }
}
