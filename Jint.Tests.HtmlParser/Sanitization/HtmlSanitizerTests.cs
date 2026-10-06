#nullable enable
using Jint.HtmlParser;
using Jint.HtmlParser.Html;
using Jint.HtmlParser.Sanitization;
using Jint.HtmlParser.Serialization;

namespace Jint.Tests.HtmlParser.Sanitization;

// Authored fixtures for HTML §8.6 (2026-09-29),
// https://html.spec.whatwg.org/multipage/dynamic-markup-insertion.html#sanitize.
[TestFixture]
public sealed class HtmlSanitizerTests
{
    private static string Sanitize(string markup, SanitizerConfiguration configuration, bool safe)
    {
        var document = Document.CreateHtml();
        var context = document.CreateElementNS(Namespaces.Html, "div");
        var fragment = HtmlParserSession.ParseFragment(markup, context);
        HtmlSanitizer.Sanitize(fragment, configuration, safe);
        return HtmlMarkupSerializer.SerializeChildren(fragment);
    }

    private static SanitizerConfiguration Empty(bool permissive = true)
    {
        var configuration = new SanitizerConfiguration();
        configuration.Canonicalize(permissive);
        return configuration;
    }

    [Test]
    public void SafeDefaultRemovesScriptsHandlersAndComments()
    {
        Sanitize("<p onclick='x()' title='t'>a<script>b</script><!--c--><b>d</b></p>", SanitizerBuiltins.SafeDefault(), safe: true)
            .Should().Be("<p title=\"t\">a<b>d</b></p>");
    }

    [Test]
    public void UnsafeWithEmptyConfigurationKeepsEverything()
    {
        const string Markup = "<p onclick=\"x()\">a<script>b</script><!--c--></p>";
        Sanitize(Markup, Empty(), safe: false).Should().Be(Markup);
    }

    [Test]
    public void SafeRemovesUnsafeEvenWhenConfigurationAllowsIt()
    {
        var configuration = new SanitizerConfiguration { Elements = [new(SanitizerName.Html("script")), new(SanitizerName.Html("p"))] };
        configuration.Canonicalize(permissiveDefaults: false);
        configuration.IsValid().Should().BeTrue();
        Sanitize("<p>a</p><script>b</script>", configuration, safe: true).Should().Be("<p>a</p>");
        configuration.Elements.Should().HaveCount(2, "sanitizing must not modify the caller's configuration");
    }

    [Test]
    public void ReplaceWithChildrenKeepsSanitizedChildren()
    {
        var configuration = new SanitizerConfiguration { ReplaceWithChildrenElements = [SanitizerName.Html("b")] };
        configuration.Canonicalize(permissiveDefaults: true);
        Sanitize("<p>1<b>2<i>3</i></b>4</p>", configuration, safe: false).Should().Be("<p>12<i>3</i>4</p>");
    }

    [Test]
    public void TemplateContentsAreSanitized()
    {
        var configuration = new SanitizerConfiguration { RemoveElements = [SanitizerName.Html("i")] };
        configuration.Canonicalize(permissiveDefaults: true);
        Sanitize("<template><i>x</i>y</template>", configuration, safe: false).Should().Be("<template>y</template>");
    }

    [Test]
    public void AttributeListsAndDataAttributes()
    {
        var configuration = new SanitizerConfiguration
        {
            Elements = [new(SanitizerName.Html("a"), attributes: [SanitizerName.Attribute("href")])],
            Attributes = [SanitizerName.Attribute("title")],
            DataAttributes = true,
        };
        configuration.Canonicalize(permissiveDefaults: false);
        configuration.IsValid().Should().BeTrue();
        Sanitize("<a href='/x' title='t' data-k='v' data-K='w' id='i'>x</a>", configuration, safe: false)
            .Should().Be("<a href=\"/x\" title=\"t\" data-k=\"v\">x</a>");
    }

    [TestCase("javascript:alert(1)", true)]
    [TestCase("  JaVa\tScRiPt:alert(1)", true)]
    [TestCase("\u0001javascript:x", true)]
    [TestCase("https://example.com/", false)]
    [TestCase("javascriptx:1", false)]
    [TestCase("/relative:javascript:", false)]
    public void ContainsJavascriptUrl(string value, bool expected)
        => HtmlSanitizer.ContainsJavascriptUrl(value).Should().Be(expected);

    [Test]
    public void JavascriptUrlsAreRemovedFromNavigatingAttributesOnly()
    {
        var configuration = new SanitizerConfiguration();
        configuration.Canonicalize(permissiveDefaults: true);
        configuration.JavascriptUrls = false;
        Sanitize("<a href='javascript:x' title='javascript:y'>a</a>", configuration, safe: false)
            .Should().Be("<a title=\"javascript:y\">a</a>");
    }

    [Test]
    public void CheckpointAndCancellationAreHonoured()
    {
        var document = Document.CreateHtml();
        var context = document.CreateElementNS(Namespaces.Html, "div");
        var fragment = HtmlParserSession.ParseFragment(string.Concat(Enumerable.Repeat("<b>x</b>", 400)), context);
        var checkpoints = 0;
        HtmlSanitizer.Sanitize(fragment, SanitizerBuiltins.SafeDefault(), safe: true, checkpoint: _ => checkpoints++);
        checkpoints.Should().BeGreaterThan(2);

        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Assert.Throws<OperationCanceledException>(() =>
            HtmlSanitizer.Sanitize(fragment, SanitizerBuiltins.SafeDefault(), safe: true, cancellationToken: cancellation.Token));
    }
}
