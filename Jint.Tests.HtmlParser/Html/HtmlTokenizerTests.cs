#nullable enable
using System.Text;
using Jint.HtmlParser;
using Jint.HtmlParser.Html;

namespace Jint.Tests.HtmlParser.Html;

public class HtmlTokenizerTests
{
    private static List<HtmlToken> Scan(string source, int quota = 1000, int split = -1,
        HtmlTokenizerContext context = default)
    {
        var tokenizer = new HtmlTokenizer(context);
        var result = new List<HtmlToken>();
        if (split < 0) tokenizer.AppendInput(source, true);
        else
        {
            tokenizer.AppendInput(source[..split]);
            Drain(tokenizer, quota, result, false);
            tokenizer.AppendInput(source[split..], true);
        }
        Drain(tokenizer, quota, result, true);
        return result;
    }

    private static void Drain(HtmlTokenizer tokenizer, int quota, List<HtmlToken> tokens, bool final)
    {
        for (var i = 0; i < 100000; i++)
        {
            var status = tokenizer.Read(quota, default, out var token);
            if (status == HtmlReadStatus.Token) { tokens.Add(token); continue; }
            if (status == HtmlReadStatus.Yielded) continue;
            Assert.That(status, Is.EqualTo(final ? HtmlReadStatus.Complete : HtmlReadStatus.NeedInput));
            return;
        }
        Assert.Fail("Tokenizer made no terminal progress.");
    }

    private static string ScanPartitioned(string source, int chunkSize, int quota)
    {
        var tokenizer = new HtmlTokenizer(default);
        var tokens = new List<HtmlToken>();
        for (var i = 0; i < source.Length; i += chunkSize)
        {
            tokenizer.AppendInput(source.Substring(i, Math.Min(chunkSize, source.Length - i)));
            Drain(tokenizer, quota, tokens, false);
        }
        tokenizer.AppendInput(string.Empty, true);
        Drain(tokenizer, quota, tokens, true);
        return Signature(tokens);
    }

    private static string Signature(IEnumerable<HtmlToken> tokens)
    {
        var output = new StringBuilder();
        var text = new StringBuilder();
        void Flush()
        {
            if (text.Length == 0) return;
            output.Append("T:").Append(text).Append('|');
            text.Clear();
        }
        foreach (var token in tokens)
        {
            if (token.Kind == HtmlTokenKind.Text) { text.Append(token.Data); continue; }
            Flush();
            switch (token.Kind)
            {
                case HtmlTokenKind.StartTag:
                case HtmlTokenKind.EndTag:
                    output.Append(token.Kind == HtmlTokenKind.StartTag ? "S:" : "E:").Append(token.Name);
                    foreach (var attr in token.Attributes) output.Append(' ').Append(attr.Name).Append('=').Append(attr.Value);
                    if (token.SelfClosing) output.Append('/');
                    break;
                case HtmlTokenKind.Comment: output.Append("C:").Append(token.Data); break;
                case HtmlTokenKind.ProcessingInstruction: output.Append("P:").Append(token.Name).Append(':').Append(token.Data); break;
                case HtmlTokenKind.Doctype:
                    output.Append("D:").Append(token.Name ?? "(null)").Append(':')
                        .Append(token.PublicIdentifier ?? "(null)").Append(':')
                        .Append(token.SystemIdentifier ?? "(null)").Append(':').Append(token.ForceQuirks);
                    break;
                case HtmlTokenKind.EndOfFile: output.Append("EOF"); break;
            }
            output.Append('|');
        }
        return output.ToString();
    }

