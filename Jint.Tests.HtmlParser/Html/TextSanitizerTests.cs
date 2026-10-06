#nullable enable
using Jint.HtmlParser;

namespace Jint.Tests.HtmlParser.Html;

public class TextSanitizerTests
{
    [TestCase(HtmlInputType.Text)]
    [TestCase(HtmlInputType.Search)]
    [TestCase(HtmlInputType.Tel)]
    [TestCase(HtmlInputType.Password)]
    public void PlainTextFamiliesRemoveOnlyCrAndLf(object expectedType)
    {
        var type = (HtmlInputType) expectedType;
        HtmlTextSanitizer.SanitizeInput(type, " \tA\r\nB\f\u00a0\ud800\udc00\n\udc00 ", false, default)
            .Should().Be(" \tAB\f\u00a0\ud800\udc00\udc00 ");
        var unchanged = " \t\u0085\u2028\ud800 ";
        ReferenceEquals(unchanged, HtmlTextSanitizer.SanitizeInput(type, unchanged, false, default))
            .Should().BeTrue();
    }

    [Test]
    public void UrlAndSingleEmailTrimAsciiEdgesAfterRemovingNewlines()
    {
        const string value = " \t\r\n a\r b \u00a0 \n\f";
        HtmlTextSanitizer.SanitizeInput(HtmlInputType.Url, value, false, default)
            .Should().Be("a b \u00a0");
        HtmlTextSanitizer.SanitizeInput(HtmlInputType.Email, value, false, default)
            .Should().Be("a b \u00a0");
        HtmlTextSanitizer.SanitizeInput(HtmlInputType.Url, "\u00a0 x \u00a0", false, default)
            .Should().Be("\u00a0 x \u00a0");
        HtmlTextSanitizer.SanitizeInput(HtmlInputType.Email, " \r\n\t", false, default)
            .Should().BeEmpty();
    }

    [Test]
    public void MultipleEmailTrimsEachCommaTokenWithoutRemovingInteriorNewlines()
    {
        HtmlTextSanitizer.SanitizeInput(HtmlInputType.Email,
            " \ta\r\nb \t,\n c\nd \r,\r\n", true, default)
            .Should().Be("a\r\nb,c\nd,");
        HtmlTextSanitizer.SanitizeInput(HtmlInputType.Email, ", ,x,", true, default)
            .Should().Be(",,x,");
        HtmlTextSanitizer.SanitizeInput(HtmlInputType.Email, "", true, default)
            .Should().BeEmpty();
        var unchanged = "a\r\nb,c\nd,";
        ReferenceEquals(unchanged, HtmlTextSanitizer.SanitizeInput(HtmlInputType.Email,
            unchanged, true, default)).Should().BeTrue();
    }

    [Test]
    public void UnimplementedFamiliesDoNotAcquireTextSanitization()
    {
        foreach (var type in new[] { HtmlInputType.Number, HtmlInputType.Date, HtmlInputType.Color,
                     HtmlInputType.Range, HtmlInputType.File, HtmlInputType.Hidden })
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => HtmlTextSanitizer.SanitizeInput(
                type, "x", false, default));
        }
    }

    [Test]
    public void TextareaNormalizationPreservesRawLengthDistinctionAndMalformedUnicode()
    {
        HtmlTextSanitizer.NormalizeTextAreaValue("A\r\nB\rC\n\ud800\udc00\ud800", default)
            .Should().Be("A\nB\nC\n\ud800\udc00\ud800");
        var unchanged = "A\n\udc00\u2028";
        ReferenceEquals(unchanged, HtmlTextSanitizer.NormalizeTextAreaValue(unchanged, default))
            .Should().BeTrue();
    }

    [Test]
    public void HardWrapCountsCodePointsAndPreservesExistingLineBoundaries()
    {
        HtmlTextSanitizer.WrapTextAreaForSubmission("A\ud83d\ude00BC\nDE\n", 2, default)
            .Should().Be("A\ud83d\ude00\nBC\nDE\n");
        HtmlTextSanitizer.WrapTextAreaForSubmission("A\ud800BC", 2, default)
            .Should().Be("A\ud800\nBC");
        HtmlTextSanitizer.WrapTextAreaForSubmission("\nAB\nCD", 2, default)
            .Should().Be("\nAB\nCD");
        HtmlTextSanitizer.WrapTextAreaForSubmission("abc", 1, default)
            .Should().Be("a\nb\nc");
        var unchanged = "AB\nCD";
        ReferenceEquals(unchanged, HtmlTextSanitizer.WrapTextAreaForSubmission(unchanged, 2, default))
            .Should().BeTrue();
        Assert.Throws<ArgumentOutOfRangeException>(() => HtmlTextSanitizer.WrapTextAreaForSubmission(
            "x", 0, default));
    }

    [Test]
    public void CancellationOccursInsideScansAndBeforeReturningUnchangedStrings()
    {
        using var source = new CancellationTokenSource();
        var checkpoints = 0;
        Assert.Throws<OperationCanceledException>(() => HtmlTextSanitizer.SanitizeInput(
            HtmlInputType.Text, new string('x', 1024), false,
            _ => { checkpoints++; source.Cancel(); }, source.Token));
        checkpoints.Should().Be(1);

        using var normalizationSource = new CancellationTokenSource();
        Assert.Throws<OperationCanceledException>(() => HtmlTextSanitizer.NormalizeTextAreaValue(
            new string('x', 1024), _ => normalizationSource.Cancel(), normalizationSource.Token));

        using var wrapSource = new CancellationTokenSource();
        Assert.Throws<OperationCanceledException>(() => HtmlTextSanitizer.WrapTextAreaForSubmission(
            new string('x', 1024), 2000, _ => wrapSource.Cancel(), wrapSource.Token));

        using var emailSource = new CancellationTokenSource();
        Assert.Throws<OperationCanceledException>(() => HtmlTextSanitizer.SanitizeInput(
            HtmlInputType.Email, new string('x', 1024), true,
            _ => emailSource.Cancel(), emailSource.Token));

        using var commasSource = new CancellationTokenSource();
        Assert.Throws<OperationCanceledException>(() => HtmlTextSanitizer.SanitizeInput(
            HtmlInputType.Email, new string(',', 1024), true,
            _ => commasSource.Cancel(), commasSource.Token));
    }

    [Test]
    public void CancellationAlsoOccursInCopyPasses()
    {
        using var source = new CancellationTokenSource();
        Assert.Throws<OperationCanceledException>(() => HtmlTextSanitizer.NormalizeTextAreaValue(
            new string('x', 255) + "\r" + new string('y', 600),
            step => { if (step == 1024) source.Cancel(); }, source.Token));

        using var wrapSource = new CancellationTokenSource();
        Assert.Throws<OperationCanceledException>(() => HtmlTextSanitizer.WrapTextAreaForSubmission(
            new string('x', 800), 2,
            step => { if (step == 1280) wrapSource.Cancel(); }, wrapSource.Token));
    }
}
