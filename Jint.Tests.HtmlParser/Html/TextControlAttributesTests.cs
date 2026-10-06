#nullable enable
using Jint.HtmlParser;

namespace Jint.Tests.HtmlParser.Html;

public class TextControlAttributesTests
{
    [TestCase("0", 0L)]
    [TestCase(" +0042tail", 42L)]
    [TestCase("\t\r\n\f 25 99", 25L)]
    [TestCase("-0suffix", 0L)]
    [TestCase("2147483647", 2147483647L)]
    [TestCase("2147483648", 2147483648L)]
    [TestCase("999999999999999999999999999", 2147483648L)]
    public void EffectiveLengthsUseHtmlIntegerPrefixAndSemanticSaturation(string value, long expected)
    {
        var element = Document.CreateHtml().CreateElement("textarea");
        element.SetAttribute("minlength", value);
        element.SetAttribute("maxlength", value);
        HtmlTextControlAttributes.GetMinimumAllowedLength(element, default).Should().Be(expected);
        HtmlTextControlAttributes.GetMaximumAllowedLength(element, default).Should().Be(expected);
    }

    [TestCase("")]
    [TestCase("   ")]
    [TestCase("+")]
    [TestCase("-1")]
    [TestCase("-0001")]
    [TestCase("+ 1")]
    [TestCase(".5")]
    [TestCase("\u00a01")]
    [TestCase("\u0661")]
    [TestCase("1e2")]
    public void InvalidLengthsAreAbsentExceptDigitPrefixes(string value)
    {
        var element = Document.CreateHtml().CreateElement("textarea");
        element.SetAttribute("minlength", value);
        var expected = value == "1e2" ? 1L : (long?) null;
        HtmlTextControlAttributes.GetMinimumAllowedLength(element, default).Should().Be(expected);
    }

    [Test]
    public void LengthsRequireExactHtmlControlAndApplicableInputType()
    {
        var html = Document.CreateHtml();
        var input = html.CreateElement("input");
        input.SetAttribute("minlength", "3");
        input.SetAttribute("maxlength", "5");
        HtmlTextControlAttributes.GetMinimumAllowedLength(input, default).Should().Be(3);
        HtmlTextControlAttributes.GetMaximumAllowedLength(input, default).Should().Be(5);
        input.SetAttribute("type", "number");
        HtmlTextControlAttributes.GetMinimumAllowedLength(input, default).Should().BeNull();
        input.SetAttribute("type", "email");
        HtmlTextControlAttributes.GetMaximumAllowedLength(input, default).Should().Be(5);
        input.RemoveAttribute("type");
        HtmlTextControlAttributes.GetMaximumAllowedLength(input, default).Should().Be(5);

        var xml = Document.CreateXml();
        var xmlInput = xml.CreateElementNS(Namespaces.Html, "input");
        xmlInput.SetAttribute("minlength", "7");
        xmlInput.SetAttribute("TYPE", "number");
        HtmlTextControlAttributes.GetMinimumAllowedLength(xmlInput, default).Should().Be(7);
        xmlInput.SetAttributeNS("urn:test", "x:type", "number");
        HtmlTextControlAttributes.GetMinimumAllowedLength(xmlInput, default).Should().Be(7);
        xmlInput.SetAttribute("type", "number");
        HtmlTextControlAttributes.GetMinimumAllowedLength(xmlInput, default).Should().BeNull();
        HtmlTextControlAttributes.GetMinimumAllowedLength(xml.CreateElementNS(null, "input"), default)
            .Should().BeNull();
        HtmlTextControlAttributes.GetMinimumAllowedLength(xml.CreateElementNS(Namespaces.Html, "Input"), default)
            .Should().BeNull();
        HtmlTextControlAttributes.GetMinimumAllowedLength(html.CreateElement("div"), default)
            .Should().BeNull();
        Assert.Throws<ArgumentNullException>(() => HtmlTextControlAttributes.GetMinimumAllowedLength(null!, default));
    }

    [Test]
    public void NamespacedLengthAttributesDoNotApply()
    {
        var element = Document.CreateXml().CreateElementNS(Namespaces.Html, "textarea");
        element.SetAttributeNS("urn:test", "x:minlength", "3");
        HtmlTextControlAttributes.GetMinimumAllowedLength(element, default).Should().BeNull();
        element.SetAttribute("minlength", "8");
        HtmlTextControlAttributes.GetMinimumAllowedLength(element, default).Should().Be(8);
    }

