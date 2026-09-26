#nullable enable
using Jint.HtmlParser;

namespace Jint.Tests.HtmlParser.Html;

public class InputTypeTests
{
    [TestCase(HtmlInputType.Hidden, "hidden", HtmlInputValueMode.Default, "", false)]
    [TestCase(HtmlInputType.Text, "text", HtmlInputValueMode.Value, "SRQLP", true)]
    [TestCase(HtmlInputType.Search, "search", HtmlInputValueMode.Value, "SRQLP", true)]
    [TestCase(HtmlInputType.Tel, "tel", HtmlInputValueMode.Value, "SRQLP", true)]
    [TestCase(HtmlInputType.Url, "url", HtmlInputValueMode.Value, "SRQLP", true)]
    [TestCase(HtmlInputType.Email, "email", HtmlInputValueMode.Value, "RQLP", true)]
    [TestCase(HtmlInputType.Password, "password", HtmlInputValueMode.Value, "SRQLP", true)]
    [TestCase(HtmlInputType.Date, "date", HtmlInputValueMode.Value, "RQ", true)]
    [TestCase(HtmlInputType.Month, "month", HtmlInputValueMode.Value, "RQ", true)]
    [TestCase(HtmlInputType.Week, "week", HtmlInputValueMode.Value, "RQ", true)]
    [TestCase(HtmlInputType.Time, "time", HtmlInputValueMode.Value, "RQ", true)]
    [TestCase(HtmlInputType.DateTimeLocal, "datetime-local", HtmlInputValueMode.Value, "RQ", true)]
    [TestCase(HtmlInputType.Number, "number", HtmlInputValueMode.Value, "RQP", true)]
    [TestCase(HtmlInputType.Range, "range", HtmlInputValueMode.Value, "", false)]
    [TestCase(HtmlInputType.Color, "color", HtmlInputValueMode.Value, "", true)]
    [TestCase(HtmlInputType.Checkbox, "checkbox", HtmlInputValueMode.DefaultOn, "Q", false)]
    [TestCase(HtmlInputType.Radio, "radio", HtmlInputValueMode.DefaultOn, "Q", false)]
    [TestCase(HtmlInputType.File, "file", HtmlInputValueMode.Filename, "Q", true)]
    [TestCase(HtmlInputType.Submit, "submit", HtmlInputValueMode.Default, "", false)]
    [TestCase(HtmlInputType.Image, "image", HtmlInputValueMode.Default, "", false)]
    [TestCase(HtmlInputType.Reset, "reset", HtmlInputValueMode.Default, "", false)]
    [TestCase(HtmlInputType.Button, "button", HtmlInputValueMode.Default, "", false)]
    public void EveryStateHasItsCanonicalKeywordModeAndApplicability(
        object expectedType, string keyword, object expectedMode, string flags, bool selectApplies)
    {
        var type = (HtmlInputType) expectedType;
        var mode = (HtmlInputValueMode) expectedMode;
        var info = HtmlInputTypes.Info(type);

        info.Keyword.Should().Be(keyword);
        info.ValueMode.Should().Be(mode);
        info.HasSelectionApi.Should().Be(flags.Contains('S'));
        info.ReadOnlyApplies.Should().Be(flags.Contains('R'));
        info.RequiredApplies.Should().Be(flags.Contains('Q'));
        info.LengthAndSizeApply.Should().Be(flags.Contains('L'));
        info.PlaceholderApplies.Should().Be(flags.Contains('P'));
        info.SelectApplies.Should().Be(selectApplies);
        HtmlInputTypes.Parse(keyword).Should().Be(type);
        HtmlInputTypes.Parse(keyword.ToUpperInvariant()).Should().Be(type);

        var input = Document.CreateHtml().CreateElement("input");
        input.SetAttribute("type", keyword.ToUpperInvariant());
        HtmlInputTypes.Get(input).Should().Be(type);
    }

    [Test]
    public void ExpectedRowsCoverTheWholeEnumAndInvalidEnumIsRejected()
    {
        Enum.GetValues<HtmlInputType>().Should().HaveCount(22);
        Assert.Throws<ArgumentOutOfRangeException>(() => HtmlInputTypes.Info((HtmlInputType) 22));
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("datetime")]
    [TestCase("datetime_local")]
    [TestCase("text ")]
    [TestCase(" text")]
    [TestCase("\tsearch")]
    [TestCase("file\n")]
    [TestCase("date time")]
    [TestCase("ſearch")]
    [TestCase("checKbox")]
    [TestCase("ｔｅｘｔ")]
    public void MissingEmptyAndInvalidTokensUseText(string? token)
    {
        HtmlInputTypes.Parse(token).Should().Be(HtmlInputType.Text);
    }

    [Test]
    public void MixedAsciiCaseMatchesWithoutNormalizingTheAttribute()
    {
        HtmlInputTypes.Parse("DaTeTiMe-LoCaL").Should().Be(HtmlInputType.DateTimeLocal);
        HtmlInputTypes.Parse("cHeCkBoX").Should().Be(HtmlInputType.Checkbox);

        var input = Document.CreateHtml().CreateElement("input");
        input.SetAttribute("type", "DaTeTiMe-LoCaL");
        HtmlInputTypes.Get(input).Should().Be(HtmlInputType.DateTimeLocal);
        input.GetAttribute("type").Should().Be("DaTeTiMe-LoCaL");
    }

    [Test]
    public void GetRequiresExactHtmlInputEvenInXmlDocuments()
    {
        var html = Document.CreateHtml();
        HtmlInputTypes.Get(html.CreateElement("input")).Should().Be(HtmlInputType.Text);
        Assert.Throws<ArgumentNullException>(() => HtmlInputTypes.Get(null!));
        Assert.Throws<ArgumentException>(() => HtmlInputTypes.Get(html.CreateElement("div")));
        Assert.Throws<ArgumentException>(() => HtmlInputTypes.Get(html.CreateElementNS(Namespaces.Svg, "input")));

        var xml = Document.CreateXml();
        Assert.Throws<ArgumentException>(() => HtmlInputTypes.Get(xml.CreateElementNS(null, "input")));
        Assert.Throws<ArgumentException>(() => HtmlInputTypes.Get(xml.CreateElementNS(Namespaces.Html, "Input")));

        var input = xml.CreateElementNS(Namespaces.Html, "input");
        HtmlInputTypes.Get(input).Should().Be(HtmlInputType.Text);
        input.SetAttribute("TYPE", "file");
        HtmlInputTypes.Get(input).Should().Be(HtmlInputType.Text);
        input.SetAttributeNS("urn:test", "x:type", "range");
        HtmlInputTypes.Get(input).Should().Be(HtmlInputType.Text);
        input.SetAttribute("type", "COLOR");
        HtmlInputTypes.Get(input).Should().Be(HtmlInputType.Color);
        input.RemoveAttribute("type");
        HtmlInputTypes.Get(input).Should().Be(HtmlInputType.Text);
    }
}
