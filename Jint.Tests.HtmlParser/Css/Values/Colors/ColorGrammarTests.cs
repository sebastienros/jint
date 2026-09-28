using System.Security.Cryptography;
using System.Text;
using Jint.HtmlParser;
using Jint.HtmlParser.Css;
using Jint.HtmlParser.Css.Syntax;
using Jint.HtmlParser.Css.Values;
using Jint.HtmlParser.Css.Values.Colors;
using Jint.HtmlParser.Css.Values.Math;
using Jint.HtmlParser.Css.Values.Properties;
using Jint.HtmlParser.Css.Values.References;

namespace Jint.Tests.HtmlParser.Css.Values.Colors;

[TestFixture]
public sealed class ColorGrammarTests
{
    [TestCase("RED", "red")]
    [TestCase("r\\65 d", "red")]
    [TestCase("TRANSPARENT", "transparent")]
    [TestCase("currentColor", "currentcolor")]
    [TestCase("CanvasText", "canvastext")]
    [TestCase("WindowText", "windowtext")]
    [TestCase("#abc", "rgb(170, 187, 204)")]
    [TestCase("#1234", "rgba(17, 34, 51, 0.266667)")]
    [TestCase("#abcdef", "rgb(171, 205, 239)")]
    [TestCase("#ff00ff80", "rgba(255, 0, 255, 0.501961)")]
    [TestCase("#Ff00FfEd", "rgba(255, 0, 255, 0.929412)")]
    [TestCase("#00000000", "rgba(0, 0, 0, 0)")]
    [TestCase("#000f", "rgb(0, 0, 0)")]
    [TestCase("#\\61 bc", "rgb(170, 187, 204)")]
    [TestCase("rgb(1, 2, 3)", "rgb(1, 2, 3)")]
    [TestCase("rgba(1, 2, 3)", "rgb(1, 2, 3)")]
    [TestCase("rgb(1, 2, 3, 50%)", "rgba(1, 2, 3, 0.5)")]
    [TestCase("rgba(1 2 3)", "rgb(1, 2, 3)")]
    [TestCase("rgb(20% 51 40%)", "rgb(51, 51, 102)")]
    [TestCase("RGB(100%,0%,0%,.25)", "rgba(255, 0, 0, 0.25)")]
    [TestCase("rgb(257 -3 1.25 / 150%)", "rgb(255, 0, 1.25)")]
    [TestCase("rgb(0 0 0 / -10%)", "rgba(0, 0, 0, 0)")]
    [TestCase("rgb(0 0 0 / 0.0000005)", "rgba(0, 0, 0, 0.000001)")]
    [TestCase("rgb(0 0 0 / 12.3456789%)", "rgba(0, 0, 0, 0.123457)")]
    [TestCase("rgb(none 50% 255)", "color(srgb none 0.5 1)")]
    [TestCase("rgba(100 none 0 / none)", "color(srgb 0.392157 none 0 / none)")]
    [TestCase("rgb(none none none / none)", "color(srgb none none none / none)")]
    [TestCase("rgb(0 0 0 / none)", "color(srgb 0 0 0 / none)")]
    [TestCase("hsl(0, 100%, 50%)", "rgb(255, 0, 0)")]
    [TestCase("hsla(120, 100%, 50%, .5)", "rgba(0, 255, 0, 0.5)")]
    [TestCase("hsl(240 100 50)", "rgb(0, 0, 255)")]
    [TestCase("hsl(-540 100% 50%)", "rgb(0, 255, 255)")]
    [TestCase("hsl(.5turn 100% 50%)", "rgb(0, 255, 255)")]
    [TestCase("hsl(200grad 100% 50%)", "rgb(0, 255, 255)")]
    [TestCase("hsl(3.141592653589793rad 100% 50%)", "rgb(0, 255, 255)")]
    [TestCase("hsl(0 -100% 50%)", "rgb(127.5, 127.5, 127.5)")]
    [TestCase("hsl(0 200% 50%)", "rgb(255, 0, 0)")]
    [TestCase("hsl(30 1e200 1e200)", "rgb(0, 255, 255)")]
    [TestCase("hsl(30 1e308 1e308)", "rgb(0, 255, 255)")]
    [TestCase("hsl(30 calc(1e200) calc(1e200))", "rgb(0, 255, 255)")]
    [TestCase("hsl(30 calc(1e308) calc(1e308) / 50%)", "rgba(0, 255, 255, 0.5)")]
    [TestCase("hsl(150 1e308 1e308)", "rgb(255, 0, 255)")]
    [TestCase("hsl(270 1e308 1e308)", "rgb(255, 255, 0)")]
    [TestCase("hsl(30 1e308 -1e308)", "rgb(0, 0, 255)")]
    [TestCase("hsl(30 0 1e308)", "rgb(255, 255, 255)")]
    [TestCase("hsla(740deg none 50 / 25%)", "hsl(20 none 50% / 0.25)")]
    [TestCase("hsl(none -5% 50% / none)", "hsl(none 0% 50% / none)")]
    [TestCase("hsl(calc(infinity) none 50%)", "hsl(0 none 50%)")]
    [TestCase("hwb(740deg 20% 30% / 50%)", "rgba(178.5, 93.5, 51, 0.5)")]
    [TestCase("hwb(0 40 80)", "rgb(85, 85, 85)")]
    [TestCase("hwb(120 0% 0%)", "rgb(0, 255, 0)")]
    [TestCase("hwb(0 1e999 1e999)", "rgb(127.5, 127.5, 127.5)")]
    [TestCase("rgb(1e999 -1e999 1e-999)", "rgb(255, 0, 0)")]
    [TestCase("hwb(0 200% 100%)", "rgb(170, 170, 170)")]
    [TestCase("hwb(380deg none 30% / none)", "hwb(20 none 30% / none)")]
    [TestCase("rgb(calc(64 * 2) 127 calc(400 - 145))", "rgb(128, 127, 255)")]
    [TestCase("rgb(calc(100 * 4) 127 calc(20 - 35))", "rgb(255, 127, 0)")]
    [TestCase("hsl(38.82 calc(2 * 50%) 50%)", "rgb(255, 164.985, 0)")]
    [TestCase("rgb(calc(infinity) calc(-infinity) calc(NaN) / calc(0 / 0))", "rgba(255, 0, 0, 0)")]
    [TestCase("rgb(calc(infinity * 1%) 0% 0% / calc(infinity))", "rgb(255, 0, 0)")]
    [TestCase("rgb(calc(20% * 2) 0 0 / calc(2 * 60%))", "rgb(102, 0, 0)")]
    [TestCase("rgb(abs(-20%) sign(-1%) calc(1in / 1px))", "rgb(51, 0, 96)")]
    [TestCase("rgb(min(20%, 30%) max(20%, 30%) clamp(20%, 50%, 40%))", "rgb(51, 76.5, 102)")]
    [TestCase("rgb(round(nearest, 21%, 10%) mod(21%, 10%) rem(21%, 10%))", "rgb(51, 2.55, 2.55)")]
    [TestCase("hsl(asin(1) 100% 50%)", "rgb(127.5, 255, 0)")]
    [TestCase("rgb(hypot(3%, 4%) sin(90deg) pow(2, 3))", "rgb(12.75, 1, 8)")]
    [TestCase("color(SRGB 10% .2 25% / 50%)", "color(srgb 0.1 0.2 0.25 / 0.5)")]
    [TestCase("color(srgb 50% -200 200 / 2)", "color(srgb 0.5 -200 200)")]
    [TestCase("color(srgb none none none / none)", "color(srgb none none none / none)")]
    [TestCase("color(srgb calc(1 + .5) calc(-.5) calc(50%) / calc(2 * 60%))", "color(srgb calc(1.5) calc(-0.5) calc(50%) / calc(120%))")]
    [TestCase("color(srgb calc(infinity) calc(-infinity) calc(NaN))", "color(srgb calc(infinity) calc(-infinity) calc(NaN))")]
    public void DeclaredSerializationRoundTrips(string source, string expected)
    {
        foreach (var name in new[] { "color", "background-color", "fill", "stroke" })
        {
            var parsed = CssPropertyParser.Parse(name, source);
            parsed.Status.Should().Be(CssPropertyStatus.Valid, source);
            parsed.Value.Kind.Should().Be(CssPropertyValueKind.Color);
            parsed.Value.Serialize().Should().Be(expected);
            var again = CssPropertyParser.Parse(name, expected);
            again.Status.Should().Be(CssPropertyStatus.Valid);
            again.Value.Serialize().Should().Be(expected);
        }
    }

