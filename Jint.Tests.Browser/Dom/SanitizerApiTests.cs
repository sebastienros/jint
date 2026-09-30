using Jint.Browser.Events;
using Jint.HtmlParser.Sanitization;

namespace Jint.Tests.Browser.Dom;

using Browser = global::Jint.Browser.Browser;

// HTML §8.6, https://html.spec.whatwg.org/multipage/dynamic-markup-insertion.html#html-sanitization-api;
// upstream wpt sanitizer-api is the conformance source, these pin the Browser projection.
public sealed class SanitizerApiTests
{
    [Test]
    public void SetHtmlUsesTheSafeDefault()
    {
        using var fixture = DomTestFixture.Create("<main></main>");
        fixture.Execute("var m = document.querySelector('main'); m.setHTML(\"<p onclick='x()'>a<script>b</script><b>c</b></p><!--d-->\");");
        fixture.Text("m.innerHTML").Should().Be("<p>a<b>c</b></p>");
    }

    [Test]
    public void SetHtmlUnsafeKeepsEverythingWithoutASanitizer()
    {
        using var fixture = DomTestFixture.Create("<main></main>");
        fixture.Execute("var m = document.querySelector('main'); m.setHTMLUnsafe(\"<p onclick='x()'>a</p><!--d-->\");");
        fixture.Text("m.innerHTML").Should().Be("<p onclick=\"x()\">a</p><!--d-->");
    }

    [Test]
    public async Task SafeMethodsRemoveUnsafeEvenFromAPermissiveSanitizer()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<main></main>");
        await page.EvaluateAsync("""
            var m = document.querySelector('main');
            var s = new Sanitizer({ elements: ['p', 'script'], attributes: ['onclick'] });
            m.setHTML("<p onclick='x()'>a</p><script>b</script>", { sanitizer: s });
            """);
        (await page.EvaluateAsync<string>("m.innerHTML")).Should().Be("<p>a</p>");
        (await page.EvaluateAsync<double>("s.get().elements.length")).Should().Be(2);
        (await page.EvaluateAsync<string>("m.setHTMLUnsafe(\"<p onclick='x()'>a</p><script>b</script><i>c</i>\", { sanitizer: s }), m.innerHTML"))
            .Should().Be("<p onclick=\"x()\">a</p><script>b</script>");
    }

    [Test]
    public async Task SanitizerModifiersAndGet()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<main></main>");
        await page.EvaluateAsync("var s = new Sanitizer({}); s.removeElement('b'); s.replaceElementWithChildren('i'); s.setComments(false);");
        (await page.EvaluateAsync<string>("JSON.stringify(s.get().removeElements)"))
            .Should().Be("[{\"name\":\"b\",\"namespace\":\"http://www.w3.org/1999/xhtml\"}]");
        (await page.EvaluateAsync<bool>("s.get().comments")).Should().BeFalse();
        (await page.EvaluateAsync<string>("var m = document.querySelector('main'); m.setHTMLUnsafe('<b>1</b><i>2</i><!--3-->', { sanitizer: s }); m.innerHTML"))
            .Should().Be("2");
        (await page.EvaluateAsync<bool>("(() => { try { new Sanitizer({ elements: [], removeElements: [] }); return false; } catch (e) { return e instanceof TypeError; } })()"))
            .Should().BeTrue();
    }

    [Test]
    public void ParseHtmlCreatesASanitizedDocument()
    {
        using var fixture = DomTestFixture.Create("<main></main>");
        fixture.Execute("var d = Document.parseHTML('<title>t</title><p>a<script>b</script></p>');");
        fixture.Text("d.body.innerHTML").Should().Be("<p>a</p>");
        fixture.Text("d.title").Should().Be("t");
        fixture.Bool("d instanceof Document && d !== document").Should().BeTrue();
        fixture.Text("Document.parseHTMLUnsafe('<p>a<script>b</script></p>').body.innerHTML").Should().Be("<p>a<script>b</script></p>");
    }

    [Test]
    public void DeclarativeShadowRootsAreParsedSanitizedAndSerialized()
    {
        using var fixture = DomTestFixture.Create("<main></main>");
        fixture.Execute("""
            var m = document.querySelector('main');
            m.setHTML("<div><template shadowrootmode='open' shadowrootserializable><b>x</b><script>y</script></template></div>");
            """);
        fixture.Text("m.firstChild.shadowRoot.innerHTML").Should().Be("<b>x</b>");
        fixture.Text("m.getHTML({ serializableShadowRoots: true })")
            .Should().Be("<div><template shadowrootmode=\"open\" shadowrootserializable=\"\"><b>x</b></template></div>");
        fixture.Text("m.getHTML()").Should().Be("<div></div>");
    }

    [Test]
    public void SetHtmlOnAScriptElementDoesNothing()
    {
        using var fixture = DomTestFixture.Create("<main></main>");
        fixture.Execute("var sc = document.createElement('script'); sc.setHTML('alert(1)');");
        fixture.Text("sc.textContent").Should().Be("");
    }

    [Test]
    public void SanitizerHandlerListCoversEveryBrowserEventHandlerAttribute()
    {
        var sanitizer = new HashSet<string>(SanitizerBuiltins.EventHandlerContentAttributes, StringComparer.Ordinal);
        var missing = EventHandlerContentAttributes.ElementHandlers
            .Concat(EventHandlerContentAttributes.BodyHandlersOwnedByTheWindow)
            .Concat(EventHandlerContentAttributes.TouchHandlers)
            .Select(type => "on" + type)
            .Where(name => !sanitizer.Contains(name))
            .ToArray();
        missing.Should().BeEmpty("the safe sanitizer must strip every handler the Browser would compile");
    }
}