    [TestCase("plain", "T:plain|EOF|")]
    [TestCase("a\r\nb", "T:a\nb|EOF|")]
    [TestCase("a\0b", "T:a\0b|EOF|")]
    [TestCase("<DIV A=one b='two' a=three/>", "S:div a=one b=two|EOF|")]
    [TestCase("</X foo=bar/>", "E:x foo=bar/|EOF|")]
    [TestCase("<3", "T:<3|EOF|")]
    [TestCase("<!--hi-->", "C:hi|EOF|")]
    [TestCase("<!--x--!>", "C:x|EOF|")]
    [TestCase("<!bogus>", "C:bogus|EOF|")]
    [TestCase("<!DOCTYPE HTML PUBLIC '' 'sys'>", "D:html::sys:False|EOF|")]
    [TestCase("<!doctype>", "D:(null):(null):(null):True|EOF|")]
    [TestCase("&amp;&notin;&NotEqualTilde;", "T:&∉≂̸|EOF|")]
    [TestCase("&#x80;&#0;&#xD800;&#x110000;", "T:€���|EOF|")]
    [TestCase("<a x='&ampx' y='&amp;' z=&notin;>", "S:a x=&ampx y=& z=∉|EOF|")]
    [TestCase("<?Pi data?>", "P:Pi:data|EOF|")]
    [TestCase("<?Ab x?>", "P:Ab:x|EOF|")]
    [TestCase("<?a>", "P:a:|EOF|")]
    [TestCase("<?TARGET?x?>", "P:TARGET:?x|EOF|")]
    [TestCase("<?_target  data>", "P:_target:data|EOF|")]
    [TestCase("<?xml foo?>", "C:?xml foo?|EOF|")]
    [TestCase("<?xml?>", "C:?xml?|EOF|")]
    [TestCase("<?Xml-Stylesheet data?>", "C:?Xml-Stylesheet data?|EOF|")]
    [TestCase("<?bad:target?>", "C:?bad:target?|EOF|")]
    [TestCase("<?1bad?>", "C:?1bad?|EOF|")]
    [TestCase("<?", "EOF|")]
    [TestCase("<?pi?", "EOF|")]
    [TestCase("&unknown;", "T:&unknown;|EOF|")]
    public void LiteralTokensAndEverySplitMatch(string source, string expected)
    {
        Assert.That(Signature(Scan(source)), Is.EqualTo(expected));
        for (var split = 0; split <= source.Length; split++)
        {
            Assert.That(Signature(Scan(source, 1, split)), Is.EqualTo(expected), $"split {split}, quota 1");
            Assert.That(Signature(Scan(source, 3, split)), Is.EqualTo(expected), $"split {split}, quota 3");
        }
    }

    // Bounded Data-state subset of html5lib-tests/tokenizer/test1.test at
    // 224991ec10db04f056a89eed8b0bd8695fd2950e, file SHA-256
    // 524fcfa4d561a14f0c4e72e0573549abe6341fd4dfb8e16bc2dcf59a608a7219.
    // Source: https://github.com/html5lib/html5lib-tests/tree/224991ec10db04f056a89eed8b0bd8695fd2950e/tokenizer
    [TestCase("<!DOCTYPE HtMl", "D:html:(null):(null):True|EOF|")]
    [TestCase("<!DOC>", "C:DOC|EOF|")]
    [TestCase("</>", "EOF|")]
    [TestCase("<>", "T:<>|EOF|")]
    [TestCase("<h a='b'c='d'>", "S:h a=b c=d|EOF|")]
    [TestCase("<!--->", "C:|EOF|")]
    [TestCase("<!----->", "C:-|EOF|")]
    [TestCase("<!-- --comment -->", "C: --comment |EOF|")]
    [TestCase("<!--<!-->", "C:<!|EOF|")]
    [TestCase("<!-- <!--", "C: <!|EOF|")]
    [TestCase("<!-- <<!--test-->", "C: <<!--test|EOF|")]
    [TestCase("&", "T:&|EOF|")]
    [TestCase("&&", "T:&&|EOF|")]
    [TestCase("&f", "T:&f|EOF|")]
    [TestCase("&#", "T:&#|EOF|")]
    [TestCase("&#x", "T:&#x|EOF|")]
    [TestCase("I'm &notit", "T:I'm ¬it|EOF|")]
    [TestCase("I'm &notin", "T:I'm ¬in|EOF|")]
    [TestCase("I'm &no", "T:I'm &no|EOF|")]
    [TestCase("&#0036;", "T:$|EOF|")]
    [TestCase("&#x3f;", "T:?|EOF|")]
    [TestCase("<h a='&notx'>", "S:h a=&notx|EOF|")]
    [TestCase("<h a='&COPY'>", "S:h a=©|EOF|")]
    [TestCase("<s o=& t>", "S:s o=& t=|EOF|")]
    [TestCase("<a a=a&>foo", "S:a a=a&|T:foo|EOF|")]
    [TestCase("<a a=f<>", "S:a a=f<|EOF|")]
    public void PinnedHtml5libDataSubset(string source, string expected)
    {
        Assert.That(Signature(Scan(source)), Is.EqualTo(expected));
    }

