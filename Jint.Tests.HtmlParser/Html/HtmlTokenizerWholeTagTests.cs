#nullable enable
using System.Text;
using Jint.HtmlParser;
using Jint.HtmlParser.Html;

namespace Jint.Tests.HtmlParser.Html;

// The whole-tag and text-reference paths take a tag or a terminated named reference in one scanner
// iteration when the work quota, the source slice and the token limit leave room for it, and claim to
// produce what the scalar states would and to charge their work. A tokenizer told to leave tags and
// references to the scalar states is the oracle for both claims at the same quota and chunking:
// tokens, offsets, script source locations, diagnostics and work must all agree. A quota of one leaves
// room for neither path, so it is an oracle too, but not for work, which differs between quotas by
// design: a yield flushes pending text and splits runs.
public class HtmlTokenizerWholeTagTests
{
    private static readonly string LongLiteralAfterReference = "<a b=\"&amp;" + new string('x', 5000) + "\">";

    private static readonly string[] Sources =
    [
        "<div>",
        "<div class=\"a b\" id='x' data-v=1 hidden>text</div>",
        "<DIV Class=\"A\" ID=x>",
        "<p title=\"A &quot;quote&quot;\">Description <em>0</em> with &nbsp; space.</p><a href=\"/items/0?a=1&amp;b=2\">Open</a>",
        "<a href=x&amp;y title=t>",
        "<a b c=d e>",
        "<a b = \"1\" c\t=\t'2'\f d\n=\ne>",
        "<input disabled/><br/><img src=a.png />",
        "<a b/>",
        "</div >",
        "</div\n>",
        "</div x>",
        "</div/>",
        "<a b=\"1\"c=\"2\">",
        "<a b=>",
        "<a b=\"1\" b=\"2\" B=3>",
        "<a =b>",
        "<a b\"c=d>",
        "<a b='x\"y' c=\"x'y\" d=`e>",
        "<a b=\"line\nbreak\" c='two\nlines\nhere'>",
        "<a\nb=\"1\"\n  c>\n<p\r\nd>",
        "<a b=\"x\r\ny\">",
        "<a b=\"x\0y\" c\0=d>",
        "<a b=\"unterminated",
        "<a b",
        "<a/",
        "<script type=\"text/javascript\" src=x.js>a<b</script>",
        "<script\nasync>\n</script>",
        "<textarea name=t>\n<b>x</b></textarea><title>a<b></title>",
        "<svg viewBox=\"0 0 1 1\"><foreignObject><div/></foreignObject><path d='M0 0'/></svg>",
        "<math><mi mathvariant=\"bold\">x</mi></math>",
        "<a aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa=1 averyveryverylongattributename=2>",
        "<averyveryverylongelementnameindeed x=1></averyveryverylongelementnameindeed>",
        "<a a1=1 a2=2 a3=3 a4=4 a5=5 a6=6 a7=7 a8=8 a9=9 a10=10 a11=11 a12=12 a13=13 a14=14 a15=15 a16=16 a17=17>",
        "<p>One\ntwo\n\nthree<br>\nfour</p>\r\n<pre>\nfive</pre>",
        "<!doctype html><html lang=en><head><meta charset=utf-8><title>T</title></head><body><!-- c --><p>x</p></body></html>",
        "<a href='&notit;' b='&not' c=\"&#x41;&#66;\" d='&unknown;'>",
        "<dív x=1><a ü=1></a>",
        "<a x:y=1 _z=2 .w=3 -v=4>",
        // Named references: terminated and complete ones batch in text and in attribute values.
        "Item 0 &amp; detail&nbsp;x &lt;&gt;&copy;&notin;&AMP;&Amp;&amp",
        "a &amp;b &amp;",
        "&amp;",
        "<p>&amp;</p><title>x &amp; y &notit; &nbsp</title><textarea>&lt;a&gt;&#65;</textarea>",
        "<a href=/x?a=1&amp;b=2 title=&quot;q&quot; c=&amp; d='&amp;' e=\"&amp;&amp;\" f=\"x&amp;\" g=\"&amp;x\">",
        "<a b=\"&amp;\" c=\"&amp;&#65;\" d=\"plain\">",
        "<a b=\"plain\" c=\"x&amp;y\" d=\"&unknown;\" e=\"z\">",
        "<a b=\"&amp;\" c=\"&notit;\">",
        "<a b=\"&amp;\" c=\"x&amp\">",
        "<a b=\"&amp;\" c=\"&amp\">",
        "<a b=\"&amp;\" c=&amp>",
        "<a b=\"&amp;\" c=\"&;\">",
        "<a b=&amp;>",
        "<a b=&amp; c=&amp;x&amp;>",
        "<a b=\"&amp;\" c=\"unterminated",
        "<a b=\"&amp;\" c=\"\r\n\">",
        "<a b=\"&amp;\" c=\"\0\">",
        "<a b=\"&amp;\" B=1>",
        "<a b=\"&amp;\"c=1>",
        "<a b=\"&amp;&amp;&amp;&amp;&amp;&amp;&amp;&amp;&amp;&amp;&amp;&amp;&amp;&amp;&amp;&amp;&amp;&amp;&amp;&amp;&amp;&amp;&amp;&amp;&amp;&amp;&amp;&amp;&amp;&amp;&amp;&amp;&amp;&amp;\">",
        "<a b=\"0&amp;1&amp;2&amp;3&amp;4&amp;5&amp;6&amp;7&amp;8&amp;9&amp;0&amp;1&amp;2&amp;3&amp;4&amp;5&amp;6&amp;7&amp;8&amp;9\" c=\"&amp;\">",
        "<a b=\"a very long attribute value that has to grow the value buffer well past its initial sixteen units &amp; a reference &quot;here&quot; and more text after it\">",
        "<a b=\"&amp;\"><b c=\"&amp;\"><i d=\"x&amp;y\" e=\"&amp;\"></i></b></a>",
        // Work accounting: the token limit after a reference, uppercase names on the resumed path, an LF
        // inside a quoted value, and a literal after a reference longer than one run.
        "&amp;x",
        "<DIV TITLE=\"x&notit;\" ID=y>",
        "<a B=1 c=\"&amp\" D=2>",
        "<a b=\"x\ny\">\n<script>",
        LongLiteralAfterReference,
    ];

