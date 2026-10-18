#if NET8_0_OR_GREATER
#nullable enable

using Jint.WebApi.Encoding;

namespace Jint.Tests.Runtime.WebApi;

/// <summary>
/// HTML's "prescan a byte stream to determine its encoding",
/// https://html.spec.whatwg.org/multipage/parsing.html#prescan-a-byte-stream-to-determine-its-encoding — the
/// engine-free helper the <c>XMLHttpRequest</c> document response decodes an HTML document with when the
/// response names no usable charset.
/// </summary>
/// <remarks>
/// Every input here is ASCII markup read one byte per character, which is how the algorithm reads it: the
/// scan is defined over bytes, and only ASCII bytes can contribute to what it finds.
/// </remarks>
public class HtmlEncodingPrescanTests
{
    // ---- the charset attribute

    [TestCase("<meta charset=\"windows-1252\">", "windows-1252")]
    [TestCase("<meta charset='iso-8859-2'>", "iso-8859-2")]
    [TestCase("<meta charset=koi8-r>", "koi8-r")]
    [TestCase("<META CharSet=\"Windows-1252\">", "windows-1252")]
    [TestCase("<meta charset = \"windows-1252\" >", "windows-1252")]
    [TestCase("<meta/charset=\"windows-1252\">", "windows-1252")]
    [TestCase("<meta\ncharset=\"windows-1252\">", "windows-1252")]
    [TestCase("<meta charset=\" windows-1252 \">", "windows-1252")]
    [TestCase("<meta name=viewport content=\"width=device-width\" charset=\"windows-1252\">", "windows-1252")]
    public void FindsTheCharsetAttribute(string markup, string expected) => Prescan(markup).Should().Be(expected);

    // ---- the http-equiv / content pragma

    [TestCase("<meta http-equiv=\"Content-Type\" content=\"text/html; charset=windows-1251\">", "windows-1251")]
    [TestCase("<meta content=\"text/html; charset=windows-1251\" http-equiv=\"content-type\">", "windows-1251")]
    [TestCase("<meta http-equiv=content-type content=\"text/html;charset='windows-1251'\">", "windows-1251")]
    [TestCase("<meta http-equiv=content-type content='text/html; CHARSET = windows-1251 ; x=y'>", "windows-1251")]
    [TestCase("<meta http-equiv=content-type content=\"charset; charset=windows-1251\">", "windows-1251")]
    public void FindsThePragmaForm(string markup, string expected) => Prescan(markup).Should().Be(expected);

    /// <summary>
    /// A <c>content</c> charset needs the pragma: without <c>http-equiv="content-type"</c> in the same element
    /// it is no declaration at all, and an unmatched quote in it is nothing either.
    /// </summary>
    [TestCase("<meta content=\"text/html; charset=windows-1251\">")]
    [TestCase("<meta http-equiv=\"refresh\" content=\"0; charset=windows-1251\">")]
    [TestCase("<meta name=\"description\" content=\"charset=windows-1251\">")]
    [TestCase("<meta http-equiv=content-type content=\"text/html; charset='windows-1251\">")]
    public void APragmaThatIsNotThereFindsNothing(string markup) => Prescan(markup).Should().BeNull();

    /// <summary>
    /// The first of each attribute counts and later repeats are ignored; a <c>charset</c> attribute replaces a
    /// charset a <c>content</c> attribute found, while a <c>content</c> attribute never replaces one.
    /// </summary>
    [TestCase("<meta charset=\"iso-8859-2\" charset=\"windows-1252\">", "iso-8859-2")]
    [TestCase("<meta content=\"text/html; charset=windows-1251\" charset=\"iso-8859-2\" http-equiv=\"content-type\">", "iso-8859-2")]
    [TestCase("<meta charset=\"iso-8859-2\" content=\"text/html; charset=windows-1251\" http-equiv=\"content-type\">", "iso-8859-2")]
    public void AttributesAreTakenInTheStandardsOrder(string markup, string expected) => Prescan(markup).Should().Be(expected);

    // ---- what the label resolves to

    /// <summary>
    /// A declaration of UTF-16 means UTF-8 — markup that can declare its encoding in ASCII bytes is not UTF-16
    /// — and <c>x-user-defined</c> means <c>windows-1252</c>.
    /// </summary>
    [TestCase("<meta charset=\"utf-16\">", "utf-8")]
    [TestCase("<meta charset=\"utf-16be\">", "utf-8")]
    [TestCase("<meta http-equiv=content-type content=\"text/html; charset=utf-16le\">", "utf-8")]
    [TestCase("<meta charset=\"x-user-defined\">", "windows-1252")]
    [TestCase("<meta charset=\"latin1\">", "windows-1252")]
    public void TheLabelIsResolvedAndMapped(string markup, string expected) => Prescan(markup).Should().Be(expected);

    /// <summary>
    /// A label that names no encoding is passed over and the scan goes on, so a later declaration still counts.
    /// </summary>
    [TestCase("<meta charset=\"bogus\"><meta charset=\"iso-8859-2\">", "iso-8859-2")]
    [TestCase("<meta http-equiv=content-type content=\"text/html; charset=bogus\"><meta charset=\"iso-8859-2\">", "iso-8859-2")]
    public void AnUnknownLabelIsIgnored(string markup, string expected) => Prescan(markup).Should().Be(expected);

