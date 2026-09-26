#nullable enable
using System.Text;
using Jint.HtmlParser;
using Jint.HtmlParser.Html;

namespace Jint.Tests.HtmlParser.Html;

public class HtmlTextModeTests
{
    private static string Scan(HtmlTextMode mode, string? endTag, string source,
        int split = -1, int quota = 1000, ParseDiagnosticCollector? diagnostics = null)
    {
        var tokenizer = new HtmlTokenizer(new HtmlTokenizerContext(diagnostics: diagnostics));
        tokenizer.SetTextMode(mode, endTag);
        var tokens = new List<HtmlToken>();
        if (split < 0) tokenizer.AppendInput(source, true);
        else
        {
            tokenizer.AppendInput(source[..split]);
            Drain(tokenizer, quota, tokens, false);
            tokenizer.AppendInput(source[split..], true);
        }
        Drain(tokenizer, quota, tokens, true);
        return Signature(tokens);
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
        Assert.Fail("Tokenizer did not reach an input boundary.");
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
            if (token.Kind == HtmlTokenKind.EndTag)
            {
                output.Append("E:").Append(token.Name);
                foreach (var attr in token.Attributes) output.Append(' ').Append(attr.Name).Append('=').Append(attr.Value);
                if (token.SelfClosing) output.Append('/');
            }
            else if (token.Kind == HtmlTokenKind.StartTag) output.Append("S:").Append(token.Name);
            else if (token.Kind == HtmlTokenKind.Comment) output.Append("C:").Append(token.Data);
            else if (token.Kind == HtmlTokenKind.EndOfFile) output.Append("EOF");
            else Assert.Fail($"Unexpected token {token.Kind}");
            output.Append('|');
        }
        return output.ToString();
    }

    private static string ScanChunks(HtmlTextMode mode, string? endTag, string source, int chunkSize, int quota)
    {
        var tokenizer = new HtmlTokenizer(default);
        tokenizer.SetTextMode(mode, endTag);
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

    [TestCase((int) HtmlTextMode.RcData, "title", "A&amp;<b></TITLE>tail", "T:A&<b>|E:title|T:tail|EOF|")]
    [TestCase((int) HtmlTextMode.RcData, "title", "x</titlex>y</TiTlE>z", "T:x</titlex>y|E:title|T:z|EOF|")]
    [TestCase((int) HtmlTextMode.RcData, "title", "&amp", "T:&|EOF|")]
    [TestCase((int) HtmlTextMode.RcData, "title", "&notin</title>", "T:¬in|E:title|EOF|")]
    [TestCase((int) HtmlTextMode.RcData, "title", "a\0b&#13;", "T:a�b\r|EOF|")]
    [TestCase((int) HtmlTextMode.RawText, "style", "&amp;<b></STYLE>&amp;", "T:&amp;<b>|E:style|T:&|EOF|")]
    [TestCase((int) HtmlTextMode.RawText, "style", "x</stylex>y</style>", "T:x</stylex>y|E:style|EOF|")]
    [TestCase((int) HtmlTextMode.RawText, "style", "a\0b", "T:a�b|EOF|")]
    [TestCase((int) HtmlTextMode.ScriptData, "script", "&amp;<b></SCRIPT>", "T:&amp;<b>|E:script|EOF|")]
    [TestCase((int) HtmlTextMode.ScriptData, "script", "</scriptx>ok</script>", "T:</scriptx>ok|E:script|EOF|")]
    [TestCase((int) HtmlTextMode.ScriptData, "script", "a\0b", "T:a�b|EOF|")]
    [TestCase((int) HtmlTextMode.ScriptData, "script", "<!-- hi -->x</script>", "T:<!-- hi -->x|E:script|EOF|")]
    [TestCase((int) HtmlTextMode.ScriptData, "script", "<!--</script>", "T:<!--|E:script|EOF|")]
    [TestCase((int) HtmlTextMode.ScriptData, "script", "<!--<script>inner</script>--></script>", "T:<!--<script>inner</script>-->|E:script|EOF|")]
    [TestCase((int) HtmlTextMode.ScriptData, "script", "<!--<ScRiPt>inner</ScRiPt>--></ScRiPt>", "T:<!--<ScRiPt>inner</ScRiPt>-->|E:script|EOF|")]
    [TestCase((int) HtmlTextMode.ScriptData, "script", "<!--<script></scriptx></script>--></script>", "T:<!--<script></scriptx></script>-->|E:script|EOF|")]
    [TestCase((int) HtmlTextMode.ScriptData, "script", "<!--a\0b--></script>", "T:<!--a�b-->|E:script|EOF|")]
    [TestCase((int) HtmlTextMode.ScriptData, "script", "<!--<script>a\0b</script>--></script>", "T:<!--<script>a�b</script>-->|E:script|EOF|")]
    [TestCase((int) HtmlTextMode.PlainText, null, "&amp;<b></plaintext>\0", "T:&amp;<b></plaintext>�|EOF|")]
    public void LiteralTokensAndEverySplit(int modeValue, string? endTag, string source, string expected)
    {
        var mode = (HtmlTextMode) modeValue;
        Assert.That(Scan(mode, endTag, source), Is.EqualTo(expected));
        for (var split = 0; split <= source.Length; split++)
        {
            Assert.That(Scan(mode, endTag, source, split, 1), Is.EqualTo(expected), $"split {split}, quota 1");
            Assert.That(Scan(mode, endTag, source, split, 3), Is.EqualTo(expected), $"split {split}, quota 3");
        }
    }

    // html5lib-tests/tokenizer/test1.test at 224991ec10db04f056a89eed8b0bd8695fd2950e,
    // SHA-256 524fcfa4d561a14f0c4e72e0573549abe6341fd4dfb8e16bc2dcf59a608a7219,
    // cases 33–45 with initial state "Script data state". These expectations remain
    // unchanged by the later WHATWG processing-instruction rules.
    [TestCase("<test-->")]
    [TestCase("<!test-->")]
    [TestCase("<!-test-->")]
    [TestCase("<!--test-->")]
    [TestCase("<!-- < test -->")]
    [TestCase("<!-- </ test -->")]
    [TestCase("<!-- <test> -->")]
    [TestCase("<!-- </test> -->")]
    [TestCase("<!--<script>-</script>-->")]
    [TestCase("<!--<script>--</script>-->")]
    [TestCase("<!--<script>---</script>-->")]
    [TestCase("<!--<script> - </script>-->")]
    [TestCase("<!--<script> -- </script>-->")]
    public void PinnedHtml5libScriptDataSubset(string source)
    {
        Assert.That(Scan(HtmlTextMode.ScriptData, "script", source), Is.EqualTo("T:" + source + "|EOF|"));
    }

    // html5lib-tests/tokenizer/contentModelFlags.test at the same pinned revision,
    // SHA-256 77784a505a528950761cfb3c76617afade28b27c3be2a8c37dce3c3d8988391d.
    [TestCase("foo</xmp>", "T:foo|E:xmp|EOF|")]
    [TestCase("foo</xMp>", "T:foo|E:xmp|EOF|")]
    [TestCase("foo</xmp ", "T:foo|EOF|")]
    [TestCase("foo</xmp", "T:foo</xmp|EOF|")]
    [TestCase("foo</xmp/", "T:foo|EOF|")]
    [TestCase("foo</xmp<", "T:foo</xmp<|EOF|")]
    [TestCase("</foo>bar</xmp>", "T:</foo>bar|E:xmp|EOF|")]
    [TestCase("</xmp</xmp</xmp>", "T:</xmp</xmp|E:xmp|EOF|")]
    [TestCase("</foo>bar</xmpaar>", "T:</foo>bar</xmpaar>|EOF|")]
    [TestCase("foo</xmp></baz>", "T:foo|E:xmp|E:baz|EOF|")]
    public void PinnedHtml5libRcDataAndRawTextCases(string source, string expected)
    {
        foreach (var mode in new[] { HtmlTextMode.RcData, HtmlTextMode.RawText })
        {
            Assert.That(Scan(mode, "xmp", source), Is.EqualTo(expected), mode.ToString());
            for (var split = 0; split <= source.Length; split++)
                Assert.That(Scan(mode, "xmp", source, split, 1), Is.EqualTo(expected), $"{mode}, split {split}");
        }
    }

    [TestCase("<head>&body;", "T:<head>&body;|EOF|")]
    [TestCase("</plaintext>&body;", "T:</plaintext>&body;|EOF|")]
    public void PinnedHtml5libPlainTextCases(string source, string expected) =>
        Assert.That(Scan(HtmlTextMode.PlainText, null, source), Is.EqualTo(expected));

    [Test]
    public void PinnedHtml5libReferenceModeCases()
    {
        Assert.That(Scan(HtmlTextMode.RawText, "xmp", "&foo;"), Is.EqualTo("T:&foo;|EOF|"));
        Assert.That(Scan(HtmlTextMode.RcData, "textarea", "&lt;"), Is.EqualTo("T:<|EOF|"));
    }

    // html5lib-tests/tokenizer/escapeFlag.test at the same revision,
    // SHA-256 edbd2e070a14fc67f6bbc104e50207f0fe206a21891c260deea3d227b32c93c9.
    [TestCase("foo<!--</xmp>--></xmp>", "T:foo<!--|E:xmp|T:-->|E:xmp|EOF|")]
    [TestCase("foo<!-->baz</xmp>", "T:foo<!-->baz|E:xmp|EOF|")]
    [TestCase("foo<!--></xmp><!-->baz</xmp>", "T:foo<!-->|E:xmp|C:|T:baz|E:xmp|EOF|")]
    [TestCase("foo<!-- x --x>x-- >x--!>x--<></xmp>", "T:foo<!-- x --x>x-- >x--!>x--<>|E:xmp|EOF|")]
    public void PinnedHtml5libCommentTextModeCases(string source, string expected)
    {
        foreach (var mode in new[] { HtmlTextMode.RcData, HtmlTextMode.RawText })
            for (var split = -1; split <= source.Length; split++)
                Assert.That(Scan(mode, "xmp", source, split, 2), Is.EqualTo(expected), $"{mode}, split {split}");
    }

    [Test]
    public void PinnedHtml5libRcDataCommentedEntities()
    {
        const string source = " &amp; <!-- &amp; --> &amp; </xmp>";
        const string expected = "T: & <!-- & --> & |E:xmp|EOF|";
        Assert.That(Scan(HtmlTextMode.RcData, "xmp", source), Is.EqualTo(expected));
    }

    [Test]
    public void SetterRequiresBoundaryAndAppropriateName()
    {
        var tokenizer = new HtmlTokenizer(default);
        Assert.Throws<ArgumentNullException>(() => tokenizer.SetTextMode(HtmlTextMode.RcData, null));
        Assert.Throws<ArgumentException>(() => tokenizer.SetTextMode(HtmlTextMode.RawText, ""));
        Assert.Throws<ArgumentException>(() => tokenizer.SetTextMode(HtmlTextMode.Data, "title"));
        Assert.Throws<ArgumentOutOfRangeException>(() => tokenizer.SetTextMode((HtmlTextMode) 100, null));

        tokenizer.AppendInput("<script>");
        Assert.That(tokenizer.Read(100, default, out var start), Is.EqualTo(HtmlReadStatus.Token));
        Assert.That(start.Kind, Is.EqualTo(HtmlTokenKind.StartTag));
        tokenizer.SetTextMode(HtmlTextMode.ScriptData, "SCRIPT");
        Assert.Throws<InvalidOperationException>(() => tokenizer.SetTextMode(HtmlTextMode.RawText, "style"));
        tokenizer.AppendInput("x</script>", true);
        var rest = new List<HtmlToken>();
        Drain(tokenizer, 1, rest, true);
        Assert.That(Signature(rest), Is.EqualTo("T:x|E:script|EOF|"));
        Assert.Throws<InvalidOperationException>(() => tokenizer.SetTextMode(HtmlTextMode.PlainText, null));

        var partial = new HtmlTokenizer(default);
        partial.AppendInput("<script");
        Assert.That(partial.Read(100, default, out _), Is.EqualTo(HtmlReadStatus.NeedInput));
        Assert.Throws<InvalidOperationException>(() => partial.SetTextMode(HtmlTextMode.ScriptData, "script"));

        var partialScriptEndTag = new HtmlTokenizer(default);
        partialScriptEndTag.SetTextMode(HtmlTextMode.ScriptData, "script");
        partialScriptEndTag.AppendInput("</scr");
        Assert.That(partialScriptEndTag.Read(100, default, out _), Is.EqualTo(HtmlReadStatus.NeedInput));
        Assert.Throws<InvalidOperationException>(() => partialScriptEndTag.SetTextMode(HtmlTextMode.RawText, "style"));

        var pending = new HtmlTokenizer(default);
        pending.AppendInput("hello<script>");
        Assert.That(pending.Read(100, default, out var before), Is.EqualTo(HtmlReadStatus.Token));
        Assert.That(before.Kind, Is.EqualTo(HtmlTokenKind.Text));
        Assert.Throws<InvalidOperationException>(() => pending.SetTextMode(HtmlTextMode.ScriptData, "script"));
        Assert.That(pending.Read(100, default, out var tag), Is.EqualTo(HtmlReadStatus.Token));
        Assert.That(tag.Kind, Is.EqualTo(HtmlTokenKind.StartTag));
        pending.SetTextMode(HtmlTextMode.ScriptData, "script");
    }

    [TestCase((int) HtmlTextMode.RcData, "title", "x", false)]
    [TestCase((int) HtmlTextMode.RcData, "title", "<", false)]
    [TestCase((int) HtmlTextMode.RcData, "title", "</", false)]
    [TestCase((int) HtmlTextMode.RcData, "title", "</ti", false)]
    [TestCase((int) HtmlTextMode.RawText, "style", "x", false)]
    [TestCase((int) HtmlTextMode.RawText, "style", "<", false)]
    [TestCase((int) HtmlTextMode.RawText, "style", "</", false)]
    [TestCase((int) HtmlTextMode.RawText, "style", "</st", false)]
    [TestCase((int) HtmlTextMode.ScriptData, "script", "x", false)]
    [TestCase((int) HtmlTextMode.ScriptData, "script", "<", false)]
    [TestCase((int) HtmlTextMode.ScriptData, "script", "</", false)]
    [TestCase((int) HtmlTextMode.ScriptData, "script", "</scr", false)]
    [TestCase((int) HtmlTextMode.ScriptData, "script", "<!", false)]
    [TestCase((int) HtmlTextMode.ScriptData, "script", "<!-", false)]
    [TestCase((int) HtmlTextMode.ScriptData, "script", "<!--", true)]
    [TestCase((int) HtmlTextMode.ScriptData, "script", "<!--x", true)]
    [TestCase((int) HtmlTextMode.ScriptData, "script", "<!--x-", true)]
    [TestCase((int) HtmlTextMode.ScriptData, "script", "<!--<", true)]
    [TestCase((int) HtmlTextMode.ScriptData, "script", "<!--</", true)]
    [TestCase((int) HtmlTextMode.ScriptData, "script", "<!--</scr", true)]
    [TestCase((int) HtmlTextMode.ScriptData, "script", "<!--<s", true)]
    [TestCase((int) HtmlTextMode.ScriptData, "script", "<!--<script>", true)]
    [TestCase((int) HtmlTextMode.ScriptData, "script", "<!--<script>-", true)]
    [TestCase((int) HtmlTextMode.ScriptData, "script", "<!--<script>--", true)]
    [TestCase((int) HtmlTextMode.ScriptData, "script", "<!--<script><", true)]
    [TestCase((int) HtmlTextMode.ScriptData, "script", "<!--<script></scr", true)]
    [TestCase((int) HtmlTextMode.PlainText, null, "abc<&", false)]
    public void EofInEachTextStatePreservesTextAndDiagnostics(int modeValue, string? endTag,
        string source, bool scriptError)
    {
        var mode = (HtmlTextMode) modeValue;
        for (var split = -1; split <= source.Length; split++)
            foreach (var quota in new[] { 1, 3 })
            {
                var diagnostics = new ParseDiagnosticCollector();
                Assert.That(Scan(mode, endTag, source, split, quota, diagnostics),
                    Is.EqualTo("T:" + source + "|EOF|"), $"split {split}, quota {quota}");
                var expectedCodes = scriptError ? "html/eof-in-script-html-comment-like-text" : "";
                Assert.That(string.Join("|", diagnostics.Items.Select(x => x.Code)), Is.EqualTo(expectedCodes),
                    $"split {split}, quota {quota}");
            }
    }

    [Test]
    public void ManyChunkPartitionsPreserveScriptEscapeTransitions()
    {
        const string source = "a\r\n😀<!--<script>inner</script>--></script>tail";
        const string expected = "T:a\n😀<!--<script>inner</script>-->|E:script|T:tail|EOF|";
        foreach (var chunkSize in new[] { 1, 2, 3, 5, 11 })
            foreach (var quota in new[] { 1, 2, 7, 19 })
                Assert.That(ScanChunks(HtmlTextMode.ScriptData, "SCRIPT", source, chunkSize, quota),
                    Is.EqualTo(expected), $"chunk {chunkSize}, quota {quota}");
    }

    [Test]
    public void EndTagOffsetsAndAttributesUseOriginalSourceUnits()
    {
        var tokenizer = new HtmlTokenizer(default);
        tokenizer.SetTextMode(HtmlTextMode.ScriptData, "SCRIPT");
        tokenizer.AppendInput("a\r\n</ScRiPt data=x />z", true);
        var tokens = new List<HtmlToken>();
        Drain(tokenizer, 2, tokens, true);
        var end = tokens.Single(x => x.Kind == HtmlTokenKind.EndTag);
        Assert.That(end.Offset, Is.EqualTo(3));
        Assert.That(end.Name, Is.EqualTo("script"));
        Assert.That(end.Attributes.Single(), Is.EqualTo(new HtmlAttribute("data", "x")));
        Assert.That(end.EndTagHadAttributes, Is.True);
        Assert.That(end.EndTagHadSelfClosing, Is.True);
        Assert.That(Signature(tokens), Is.EqualTo("T:a\n|E:script data=x/|T:z|EOF|"));
    }

    [Test]
    public void TextModesRemainBoundedAndCancelable()
    {
        var bounded = new HtmlTokenizer(new HtmlTokenizerContext(new ParseLimits { MaxTokenCharacters = 9 }));
        bounded.SetTextMode(HtmlTextMode.ScriptData, "script");
        bounded.AppendInput("</scriptlong>", true);
        var failure = Assert.Throws<ParseLimitException>(() => bounded.Read(100, default, out _));
        Assert.That(failure!.Kind, Is.EqualTo(ParseLimitKind.TokenCharacters));
        Assert.Throws<InvalidOperationException>(() => bounded.Read(1, default, out _));

        var plain = new HtmlTokenizer(new HtmlTokenizerContext(new ParseLimits { MaxTokenCharacters = 2 }));
        plain.SetTextMode(HtmlTextMode.PlainText, null);
        plain.AppendInput(new string('x', 20_000), true);
        var tokens = new List<HtmlToken>();
        Drain(plain, 17, tokens, true);
        Assert.That(tokens.Where(x => x.Kind == HtmlTokenKind.Text).Max(x => x.Data.Length), Is.LessThanOrEqualTo(4096));
        Assert.That(tokens.Where(x => x.Kind == HtmlTokenKind.Text).Sum(x => x.Data.Length), Is.EqualTo(20_000));

        var canceled = new HtmlTokenizer(default);
        canceled.SetTextMode(HtmlTextMode.ScriptData, "script");
        canceled.AppendInput("<!--" + new string('x', 100_000), true);
        Assert.That(canceled.Read(100, default, out _), Is.EqualTo(HtmlReadStatus.Yielded));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Assert.Throws<OperationCanceledException>(() => canceled.Read(100, cancellation.Token, out _));
        Assert.Throws<InvalidOperationException>(() => canceled.SetTextMode(HtmlTextMode.Data, null));
    }

    [Test]
    public void LongPlainTextAndEscapedScriptAccumulateLinearly()
    {
        static long WorkFor(int length, HtmlTextMode mode)
        {
            var tokenizer = new HtmlTokenizer(default);
            tokenizer.SetTextMode(mode, mode == HtmlTextMode.ScriptData ? "script" : null);
            tokenizer.AppendInput((mode == HtmlTextMode.ScriptData ? "<!--" : "") + new string('x', length), true);
            var tokens = new List<HtmlToken>();
            Drain(tokenizer, 11, tokens, true);
            Assert.That(tokens.Where(x => x.Kind == HtmlTokenKind.Text).Sum(x => x.Data.Length),
                Is.EqualTo(length + (mode == HtmlTextMode.ScriptData ? 4 : 0)));
            return tokenizer.WorkCount;
        }
        foreach (var mode in new[] { HtmlTextMode.PlainText, HtmlTextMode.ScriptData })
            Assert.That(WorkFor(20_000, mode), Is.LessThan(WorkFor(10_000, mode) * 3));
    }

    [Test]
    public void NulReplacementAndDiagnosticsRemainStateSpecific()
    {
        foreach (var mode in new[] { HtmlTextMode.RcData, HtmlTextMode.RawText, HtmlTextMode.ScriptData, HtmlTextMode.PlainText })
        {
            var diagnostics = new ParseDiagnosticCollector();
            var name = mode == HtmlTextMode.PlainText ? null : "script";
            Assert.That(Scan(mode, name, "a\0b", diagnostics: diagnostics), Is.EqualTo("T:a�b|EOF|"));
            Assert.That(diagnostics.Items.Single().Code, Is.EqualTo("html/unexpected-null-character"));
            Assert.That(diagnostics.Items.Single().Offset, Is.EqualTo(1));
        }

        var doubleEscaped = new ParseDiagnosticCollector();
        Assert.That(Scan(HtmlTextMode.ScriptData, "script", "<!--<script>a\0b</script>-->", diagnostics: doubleEscaped),
            Is.EqualTo("T:<!--<script>a�b</script>-->|EOF|"));
        Assert.That(doubleEscaped.Items.Single().Code, Is.EqualTo("html/unexpected-null-character"));
    }
}