    private static readonly int[] Quotas = [1, 2, 3, 7, 16, 4096, 100_000];
    private static readonly int[] Chunks = [0, 1, 2, 5, 9];

    [Test]
    public void QuotasAndChunksMatchTheScalarStates()
    {
        foreach (var source in Sources)
        {
            var expected = Scan(source, 1);
            foreach (var quota in new[] { 2, 3, 7, 16, 4096, 100_000 })
            {
                var actual = Scan(source, quota);
                Assert.That((actual.Tokens, actual.Diagnostics), Is.EqualTo((expected.Tokens, expected.Diagnostics)), $"quota={quota}: {source}");
            }
            foreach (var chunk in new[] { 1, 2, 5, 9 })
            {
                var scalar = Scan(source, 1, chunk);
                var actual = Scan(source, 100_000, chunk);
                Assert.That((actual.Tokens, actual.Diagnostics), Is.EqualTo((scalar.Tokens, scalar.Diagnostics)), $"chunk={chunk}: {source}");
            }
        }
    }

    [Test]
    public void WorkMatchesTheScalarStatesAtTheSameQuotaAndChunking()
    {
        foreach (var source in Sources)
        foreach (var quota in Quotas)
        foreach (var chunk in Chunks)
            Assert.That(Scan(source, quota, chunk), Is.EqualTo(Scan(source, quota, chunk, scalar: true)), $"quota={quota}, chunk={chunk}: {source}");
    }

