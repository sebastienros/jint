using Jint.Browser;

namespace Jint.Tests.Browser.CustomElements;

// The test namespace sits under Jint.Tests.Browser, so the bare name Browser binds to that namespace rather
// than to the type. The alias belongs inside the namespace declaration, where it wins that lookup.
using Browser = global::Jint.Browser.Browser;

/// <summary>
/// <c>adoptedCallback</c>: https://dom.spec.whatwg.org/#concept-node-adopt step 3.2 enqueues one for every
/// custom element in an adopted subtree, with « oldDocument, newDocument ».
/// </summary>
public sealed class CustomElementAdoptionTests
{
    private static async Task<Page> PageWith(Browser browser, string body)
    {
        var page = await browser.NewPageAsync();
        await page.SetContentAsync(body);
        return page;
    }

    [Test]
    public async Task AdoptingADetachedElementRunsTheCallbackWithBothDocuments()
    {
        await using var browser = new Browser();
        var page = await PageWith(browser,
            """
            <script>
              window.log = [];
              class Thing extends HTMLElement {
                constructor() { super(); window.log.push('constructed'); }
                adoptedCallback(oldDocument, newDocument) {
                  window.log.push('adopted:' + arguments.length + ':' + (oldDocument === document) + ':' + (newDocument === window.other));
                }
              }
              customElements.define('x-thing', Thing);
              const other = document.implementation.createHTMLDocument();
              window.other = other;
              const instance = document.createElement('x-thing');
              window.log.push('--created--');
              other.adoptNode(instance);
              window.log.push('owner:' + (instance.ownerDocument === other));
            </script>
            """);

        (await page.EvaluateAsync<string>("window.log.join('|')"))
            .Should().Be("constructed|--created--|adopted:2:true:true|owner:true");
        page.Errors.Should().BeEmpty();
    }

    [Test]
    public async Task AdoptingAConnectedElementDisconnectsItFirst()
    {
        await using var browser = new Browser();
        var page = await PageWith(browser,
            """
            <div id="host"></div>
            <script>
              window.log = [];
              class Thing extends HTMLElement {
                connectedCallback() { window.log.push('connected'); }
                disconnectedCallback() { window.log.push('disconnected'); }
                adoptedCallback() { window.log.push('adopted'); }
              }
              customElements.define('x-thing', Thing);
              const other = document.implementation.createHTMLDocument();
              const instance = document.createElement('x-thing');
              document.getElementById('host').appendChild(instance);
              other.adoptNode(instance);
              window.log.push('parent:' + (instance.parentNode === null));
            </script>
            """);

        (await page.EvaluateAsync<string>("window.log.join('|')"))
            .Should().Be("connected|disconnected|adopted|parent:true");
        page.Errors.Should().BeEmpty();
    }

    [Test]
    public async Task AdoptingBackIntoThisDocumentReversesTheTwoArguments()
    {
        await using var browser = new Browser();
        var page = await PageWith(browser,
            """
            <script>
              window.log = [];
              class Thing extends HTMLElement {
                adoptedCallback(oldDocument, newDocument) {
                  window.log.push((oldDocument === window.other) + ':' + (newDocument === document));
                }
              }
              customElements.define('x-thing', Thing);
              const other = document.implementation.createHTMLDocument();
              window.other = other;
              const instance = document.createElement('x-thing');
              other.adoptNode(instance);
              window.log.length = 0;
              document.adoptNode(instance);
            </script>
            """);

        (await page.EvaluateAsync<string>("window.log.join('|')")).Should().Be("true:true");
        page.Errors.Should().BeEmpty();
    }

    [Test]
    public async Task EveryCustomElementOfTheAdoptedSubtreeRunsItInTreeOrder()
    {
        await using var browser = new Browser();
        var page = await PageWith(browser,
            """
            <script>
              window.log = [];
              class Thing extends HTMLElement {
                adoptedCallback() { window.log.push('adopted:' + this.id); }
              }
              customElements.define('x-thing', Thing);
              const other = document.implementation.createHTMLDocument();
              const root = document.createElement('div');
              root.innerHTML = '<x-thing id="a"></x-thing><div><x-thing id="b"></x-thing></div><x-thing id="c"></x-thing>';
              other.adoptNode(root);
              window.log.push('owner:' + (root.firstElementChild.ownerDocument === other));
            </script>
            """);

        (await page.EvaluateAsync<string>("window.log.join('|')"))
            .Should().Be("adopted:a|adopted:b|adopted:c|owner:true");
        page.Errors.Should().BeEmpty();
    }

