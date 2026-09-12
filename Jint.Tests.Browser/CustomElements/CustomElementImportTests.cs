using Jint.Browser;

namespace Jint.Tests.Browser.CustomElements;

// The test namespace sits under Jint.Tests.Browser, so the bare name Browser binds to that namespace rather
// than to the type. The alias belongs inside the namespace declaration, where it wins that lookup.
using Browser = global::Jint.Browser.Browser;

/// <summary>
/// <c>document.importNode</c>, which https://dom.spec.whatwg.org/#dom-document-importnode defines as "the
/// result of cloning a node given node with <b>document set to this</b>" — so the copy is created in this
/// document, with the source's is value, and upgraded there.
/// </summary>
public sealed class CustomElementImportTests
{
    private static async Task<Page> PageWith(Browser browser, string body)
    {
        var page = await browser.NewPageAsync();
        await page.SetContentAsync(body);
        return page;
    }

    [Test]
    public async Task AnImportedAutonomousElementIsConstructedInThisDocument()
    {
        await using var browser = new Browser();
        var page = await PageWith(browser,
            """
            <script>
              window.log = [];
              class Thing extends HTMLElement {
                constructor() { super(); window.log.push('ctor:' + (this.ownerDocument === document)); }
              }
              customElements.define('x-thing', Thing);
              const source = document.createElement('div');
              source.innerHTML = '<x-thing id="deep"></x-thing>';
              window.log.length = 0;
              const copy = document.importNode(source, true);
              const imported = copy.firstElementChild;
              window.log.push(imported instanceof Thing, imported.ownerDocument === document);
            </script>
            """);

        (await page.EvaluateAsync<string>("window.log.join('|')")).Should().Be("ctor:true|true|true");
        page.Errors.Should().BeEmpty();
    }

    [Test]
    public async Task AShallowImportUpgradesTheElementItselfAndCopiesNoChild()
    {
        await using var browser = new Browser();
        var page = await PageWith(browser,
            """
            <script>
              window.log = [];
              class Thing extends HTMLElement {
                constructor() { super(); window.log.push('ctor'); }
              }
              customElements.define('x-thing', Thing);
              const source = document.createElement('x-thing');
              source.appendChild(document.createElement('span'));
              window.log.length = 0;
              const shallow = document.importNode(source);
              window.log.push(shallow instanceof Thing, shallow.childNodes.length);
            </script>
            """);

        (await page.EvaluateAsync<string>("window.log.join('|')")).Should().Be("ctor|true|0");
        page.Errors.Should().BeEmpty();
    }

    [Test]
    public async Task AnImportedCustomizedBuiltInKeepsItsIsValue()
    {
        await using var browser = new Browser();
        var page = await PageWith(browser,
            """
            <script>
              window.log = [];
              class FancyButton extends HTMLButtonElement {
                constructor() { super(); window.log.push('ctor'); }
              }
              customElements.define('fancy-button', FancyButton, { extends: 'button' });
              // The is value is a slot rather than a content attribute, so nothing about the markup carries it.
              const source = document.createElement('button', { is: 'fancy-button' });
              window.log.length = 0;
              const imported = document.importNode(source);
              window.log.push(imported instanceof FancyButton, imported.localName, String(imported.getAttribute('is')));
            </script>
            """);

        (await page.EvaluateAsync<string>("window.log.join('|')")).Should().Be("ctor|true|button|null");
        page.Errors.Should().BeEmpty();
    }

    [Test]
    public async Task AnElementImportedFromAParsedDocumentIsUpgradedByThisDocumentsRegistry()
    {
        await using var browser = new Browser();
        var page = await PageWith(browser,
            """
            <div id="host"></div>
            <script>
              window.log = [];
              class Thing extends HTMLElement {
                constructor() { super(); window.log.push('ctor'); }
                connectedCallback() { window.log.push('connected'); }
              }
              customElements.define('x-thing', Thing);
              const parsed = new DOMParser().parseFromString('<x-thing></x-thing>', 'text/html');
              const source = parsed.querySelector('x-thing');
              // A parsed document has no browsing context, so its own element is never upgraded.
              window.log.push('parsed:' + (source instanceof Thing));
              const imported = document.importNode(source, true);
              window.log.push(imported instanceof Thing, imported.ownerDocument === document);
              document.getElementById('host').appendChild(imported);
            </script>
            """);

        (await page.EvaluateAsync<string>("window.log.join('|')"))
            .Should().Be("parsed:false|ctor|true|true|connected");
        page.Errors.Should().BeEmpty();
    }

    [Test]
    public async Task AnOrdinaryImportOfANonCustomTreeIsUnchanged()
    {
        await using var browser = new Browser();
        var page = await PageWith(browser,
            """
            <script>
              window.log = [];
              class Thing extends HTMLElement { constructor() { super(); window.log.push('ctor'); } }
              customElements.define('x-thing', Thing);
              const source = document.createElement('section');
              source.innerHTML = '<p class="a">text</p>';
              window.log.length = 0;
              const imported = document.importNode(source, true);
              const ranNoConstructor = window.log.length === 0;
              window.log.push(
                imported.outerHTML,
                imported.ownerDocument === document,
                imported !== source,
                ranNoConstructor);
            </script>
            """);

        (await page.EvaluateAsync<string>("window.log.join('|')"))
            .Should().Be("<section><p class=\"a\">text</p></section>|true|true|true");
        page.Errors.Should().BeEmpty();
    }
}
