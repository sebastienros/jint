#nullable enable
using Jint.HtmlParser.Html;

namespace Jint.Tests.HtmlParser.Html.TreeConstruction;

public partial class HtmlTreeConstructionTests
{
    // Original #data ordinals in the pinned html5lib tree-construction files.
    // The expected HTML below is the #document tree rendered compactly, rather
    // than a second invocation of the implementation under test.
    private static readonly (string Id, string Source, string Expected)[] Html5libSubset =
    [
        ("blocks.dat#1", "<!doctype html><p>foo<address>bar<p>baz",
            "<!doctype html><html><head></head><body><p>foo</p><address>bar<p>baz</p></address></body></html>"),
        ("blocks.dat#2", "<!doctype html><address><p>foo</address>bar",
            "<!doctype html><html><head></head><body><address><p>foo</p></address>bar</body></html>"),
        ("inbody01.dat#1", "<button>1</foo>",
            "<html><head></head><body><button>1</button></body></html>"),
        ("inbody01.dat#2", "<foo>1<p>2</foo>",
            "<html><head></head><body><foo>1<p>2</p></foo></body></html>"),
        ("ruby.dat#1", "<html><ruby>a<rb>b<rb></ruby></html>",
            "<html><head></head><body><ruby>a<rb>b</rb><rb></rb></ruby></body></html>"),
        ("ruby.dat#2", "<html><ruby>a<rb>b<rt></ruby></html>",
            "<html><head></head><body><ruby>a<rb>b</rb><rt></rt></ruby></body></html>"),
        ("ruby.dat#3", "<html><ruby>a<rb>b<rtc></ruby></html>",
            "<html><head></head><body><ruby>a<rb>b</rb><rtc></rtc></ruby></body></html>"),
        ("ruby.dat#4", "<html><ruby>a<rb>b<rp></ruby></html>",
            "<html><head></head><body><ruby>a<rb>b</rb><rp></rp></ruby></body></html>"),
        ("scriptdata01.dat#1", "FOO<script>'Hello'</script>BAR",
            "<html><head></head><body>FOO<script>'Hello'</script>BAR</body></html>"),
        ("doctype01.dat#1", "<!DOCTYPE html>Hello",
            "<!doctype html><html><head></head><body>Hello</body></html>"),
        ("doctype01.dat#2", "<!dOctYpE HtMl>Hello",
            "<!doctype html><html><head></head><body>Hello</body></html>"),
        ("doctype01.dat#4", "<!DOCTYPE>Hello",
            "<!doctype ><html><head></head><body>Hello</body></html>"),
        ("noscript01.dat#3", "<head><noscript></noscript>",
            "<html><head><noscript></noscript></head><body></body></html>"),
        ("noscript01.dat#4", "<head><noscript>   </noscript>",
            "<html><head><noscript>   </noscript></head><body></body></html>")
    ];

    [Test]
    public void PinnedHtml5libSubsetMatchesDocumentTrees()
    {
        Html5libSubset.Length.Should().Be(14);
        foreach (var (id, source, expected) in Html5libSubset)
        {
            var parsed = Parse(source);
            Assert.Multiple(() =>
            {
                Assert.That(parsed.Step.Kind, Is.EqualTo(HtmlParseStepKind.Complete), id);
                Assert.That(Serialize(parsed.Document), Is.EqualTo(expected), id);
            });
        }
    }
}
