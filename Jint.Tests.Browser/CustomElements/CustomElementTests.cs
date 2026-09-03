using Jint.Browser;

namespace Jint.Tests.Browser.CustomElements;

// The test namespace sits under Jint.Tests.Browser, so the bare name Browser binds to that namespace rather
// than to the type. The alias belongs inside the namespace declaration, where it wins that lookup.
using Browser = global::Jint.Browser.Browser;

/// <summary>
/// The registry itself: <c>define</c> and its validation, <c>get</c>, <c>getName</c>, <c>whenDefined</c> and
/// the three creation paths that end in a constructed element.
/// </summary>
public sealed class CustomElementTests
{
    private static async Task<Page> PageWith(Browser browser, string body)
    {
        var page = await browser.NewPageAsync();
        await page.SetContentAsync(body);
        return page;
    }

    [Test]
    public async Task TheRegistryIsOnTheWindowAndIsTheSameObjectEveryTime()
    {
        await using var browser = new Browser();
        var page = await PageWith(browser, "<p></p>");

        (await page.EvaluateAsync<string>("typeof window.customElements")).Should().Be("object");
        (await page.EvaluateAsync<bool>("window.customElements === window.customElements")).Should().BeTrue();
        (await page.EvaluateAsync<bool>("customElements instanceof CustomElementRegistry")).Should().BeTrue();
        (await page.EvaluateAsync<string>("Object.prototype.toString.call(customElements)"))
            .Should().Be("[object CustomElementRegistry]");
    }

    [Test]
    public async Task DefineThenCreateElementRunsTheConstructorAndGivesTheElementTheConstructorsPrototype()
    {
        await using var browser = new Browser();
        var page = await PageWith(browser,
            """
            <script>
              window.log = [];
              class Thing extends HTMLElement {
                constructor() { super(); window.log.push('ctor:' + this.localName + ':' + this.isConnected); }
                hello() { return 'hi'; }
              }
              customElements.define('x-thing', Thing);
              const el = document.createElement('x-thing');
              window.log.push(el instanceof Thing, el instanceof HTMLElement, el.hello(), el.tagName);
            </script>
            """);

        (await page.EvaluateAsync<string>("window.log.join('|')"))
            .Should().Be("ctor:x-thing:false|true|true|hi|X-THING");
        page.Errors.Should().BeEmpty();
    }

    [Test]
    public async Task NewOfTheConstructorCreatesAnElementOfTheDefinitionsLocalName()
    {
        await using var browser = new Browser();
        var page = await PageWith(browser,
            """
            <script>
              class Thing extends HTMLElement {}
              customElements.define('x-thing', Thing);
              const el = new Thing();
              window.result = [el.localName, el.namespaceURI, el instanceof Thing, el.ownerDocument === document].join('|');
            </script>
            """);

        (await page.EvaluateAsync<string>("window.result"))
            .Should().Be("x-thing|http://www.w3.org/1999/xhtml|true|true");
        page.Errors.Should().BeEmpty();
    }

    [Test]
    public async Task TheHtmlElementConstructorIsNotCallableOnItsOwn()
    {
        await using var browser = new Browser();
        var page = await PageWith(browser,
            """
            <script>
              window.log = [];
              try { new HTMLElement(); } catch (e) { window.log.push(e.constructor.name); }
              class Unregistered extends HTMLElement {}
              try { new Unregistered(); } catch (e) { window.log.push(e.constructor.name); }
            </script>
            """);

        (await page.EvaluateAsync<string>("window.log.join('|')")).Should().Be("TypeError|TypeError");
    }

    [Test]
    public async Task GetAndGetNameAnswerBothDirectionsAndUndefinedForAnUnknownName()
    {
        await using var browser = new Browser();
        var page = await PageWith(browser,
            """
            <script>
              class Thing extends HTMLElement {}
              customElements.define('x-thing', Thing);
              window.result = [
                customElements.get('x-thing') === Thing,
                String(customElements.get('x-missing')),
                customElements.getName(Thing),
                String(customElements.getName(function () {}))
              ].join('|');
            </script>
            """);

        (await page.EvaluateAsync<string>("window.result")).Should().Be("true|undefined|x-thing|null");
    }

    [TestCase("nodash", "SyntaxError")]
    [TestCase("Uppercase-name", "SyntaxError")]
    [TestCase("-leading", "SyntaxError")]
    [TestCase("annotation-xml", "SyntaxError")]
    [TestCase("font-face", "SyntaxError")]
    [TestCase("", "SyntaxError")]
    public async Task AnInvalidNameIsASyntaxError(string name, string expected)
    {
        await using var browser = new Browser();
        var page = await PageWith(browser, "<p></p>");

        var thrown = await page.EvaluateAsync<string>(
            "(() => { try { customElements.define(" + System.Text.Json.JsonSerializer.Serialize(name)
            + ", class extends HTMLElement {}); return 'no throw'; } catch (e) { return e.name; } })()");

        thrown.Should().Be(expected);
    }

