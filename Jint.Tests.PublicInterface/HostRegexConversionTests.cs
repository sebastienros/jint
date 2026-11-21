#nullable enable

using System.Text.RegularExpressions;
using Jint.Native;
using Jint.Runtime;

namespace Jint.Tests.PublicInterface;

/// <summary>
/// Pins what a host-supplied <see cref="Regex"/> becomes in script, and what a <c>RegExp</c> becomes on the way
/// back out, under each <see cref="RegexConversionMode"/>.
/// </summary>
/// <remarks>
/// Script patterns run on Jint's own ECMAScript matcher. A <see cref="Regex"/> the host hands over is a .NET
/// pattern, so by default it keeps matching with .NET semantics exactly as it would in C#;
/// <see cref="RegexConversionMode.EcmaScript"/> is the opt-in that reads its pattern text as ECMAScript instead.
/// </remarks>
public class HostRegexConversionTests
{
    [Test]
    public void HostRegexKeepsDotNetSemanticsByDefault()
    {
        var engine = new Engine();

        // \A anchors in .NET, where ECMAScript (Annex B) would read it as a literal "A": the .NET reading wins.
        engine.SetValue("r", new Regex(@"\A(?<n>\d+)(?i:X)"));

        engine.Evaluate("r.source").AsString().Should().Be("?[native regex]");
        engine.Evaluate("r.exec('42x')[1]").AsString().Should().Be("42");
        engine.Evaluate("r.exec('42x').groups.n").AsString().Should().Be("42");
        engine.Evaluate("r.test('a42x')").AsBoolean().Should().BeFalse();
    }

    [Test]
    public void HostRegexRoundTripsAsItself()
    {
        var engine = new Engine();
        var regex = new Regex("a+");
        engine.SetValue("r", regex);

        engine.Evaluate("r").ToObject().Should().BeSameAs(regex);
    }

    [Test]
    public void AScriptRegExpReachesTheHostAsTheJsRegExp()
    {
        var engine = new Engine();

        // A pattern written in script has no .NET equivalent, so it is not converted to a Regex.
        var value = engine.Evaluate("/a(?<x>b)/gi");

        var converted = value.ToObject().Should().BeOfType<JsRegExp>().Which;
        converted.Source.Should().Be("a(?<x>b)");
        converted.Flags.Should().Be("gi");
    }

    [Test]
    public void EcmaScriptConversionCompilesThePatternTextWithMappedFlags()
    {
        var engine = new Engine(options => options.Interop.RegexConversion = RegexConversionMode.EcmaScript);

        engine.SetValue("r", new Regex("^a.c$", RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Multiline));

        engine.Evaluate("r.source").AsString().Should().Be("^a.c$");
        engine.Evaluate("r.flags").AsString().Should().Be("ims");
        engine.Evaluate("r.test('x\\nA\\nC')").AsBoolean().Should().BeTrue();

        // It is an ordinary RegExp now, so it is no longer the host's Regex on the way back out.
        engine.Evaluate("r").ToObject().Should().BeOfType<JsRegExp>();
    }

    [Test]
    public void EcmaScriptConversionRefusesAPatternThatIsNotEcmaScript()
    {
        var engine = new Engine(options => options.Interop.RegexConversion = RegexConversionMode.EcmaScript);

        // An inline option group is .NET syntax with no ECMAScript reading.
        var error = Invoking(() => engine.SetValue("r", new Regex("(?i)a"))).Should().Throw<JavaScriptException>().Which;
        error.Error.AsObject().Get("name").AsString().Should().Be("SyntaxError");
    }

    [Test]
    public void EcmaScriptConversionRefusesAnOptionWithoutAFlag()
    {
        var engine = new Engine(options => options.Interop.RegexConversion = RegexConversionMode.EcmaScript);

        var error = Invoking(() => engine.SetValue("r", new Regex("a", RegexOptions.ExplicitCapture))).Should().Throw<JavaScriptException>().Which;
        error.Message.Should().Contain("ExplicitCapture");
    }

    [Test]
    public void EcmaScriptConversionAcceptsTheEcmaScriptOption()
    {
        var engine = new Engine(options => options.Interop.RegexConversion = RegexConversionMode.EcmaScript);
        engine.SetValue("r", new Regex("^a\\d$", RegexOptions.ECMAScript | RegexOptions.IgnoreCase));

        engine.Evaluate("r.flags").AsString().Should().Be("i");
        engine.Evaluate("r.test('A1')").AsBoolean().Should().BeTrue();
    }

    [Test]
    public void EcmaScriptConversionKeepsTheRegexMatchTimeout()
    {
        // The engine allows ten seconds; the host's Regex says 100 ms, and that is what fires.
        var engine = new Engine(options => options.Interop.RegexConversion = RegexConversionMode.EcmaScript);
        engine.SetValue("r", new Regex("^(?:a|a)*b", RegexOptions.None, TimeSpan.FromMilliseconds(100)));

        var act = () => engine.Evaluate("r.test('a'.repeat(40) + '!b')");
        act.Should().Throw<RegexMatchTimeoutException>().Which.MatchTimeout.Should().Be(TimeSpan.FromMilliseconds(100));
    }
}