    [TestCase("rgb(none, 0, 0)")]
    [TestCase("rgb(1, 20%, 3)")]
    [TestCase("rgb(calc(1), calc(20%), 3)")]
    [TestCase("rgb(1 2, 3)")]
    [TestCase("rgb(1, 2 3)")]
    [TestCase("rgb(1 2 3, .5)")]
    [TestCase("rgb(1,2,3 / .5)")]
    [TestCase("rgb(1,2,3,none)")]
    [TestCase("rgb(,1,2,3)")]
    [TestCase("rgb(1,2,,3)")]
    [TestCase("rgb(1,2,3,)")]
    [TestCase("rgb(1 2)")]
    [TestCase("rgb(1 2 3 4)")]
    [TestCase("rgb(1 2 3 /)")]
    [TestCase("rgb(1 2 3 / .5 .6)")]
    [TestCase("rgb(1px 2 3)")]
    [TestCase("rgb(1 2 3deg)")]
    [TestCase("rgb(calc(1 + 2%) 0 0)")]
    [TestCase("rgb(calc(1+2) 0 0)")]
    [TestCase("hsl(0, 100, 50%)")]
    [TestCase("hsl(0, 100%, 50)")]
    [TestCase("hsl(0%, 100%, 50%)")]
    [TestCase("hsl(0,none,50%)")]
    [TestCase("hsl(0 100% 50% / 1deg)")]
    [TestCase("hwb(0, 20%, 30%)")]
    [TestCase("hwb(0 1px 0)")]
    [TestCase("color()")]
    [TestCase("color(bogus 1 2 3)")]
    [TestCase("color(1 2 3)")]
    [TestCase("color(srgb 1,2,3)")]
    [TestCase("color(srgb 1 2)")]
    [TestCase("color(srgb 1 2 3 /)")]
    [TestCase("color(srgb 1 2 3deg)")]
    [TestCase("#ab")]
    [TestCase("#abcde")]
    [TestCase("#abcdefg")]
    [TestCase("#ggg")]
    [TestCase("#abc def")]
    [TestCase("1")]
    [TestCase("\"red\"")]
    [TestCase("red blue")]
    [TestCase("rebeccapurpl")]
    [TestCase("rgbb(1 2 3)")]
    public void CompletedGrammarRejectsInvalidCss(string source) =>
        CssPropertyParser.Parse("color", source).Status.Should().Be(CssPropertyStatus.Invalid);