    [Test]
    public async Task ANameOrAConstructorUsedTwiceIsANotSupportedError()
    {
        await using var browser = new Browser();
        var page = await PageWith(browser,
            """
            <script>
              window.log = [];
              class A extends HTMLElement {}
              class B extends HTMLElement {}
              customElements.define('x-a', A);
              try { customElements.define('x-a', B); } catch (e) { window.log.push(e.name); }
              try { customElements.define('x-b', A); } catch (e) { window.log.push(e.name); }
              try { customElements.define('x-c', {}); } catch (e) { window.log.push(e.constructor.name); }
            </script>
            """);

        (await page.EvaluateAsync<string>("window.log.join('|')"))
            .Should().Be("NotSupportedError|NotSupportedError|TypeError");
    }

    [Test]
    public async Task WhenDefinedResolvesWithTheConstructorAndIsAlreadyResolvedForADefinedName()
    {
        await using var browser = new Browser();
        var page = await PageWith(browser,
            """
            <script>
              window.log = [];
              class Thing extends HTMLElement {}
              customElements.whenDefined('x-thing').then(c => window.log.push('pending:' + (c === Thing)));
              customElements.define('x-thing', Thing);
              customElements.whenDefined('x-thing').then(c => window.log.push('already:' + (c === Thing)));
              customElements.whenDefined('bad name').catch(e => window.log.push('rejected:' + e.name));
            </script>
            """);

        (await page.WaitForIdleAsync(TimeSpan.FromSeconds(5))).Should().BeTrue();

        (await page.EvaluateAsync<string>("window.log.slice().sort().join('|')"))
            .Should().Be("already:true|pending:true|rejected:SyntaxError");
    }

    [Test]
    public async Task AConstructorThatThrowsIsReportedAndLeavesTheElementUncustomized()
    {
        await using var browser = new Browser();
        var page = await PageWith(browser,
            """
            <script>
              class Bad extends HTMLElement { constructor() { super(); throw new Error('nope'); } }
              customElements.define('x-bad', Bad);
              const el = document.createElement('x-bad');
              window.result = [el.localName, el instanceof Bad, el instanceof HTMLElement].join('|');
            </script>
            """);

        (await page.EvaluateAsync<string>("window.result")).Should().Be("x-bad|false|true");
        page.Errors.Should().NotBeEmpty();
        string.Join("\n", page.Errors.Select(e => e.Message)).Should().Contain("nope");
    }

    [Test]
    public async Task TheConstructionStackLetsAConstructorCreateAnElementOfItsOwnName()
    {
        await using var browser = new Browser();
        var page = await PageWith(browser,
            """
            <script>
              window.log = [];
              let depth = 0;
              class Thing extends HTMLElement {
                constructor() {
                  super();
                  window.log.push('enter:' + depth);
                  if (depth++ === 0) { document.createElement('x-thing'); }
                }
              }
              customElements.define('x-thing', Thing);
              const el = document.createElement('x-thing');
              window.log.push('outer:' + (el instanceof Thing));
            </script>
            """);

        (await page.EvaluateAsync<string>("window.log.join('|')")).Should().Be("enter:0|enter:1|outer:true");
        page.Errors.Should().BeEmpty();
    }

    [Test]
    public async Task ReachingTheBaseConstructorTwiceDuringAnUpgradeIsAnInvalidStateError()
    {
        await using var browser = new Browser();
        var page = await PageWith(browser,
            """
            <x-thing></x-thing>
            <script>
              window.log = [];
              class Thing extends HTMLElement {
                constructor() {
                  super();
                  try { Reflect.construct(HTMLElement, [], Thing); } catch (e) { window.log.push(e.name); }
                }
              }
              customElements.define('x-thing', Thing);
            </script>
            """);

        // The upgrade put the element on the construction stack and `super()` replaced it with the
        // already-constructed marker, which is what the second reach finds. A `new Thing()` outside an
        // upgrade has an empty stack and makes a second element instead, exactly as a browser does.
        (await page.EvaluateAsync<string>("window.log.join('|')")).Should().Be("InvalidStateError");
    }

    [Test]
    public async Task AnUndefinedCustomElementNameIsAnHtmlElementRatherThanAnHtmlUnknownElement()
    {
        await using var browser = new Browser();
        var page = await PageWith(browser, "<x-undefined></x-undefined><bogus></bogus>");

        (await page.EvaluateAsync<string>(
                "[Object.prototype.toString.call(document.querySelector('x-undefined')),"
                + " Object.prototype.toString.call(document.querySelector('bogus'))].join('|')"))
            .Should().Be("[object HTMLElement]|[object HTMLUnknownElement]");
    }

    [Test]
    public async Task CreateElementNsInTheHtmlNamespaceRunsTheConstructorAndAnotherNamespaceDoesNot()
    {
        await using var browser = new Browser();
        var page = await PageWith(browser,
            """
            <script>
              window.log = [];
              class Thing extends HTMLElement { constructor() { super(); window.log.push('ctor'); } }
              customElements.define('x-thing', Thing);
              const html = document.createElementNS('http://www.w3.org/1999/xhtml', 'x-thing');
              const svg = document.createElementNS('http://www.w3.org/2000/svg', 'x-thing');
              window.log.push(html instanceof Thing, svg instanceof Thing);
            </script>
            """);

        (await page.EvaluateAsync<string>("window.log.join('|')")).Should().Be("ctor|true|false");
    }
}
