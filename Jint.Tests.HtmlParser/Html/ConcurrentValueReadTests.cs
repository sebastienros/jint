#nullable enable
using Jint.HtmlParser;
using Jint.HtmlParser.Serialization;

namespace Jint.Tests.HtmlParser.Html;

public class ConcurrentValueReadTests
{
    [Test]
    public async Task ParsedTreeColdGettersAndSpansAgreeAcrossReaders()
    {
        const int count = 2048;
        const string value = "ordinary 😀 text with a nonzero source offset";
        var document = MarkupParser.ParseHtml(string.Concat(Enumerable.Repeat($"<div data-x='{value}'>{value}</div>", count)));
        var body = document.DocumentElement!.LastChild!;
        var nodes = new List<(Element Element, Text Text, Attr Attr)>();
        for (var node = body.FirstChild; node is not null; node = node.NextSibling)
        {
            var element = (Element) node;
            nodes.Add((element, (Text) element.FirstChild!, element.GetAttributeNode("data-x")!));
        }
        nodes.Count.Should().Be(count);
        var stamp = document.MutationStamp;
        await ReadTogether(count, (reader, index) =>
        {
            var (element, text, attr) = nodes[index];
            // Interleave readers of the original span with first string getters.
            if ((reader & 1) == 0)
            {
                Check(text.Data, value);
                Check(attr.Value, value);
            }
            Check(text.DataSpan.ToString(), value);
            Check(attr.ValueSpan.ToString(), value);
            Check(text.SubstringData(0, text.Length), value);
            Check(HtmlMarkupSerializer.Serialize(element), $"<div data-x=\"{value}\">{value}</div>");
            Check(text.Data, value);
            Check(attr.Value, value);
        });
        document.MutationStamp.Should().Be(stamp);
        // Mutations are single-writer and invalidate both materialization lanes.
        nodes[0].Text.Data = "replacement";
        nodes[0].Attr.Value = "new attribute";
        Check(nodes[0].Text.DataSpan.ToString(), "replacement");
        Check(nodes[0].Attr.ValueSpan.ToString(), "new attribute");
    }

    [Test]
    public async Task SliceSnapshotsRemainValidWhileStorageMaterializes()
    {
        const string expected = "value 😀";
        const string source = "prefix padding/value 😀/suffix";
        var slices = Enumerable.Range(0, 2048).Select(_ => new StringSlice(source, 15, expected.Length)).ToArray();
        var storage = slices.Select(slice => new StringSliceStorage(slice)).ToArray();
        await ReadTogether(storage.Length, (reader, index) =>
        {
            if ((reader & 1) == 0) Check(storage[index].Materialize(), expected);
            Check(storage[index].Span.ToString(), expected);
            var snapshot = storage[index].Snapshot;
            Check(snapshot.ToString(), expected);
            Check(snapshot.Slice(0, 5).ToString(), "value");
            if (!snapshot.Slice(0, 5).TryConcat(snapshot.Slice(5, expected.Length - 5), out var combined))
                throw new InvalidOperationException("Adjacent snapshot slices must coalesce.");
            Check(combined.ToString(), expected);
            Check(slices[index].Span.ToString(), expected);
            Check(storage[index].Materialize(), expected);
        });
        foreach (var item in storage)
            Assert.That(source.AsSpan().Overlaps(item.Span), Is.False);
        var empty = new StringSliceStorage(default);
        empty.Materialize().Should().BeEmpty();
        empty.Span.IsEmpty.Should().BeTrue();
    }

    [Test]
    public async Task DecodedAndBufferedParsedTextCanBeReadConcurrently()
    {
        var document = MarkupParser.ParseHtml("<div data-x='a&amp;b'>a&amp;b</div>");
        var element = (Element) document.DocumentElement!.LastChild!.FirstChild!;
        var text = (Text) element.FirstChild!;
        var attr = element.GetAttributeNode("data-x")!;
        // Noncontiguous parsed chunks exercise Text's separate char[] cache.
        text.AppendParsedData(" extra".AsSpan(), default);
        await ReadTogether(128, (_, _) =>
        {
            Check(text.Data, "a&b extra");
            Check(text.DataSpan.ToString(), "a&b extra");
            Check(attr.Value, "a&b");
            Check(attr.ValueSpan.ToString(), "a&b");
        });
        var cached = text.Data;
        text.Data.Should().BeSameAs(cached);
        text.AppendParsedData(" more".AsSpan(), default);
        Check(text.Data, "a&b extra more");
    }

    private static void Check(string actual, string expected)
    {
        if (!string.Equals(actual, expected, StringComparison.Ordinal))
            throw new InvalidOperationException($"Concurrent read returned '{actual}', expected '{expected}'.");
    }

    private static async Task ReadTogether(int count, Action<int, int> read)
    {
        // This is a wedge ceiling, not a duration assertion. Each cold value is
        // released to all four dedicated threads together; no sleeps or pool
        // injection timing decide whether first materialization races.
        using var barrier = new Barrier(4);
        var workers = Enumerable.Range(0, 4).Select(reader => DedicatedThread.RunAsync(() =>
        {
            try
            {
                for (var index = 0; index < count; index++)
                {
                    if (!barrier.SignalAndWait(TimeSpan.FromMinutes(2)))
                        throw new TimeoutException("Concurrent readers did not reach the next value.");
                    read(reader, index);
                }
            }
            finally
            {
                barrier.RemoveParticipant();
            }
        })).ToArray();
        await Task.WhenAll(workers).WaitAsync(TimeSpan.FromMinutes(2));
    }
}