    // Selected test2.test cases from the same html5lib revision; file SHA-256
    // f6450e77760cea823258de86f8e08894a1815671dbec0d74e7fbdab075596e37.
    [TestCase("<!DOCTYPEhtml>", "D:html:(null):(null):False|EOF|")]
    [TestCase("<!DOCTYPE html PUBLIC", "D:html:(null):(null):True|EOF|")]
    [TestCase("<!DOCTYPE html PUBLIC '", "D:html::(null):True|EOF|")]
    [TestCase("<!DOCTYPE html SYSTEM 'sys'>", "D:html:(null):sys:False|EOF|")]
    [TestCase("<!DOCTYPE html PUBLIC '' ''>", "D:html:::False|EOF|")]
    [TestCase("<!DOCTYPE html PUBLIC \">x", "D:html::(null):True|T:x|EOF|")]
    [TestCase("<!DOCTYPE html ", "D:html:(null):(null):True|EOF|")]
    [TestCase("&#xD869;&#xDED6;", "T:��|EOF|")]
    [TestCase("&;", "T:&;|EOF|")]
    [TestCase("<a<b>", "S:a<b|EOF|")]
    [TestCase("<h/a='b'>", "S:h a=b|EOF|")]
    [TestCase("</1>", "C:1|EOF|")]
    // The historical PI-as-comment rows (<?namespace>, <?foo-->) are intentionally
    // replaced by current HTML Standard §13.2.5.72–76 expectations below.
    [TestCase("foo < bar", "T:foo < bar|EOF|")]
    [TestCase("<!---x", "C:-x|EOF|")]
    [TestCase("a</>bc", "T:abc|EOF|")]
    [TestCase("a</><!--b-->c", "T:a|C:b|T:c|EOF|")]
    public void PinnedHtml5libMalformedSubset(string source, string expected)
    {
        Assert.That(Signature(Scan(source)), Is.EqualTo(expected));
    }

    [TestCase("<?namespace>", "P:namespace:|EOF|")]
    [TestCase("<?foo-->", "P:foo--:|EOF|")]
    [TestCase("<?pi data?>", "P:pi:data|EOF|")]
    [TestCase("<?pi a?b>", "P:pi:a?b|EOF|")]
    [TestCase("<?xml-stylesheet?>", "C:?xml-stylesheet?|EOF|")]
    public void CurrentProcessingInstructionRules(string source, string expected)
    {
        Assert.That(Signature(Scan(source)), Is.EqualTo(expected));
        for (var split = 0; split <= source.Length; split++)
            Assert.That(Signature(Scan(source, 1, split)), Is.EqualTo(expected), $"split {split}");
    }

    // Selected test4.test cases, SHA-256
    // c4967118aecbf8eb2ca34d5c5306f536614acca03e58610f75fbd9efa89fbb42.
    [TestCase("<z/0  <>", "S:z 0= <=|EOF|")]
    [TestCase("<z x=<>", "S:z x=<|EOF|")]
    [TestCase("<z z=z=z>", "S:z z=z=z|EOF|")]
    [TestCase("<z =>", "S:z ==|EOF|")]
    [TestCase("<z ===>", "S:z ===|EOF|")]
    [TestCase("<z z='&xlink_xmlns;'>bar<z>", "S:z z=&xlink_xmlns;|T:bar|S:z|EOF|")]
    [TestCase("<foo a=\"b\"c>", "S:foo a=b c=|EOF|")]
    [TestCase("<!doctype html \r", "D:html:(null):(null):True|EOF|")]
    [TestCase("<z/", "EOF|")]
    [TestCase("&#xZ", "T:&#xZ|EOF|")]
    [TestCase("&#x10000;", "T:𐀀|EOF|")]
    [TestCase("&#x110000;", "T:�|EOF|")]
    public void PinnedHtml5libAttributeAndNumericSubset(string source, string expected)
    {
        Assert.That(Signature(Scan(source)), Is.EqualTo(expected));
    }

    [Test]
    public void CDataDependsOnParsingContext()
    {
        const string source = "<![CDATA[x<y]]>";
        Assert.That(Signature(Scan(source)), Is.EqualTo("C:[CDATA[x<y]]|EOF|"));
        Assert.That(Signature(Scan(source, context: new HtmlTokenizerContext(allowCData: true))), Is.EqualTo("T:x<y|EOF|"));
    }