    // A yield inside a tag without a reference costs the scalar states nothing, so there the work is
    // theirs at every quota, including one that covers a tag's steps but not its copy charges.
    [Test]
    public void WithoutReferencesWorkMatchesTheScalarStatesAtEveryQuota()
    {
        foreach (var source in Sources.Where(s => !s.Contains('&')))
        for (var quota = 1; quota <= 3 * source.Length; quota++)
            Assert.That(Scan(source, quota), Is.EqualTo(Scan(source, quota, scalar: true)), $"quota={quota}: {source}");
    }

    // The whole-tag path checks only the steps of a tag, or of the prefix it resumes the scalar states
    // from, against the quota. A quota that covers the steps but not the copy charges as well takes them
    // where the scalar states would yield inside the tag. In a value holding a reference, that yield
    // moves where they cut the reference's name, which can cost a scanner iteration, or the literal after
    // it, which can grow the value buffer differently. Closing the gap would need each name's charges
    // before the tag is committed, so it is pinned instead: the tokens are the same, and a quota that
    // covers the copy charges too brings the work back to the scalar states' own.
    [TestCase("<a b=&amp; c=&amp;x&amp;>", 33, 47, 48)]
    [TestCase("<a b=\"&amp;\" c=\"&amp;yyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyy\">", 58, 105, 135)]
    [TestCase("<a href=x b=\"x&notit;yyyy&amp;zzzzzzzzzzzzzzzzzzzzzzzzz\">", 24, 134, 169)]
    public void AQuotaCoveringOnlyATagsStepsCanShiftTheScalarStatesWork(string source, int quota, long work, long scalarWork)
    {
        var batched = Scan(source, quota);
        var scalar = Scan(source, quota, scalar: true);
        Assert.That((batched.Tokens, batched.Diagnostics), Is.EqualTo((scalar.Tokens, scalar.Diagnostics)));
        Assert.That((batched.Work, scalar.Work), Is.EqualTo((work, scalarWork)));
        Assert.That(Scan(source, 4096), Is.EqualTo(Scan(source, 4096, scalar: true)));
    }

    // HTML Standard §13.2.5.72–73 under ParseLimits.MaxTokenCharacters: after the '&' the scalar states
    // walk a name for at most the limit less one unit, so they see the unit after "amp;" only when the
    // limit is six. At five they finish the reference in one more scanner iteration, and at four the ';'
    // exceeds the limit.
    [TestCase(4, 5, 5)]
    [TestCase(5, 10, 12)]
    [TestCase(6, 10, 11)]
    public void ATextReferenceAtTheTokenLimitChargesTheScalarWork(int maxToken, long workAtQuotaOne, long work)
    {
        foreach (var quota in new[] { 1, 4096, 100_000 })
        {
            var batched = Scan("&amp;x", quota, maxToken: maxToken);
            Assert.That(batched, Is.EqualTo(Scan("&amp;x", quota, scalar: true, maxToken: maxToken)), $"quota={quota}");
            Assert.That(batched.Work, Is.EqualTo(quota == 1 ? workAtQuotaOne : work), $"quota={quota}");
            Assert.That(batched.Tokens, maxToken == 4 ? Is.EqualTo("limit:TokenCharacters@5") : Is.EqualTo("Text@0:&x\nEndOfFile@6:::False:False:False:0:\n"));
        }
    }

    // A tag exactly as long as the token limit is within it, and the whole-tag path may take it; one
    // unit longer, the scalar states throw at the unit that exceeds the limit.
    [TestCase(13, 27)]
    [TestCase(12, 19)]
    public void ATagAsLongAsTheTokenLimitIsAllowedAndOneUnitLongerThrows(int maxToken, long work)
    {
        const string tag = "<a b=\"c\" d=e>";
        foreach (var quota in new[] { 1, 17, 4096, 100_000 })
        {
            var batched = Scan(tag, quota, maxToken: maxToken);
            Assert.That(batched, Is.EqualTo(Scan(tag, quota, scalar: true, maxToken: maxToken)), $"quota={quota}");
            Assert.That(batched.Work, Is.EqualTo(work), $"quota={quota}");
            Assert.That(batched.Tokens, maxToken == tag.Length ? Does.StartWith("StartTag@0:a:") : Is.EqualTo("limit:TokenCharacters@13"));
        }
        // Seventeen units cover the tag's steps: one read takes it whole.
        var tokenizer = Tokenizer(scalar: false, maxToken);
        tokenizer.AppendInput(tag, true);
        Assert.That(tokenizer.Read(17, default, out _), maxToken == tag.Length ? Is.EqualTo(HtmlReadStatus.Token) : Is.EqualTo(HtmlReadStatus.Yielded));
    }

