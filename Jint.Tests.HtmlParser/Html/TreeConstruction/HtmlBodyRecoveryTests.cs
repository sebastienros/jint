#nullable enable
using Jint.HtmlParser.Html;

namespace Jint.Tests.HtmlParser.Html.TreeConstruction;

public partial class HtmlTreeConstructionTests
{
    [TestCase("<head></head><title>T</title><p>B",
        "<html><head><title>T</title></head><body><p>B</p></body></html>")]
    [TestCase("<body><title>T</title><p>B",
        "<html><head></head><body><title>T</title><p>B</p></body></html>")]
    [TestCase("<body><p>x</div>y",
        "<html><head></head><body><p>xy</p></body></html>")]
    [TestCase("<body><foo><div></foo>x",
        "<html><head></head><body><foo><div>x</div></foo></body></html>")]
    [TestCase("<body><foo><span></foo>x",
        "<html><head></head><body><foo><span></span></foo>x</body></html>")]
    [TestCase("<body><form><span>x</form>y",
        "<html><head></head><body><form><span>xy</span></form></body></html>")]
    [TestCase("<body><button><p>x<button>y",
        "<html><head></head><body><button><p>x</p></button><button>y</button></body></html>")]
    [TestCase("<body><dl><dt>a<dd>b",
        "<html><head></head><body><dl><dt>a</dt><dd>b</dd></dl></body></html>")]
    [TestCase("<body><image src=x><p/>X",
        "<html><head></head><body><img></img><p>X</p></body></html>")]
    [TestCase("<head><noscript>abc</noscript>",
        "<html><head><noscript></noscript></head><body>abc</body></html>")]
    [TestCase("<title>A</titlEzzz>B</title>",
        "<html><head><title>A</titlEzzz>B</title></head><body></body></html>")]
    [TestCase("<style>&amp;</style>",
        "<html><head><style>&amp;</style></head><body></body></html>")]
    [TestCase("<plaintext><b>&amp;",
        "<html><head></head><body><plaintext><b>&amp;</plaintext></body></html>")]
    public void SupportedRecoveryFamiliesProduceExpectedTrees(string source, string expected)
    {
        var parsed = Parse(source);
        parsed.Step.Kind.Should().Be(HtmlParseStepKind.Complete);
        Serialize(parsed.Document).Should().Be(expected);
    }

    [Test]
    public void ScriptingModeChangesNoscriptGrammarButDoesNotExecuteScripts()
    {
        const string source = "<head><noscript><b>literal</noscript></head><body><noscript><i>more</noscript>";
        var disabled = Parse(source);
        // The head noscript closes on the unexpected start tag. Its following
        // formatting token is deliberately a MissingFeature, so only the
        // scripting-enabled parse is complete for this source.
        disabled.Step.Kind.Should().Be(HtmlParseStepKind.MissingFeature);
        disabled.Step.MissingFeature.Should().Be(HtmlMissingFeature.Formatting);
        var enabled = Parse(source, options: new Jint.HtmlParser.HtmlParseOptions { ScriptingEnabled = true });
        enabled.Step.Kind.Should().Be(HtmlParseStepKind.Complete);
        Serialize(enabled.Document).Should().Be(
            "<html><head><noscript><b>literal</noscript></head><body><noscript><i>more</noscript></body></html>");
    }

    [Test]
    public void TextEofRestoresModeAndReportsError()
    {
        var parsed = Parse("<script><b>literal");
        parsed.Step.Kind.Should().Be(HtmlParseStepKind.Complete);
        Serialize(parsed.Document).Should().Be(
            "<html><head><script><b>literal</script></head><body></body></html>");
        parsed.Diagnostics.Items.Any(item => item.Code == "html/tree-eof-in-text").Should().BeTrue();
    }
}