    [Test]
    public void ManyChunkPartitionsAndQuotasPreserveTokenStream()
    {
        const string source = "A\r\n😀&NotEqualTilde;<!DOCTYPE html PUBLIC '' 's'><!--x--><B a=&amp; b='v'>tail";
        var expected = Signature(Scan(source));
        foreach (var chunkSize in new[] { 1, 2, 3, 5, 11 })
            foreach (var quota in new[] { 1, 2, 7, 19 })
                Assert.That(ScanPartitioned(source, chunkSize, quota), Is.EqualTo(expected), $"chunk={chunkSize}, quota={quota}");
    }

    [Test]
    public void WorkAndBufferGrowthRemainLinear()
    {
        static long ScanWork(int length)
        {
            var tokenizer = new HtmlTokenizer(default);
            tokenizer.AppendInput("<!--" + new string('x', length) + "-->", true);
            var tokens = new List<HtmlToken>();
            Drain(tokenizer, 11, tokens, true);
            Assert.That(tokens[0].Data.Length, Is.EqualTo(length));
            return tokenizer.WorkCount;
        }
        var small = ScanWork(10_000);
        var large = ScanWork(20_000);
        Assert.That(large, Is.LessThan(small * 3));

        var text = Scan(new string('x', 20_000));
        Assert.That(text.Where(t => t.Kind == HtmlTokenKind.Text).Max(t => t.Data.Length), Is.LessThanOrEqualTo(4096));
        Assert.That(text.Where(t => t.Kind == HtmlTokenKind.Text).Sum(t => t.Data.Length), Is.EqualTo(20_000));
    }

    [Test]
    public void StatusAndTerminalRules()
    {
        var tokenizer = new HtmlTokenizer(default);
        Assert.That(tokenizer.Read(1, default, out _), Is.EqualTo(HtmlReadStatus.NeedInput));
        tokenizer.AppendInput("", false);
        Assert.That(tokenizer.Read(1, default, out _), Is.EqualTo(HtmlReadStatus.NeedInput));
        Assert.Throws<ArgumentOutOfRangeException>(() => tokenizer.Read(0, default, out _));
        tokenizer.AppendInput("", true);
        Assert.That(tokenizer.Read(1, default, out var eof), Is.EqualTo(HtmlReadStatus.Token));
        Assert.That(eof.Kind, Is.EqualTo(HtmlTokenKind.EndOfFile));
        Assert.That(tokenizer.Read(1, default, out _), Is.EqualTo(HtmlReadStatus.Complete));
        Assert.Throws<InvalidOperationException>(() => tokenizer.AppendInput("x"));
        Assert.Throws<ArgumentNullException>(() => new HtmlTokenizer(default).AppendInput(null!));

        var canceled = new HtmlTokenizer(default);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Assert.Throws<OperationCanceledException>(() => canceled.Read(1, cancellation.Token, out _));
        Assert.Throws<InvalidOperationException>(() => canceled.AppendInput("x"));
    }

    [Test]
    public void LimitsAreTerminalAndOffsetsCountRawUnits()
    {
        var tokenizer = new HtmlTokenizer(new HtmlTokenizerContext(new ParseLimits { MaxInputCharacters = 3 }));
        tokenizer.AppendInput("a\r");
        Assert.Throws<ParseLimitException>(() => tokenizer.AppendInput("\nb"));
        Assert.Throws<InvalidOperationException>(() => tokenizer.Read(1, default, out _));

        var diagnostics = new ParseDiagnosticCollector();
        var tokens = Scan("a\r\n<a x=1 X=2>", context: new HtmlTokenizerContext(diagnostics: diagnostics));
        Assert.That(tokens.Single(t => t.Kind == HtmlTokenKind.StartTag).Offset, Is.EqualTo(3));
        Assert.That(diagnostics.Items.Select(d => d.Code), Does.Contain("html/duplicate-attribute"));

        var bounded = new HtmlTokenizer(new HtmlTokenizerContext(new ParseLimits { MaxTokenCharacters = 8 }));
        bounded.AppendInput("<!--0123456789-->", true);
        var failure = Assert.Throws<ParseLimitException>(() => bounded.Read(100, default, out _));
        Assert.That(failure!.Kind, Is.EqualTo(ParseLimitKind.TokenCharacters));
        Assert.Throws<InvalidOperationException>(() => bounded.Read(1, default, out _));

        var reference = new HtmlTokenizer(new HtmlTokenizerContext(new ParseLimits { MaxTokenCharacters = 5 }));
        reference.AppendInput("&CounterClockwiseContourIntegral;", true);
        Assert.Throws<ParseLimitException>(() => reference.Read(100, default, out _));
    }

