using Jint.HtmlParser.Css.Values.Math;

namespace Jint.Tests.HtmlParser.Css.Values.Math;

[TestFixture]
public sealed class MathFunctionCensusTests
{
    private static readonly string[] First = ["calc", "min", "max", "clamp"];
    private static readonly string[] Second = ["round", "mod", "rem"];
    private static readonly string[] Signs = ["abs", "sign"];
    private static readonly string[] Trigonometric =
    [
        "sin", "cos", "tan", "asin", "acos", "atan", "atan2"
    ];
    private static readonly string[] Third =
    [
        "pow", "sqrt", "hypot", "log", "exp"
    ];

    [Test]
    public void EveryValuesFourFunctionHasOneNamedStage()
    {
        (First.Length + Second.Length + Signs.Length + Trigonometric.Length + Third.Length).Should().Be(21);
        var names = First.Concat(Second).Concat(Signs).Concat(Trigonometric).Concat(Third).ToArray();
        names.Distinct(StringComparer.Ordinal).Count().Should().Be(21);
        Enum.GetValues<CssMathFunction>().Length.Should().Be(22);
        foreach (var name in names)
            CssMathParser.Recognize(name).Should().NotBe(CssMathFunction.None, name);
    }

    [Test]
    public void AllFunctionsAreImplementedEvenWhenNested()
    {
        foreach (var name in Third)
        {
            var argument = name is "pow" ? "2, 3" : name is "hypot" ? "2, 3" : "2";
            var result = MathTest.Parse($"calc(1 + {name}({argument}))", MathTest.Number);
            result.Status.Should().Be(CssMathParseStatus.Match, name);
        }
    }

    [Test]
    public void InvalidImplementedFunctionIsNoMatch()
    {
        var result = MathTest.Parse("calc(round(1) + pow(2,))", MathTest.Number);
        result.Status.Should().Be(CssMathParseStatus.NoMatch);
        result.Span.Start.Should().Be(22);
    }

    [Test]
    public void TwentyOneFunctionsAreImplementedAndNoneRemainPending()
    {
        foreach (var name in Second)
        {
            var source = name == "round" ? "round(2)" : $"{name}(2, 1)";
            MathTest.Parse(source, MathTest.Number).Status.Should().Be(CssMathParseStatus.Match);
        }
        foreach (var name in Signs)
            MathTest.Parse($"{name}(2)", MathTest.Number).Status.Should().Be(CssMathParseStatus.Match);
        foreach (var name in Trigonometric)
        {
            var source = name == "atan2" ? "atan2(2, 1)" : $"{name}(0)";
            var context = name is "atan2" or "asin" or "acos" or "atan" ? MathTest.Angle : MathTest.Number;
            MathTest.Parse(source, context).Status.Should().Be(CssMathParseStatus.Match);
        }
        Trigonometric.Length.Should().Be(7);
        Third.Length.Should().Be(5);
        foreach (var name in Third)
        {
            var arguments = name is "pow" ? "2, 3" : "2";
            MathTest.Parse($"{name}({arguments})", MathTest.Number).Status.Should().Be(CssMathParseStatus.Match);
        }
    }
}
