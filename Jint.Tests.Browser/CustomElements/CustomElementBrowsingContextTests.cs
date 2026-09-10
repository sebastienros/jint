using Jint.Browser;

namespace Jint.Tests.Browser.CustomElements;

// The test namespace sits under Jint.Tests.Browser, so the bare name Browser binds to that namespace rather
// than to the type. The alias belongs inside the namespace declaration, where it wins that lookup.
using Browser = global::Jint.Browser.Browser;

/// <summary>
/// <a href="https://html.spec.whatwg.org/multipage/custom-elements.html#look-up-a-custom-element-definition">Look
/// up a custom element definition</a> step 1 — "if document's browsing context is null, then return null" —
/// which makes every creation, clone, parse and upgrade in a document nothing is showing answer "no
/// definition", and the element come back uncustomized.
/// </summary>
/// <remarks>
/// The four spellings of such a document are <c>DOMImplementation.createHTMLDocument</c>,
/// <c>DOMImplementation.createDocument</c>, <c>new Document()</c> and <c>DOMParser</c>. Each is the active
/// document of no browsing context, and the gate is one: it is at the lookup, so the six paths that consult
/// a definition need no test of their own for it.
/// </remarks>
public sealed class CustomElementBrowsingContextTests
{
    private static async Task<Page> PageWith(Browser browser, string body)
    {
        var page = await browser.NewPageAsync();
        await page.SetContentAsync(body);
        return page;
    }

