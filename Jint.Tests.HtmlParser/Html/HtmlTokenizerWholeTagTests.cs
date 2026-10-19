#nullable enable
using System.Text;
using Jint.HtmlParser;
using Jint.HtmlParser.Html;

namespace Jint.Tests.HtmlParser.Html;

// The whole-tag and LF-crossing run paths batch a tag or a run into one scanner iteration when the
// work quota leaves room for it. A quota of one never leaves room, so it is the scalar oracle: every
// other quota and chunking must produce the same tokens, offsets, script source locations and
// diagnostics. Work is not compared: before these paths existed, a tokenizer's work could already
// differ between quotas by a few units on several of these inputs.
public class HtmlTokenizerWholeTagTests
{
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
    ];

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

    private readonly record struct Result(string Tokens, string Diagnostics);

    private static Result Scan(string source, int quota, int chunk = 0)
    {
        var diagnostics = new ParseDiagnosticCollector(1000);
        var tokenizer = new HtmlTokenizer(new HtmlTokenizerContext(diagnostics: diagnostics));
        var output = new StringBuilder();
        if (chunk <= 0) tokenizer.AppendInput(source, true);
        else
        {
            for (var i = 0; i < source.Length; i += chunk)
            {
                tokenizer.AppendInput(source.Substring(i, Math.Min(chunk, source.Length - i)));
                Drain(tokenizer, quota, output, final: false);
            }
            tokenizer.AppendInput(string.Empty, true);
        }
        Drain(tokenizer, quota, output, final: true);
        var errors = string.Join(",", diagnostics.Items.Select(d => d.Code + "@" + d.Offset));
        return new Result(output.ToString(), errors);
    }

    // A yield or a chunk boundary flushes pending text, so adjacent text tokens are merged.
    private static void Drain(HtmlTokenizer tokenizer, int quota, StringBuilder output, bool final)
    {
        for (var i = 0; i < 1_000_000; i++)
        {
            var status = tokenizer.Read(quota, default, out var token);
            if (status == HtmlReadStatus.Yielded) continue;
            if (status != HtmlReadStatus.Token)
            {
                Assert.That(status, Is.EqualTo(final ? HtmlReadStatus.Complete : HtmlReadStatus.NeedInput));
                return;
            }
            if (token.Kind == HtmlTokenKind.Text && output.Length > 0 && output[^1] == '\u0001')
            {
                output.Length--;
                output.Append(token.Data).Append('\u0001');
                continue;
            }
            if (output.Length > 0 && output[^1] == '\u0001') output[^1] = '\n';
            if (token.Kind == HtmlTokenKind.Text)
            {
                output.Append("Text@").Append(token.Offset).Append(':').Append(token.Data).Append('\u0001');
                continue;
            }
            output.Append(token.Kind).Append('@').Append(token.Offset).Append(':').Append(token.Name)
                .Append(':').Append(token.Data).Append(':').Append(token.SelfClosing)
                .Append(':').Append(token.EndTagHadAttributes).Append(':').Append(token.EndTagHadSelfClosing)
                .Append(':').Append(token.SourceChanges).Append(':').Append(token.ScriptSourceLocation);
            foreach (var attribute in token.Attributes) output.Append(' ').Append(attribute.Name).Append('=').Append(attribute.Value);
            output.Append('\n');
        }
        Assert.Fail("Tokenizer made no terminal progress.");
    }
}