    // A value's literal reaches its buffer in runs of at most 4096 units, and after a reference each run
    // grows the buffer as it needs, whichever path assembles the value. The production quota of 4096 never
    // takes this tag whole; 100,000 does.
    [TestCase(4096, 14106)]
    [TestCase(100_000, 14120)]
    public void ALiteralAfterAReferenceLongerThanOneRunChargesTheScalarWork(int quota, long work)
    {
        var batched = Scan(LongLiteralAfterReference, quota);
        Assert.That(batched, Is.EqualTo(Scan(LongLiteralAfterReference, quota, scalar: true)));
        Assert.That(batched.Work, Is.EqualTo(work));
        Assert.That(batched.Tokens, Does.Contain(" b=&" + new string('x', 5000) + "\n"));
    }

    // HTML Standard §13.2.3, dynamic markup insertion: each write is a source slice of its own, so a tag
    // split between two writes crosses a slice, and the scalar states take it from its first '&', or from
    // its start. ReadUntil stops inside it at the insertion point.
    [TestCase("<a b=\"x", "&amp;y\" c=d>", 64)]
    [TestCase("<a b", "=\"&amp;\">", 46)]
    [TestCase("<a b=\"&amp;", "\" c=d>", 60)]
    public void AWriteSplittingATagChargesTheScalarWork(string first, string second, long work)
    {
        foreach (var quota in new[] { 1, 3, 4096, 100_000 })
            Assert.That(Write(first, second, quota), Is.EqualTo(Write(first, second, quota, scalar: true)), $"quota={quota}");
        Assert.That(Write(first, second, 4096).Work, Is.EqualTo(work));
    }

    // Each write is its own source unit as well, so a script start tag across two of them has a mixed
    // source location, named after the unit its last unit came from.
    [TestCase("<scr", "ipt a=b>", 49, 8, 8)]
    [TestCase("<script a=\"&amp;", "\">x", 59, 2, 2)]
    public void AScriptStartTagAcrossTwoSourceUnitsChargesTheScalarWork(string first, string second, long work, long offset, long column)
    {
        foreach (var quota in new[] { 1, 3, 4096, 100_000 })
            Assert.That(Write(first, second, quota), Is.EqualTo(Write(first, second, quota, scalar: true)), $"quota={quota}");
        var result = Write(first, second, 4096);
        Assert.That(result.Work, Is.EqualTo(work));
        Assert.That(result.Tokens, Does.Contain(":script:").And.Contain(new HtmlSourceLocation(HtmlSourceKind.Mixed, 2, offset, 1, column).ToString()));
    }

    // A reference the scan cannot take hands the tag to the scalar states at its first '&', with the tag
    // name, the finished attributes and the pending attribute's name lowered as they lower them.
    [TestCase("<DIV TITLE=\"x&notit;\" ID=y>", "StartTag@0:div:", " title=x&notit; id=y\n", 65)]
    [TestCase("<a B=1 c=\"&amp\" D=2>", "StartTag@0:a:", " b=1 c=& d=2\n", 43)]
    public void UppercaseNamesOnTheResumedPathChargeTheScalarWork(string source, string start, string attributes, long work)
    {
        foreach (var quota in Quotas)
            Assert.That(Scan(source, quota), Is.EqualTo(Scan(source, quota, scalar: true)), $"quota={quota}");
        var result = Scan(source, 4096);
        Assert.That(result.Tokens, Does.StartWith(start).And.Contain(attributes));
        Assert.That(result.Work, Is.EqualTo(work));
    }