    [TestCase("<meta charset=\"bogus\">")]
    [TestCase("<meta charset=\"\">")]
    public void AnUnknownLabelAloneFindsNothing(string markup) => Prescan(markup).Should().BeNull();

    // ---- what the scan skips

    /// <summary>
    /// Comments, other tags with their attributes, and <c>&lt;!</c>, <c>&lt;/</c> and <c>&lt;?</c>
    /// constructs are stepped over whole, so nothing inside them is taken for a declaration.
    /// </summary>
    [TestCase("<!-- <meta charset=\"windows-1252\"> --><meta charset=\"iso-8859-2\">", "iso-8859-2")]
    [TestCase("<!--><meta charset=\"iso-8859-2\">", "iso-8859-2")]
    [TestCase("<!-- -- > --><meta charset=\"iso-8859-2\">", "iso-8859-2")]
    [TestCase("<p title=\"<meta charset=windows-1252>\"><meta charset=\"iso-8859-2\">", "iso-8859-2")]
    [TestCase("<!DOCTYPE html><html lang=\"en\"><head><title>x</title><meta charset=\"iso-8859-2\">", "iso-8859-2")]
    [TestCase("</p><?php echo 1 ?><meta charset=\"iso-8859-2\">", "iso-8859-2")]
    [TestCase("<metadata charset=\"windows-1252\"><meta charset=\"iso-8859-2\">", "iso-8859-2")]
    public void SkipsWhatIsNotADeclaration(string markup, string expected) => Prescan(markup).Should().Be(expected);

    [TestCase("")]
    [TestCase("<p>no declaration</p>")]
    [TestCase("<!-- <meta charset=\"windows-1252\"> -->")]
    [TestCase("<!-- <meta charset=\"windows-1252\">")]
    public void FindsNothingWhereThereIsNoDeclaration(string markup) => Prescan(markup).Should().BeNull();

    /// <summary>
    /// Running out of bytes anywhere aborts the scan — so a declaration the input ends inside is not found.
    /// </summary>
    [TestCase("<meta charset=windows-1252")]
    [TestCase("<meta charset=\"windows-1252\"")]
    public void ADeclarationTheInputEndsInsideIsNotFound(string markup) => Prescan(markup).Should().BeNull();

    // ---- the end condition

    /// <summary>
    /// Only the first 1024 bytes are read: a declaration that ends within them counts, one they cut short or
    /// that starts after them does not.
    /// </summary>
    [Test]
    public void ReadsTheFirst1024BytesAndNoMore()
    {
        const string Declaration = "<meta charset=koi8-r>";

        Prescan(new string(' ', HtmlEncodingPrescan.EndCondition - Declaration.Length) + Declaration).Should().Be("koi8-r");
        Prescan(new string(' ', HtmlEncodingPrescan.EndCondition - Declaration.Length + 1) + Declaration).Should().BeNull();
        Prescan(new string(' ', HtmlEncodingPrescan.EndCondition) + Declaration).Should().BeNull();
    }

    // ---- the XML declarations

    /// <summary>
    /// "&lt;?x" in UTF-16 is UTF-16, in either endianness — the one answer the algorithm does not map to UTF-8.
    /// </summary>
    [Test]
    public void AUtf16XmlDeclarationIsUtf16()
    {
        Prescan([0x3C, 0x00, 0x3F, 0x00, 0x78, 0x00, 0x6D, 0x00]).Should().Be("utf-16le");
        Prescan([0x00, 0x3C, 0x00, 0x3F, 0x00, 0x78, 0x00, 0x6D]).Should().Be("utf-16be");
    }

    /// <summary>
    /// An aborted scan answers with "get an XML encoding": the <c>encoding</c> of an XML declaration at the very
    /// start, where UTF-16 means UTF-8 again.
    /// </summary>
    [TestCase("<?xml version=\"1.0\" encoding=\"windows-1252\"?><p>", "windows-1252")]
    [TestCase("<?xml version='1.0' encoding = 'iso-8859-2'?>", "iso-8859-2")]
    [TestCase("<?xml version=\"1.0\" encoding=\"utf-16\"?>", "utf-8")]
    public void AnXmlDeclarationIsTheFallback(string markup, string expected) => Prescan(markup).Should().Be(expected);

    [TestCase("<?xml version=\"1.0\"?>")]
    [TestCase(" <?xml version=\"1.0\" encoding=\"windows-1252\"?>")]
    [TestCase("<?xml version=\"1.0\" encoding=\"windows 1252\"?>")]
    [TestCase("<?xml version=\"1.0\" encoding=windows-1252?>")]
    public void AnXmlDeclarationWithoutAUsableEncodingFindsNothing(string markup) => Prescan(markup).Should().BeNull();

    private static string? Prescan(string markup) => Prescan(System.Text.Encoding.Latin1.GetBytes(markup));

    private static string? Prescan(byte[] bytes)
        => HtmlEncodingPrescan.TryPrescan(bytes, out var encoding) ? encoding.LowercasedName : null;
}
#endif