    [TestCase("document.implementation.createHTMLDocument('t')")]
    [TestCase("document.implementation.createDocument(null, 'root', null)")]
    [TestCase("new Document()")]
    [TestCase("new DOMParser().parseFromString('<p></p>', 'text/html')")]
    public async Task CreateElementAnswersAnUncustomizedElementForADocumentWithNoBrowsingContext(string spelling)
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
              const other = SPELLING;
              const el = other.createElement('x-thing');
              window.log.push(
                'instance=' + (el instanceof Thing),
                'owner=' + (el.ownerDocument === other),
                'name=' + el.localName);
            </script>
            """.Replace("SPELLING", spelling, StringComparison.Ordinal));

        (await page.EvaluateAsync<string>("window.log.join('|')"))
            .Should().Be("instance=false|owner=true|name=x-thing");
        page.Errors.Should().BeEmpty();
    }

    [Test]
    public async Task ACustomizedBuiltInIsNotUpgradedInADocumentWithNoBrowsingContext()
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
              const other = document.implementation.createHTMLDocument('t');
              const el = other.createElement('button', { is: 'fancy-button' });
              window.log.push(
                'instance=' + (el instanceof FancyButton),
                'owner=' + (el.ownerDocument === other),
                'name=' + el.localName);
            </script>
            """);

        (await page.EvaluateAsync<string>("window.log.join('|')"))
            .Should().Be("instance=false|owner=true|name=button");
        page.Errors.Should().BeEmpty();
    }

    [Test]
    public async Task ACloneOfAnElementADocumentWithNoBrowsingContextHoldsIsNotUpgraded()
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
              const parsed = new DOMParser().parseFromString('<x-thing></x-thing>', 'text/html');
              const source = parsed.querySelector('x-thing');
              const copy = source.cloneNode(true);
              window.log.push(
                'source=' + (source instanceof Thing),
                'copy=' + (copy instanceof Thing),
                'owner=' + (copy.ownerDocument === parsed));
            </script>
            """);

        (await page.EvaluateAsync<string>("window.log.join('|')"))
            .Should().Be("source=false|copy=false|owner=true");
        page.Errors.Should().BeEmpty();
    }

    [Test]
    public async Task MarkupParsedIntoADocumentWithNoBrowsingContextIsNotUpgraded()
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
              const other = document.implementation.createHTMLDocument('t');
              const host = other.createElement('div');
              host.innerHTML = '<x-thing></x-thing>';
              window.log.push('parsed=' + (host.firstElementChild instanceof Thing));
              // Insertion into that document reaches no definition either, and neither does the member
              // whose whole purpose is to upgrade a subtree.
              other.body.appendChild(host);
              window.log.push('inserted=' + (host.firstElementChild instanceof Thing));
              customElements.upgrade(other.body);
              window.log.push('upgraded=' + (host.firstElementChild instanceof Thing));
            </script>
            """);

        (await page.EvaluateAsync<string>("window.log.join('|')"))
            .Should().Be("parsed=false|inserted=false|upgraded=false");
        page.Errors.Should().BeEmpty();
    }

    [Test]
    public async Task AnElementCreatedInSuchADocumentIsUndefinedRatherThanFailedAndUpgradesInThePage()
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
              const other = document.implementation.createHTMLDocument('t');
              const made = other.createElement('x-thing');
              // The element is in DOM's "undefined" state and not the failed one, which is what lets the
              // page upgrade it once it becomes the page's: a constructor that ran and produced the wrong
              // element would have failed it for good.
              const moved = document.adoptNode(made);
              document.getElementById('host').appendChild(moved);
              window.log.push('upgraded=' + (moved instanceof Thing));
            </script>
            """);

        (await page.EvaluateAsync<string>("window.log.join('|')"))
            .Should().Be("ctor|connected|upgraded=true");
        page.Errors.Should().BeEmpty();
    }

    [Test]
    public async Task AnAlreadyCustomElementAdoptedIntoSuchADocumentStillRunsAdoptedCallback()
    {
        await using var browser = new Browser();
        var page = await PageWith(browser,
            """
            <script>
              window.log = [];
              class Thing extends HTMLElement {
                constructor() { super(); window.log.push('ctor'); }
                adoptedCallback(from, to) { window.log.push('adopted:' + (from === document) + ':' + (to === window.other)); }
              }
              customElements.define('x-thing', Thing);
              window.other = document.implementation.createHTMLDocument('t');
              const custom = document.createElement('x-thing');
              // https://dom.spec.whatwg.org/#concept-node-adopt step 3.2 has no browsing-context clause: it
              // enqueues the callback for every element of the subtree that is *already* custom, and this
              // one became custom in the page. The gate is on looking a definition up, not on a reaction
              // for an element that already has one.
              window.other.adoptNode(custom);
              window.log.push('still=' + (custom instanceof Thing));
            </script>
            """);

        (await page.EvaluateAsync<string>("window.log.join('|')"))
            .Should().Be("ctor|adopted:true:true|still=true");
        page.Errors.Should().BeEmpty();
    }

    [Test]
    public async Task AdoptingIntoADocumentWithNoBrowsingContextConstructsNothing()
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
              const other = document.implementation.createHTMLDocument('t');
              // The node document is what the lookup is asked about, so an element the page made and this
              // document adopted parses its own markup against no definition at all.
              const adopted = other.adoptNode(document.createElement('span'));
              adopted.innerHTML = '<x-thing></x-thing>';
              window.log.push(
                'owner=' + (adopted.ownerDocument === other),
                'inner=' + (adopted.firstElementChild instanceof Thing));
            </script>
            """);

        (await page.EvaluateAsync<string>("window.log.join('|')"))
            .Should().Be("owner=true|inner=false");
        page.Errors.Should().BeEmpty();
    }

    [Test]
    public async Task AdoptingOutOfSuchADocumentUpgradesOnInsertionAsItDidBefore()
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
              window.log.push('source=' + (source instanceof Thing));
              const out = document.adoptNode(source);
              // Adopting moves the node; DOM upgrades on insertion, and that is where the page's own
              // definition is reached.
              window.log.push('adopted=' + (out instanceof Thing), 'owner=' + (out.ownerDocument === document));
              document.getElementById('host').appendChild(out);
              window.log.push('inserted=' + (out instanceof Thing));
            </script>
            """);

        (await page.EvaluateAsync<string>("window.log.join('|')"))
            .Should().Be("source=false|adopted=false|owner=true|ctor|connected|inserted=true");
        page.Errors.Should().BeEmpty();
    }

    [Test]
    public async Task AnImportedNodeFromSuchADocumentIsStillUpgradedInThePage()
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
              const parsed = new DOMParser().parseFromString('<div><x-thing></x-thing></div>', 'text/html');
              const source = parsed.querySelector('div');
              window.log.push('source=' + (source.firstElementChild instanceof Thing));
              const copy = document.importNode(source, true);
              window.log.push(
                'copy=' + (copy.firstElementChild instanceof Thing),
                'owner=' + (copy.ownerDocument === document));
            </script>
            """);

        (await page.EvaluateAsync<string>("window.log.join('|')"))
            .Should().Be("source=false|ctor|copy=true|owner=true");
        page.Errors.Should().BeEmpty();
    }

    [Test]
    public async Task ThePagesOwnDocumentStillReachesEveryDefinitionItRegistered()
    {
        await using var browser = new Browser();
        var page = await PageWith(browser,
            """
            <x-parsed></x-parsed>
            <script>
              window.log = [];
              class Thing extends HTMLElement {
                constructor() { super(); window.log.push('ctor'); }
              }
              class Parsed extends HTMLElement {}
              customElements.define('x-thing', Thing);
              customElements.define('x-parsed', Parsed);
              window.log.push('parsed=' + (document.querySelector('x-parsed') instanceof Parsed));
              window.log.push('create=' + (document.createElement('x-thing') instanceof Thing));
              window.log.push('createNS=' + (document.createElementNS('http://www.w3.org/1999/xhtml', 'x-thing') instanceof Thing));
              window.log.push('new=' + (new Thing() instanceof Thing));
              const host = document.createElement('div');
              host.innerHTML = '<x-thing></x-thing>';
              window.log.push('innerHTML=' + (host.firstElementChild instanceof Thing));
              window.log.push('clone=' + (host.firstElementChild.cloneNode() instanceof Thing));
            </script>
            """);

        (await page.EvaluateAsync<string>("window.log.join('|')"))
            .Should().Be(
                "parsed=true|ctor|create=true|ctor|createNS=true|ctor|new=true|ctor|innerHTML=true|ctor|clone=true");
        page.Errors.Should().BeEmpty();
    }

    [Test]
    public async Task DefineIsUnaffectedBecauseItIsTheLookupThatGates()
    {
        await using var browser = new Browser();
        var page = await PageWith(browser,
            """
            <script>
              window.log = [];
              class Thing extends HTMLElement {}
              const other = document.implementation.createHTMLDocument('t');
              const host = other.createElement('div');
              host.innerHTML = '<x-thing></x-thing>';
              customElements.define('x-thing', Thing);
              customElements.whenDefined('x-thing').then(c => window.log.push('whenDefined=' + (c === Thing)));
              window.log.push(
                'get=' + (customElements.get('x-thing') === Thing),
                'getName=' + customElements.getName(Thing),
                'secondary=' + (host.firstElementChild instanceof Thing),
                'page=' + (document.createElement('x-thing') instanceof Thing));
            </script>
            """);

        (await page.EvaluateAsync<string>("window.log.join('|')"))
            .Should().Be("get=true|getName=x-thing|secondary=false|page=true|whenDefined=true");
        page.Errors.Should().BeEmpty();
    }
}