    // A quoted value crossing an LF is one run, and the input counts the line it crosses.
    [Test]
    public void AnLfInsideAQuotedValueMovesTheNextScriptToTheNextLine()
    {
        const string source = "<a b=\"x\ny\">\n<script>";
        var location = new HtmlSourceLocation(HtmlSourceKind.Primary, 0, 20, 3, 8).ToString();
        foreach (var quota in Quotas)
        {
            var batched = Scan(source, quota);
            Assert.That(batched, Is.EqualTo(Scan(source, quota, scalar: true)), $"quota={quota}");
            Assert.That(batched.Tokens, Does.Contain(location), $"quota={quota}");
        }
        Assert.That(Scan(source, 4096).Work, Is.EqualTo(37));
    }

    // HTML Standard §13.2.3.5: a CR LF pair is one newline. With a write between them, the tokenizer
    // takes the write's first unit after the CR, so the LF is a newline of its own in the token stream,
    // but in the source unit that holds both the pair still counts one line. That LF opens a text run,
    // which counts lines as the scalar states would.
    [Test]
    public void ACrAndAnLfAroundAWriteCountOneLineInTheirUnit()
    {
        var location = new HtmlSourceLocation(HtmlSourceKind.Primary, 0, 12, 2, 9).ToString();
        foreach (var quota in new[] { 1, 3, 4096, 100_000 })
        {
            var batched = WriteBetweenCrAndLf(quota, scalar: false);
            Assert.That(batched, Is.EqualTo(WriteBetweenCrAndLf(quota, scalar: true)), $"quota={quota}");
            Assert.That(batched.Tokens, Does.Contain(location), $"quota={quota}");
        }
        Assert.That(WriteBetweenCrAndLf(4096, scalar: false).Work, Is.EqualTo(28));
    }

    // A CR ending one write and an LF opening the next are a pair as well: the tokenizer skips the LF,
    // and it starts the second write's second line.
    [Test]
    public void ACrEndingOneWriteAndAnLfOpeningTheNextAreOnePair()
    {
        var location = new HtmlSourceLocation(HtmlSourceKind.Inserted, 2, 9, 2, 8).ToString();
        foreach (var quota in new[] { 1, 3, 4096, 100_000 })
        {
            var batched = Write("x\r", "\n<script>", quota);
            Assert.That(batched, Is.EqualTo(Write("x\r", "\n<script>", quota, scalar: true)), $"quota={quota}");
            Assert.That(batched.Tokens, Does.Contain(location).And.Contain("Text@0:x\n\n"), $"quota={quota}");
        }
        Assert.That(Write("x\r", "\n<script>", 4096).Work, Is.EqualTo(45));
    }

    // A slice that ends inside a tag leaves the tokenizer waiting in the scalar states' exact state,
    // whether the scan resumed the tag at a reference or never took it. A cancelled read there throws
    // before it charges or consumes anything, and leaves the session terminal.
    [TestCase("<a b=\"&amp;x", 16, 12)]
    [TestCase("<a b=\"x&notit;y", 24, 15)]
    [TestCase("<a href=x b=\"&amp;", 31, 18)]
    public void CancellationInsideATagChargesNothingMore(string partial, long work, long consumed)
    {
        foreach (var quota in new[] { 1, 3, 4096, 100_000 })
        {
            var batched = CancelInside(partial, quota, scalar: false);
            Assert.That(batched, Is.EqualTo(CancelInside(partial, quota, scalar: true)), $"quota={quota}");
            Assert.That(batched.Consumed, Is.EqualTo(consumed), $"quota={quota}");
            if (quota >= 4096) Assert.That(batched.Work, Is.EqualTo(work), $"quota={quota}");
        }
    }

