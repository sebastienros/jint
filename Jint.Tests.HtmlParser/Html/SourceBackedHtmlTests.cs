#nullable enable
using System.IO.Hashing;
using System.Reflection;
using System.Runtime.InteropServices;
using Jint.HtmlParser;
using Jint.HtmlParser.Html;
using Jint.HtmlParser.Serialization;

namespace Jint.Tests.HtmlParser.Html;

public class SourceBackedHtmlTests
{
    [TestCase(1)]
    [TestCase(31)]
    [TestCase(10000)]
    public void PlainValuesReferenceTheInputUntilTheirStringGetterIsRead(int quota)
    {
        const string source = "<!doctype html><div data-x='attribute value'>ordinary text</div>";
        var document = Parse(source, quota);
        var element = (Element) document.DocumentElement!.LastChild!.FirstChild!;
        var text = (Text) element.FirstChild!;
        var attribute = element.GetAttributeNode("data-x")!;
        Assert.That(source.AsSpan().Overlaps(text.DataSpan), Is.True);
        Assert.That(source.AsSpan().Overlaps(attribute.ValueSpan), Is.True);
        text.Length.Should().Be(13);

        var stamp = document.MutationStamp;
        using var observer = document.ObserveMutations(element,
            new MutationObserverOptions { Attributes = true, CharacterData = true, Subtree = true });
        long checksum = 0;
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 1000; i++) checksum += text.DataSpan.Length + attribute.ValueSpan.Length;
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        allocated.Should().Be(0);
        checksum.Should().Be(28000);

