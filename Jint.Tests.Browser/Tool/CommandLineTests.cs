using CommandLine = global::Jint.Browser.Tool.CommandLine;
using OptionKind = global::Jint.Browser.Tool.OptionKind;

namespace Jint.Tests.Browser.Tool;

/// <summary>
/// The hand-rolled parser, which is the one place a typo can become something the tool silently did.
/// </summary>
public sealed class CommandLineTests
{
    private static readonly Dictionary<string, OptionKind> Syntax = new(StringComparer.Ordinal)
    {
        ["flag"] = OptionKind.Flag,
        ["value"] = OptionKind.Value,
        ["many"] = OptionKind.Repeated,
    };

    [Test]
    public void AnOptionTakesItsValueEitherWay()
    {
        Parse("--value", "one").Value("value").Should().Be("one");
        Parse("--value=two").Value("value").Should().Be("two");
    }

    [Test]
    public void ARepeatedOptionKeepsEveryValueAndASingleOneKeepsTheLast()
    {
        Parse("--many", "a", "--many", "b").Values("many").Should().Equal("a", "b");
        Parse("--value", "a", "--value", "b").Value("value").Should().Be("b");
    }

    [Test]
    public void AFlagIsPresentOrAbsentAndNeverCarriesAValue()
    {
        Parse("--flag").Flag("flag").Should().BeTrue();
        Parse().Flag("flag").Should().BeFalse();

        Fail("--flag=1").Should().Contain("switch");
    }

    [Test]
    public void AnUnknownOptionIsAnErrorRatherThanAPositionalArgument()
        => Fail("https://example.com", "--main-contnet").Should().Contain("unknown option '--main-contnet'");

    [Test]
    public void AnOptionWithNoValueLeftIsAnError()
        => Fail("--value").Should().Contain("needs a value");

    [Test]
    public void EverythingAfterTwoDashesIsPositional()
    {
        var line = Parse("url", "--", "-1", "--value");

        line.Positional.Should().Equal("url", "-1", "--value");
        line.Value("value").Should().BeNull("'--value' after '--' is an argument, not an option");
    }

    private static CommandLine Parse(params string[] arguments)
    {
        CommandLine.TryParse(arguments, Syntax, out var line, out var error).Should().BeTrue(error);
        return line!;
    }

    private static string Fail(params string[] arguments)
    {
        CommandLine.TryParse(arguments, Syntax, out _, out var error).Should().BeFalse();
        return error!;
    }
}