    [TestCase("lab(50% 0 0)", "color:lab")]
    [TestCase("lch(50% 0 0)", "color:lch")]
    [TestCase("oklab(.5 0 0)", "color:oklab")]
    [TestCase("oklch(.5 0 0)", "color:oklch")]
    [TestCase("color(display-p3 1 0 0)", "color:color")]
    [TestCase("color-mix(in srgb, red, blue)", "color:color-mix")]
    [TestCase("light-dark(red, blue)", "color:light-dark")]
    [TestCase("contrast-color(red)", "color:contrast-color")]
    [TestCase("device-cmyk(0 1 1 0)", "color:device-cmyk")]
    [TestCase("rgb(from red r g b)", "color:relative-rgb")]
    [TestCase("color(from red srgb r g b)", "color:relative-color")]
    [TestCase("rgb(calc(sign(1em - 10px) * 50%) 0 0)", "color:channel-environment")]
    [TestCase("rgb(calc(round(line-width,1px)/1px) 0 0)", "color:channel-environment")]
    [TestCase("color(srgb calc(round(line-width,1px)/1px) 0 0)", "color:channel-environment")]
    [TestCase("rgb(0 0 0 / calc(round(line-width,1px)/1px))", "color:channel-environment")]
    public void IncompleteGrammarIsNamed(string source, string blocker)
    {
        var parsed = CssPropertyParser.Parse("color", source);
        parsed.Status.Should().Be(CssPropertyStatus.UnimplementedGrammar);
        parsed.Blocker.Should().Be(blocker);
        Assert.Throws<InvalidOperationException>(() => _ = parsed.Value);
    }