        HtmlMarkupSerializer.Serialize(element).Should().Be("<div data-x=\"attribute value\">ordinary text</div>");
        Assert.That(source.AsSpan().Overlaps(text.DataSpan), Is.True);
        Assert.That(source.AsSpan().Overlaps(attribute.ValueSpan), Is.True);
        var data = text.Data;
        var value = attribute.Value;
        data.Should().Be("ordinary text");
        value.Should().Be("attribute value");
        text.Data.Should().BeSameAs(data);
        attribute.Value.Should().BeSameAs(value);
        Assert.That(source.AsSpan().Overlaps(text.DataSpan), Is.False);
        Assert.That(source.AsSpan().Overlaps(attribute.ValueSpan), Is.False);
        document.MutationStamp.Should().Be(stamp);
        observer.TakeRecords().Should().BeEmpty();
    }

    [Test]
    public void TokenSlicesRemainValidAfterTheTokenizerAdvances()
    {
        const string source = "<div data-x='first'>text</div><div data-x='second'>later</div>";
        var tokens = Scan(source);
        var first = tokens[0];
        var text = tokens[1];
        Assert.That(source.AsSpan().Overlaps(first.Attributes[0].ValueSlice.Span), Is.True);
        Assert.That(source.AsSpan().Overlaps(text.DataSlice.Span), Is.True);
        first.Attributes[0].Should().Be(new HtmlAttribute("data-x", "first"));
        first.Attributes[0].GetHashCode().Should().Be(new HtmlAttribute("data-x", "first").GetHashCode());
        text.Data.Should().Be("text");
        tokens[3].Attributes[0].Value.Should().Be("second");
    }

    [Test]
    public void NamesAreReusedAfterAsciiCaseFoldingWithoutChangingUnicode()
    {
        var tokens = Scan("<CUSTOM-WIDGET DATA-NAME=x/><custom-widget data-name=y/><x \u00C9=a \u00E9=b>");
        tokens[0].Name.Should().Be("custom-widget");
        tokens[1].Name.Should().BeSameAs(tokens[0].Name);
        tokens[1].Attributes[0].Name.Should().BeSameAs(tokens[0].Attributes[0].Name);
        tokens[2].Attributes.Select(attribute => attribute.Name).Should().Equal("\u00C9", "\u00E9");
    }

    [Test]
    public void KnownNameLookupRequiresBothBoundariesAndSharesCanonicalStrings()
    {
        string[] names = ["a", "class", "head", "thead", "li", "link", "input", "value", "section", "textarea"];
        var source = string.Concat(names.Select(name => "<" + name + ">"));
        var first = Scan(source);
        var second = Scan(source);
        first.Where(token => token.Kind == HtmlTokenKind.StartTag).Select(token => token.Name).Should().Equal(names);
        for (var i = 0; i < names.Length; i++)
        {
            first[i].Name.Should().BeSameAs(names[i]);
            second[i].Name.Should().BeSameAs(first[i].Name);
        }

        string[] partials = ["c", "lass", "hea", "ead", "theadx", "l", "ink", "put", "val", "ection", "area"];
        Scan(string.Concat(partials.Select(name => "<" + name + ">")))
            .Where(token => token.Kind == HtmlTokenKind.StartTag).Select(token => token.Name).Should().Equal(partials);
    }

    [Test]
    public void UnknownNameCacheHashesUtf16BytesWithXxHash3()
    {
        const string name = "custom-\u00E9-\uD800";
        var tokenizer = new HtmlTokenizer(default);
        tokenizer.AppendInput("<" + name + ">", true);
        var tokens = new List<HtmlToken>();
        Drain(tokenizer, 10000, tokens);
        var cache = (string?[]) typeof(HtmlTokenizer).GetField("_names", BindingFlags.NonPublic | BindingFlags.Instance)!
            .GetValue(tokenizer)!;
        var slot = (int) (XxHash3.HashToUInt64(MemoryMarshal.AsBytes(name.AsSpan())) & (uint) (cache.Length - 1));
        cache[slot].Should().BeSameAs(tokens[0].Name);
        tokens[0].Name.Should().Be(name);
    }

    [Test]
    public void KnownNameTableStoresLiteralReferencesAtSourceOffsetsWithoutTrailingSlots()
    {
        var table = ((string Source, string?[] ByOffset, (int Start, int Length)[] Ranges))
            typeof(HtmlTokenizer).GetField("KnownNames", BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!;
        table.ByOffset[^1].Should().BeSameAs("textarea");
        for (var i = 0; i < table.ByOffset.Length; i++)
        {
            if (table.ByOffset[i] is not { } name) continue;
            table.Source[i - 1].Should().Be('\0');
            table.Source[i + name.Length].Should().Be('\0');
            Assert.That(table.Source.AsSpan(i, name.Length).SequenceEqual(name), Is.True);
            var range = table.Ranges[name.Length];
            i.Should().BeGreaterThan(range.Start);
            (i + name.Length).Should().BeLessThan(range.Start + range.Length);
        }
    }

    [Test]
    public void NameCacheIsBoundedAndCollisionsNeverChangeNames()
    {
        var tokenizer = new HtmlTokenizer(default);
        var names = Enumerable.Range(0, 4096).Select(i => "custom-" + i).ToArray();
        var longName = new string('x', 1024);
        tokenizer.AppendInput(string.Concat(names.Select(name => "<" + name + ">")) + "<" + longName + ">", true);
        var tokens = new List<HtmlToken>();
        Drain(tokenizer, 10000, tokens);
        tokens.Where(token => token.Kind == HtmlTokenKind.StartTag).Select(token => token.Name)
            .Should().Equal(names.Append(longName));
        var cache = (string?[]) typeof(HtmlTokenizer).GetField("_names", BindingFlags.NonPublic | BindingFlags.Instance)!
            .GetValue(tokenizer)!;
        cache.Length.Should().Be(128);
        cache.Where(name => name is not null).Should().OnlyContain(name => name!.Length <= 64);
    }

    [TestCase("<div data-x='a&amp;b\r\nc'>text&amp;more\r\nend</div>")]
    [TestCase("<div data-x=unquoted>one \0two\uD800</div>")]
    [TestCase("<body><table>one<!-- gap -->two<tr><td>cell</table>tail")]
    [TestCase("<template><div data-x='value'>template text</div></template>")]
    [TestCase("<svg xmlns='http://www.w3.org/2000/svg'><text>A\0B</text></svg>")]
    [TestCase("<script>if(a<b){x='&';}</script><textarea>a&amp;b\r\nc</textarea>")]
    [TestCase("<div data-x='first' DATA-X='discarded'><!doctype html PUBLIC 'pub' 'sys'>tail")]
    public void EveryInputSplitPreservesDecodedValuesAndTreeRecovery(string source)
    {
        var expected = HtmlMarkupSerializer.Serialize(MarkupParser.ParseHtml(source));
        for (var split = 0; split <= source.Length; split++)
        {
            var document = Document.CreateHtml();
            var session = new HtmlParserSession(document);
            session.AppendInput(source[..split]);
            Drive(session, 1).Should().Be(HtmlParseStepKind.NeedInput);
            session.AppendInput(source[split..], true);
            Drive(session, 1).Should().Be(HtmlParseStepKind.Complete);
            HtmlMarkupSerializer.Serialize(document).Should().Be(expected, $"split {split}");
        }
    }

    [Test]
    public void ContiguousTextSpansMergeAcrossTokenAndWorkBoundaries()
    {
        var value = new string('x', 9000);
        var source = "<div>" + value + "</div>";
        var document = Parse(source, 7);
        var element = document.DocumentElement!.LastChild!.FirstChild!;
        element.ChildCount.Should().Be(1);
        var text = (Text) element.FirstChild!;
        Assert.That(source.AsSpan().Overlaps(text.DataSpan), Is.True);
        text.DataSpan.ToString().Should().Be(value);
    }

    [Test]
    public void MutationsAndClonesDoNotModifyCapturedSourceSpans()
    {
        const string source = "<div data-x='value'>original</div>";
        var document = MarkupParser.ParseHtml(source);
        var element = (Element) document.DocumentElement!.LastChild!.FirstChild!;
        var text = (Text) element.FirstChild!;
        var attribute = element.GetAttributeNode("data-x")!;
        var capturedText = text.DataSpan;
        var capturedValue = attribute.ValueSpan;
        using var observer = document.ObserveMutations(element, new MutationObserverOptions
        {
            Attributes = true, AttributeOldValue = true, CharacterData = true, CharacterDataOldValue = true, Subtree = true
        });
        var clone = (Element) element.CloneNode(true);
        text.ReplaceData(0, 3, "NEW");
        attribute.Value = "changed";
        capturedText.ToString().Should().Be("original");
        capturedValue.ToString().Should().Be("value");
        ((Text) clone.FirstChild!).Data.Should().Be("original");
        clone.GetAttribute("data-x").Should().Be("value");
        observer.TakeRecords().Select(record => record.OldValue).Should().Equal("original", "value");
    }

    [Test]
    public void SourceAppendPreservesOldValuesAndCancellation()
    {
        const string source = "abcdef";
        var document = Document.CreateHtml();
        var text = document.CreateTextNode("");
        text.AppendParsedData(new StringSlice(source, 0, 3), default);
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();
        Assert.Throws<OperationCanceledException>(() =>
            text.AppendParsedData(new StringSlice(source, 3, 3), canceled.Token));
        text.DataSpan.ToString().Should().Be("abc");
        using var observer = document.ObserveMutations(text,
            new MutationObserverOptions { CharacterData = true, CharacterDataOldValue = true });
        text.AppendParsedData(new StringSlice(source, 3, 3), default);
        text.DataSpan.ToString().Should().Be(source);
        Assert.That(source.AsSpan().Overlaps(text.DataSpan), Is.True);
        observer.TakeRecords().Single().OldValue.Should().Be("abc");
        var previous = text.Data;
        text.AppendParsedData(new StringSlice("later"), default);
        previous.Should().Be("abcdef");
        text.Data.Should().Be("abcdeflater");
    }

    private static Document Parse(string source, int quota)
    {
        var document = Document.CreateHtml();
        var session = new HtmlParserSession(document);
        session.AppendInput(source, true);
        Drive(session, quota).Should().Be(HtmlParseStepKind.Complete);
        return document;
    }

    private static HtmlParseStepKind Drive(HtmlParserSession session, int quota)
    {
        for (var i = 0; i < 100000; i++)
        {
            var kind = session.Drive(quota, default).Kind;
            if (kind != HtmlParseStepKind.Yielded) return kind;
        }
        throw new InvalidOperationException("Parser made no terminal progress.");
    }

    private static List<HtmlToken> Scan(string source)
    {
        var tokenizer = new HtmlTokenizer(default);
        tokenizer.AppendInput(source, true);
        var tokens = new List<HtmlToken>();
        Drain(tokenizer, 10000, tokens);
        return tokens;
    }

    private static void Drain(HtmlTokenizer tokenizer, int quota, List<HtmlToken> tokens)
    {
        for (var i = 0; i < 100000; i++)
        {
            var status = tokenizer.Read(quota, default, out var token);
            if (status == HtmlReadStatus.Token) tokens.Add(token);
            else if (status != HtmlReadStatus.Yielded)
            {
                status.Should().Be(HtmlReadStatus.Complete);
                return;
            }
        }
        throw new InvalidOperationException("Tokenizer made no terminal progress.");
    }
}