    [Test]
    public void CancellationDuringResumableLongScanTerminatesSession()
    {
        var tokenizer = new HtmlTokenizer(default);
        tokenizer.AppendInput("<!--" + new string('x', 100_000) + "-->", true);
        Assert.That(tokenizer.Read(100, default, out _), Is.EqualTo(HtmlReadStatus.Yielded));
        Assert.That(tokenizer.ConsumedInput, Is.GreaterThan(10));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Assert.Throws<OperationCanceledException>(() => tokenizer.Read(100, cancellation.Token, out _));
        Assert.Throws<InvalidOperationException>(() => tokenizer.AppendInput("x"));
    }

    [Test]
    public void EmittedTokensOwnTheirAttributeDataAndPreserveEndTagMetadata()
    {
        var tokenizer = new HtmlTokenizer(default);
        tokenizer.AppendInput("<a x='first'></a y=second /><b x=third>", true);
        Assert.That(tokenizer.Read(1000, default, out var first), Is.EqualTo(HtmlReadStatus.Token));
        Assert.That(first.Attributes[0], Is.EqualTo(new HtmlAttribute("x", "first")));
        Assert.That(tokenizer.Read(1000, default, out var end), Is.EqualTo(HtmlReadStatus.Token));
        Assert.That(end.Kind, Is.EqualTo(HtmlTokenKind.EndTag));
        Assert.That(end.EndTagHadAttributes, Is.True);
        Assert.That(end.EndTagHadSelfClosing, Is.True);
        Assert.That(tokenizer.Read(1000, default, out var next), Is.EqualTo(HtmlReadStatus.Token));
        Assert.That(next.Attributes[0], Is.EqualTo(new HtmlAttribute("x", "third")));
        Assert.That(first.Attributes[0], Is.EqualTo(new HtmlAttribute("x", "first")));
    }

    [Test]
    public void NewSessionClearsOnlyItsCollectorAndRecordsOriginalOffsets()
    {
        var collector = new ParseDiagnosticCollector();
        Scan("<a X=1 x=2>", context: new HtmlTokenizerContext(diagnostics: collector));
        Assert.That(collector.Items.Count, Is.GreaterThan(0));
        var newSession = new HtmlTokenizer(new HtmlTokenizerContext(diagnostics: collector));
        Assert.That(collector.Items, Is.Empty);
        newSession.AppendInput("a\r\n\0", true);
        Drain(newSession, 10, new List<HtmlToken>(), true);
        Assert.That(collector.Items.Single().Offset, Is.EqualTo(3));

        Scan("&zzzzzz; &#13;", context: new HtmlTokenizerContext(diagnostics: collector));
        Assert.That(collector.Items.Select(x => x.Code), Does.Contain("html/unknown-named-character-reference"));
        Assert.That(collector.Items.Select(x => x.Code), Does.Contain("html/control-character-reference"));
    }

    [Test]
    public void ProcessingInstructionLimitsCancellationAndDiagnostics()
    {
        var collector = new ParseDiagnosticCollector();
        Scan("<?xml?><?bad:target?><?pi?", context: new HtmlTokenizerContext(diagnostics: collector));
        Assert.That(collector.Items.Select(x => x.Code), Does.Contain("html/disallowed-processing-instruction-target"));
        Assert.That(collector.Items.Select(x => x.Code), Does.Contain("html/invalid-processing-instruction-target"));
        Assert.That(collector.Items.Select(x => x.Code), Does.Contain("html/eof-in-processing-instruction"));

        var bounded = new HtmlTokenizer(new HtmlTokenizerContext(new ParseLimits { MaxTokenCharacters = 12 }));
        bounded.AppendInput("<?pi " + new string('x', 100) + "?>", true);
        Assert.Throws<ParseLimitException>(() => bounded.Read(1000, default, out _));
        Assert.Throws<InvalidOperationException>(() => bounded.AppendInput("x"));

        var canceled = new HtmlTokenizer(default);
        canceled.AppendInput("<?pi " + new string('x', 100_000) + "?>", true);
        Assert.That(canceled.Read(10, default, out _), Is.EqualTo(HtmlReadStatus.Yielded));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Assert.Throws<OperationCanceledException>(() => canceled.Read(10, cancellation.Token, out _));
        Assert.Throws<InvalidOperationException>(() => canceled.Read(10, default, out _));
    }
}
