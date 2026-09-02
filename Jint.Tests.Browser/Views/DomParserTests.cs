namespace Jint.Tests.Browser.Views;

// The test namespace sits under Jint.Tests.Browser, so the bare name Browser binds to that namespace rather
// than to the type. The alias belongs inside the namespace declaration, where it wins that lookup.
using Browser = global::Jint.Browser.Browser;

/// <summary>
/// <c>DOMParser</c> and <c>XMLSerializer</c>: markup into a document that cannot run, and back out again.
/// </summary>
public sealed class DomParserTests
{
    [Test]
    public async Task HtmlIsParsedIntoADocumentTheWrapperUnderstands()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();

        await page.SetContentAsync(
            """
            <script>
              const doc = new DOMParser().parseFromString('<p id="x">hi</p>', 'text/html');
              window.log = [
                doc instanceof Document,
                doc.nodeType,
                doc.getElementById('x').textContent,
                doc.querySelector('p').tagName,
                doc.documentElement.tagName,
                doc !== document,
              ].join('|');
            </script>
            """);

        (await page.EvaluateAsync<string>("window.log")).Should().Be("true|9|hi|P|HTML|true");
        page.Errors.Should().BeEmpty();
    }

    [Test]
    public async Task AParsedDocumentDoesNotRunItsScripts()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();

        await page.SetContentAsync(
            """
            <script>
              window.ran = false;
              const doc = new DOMParser().parseFromString('<script>window.ran = true<\/script><p>after</p>', 'text/html');
              window.log = [doc.querySelector('script') !== null, doc.querySelector('p').textContent, window.ran].join('|');
            </script>
            """);

        // The script element is in the tree with its text, and it never ran: a DOMParser document has no
        // browsing context, which is what the standard requires.
        (await page.EvaluateAsync<string>("window.log")).Should().Be("true|after|false");
        (await page.WaitForIdleAsync(TimeSpan.FromSeconds(5))).Should().BeTrue();
        (await page.EvaluateAsync<bool>("window.ran")).Should().BeFalse();
    }

    [Test]
    public async Task XmlIsParsedWithTheXmlParser()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();

        await page.SetContentAsync(
            """
            <script>
              const doc = new DOMParser().parseFromString('<root><item id="a">one</item></root>', 'text/xml');
              window.log = [
                doc.documentElement.tagName,
                doc.querySelector('item').textContent,
                doc.querySelector('parsererror') === null,
              ].join('|');
            </script>
            """);

        (await page.EvaluateAsync<string>("window.log")).Should().Be("root|one|true");
    }

    [Test]
    public async Task MalformedXmlAnswersAParsererrorDocument()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();

        await page.SetContentAsync(
            """
            <script>
              const doc = new DOMParser().parseFromString('<root><unclosed></root>', 'application/xml');
              const error = doc.getElementsByTagName('parsererror')[0];
              window.log = [error !== undefined, error && error.textContent.length > 0].join('|');
            </script>
            """);

        (await page.EvaluateAsync<string>("window.log")).Should().Be("true|true");
        page.Errors.Should().BeEmpty();
    }

    [Test]
    public async Task AnUnknownContentTypeIsATypeError()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();

        (await page.EvaluateAsync<string>(
            "(() => { try { new DOMParser().parseFromString('<p/>', 'text/plain'); return 'no throw' } catch (e) { return e.constructor.name } })()"))
            .Should().Be("TypeError");
    }

    [Test]
    public async Task TheFourXmlContentTypesAreAllAccepted()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();

        foreach (var type in new[] { "text/xml", "application/xml", "application/xhtml+xml", "image/svg+xml" })
        {
            (await page.EvaluateAsync<string>(
                "new DOMParser().parseFromString('<root/>', '" + type + "').documentElement.tagName"))
                .Should().Be("root", "{0} is a DOMParserSupportedType", type);
        }
    }

    [Test]
    public async Task ANodeRoundTripsThroughTheSerializerAndTheParser()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();

        await page.SetContentAsync(
            """
            <script>
              const serializer = new XMLSerializer();
              const first = new DOMParser().parseFromString('<root><item a="1">text</item><empty/></root>', 'text/xml');
              const markup = serializer.serializeToString(first.documentElement);
              const second = new DOMParser().parseFromString(markup, 'text/xml');
              window.markup = markup;
              window.again = serializer.serializeToString(second.documentElement);
            </script>
            """);

        var markup = await page.EvaluateAsync<string>("window.markup");
        markup.Should().Contain("<item a=\"1\">text</item>");
        (await page.EvaluateAsync<string>("window.again")).Should().Be(markup);
    }

    [Test]
    public async Task TheSerializerWritesTheLivingDocumentToo()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();

        await page.SetContentAsync("<div id='d'><br></div>");

        // XML serialization closes every element, so a void element comes out self-closed rather than bare.
        (await page.EvaluateAsync<string>("new XMLSerializer().serializeToString(document.getElementById('d'))"))
            .Should().Be("<div id=\"d\"><br /></div>");
    }

    [Test]
    public async Task BothInterfacesAreConstructibleAndBranded()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();

        (await page.EvaluateAsync<bool>("new DOMParser() instanceof DOMParser")).Should().BeTrue();
        (await page.EvaluateAsync<bool>("new XMLSerializer() instanceof XMLSerializer")).Should().BeTrue();
        (await page.EvaluateAsync<string>("Object.prototype.toString.call(new DOMParser())")).Should().Be("[object DOMParser]");

        (await page.EvaluateAsync<string>(
            "(() => { try { DOMParser.prototype.parseFromString.call({}, '<p/>', 'text/html'); return 'no throw' } catch (e) { return e.constructor.name } })()"))
            .Should().Be("TypeError");
    }
}
