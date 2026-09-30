using Jint.HtmlParser.Css.Values;

namespace Jint.Tests.HtmlParser.Css.Values;

// CSS Font Loading's descriptor, src and font-shorthand reads:
// https://drafts.csswg.org/css-font-loading/#font-face-constructor and
// https://drafts.csswg.org/css-font-loading/#find-the-matching-font-faces.
public sealed class CssFontFaceValuesTests
{
    [TestCase("Style", "ITALIC", "italic")]
    [TestCase("Style", "oblique 10deg 20deg", "oblique 10deg 20deg")]
    [TestCase("Weight", "bold", "bold")]
    [TestCase("Weight", "100 400.50", "100 400.5")]
    [TestCase("Stretch", "50% 200%", "50% 200%")]
    [TestCase("Stretch", "Condensed", "condensed")]
    [TestCase("UnicodeRange", "u+0-7f, U+1e??", "U+0-7F, U+1E00-1EFF")]
    [TestCase("UnicodeRange", "U+263A", "U+263A")]
    [TestCase("FeatureSettings", "'liga' on, \"smcp\" 2, 'kern' off", "\"liga\", \"smcp\" 2, \"kern\" 0")]
    [TestCase("VariationSettings", "'wght' 400", "\"wght\" 400")]
    [TestCase("Display", "SWAP", "swap")]
    [TestCase("AscentOverride", "90%", "90%")]
    [TestCase("LineGapOverride", "normal", "normal")]
    [TestCase("SizeAdjust", "110%", "110%")]
    [TestCase("Variant", "Small-Caps", "small-caps")]
    public void SerializesWhatTheDescriptorGrammarAccepts(string descriptor, string source, string expected)
    {
        CssFontFaceValues.TryParseDescriptor(Enum.Parse<CssFontFaceDescriptor>(descriptor), source, new CssValueWork(default), out var serialization).Should().BeTrue();
        serialization.Should().Be(expected);
    }

    [TestCase("Style", "")]
    [TestCase("Style", "inherit")]
    [TestCase("Style", "oblique 10px")]
    [TestCase("Weight", "0")]
    [TestCase("Weight", "100 200 300")]
    [TestCase("Stretch", "-5%")]
    [TestCase("UnicodeRange", "U+110000")]
    [TestCase("UnicodeRange", "U+7F-0")]
    [TestCase("UnicodeRange", "0-7F")]
    [TestCase("FeatureSettings", "'toolong' 1")]
    [TestCase("FeatureSettings", "liga")]
    [TestCase("VariationSettings", "'wght'")]
    [TestCase("Display", "fast")]
    [TestCase("SizeAdjust", "1.1")]
    [TestCase("Variant", "initial")]
    public void RefusesWhatTheDescriptorGrammarRefuses(string descriptor, string source)
    {
        CssFontFaceValues.TryParseDescriptor(Enum.Parse<CssFontFaceDescriptor>(descriptor), source, new CssValueWork(default), out _).Should().BeFalse();
    }

    [Test]
    public void ReadsUrlAndLocalSources()
    {
        CssFontFaceValues.TryParseSource(
            "local(\"Open Sans\"), url(a.woff2) format(\"woff2\"), url('b.ttf') tech(variations), local(Foo Bar)",
            new CssValueWork(default),
            out var sources).Should().BeTrue();

        sources.Should().Equal(
            new CssFontSource(true, "Open Sans", null),
            new CssFontSource(false, "a.woff2", "woff2"),
            new CssFontSource(false, "b.ttf", null),
            new CssFontSource(true, "Foo Bar", null));
    }

    [TestCase("")]
    [TestCase("a.woff")]
    [TestCase("url(a.woff),")]
    [TestCase("url(a.woff) format()")]
    [TestCase("local()")]
    [TestCase("url(a.woff) url(b.woff)")]
    public void RefusesMalformedSources(string source)
    {
        CssFontFaceValues.TryParseSource(source, new CssValueWork(default), out _).Should().BeFalse();
    }

    [TestCase("16px Foo", new[] { "Foo" })]
    [TestCase("italic bold 12px/30px Georgia, serif", new[] { "Georgia", "serif" })]
    [TestCase("1em 'Open Sans', Foo  Bar", new[] { "Open Sans", "Foo Bar" })]
    [TestCase("caption", new string[0])]
    public void ReadsTheFamiliesOfAFontShorthand(string font, string[] expected)
    {
        CssFontFaceValues.TryParseFontFamilies(font, new CssValueWork(default), out var families).Should().BeTrue();
        families.Should().Equal(expected);
    }

    [TestCase("")]
    [TestCase("Foo")]
    [TestCase("16px")]
    [TestCase("inherit")]
    [TestCase("16px Foo,")]
    [TestCase("bold 12px/ Foo")]
    public void RefusesAFontShorthandWithoutASizeAndAFamily(string font)
    {
        CssFontFaceValues.TryParseFontFamilies(font, new CssValueWork(default), out _).Should().BeFalse();
    }
}