    [Test]
    public async Task AdoptingIntoTheDocumentTheNodeAlreadyBelongsToRunsNothing()
    {
        await using var browser = new Browser();
        var page = await PageWith(browser,
            """
            <script>
              window.log = [];
              class Thing extends HTMLElement {
                adoptedCallback() { window.log.push('adopted'); }
              }
              customElements.define('x-thing', Thing);
              const instance = document.createElement('x-thing');
              window.log.length = 0;
              const same = document.adoptNode(instance);
              window.log.push('same:' + (same === instance), 'silent:' + (window.log.length === 0));
            </script>
            """);

        (await page.EvaluateAsync<string>("window.log.join('|')")).Should().Be("same:true|silent:true");
        page.Errors.Should().BeEmpty();
    }

    [Test]
    public async Task TheDefinitionHoldsTheCallbackSoDeletingItFromThePrototypeChangesNothing()
    {
        await using var browser = new Browser();
        var page = await PageWith(browser,
            """
            <script>
              window.log = [];
              class Thing extends HTMLElement {
                adoptedCallback() { window.log.push('adopted'); }
              }
              customElements.define('x-thing', Thing);
              // HTML reads the four callbacks once, when the definition is created.
              delete Thing.prototype.adoptedCallback;
              const other = document.implementation.createHTMLDocument();
              other.adoptNode(document.createElement('x-thing'));
            </script>
            """);

        (await page.EvaluateAsync<string>("window.log.join('|')")).Should().Be("adopted");
        page.Errors.Should().BeEmpty();
    }

    [Test]
    public async Task AnOrdinaryElementAndAnUndefinedNameAdoptSilently()
    {
        await using var browser = new Browser();
        var page = await PageWith(browser,
            """
            <script>
              window.log = [];
              class Thing extends HTMLElement {
                adoptedCallback() { window.log.push('adopted'); }
              }
              customElements.define('x-thing', Thing);
              const other = document.implementation.createHTMLDocument();
              const plain = document.createElement('div');
              const undefinedName = document.createElement('x-other');
              other.adoptNode(plain);
              other.adoptNode(undefinedName);
              window.log.push(
                'silent:' + (window.log.length === 0),
                'plain:' + (plain.ownerDocument === other),
                'undefined:' + (undefinedName.ownerDocument === other));
            </script>
            """);

        (await page.EvaluateAsync<string>("window.log.join('|')"))
            .Should().Be("silent:true|plain:true|undefined:true");
        page.Errors.Should().BeEmpty();
    }

    [Test]
    public async Task ImportNodeEnqueuesNoAdoptedReaction()
    {
        // https://dom.spec.whatwg.org/#dom-document-importnode is "the result of cloning a node given node
        // with document set to this": the copy is created in this document rather than moved into it, so a
        // member that clones enqueues no adoptedCallback however the copy is then upgraded.
        await using var browser = new Browser();
        var page = await PageWith(browser,
            """
            <script>
              window.log = [];
              class Thing extends HTMLElement {
                adoptedCallback() { window.log.push('adopted'); }
              }
              customElements.define('x-thing', Thing);
              const other = new DOMParser().parseFromString('<x-thing></x-thing>', 'text/html');
              const source = other.querySelector('x-thing');
              const imported = document.importNode(source, true);
              window.log.push(
                'silent:' + (window.log.length === 0),
                'owner:' + (imported.ownerDocument === document),
                'sourceOwner:' + (source.ownerDocument === other));
            </script>
            """);

        (await page.EvaluateAsync<string>("window.log.join('|')"))
            .Should().Be("silent:true|owner:true|sourceOwner:true");
        page.Errors.Should().BeEmpty();
    }
}