    // The whole-tag path takes a tag when the quota covers its steps, the first of them charged by the
    // scanner iteration it runs in: at exactly that quota one read returns the tag, and one less leaves
    // it to the scalar states. At both, and at a quota equal to all of the tag's work, the work is theirs.
    [TestCase("<a href=\"/x?a=1&amp;b=2\" class=c>", 37, 75)]
    [TestCase("<div class=\"a b\" id='x' data-v=1 hidden>", 47, 116)]
    [TestCase("<a b=\"&amp;xxxxxxxxxxxxxxxxxxxx\">", 35, 62)]
    public void AQuotaEqualToATagsStepsOrToItsWorkChargesTheScalarWork(string tag, int steps, long work)
    {
        foreach (var quota in new[] { steps - 1, steps, (int) work - 1, (int) work, (int) work + 1 })
            Assert.That(Scan(tag, quota), Is.EqualTo(Scan(tag, quota, scalar: true)), $"quota={quota}");
        Assert.That(FirstRead(tag, steps, scalar: false), Is.EqualTo((HtmlReadStatus.Token, work)));
        Assert.That(FirstRead(tag, steps - 1, scalar: false).Status, Is.EqualTo(HtmlReadStatus.Yielded));
        Assert.That(FirstRead(tag, (int) work, scalar: false), Is.EqualTo((HtmlReadStatus.Token, work)));
        Assert.That(FirstRead(tag, (int) work, scalar: true), Is.EqualTo((HtmlReadStatus.Token, work)));
    }

    private readonly record struct Result(string Tokens, string Diagnostics, long Work, long Consumed = 0)
    {
        // A failure prints the work first, and no more of a long tag than fits a line.
        public override string ToString() =>
            $"Work = {Work}, Consumed = {Consumed}, Diagnostics = {Diagnostics}, Tokens = {(Tokens.Length > 160 ? Tokens[..160] + "..." : Tokens)}";
    }

    private static HtmlTokenizer Tokenizer(bool scalar, int maxToken = 0, ParseDiagnosticCollector? diagnostics = null)
    {
        var limits = maxToken > 0 ? new ParseLimits { MaxTokenCharacters = maxToken } : null;
        var tokenizer = new HtmlTokenizer(new HtmlTokenizerContext(limits, diagnostics));
        if (scalar) tokenizer.UseScalarTagsAndReferences();
        return tokenizer;
    }

    private static Result Scan(string source, int quota, int chunk = 0, bool scalar = false, int maxToken = 0)
    {
        var diagnostics = new ParseDiagnosticCollector(1000);
        var tokenizer = Tokenizer(scalar, maxToken, diagnostics);
        var output = new StringBuilder();
        try
        {
            if (chunk <= 0) tokenizer.AppendInput(source, true);
            else
            {
                for (var i = 0; i < source.Length; i += chunk)
                {
                    tokenizer.AppendInput(source.Substring(i, Math.Min(chunk, source.Length - i)));
                    Drain(tokenizer, quota, output, HtmlReadStatus.NeedInput);
                }
                tokenizer.AppendInput(string.Empty, true);
            }
            Drain(tokenizer, quota, output, HtmlReadStatus.Complete);
        }
        catch (ParseLimitException failure)
        {
            output.Append("limit:").Append(failure.Kind).Append('@').Append(tokenizer.ConsumedInput);
        }
        var errors = string.Join(",", diagnostics.Items.Select(d => d.Code + "@" + d.Offset));
        return new Result(output.ToString(), errors, tokenizer.WorkCount);
    }

    // Two writes at an insertion point before the primary input, each read up to that point.
    private static Result Write(string first, string second, int quota, bool scalar = false)
    {
        var tokenizer = Tokenizer(scalar);
        tokenizer.AppendInput("<p>tail</p>", true);
        var point = tokenizer.CreateInsertionPoint();
        var output = new StringBuilder();
        tokenizer.InsertInput(point, first, default);
        Drain(tokenizer, quota, output, HtmlReadStatus.InsertionBoundary, point);
        tokenizer.InsertInput(point, second, default);
        Drain(tokenizer, quota, output, HtmlReadStatus.InsertionBoundary, point);
        tokenizer.ReleaseInsertionPoint(point);
        Drain(tokenizer, quota, output, HtmlReadStatus.Complete);
        return new Result(output.ToString(), "", tokenizer.WorkCount);
    }