    [Test]
    public void PayloadDefaultsAndWrongKindsAreGuarded()
    {
        Assert.Throws<InvalidOperationException>(() => _ = default(CssColorParseResult).Value);
        Assert.Throws<InvalidOperationException>(() => _ = default(CssColorChannel).Resolved);
        var named = CssPropertyParser.Parse("color", "red").Value.Color;
        named.Kind.Should().Be(CssColorKind.Named);
        named.NamedRgb.Should().Be(0xff0000u);
        Assert.Throws<InvalidOperationException>(() => _ = named.Space);
        Assert.Throws<InvalidOperationException>(() => _ = named.GetChannel(0));
        var absolute = CssPropertyParser.Parse("color", "rgb(1 calc(2 * 3) none / 50%)").Value.Color;
        absolute.GetChannel(0).Numeric.Number.Spelling.Should().Be("1");
        absolute.GetChannel(1).Math.NodeCount.Should().Be(1);
        absolute.GetChannel(2).Kind.Should().Be(CssColorChannelKind.Missing);
        absolute.GetChannel(3).Numeric.Kind.Should().Be(CssNumericKind.Percentage);
        Assert.Throws<InvalidOperationException>(() => _ = absolute.Keyword);
        Assert.Throws<InvalidOperationException>(() => _ = absolute.GetChannel(0).Math);
    }