    [Test]
    public void TextAreaWrapAndColumnsUseEffectiveValues()
    {
        var element = Document.CreateHtml().CreateElement("textarea");
        HtmlTextControlAttributes.GetEffectiveTextAreaWrap(element, default).Should().Be(HtmlTextAreaWrapMode.Soft);
        HtmlTextControlAttributes.GetEffectiveTextAreaColumns(element, default).Should().Be(20);
        var softValue = new string('x', 21);
        ReferenceEquals(softValue, HtmlTextSanitizer.GetTextAreaSubmissionValue(softValue,
            HtmlTextControlAttributes.GetEffectiveTextAreaWrap(element, default),
            HtmlTextControlAttributes.GetEffectiveTextAreaColumns(element, default), default)).Should().BeTrue();
        element.SetAttribute("wrap", "HaRd");
        element.SetAttribute("cols", " +4px");
        HtmlTextControlAttributes.GetEffectiveTextAreaWrap(element, default).Should().Be(HtmlTextAreaWrapMode.Hard);
        HtmlTextControlAttributes.GetEffectiveTextAreaColumns(element, default).Should().Be(4);
        HtmlTextSanitizer.GetTextAreaSubmissionValue("abcde",
            HtmlTextControlAttributes.GetEffectiveTextAreaWrap(element, default),
            HtmlTextControlAttributes.GetEffectiveTextAreaColumns(element, default), default)
            .Should().Be("abcd\ne");
        element.SetAttribute("wrap", " hard ");
        element.SetAttribute("cols", "0");
        HtmlTextControlAttributes.GetEffectiveTextAreaWrap(element, default).Should().Be(HtmlTextAreaWrapMode.Soft);
        HtmlTextControlAttributes.GetEffectiveTextAreaColumns(element, default).Should().Be(20);
        element.SetAttribute("cols", "-2");
        HtmlTextControlAttributes.GetEffectiveTextAreaColumns(element, default).Should().Be(20);
        Assert.Throws<ArgumentException>(() => HtmlTextControlAttributes.GetEffectiveTextAreaColumns(
            Document.CreateHtml().CreateElement("input"), default));
    }

    [Test]
    public void HugeDigitStringsAndAttributeListsCanCancelDuringRealWork()
    {
        var textArea = Document.CreateHtml().CreateElement("textarea");
        textArea.SetAttribute("maxlength", new string('9', 4096));
        using var digitSource = new CancellationTokenSource();
        Assert.Throws<OperationCanceledException>(() => HtmlTextControlAttributes.GetMaximumAllowedLength(
            textArea, _ => digitSource.Cancel(), digitSource.Token));
        HtmlTextControlAttributes.GetMaximumAllowedLength(textArea, default).Should().Be(2147483648L);

        var input = Document.CreateHtml().CreateElement("input");
        for (var i = 0; i < 320; i++)
        {
            input.SetAttribute($"data-{i}", "x");
        }

        input.SetAttribute("type", "text");
        input.SetAttribute("maxlength", "7");
        using var lookupSource = new CancellationTokenSource();
        var checkpoints = 0;
        Assert.Throws<OperationCanceledException>(() => HtmlTextControlAttributes.GetMaximumAllowedLength(
            input, _ => { checkpoints++; lookupSource.Cancel(); }, lookupSource.Token));
        checkpoints.Should().Be(1);
        HtmlTextControlAttributes.GetMaximumAllowedLength(input, default).Should().Be(7);

        var earlyType = Document.CreateHtml().CreateElement("input");
        earlyType.SetAttribute("type", "email");
        for (var i = 0; i < 320; i++)
        {
            earlyType.SetAttribute($"data-{i}", "x");
        }

        earlyType.SetAttribute("minlength", "8");
        using var targetSource = new CancellationTokenSource();
        Assert.Throws<OperationCanceledException>(() => HtmlTextControlAttributes.GetMinimumAllowedLength(
            earlyType, _ => targetSource.Cancel(), targetSource.Token));
        HtmlTextControlAttributes.GetMinimumAllowedLength(earlyType, default).Should().Be(8);
    }
}