    // The primary input is read through its CR, one unit per read; a write then goes in before its LF.
    private static Result WriteBetweenCrAndLf(int quota, bool scalar)
    {
        var tokenizer = Tokenizer(scalar);
        tokenizer.AppendInput("x\r\nz<script>", true);
        var output = new StringBuilder();
        while (tokenizer.ConsumedInput < 2) Drain(tokenizer, 1, output, HtmlReadStatus.Token);
        var point = tokenizer.CreateInsertionPoint();
        tokenizer.InsertInput(point, "y", default);
        Drain(tokenizer, quota, output, HtmlReadStatus.InsertionBoundary, point);
        tokenizer.ReleaseInsertionPoint(point);
        Drain(tokenizer, quota, output, HtmlReadStatus.Complete);
        return new Result(output.ToString(), "", tokenizer.WorkCount);
    }

    private static Result CancelInside(string partial, int quota, bool scalar)
    {
        var tokenizer = Tokenizer(scalar);
        tokenizer.AppendInput(partial);
        var output = new StringBuilder();
        Drain(tokenizer, quota, output, HtmlReadStatus.NeedInput);
        Assert.That(output.ToString(), Is.Empty);
        var work = tokenizer.WorkCount;
        var consumed = tokenizer.ConsumedInput;
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Assert.Throws<OperationCanceledException>(() => tokenizer.Read(quota, cancellation.Token, out _));
        Assert.That((tokenizer.WorkCount, tokenizer.ConsumedInput), Is.EqualTo((work, consumed)));
        Assert.Throws<InvalidOperationException>(() => tokenizer.Read(quota, default, out _));
        Assert.Throws<InvalidOperationException>(() => tokenizer.AppendInput("\">", true));
        return new Result("", "", work, consumed);
    }

    private static (HtmlReadStatus Status, long Work) FirstRead(string source, int quota, bool scalar)
    {
        var tokenizer = Tokenizer(scalar);
        tokenizer.AppendInput(source, true);
        return (tokenizer.Read(quota, default, out _), tokenizer.WorkCount);
    }

    // A yield, a chunk boundary or a write flushes pending text, so adjacent text tokens are merged. A
    // terminal of Token stops after the first token.
    private static void Drain(HtmlTokenizer tokenizer, int quota, StringBuilder output, HtmlReadStatus terminal, HtmlInsertionPoint? point = null)
    {
        for (var i = 0; i < 1_000_000; i++)
        {
            var status = point is null
                ? tokenizer.Read(quota, default, out var token)
                : tokenizer.ReadUntil(point, quota, false, default, out token);
            if (status == HtmlReadStatus.Yielded) continue;
            if (status != HtmlReadStatus.Token)
            {
                Assert.That(status, Is.EqualTo(terminal));
                return;
            }
            Append(output, token);
            if (terminal == HtmlReadStatus.Token) return;
        }
        Assert.Fail("Tokenizer made no terminal progress.");
    }

    private static void Append(StringBuilder output, in HtmlToken token)
    {
        if (token.Kind == HtmlTokenKind.Text && output.Length > 0 && output[^1] == '\u0001')
        {
            output.Length--;
            output.Append(token.Data).Append('\u0001');
            return;
        }
        if (output.Length > 0 && output[^1] == '\u0001') output[^1] = '\n';
        if (token.Kind == HtmlTokenKind.Text)
        {
            output.Append("Text@").Append(token.Offset).Append(':').Append(token.Data).Append('\u0001');
            return;
        }
        output.Append(token.Kind).Append('@').Append(token.Offset).Append(':').Append(token.Name)
            .Append(':').Append(token.Data).Append(':').Append(token.SelfClosing)
            .Append(':').Append(token.EndTagHadAttributes).Append(':').Append(token.EndTagHadSelfClosing)
            .Append(':').Append(token.SourceChanges).Append(':').Append(token.ScriptSourceLocation);
        foreach (var attribute in token.Attributes) output.Append(' ').Append(attribute.Name).Append('=').Append(attribute.Value);
        output.Append('\n');
    }
}