    [Test]
    public void CompleteNamedTableMatchesTheSpecCensusAndConstants()
    {
        CssColorKeywords.Named.Count.Should().Be(148);
        var table = string.Join("\n", CssColorKeywords.Named.OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => pair.Key + " " + pair.Value.ToString("x6", System.Globalization.CultureInfo.InvariantCulture)));
        // Independently extracted Color 4 §6.1 table, 2026-09-26. Detects any missing,
        // extra, misspelled or numerically incorrect row, including synonym constants.
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(table))).Should().Be("99935F50BEF1574B1A467344006246E1DB7929F6AB0B282D17CBAF74B37165F1");
        foreach (var pair in CssColorKeywords.Named)
        {
            var value = CssPropertyParser.Parse("color", pair.Key.ToUpperInvariant()).Value;
            value.Serialize().Should().Be(pair.Key);
            value.Color.NamedRgb.Should().Be(pair.Value);
        }
    }

    [Test]
    public void EverySystemColorRetainsContextualIdentity()
    {
        const string current = "accentcolor accentcolortext activetext buttonborder buttonface buttontext canvas canvastext field fieldtext graytext highlight highlighttext linktext mark marktext selecteditem selecteditemtext visitedtext";
        const string deprecated = "activeborder activecaption appworkspace background buttonhighlight buttonshadow captiontext inactiveborder inactivecaption inactivecaptiontext infobackground infotext menu menutext scrollbar threeddarkshadow threedface threedhighlight threedlightshadow threedshadow window windowframe windowtext";
        foreach (var (names, kind, count) in new[] { (current, CssColorKind.System, 19), (deprecated, CssColorKind.DeprecatedSystem, 23) })
        {
            var words = names.Split(' ');
            words.Distinct().Count().Should().Be(count);
            foreach (var word in words)
            {
                var value = CssPropertyParser.Parse("color", word.ToUpperInvariant()).Value;
                value.Color.Kind.Should().Be(kind);
                value.Serialize().Should().Be(word);
                Assert.Throws<InvalidOperationException>(() => _ = value.Color.NamedRgb);
            }
        }
    }

    [TestCase("var(--Color)")]
    [TestCase("rgb(var(--r) 0 0)")]
    [TestCase("hsl(0 0% env(lightness, 50%))")]
    public void ReferenceAnalysisPrecedesColorGrammar(string source)
    {
        var parsed = CssPropertyParser.Parse("color", source);
        parsed.Status.Should().Be(CssPropertyStatus.Deferred);
        parsed.Value.References.Count.Should().BeGreaterThan(0);
        parsed.Value.Serialize().Should().Be(source);
    }

    [Test]
    public void MetadataIsNarrowAndWideKeywordsRemainDistinct()
    {
        var color = CssPropertyRegistry.Completed["color"];
        color.InitialValue.Should().Be("canvastext");
        color.Inherited.Should().BeTrue();
        CssPropertyRegistry.Completed["background-color"].InitialValue.Should().Be("transparent");
        CssPropertyRegistry.Completed["background-color"].Inherited.Should().BeFalse();
        CssPropertyParser.Parse("color", "inherit").Value.Kind.Should().Be(CssPropertyValueKind.Keyword);
        CssPropertyParser.Parse("color", "red", CssDeclarationContext.FontFace).Status.Should().Be(CssPropertyStatus.UnsupportedProperty);
        CssPropertyParser.Parse("color", "red", CssDeclarationContext.Keyframe).Status.Should().Be(CssPropertyStatus.Valid);
        CssPropertyParser.Parse("border-color", "red").Status.Should().Be(CssPropertyStatus.UnimplementedGrammar);
    }

    [Test]
    public void OriginalSpansAndLimitsSurviveChildMath()
    {
        const string source = "width: 1px; color: rgb(calc((1)) 2 3 / 50%)";
        var declaration = new CssSyntaxParser(source, null, default).ParseDeclarationList()[1];
        var input = CssReferenceInput.FromComponents(source, declaration.Value, 3);
        var parsed = CssPropertyParser.Parse("color", input, CssDeclarationContext.Style, new CssValueWork(default));
        parsed.Value.Color.Span.Start.Should().Be(source.IndexOf("rgb", StringComparison.Ordinal));
        parsed.Value.Color.GetChannel(0).Span.Start.Should().Be(source.IndexOf("calc", StringComparison.Ordinal));
        var constrained = CssReferenceInput.FromComponents(source, declaration.Value, 2);
        Assert.Throws<ParseLimitException>(() => CssPropertyParser.Parse("color", constrained, CssDeclarationContext.Style,
            new CssValueWork(default)))!.Kind.Should().Be(ParseLimitKind.NestingDepth);
        const string small = "rgb(calc(1 + 2) 0 0)";
        CssPropertyParser.Parse("color", small, options: new CssParseOptions { Limits = new ParseLimits {
            MaxInputCharacters = small.Length, MaxTokenCharacters = 5, MaxNestingDepth = 2 } }).Status.Should().Be(CssPropertyStatus.Valid);
    }

    [Test]
    public void HostileValuesAndPostComponentCancellationStayBounded()
    {
        var deep = "rgb(" + string.Concat(Enumerable.Repeat("calc(", 128)) + "1" + new string(')', 128) + " 0 0)";
        CssPropertyParser.Parse("color", deep).Value.Serialize().Should().Be("rgb(1, 0, 0)");
        var flat = "rgb(calc(" + string.Join(" + ", Enumerable.Repeat("1%", 4096)) + ") 0 0)";
        CssPropertyParser.Parse("color", flat).Value.Serialize().Should().Be("rgb(255, 0, 0)");
        var input = CssReferenceInput.Parse("rgb(1." + new string('1', 20000) + " 2 3)", null, default);
        using var cancellation = new CancellationTokenSource();
        var checks = 0;
        var work = new CssValueWork(cancellation.Token, () => { if (++checks == 6) cancellation.Cancel(); });
        Assert.Throws<OperationCanceledException>(() => CssColorParser.Parse(input.Components, input.MaxNestingDepth, work));
        checks.Should().Be(6);
        Assert.Throws<OperationCanceledException>(() => CssColorSerializer.SerializeSpecified(
            CssPropertyParser.Parse("color", "rgb(1 2 3)").Value.Color, work));
    }

    [Test]
    public void EvaluationPollsItsOwnWorkAcrossAnAlreadyParsedArena()
    {
        var source = "hypot(" + string.Join(",", Enumerable.Range(1, 2048).Select(i => i + "%")) + ")";
        var component = MarkupParser.ParseCssComponentValues(source)[0];
        var math = CssMathParser.ParseMath(component, new CssMathContext(CssMathProduction.Percentage, CssMathPercentageMode.Raw),
            new CssValueWork(default)).Value;
        math.NodeCount.Should().BeGreaterThan(2048);
        using var cancellation = new CancellationTokenSource();
        var checks = 0;
        var work = new CssValueWork(cancellation.Token, () => { if (++checks == 3) cancellation.Cancel(); });
        Assert.Throws<OperationCanceledException>(() => CssColorMath.TryEvaluate(math, work, out _));
        checks.Should().Be(3);
    }
}
