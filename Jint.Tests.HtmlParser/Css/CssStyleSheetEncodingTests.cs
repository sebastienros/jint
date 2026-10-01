using System.Text;
using Jint.HtmlParser.Css.Syntax;

namespace Jint.Tests.HtmlParser.Css;

// CSS Syntax §3.2 "determine the fallback encoding".
[TestFixture]
public sealed class CssStyleSheetEncodingTests
{
    private static string? GetEncoding(string label) => label.Trim().ToLowerInvariant() switch
    {
        "utf-8" or "utf8" => "utf-8",
        "latin1" or "iso-8859-1" or "windows-1252" => "windows-1252",
        "utf-16" or "utf-16le" => "utf-16le",
        "utf-16be" => "utf-16be",
        "iso-8859-2" => "iso-8859-2",
        _ => null,
    };

    private static string Fallback(string css, string? protocol = null, string? environment = null)
        => CssStyleSheetEncoding.DetermineFallback(Encoding.Latin1.GetBytes(css), protocol, environment, GetEncoding);

    [Test]
    public void ProtocolLabelWinsUnlessItFails()
    {
        Fallback("@charset \"iso-8859-2\";", protocol: "latin1").Should().Be("windows-1252");
        Fallback("@charset \"iso-8859-2\";", protocol: "bogus").Should().Be("iso-8859-2");
    }

    [TestCase("@charset \"iso-8859-2\";p{}", "iso-8859-2")]
    [TestCase("@charset \"utf-16\";", "utf-8")]
    [TestCase("@charset \"UTF-16BE\";", "utf-8")]
    [TestCase("@charset \"bogus\";", "env")]
    [TestCase("@charset 'iso-8859-2';", "env")]
    [TestCase("@charset  \"iso-8859-2\";", "env")]
    [TestCase("@CHARSET \"iso-8859-2\";", "env")]
    [TestCase("@charset \"iso-8859-2\" ;", "env")]
    [TestCase("/**/@charset \"iso-8859-2\";", "env")]
    [TestCase("@charset \"iso-8859-2\"", "env")]
    public void DeclarationIsMatchedByteForByte(string css, string expected)
        => Fallback(css, environment: "env").Should().Be(expected);

    [Test]
    public void EnvironmentThenUtf8()
    {
        Fallback("p{}", environment: "windows-1252").Should().Be("windows-1252");
        Fallback("p{}").Should().Be("utf-8");
    }

    [Test]
    public void DeclarationMustFitTheFirst1024Bytes()
    {
        var label = new string('a', 1024 - "@charset \"\";".Length);
        CssStyleSheetEncoding.TryReadDeclarationLabel(Encoding.ASCII.GetBytes("@charset \"" + label + "\";"), out var read)
            .Should().BeTrue();
        read.Should().Be(label);
        CssStyleSheetEncoding.TryReadDeclarationLabel(Encoding.ASCII.GetBytes("@charset \"" + label + "a\";"), out _)
            .Should().BeFalse();
    }

    [Test]
    public void NonAsciiLabelBytesAreRejected()
        => CssStyleSheetEncoding.TryReadDeclarationLabel([.. "@charset \""u8, 0xE9, .. "\";"u8], out _).Should().BeFalse();
}
