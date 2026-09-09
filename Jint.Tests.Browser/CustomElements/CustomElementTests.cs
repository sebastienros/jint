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

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/custom-elements.html#dom-customelementregistry-getname takes a
    /// <c>CustomElementConstructor</c>, so a value that is not callable is refused by the WebIDL conversion
    /// rather than looked up and answered as <see langword="null"/>.
    /// </summary>
    [TestCase("undefined")]
    [TestCase("null")]
    [TestCase("'foo-bar'")]
    [TestCase("1")]
    [TestCase("({})")]
    [TestCase("[]")]
    public async Task GetNameRefusesAnArgumentThatIsNotCallable(string argument)
    {
        await using var browser = new Browser();
        var page = await PageWith(browser, "<p></p>");

        (await page.EvaluateAsync<string>(
                "(() => { try { customElements.getName(" + argument
                + "); return 'no throw'; } catch (e) { return e.constructor.name; } })()"))
            .Should().Be("TypeError");
    }

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/dom.html#html-element-constructors step 2: an interface object
    /// registered as its own constructor cannot be the <c>NewTarget</c> of its own constructor.
    /// </summary>
    [Test]
    public async Task ConstructingAnInterfaceObjectRegisteredAsItsOwnConstructorIsATypeError()
    {
        await using var browser = new Browser();
        var page = await PageWith(browser,
            """
            <script>
              customElements.define('x-html-element', HTMLElement);
              window.direct = (() => { try { new HTMLElement(); return 'no throw'; } catch (e) { return e.constructor.name; } })();
              window.viaProxy = (() => {
                customElements.define('x-proxy-element', new Proxy(HTMLElement, {}));
                try { new HTMLElement(); return 'no throw'; } catch (e) { return e.constructor.name; }
              })();
            </script>
            """);

        (await page.EvaluateAsync<string>("window.direct + '|' + window.viaProxy")).Should().Be("TypeError|TypeError");
    }

    /// <summary>
    /// WebIDL's <c>[Global]</c> puts an interface's members on the global object itself, so a page can save
    /// <c>customElements</c>'s descriptor, replace the global and put the descriptor back — and creation goes
    /// on working while it is replaced, because nothing reads the registry through the global.
    /// </summary>
    [Test]
    public async Task TheRegistryIsAnOwnPropertyOfTheGlobalThatSurvivesBeingReplaced()
    {
        await using var browser = new Browser();
        var page = await PageWith(browser,
            """
            <script>
              class Thing extends HTMLElement {}
              customElements.define('x-thing', Thing);
              const saved = Object.getOwnPropertyDescriptor(window, 'customElements');
              Object.defineProperty(window, 'customElements', { value: {}, configurable: true });
              const created = document.createElement('x-thing') instanceof Thing;
              const constructed = new Thing() instanceof Thing;
              Object.defineProperty(window, 'customElements', saved);
              window.result = [typeof saved, created, constructed, customElements.get('x-thing') === Thing].join('|');
            </script>
            """);

        (await page.EvaluateAsync<string>("window.result")).Should().Be("object|true|true|true");
    }

    /// <summary>
    /// https://dom.spec.whatwg.org/#validate-and-extract: a qualified name's <i>local</i> name is what the
    /// definition is looked up under, and the element keeps the prefix.
    /// </summary>
    /// <remarks>
    /// The prefix is readable inside the constructor, where DOM says it is still null: the divergence
    /// <c>CustomElementRegistry.Construction</c> argues, because AngleSharp's <c>Prefix</c> is read-only.
    /// </remarks>
    [Test]
    public async Task CreateElementNsWithAPrefixLooksTheDefinitionUpUnderTheLocalName()
    {
        await using var browser = new Browser();
        var page = await PageWith(browser,
            """
            <script>
              class Thing extends HTMLElement {}
              customElements.define('x-thing', Thing);
              const el = document.createElementNS('http://www.w3.org/1999/xhtml', 'p:x-thing');
              const inner = document.createElementNS('http://www.w3.org/1999/xhtml', 'x-thing');
              window.result = [el instanceof Thing, el.prefix, el.localName, el.tagName, String(inner.prefix)].join('|');
            </script>
            """);

        (await page.EvaluateAsync<string>("window.result")).Should().Be("true|p|x-thing|P:X-THING|null");
    }

    /// <summary>
    /// https://dom.spec.whatwg.org/#concept-range-clone and #concept-range-extract reach "clone a node" for
    /// every partially contained ancestor, and cloning an element a definition names enqueues an upgrade — so
    /// the constructors run, in tree order, before the member returns.
    /// </summary>
    [Test]
    public async Task RangeCloneAndExtractRunTheConstructorsOfTheElementsTheyClone()
    {
        await using var browser = new Browser();
        var page = await PageWith(browser,
            """
            <x-thing id="root"><x-thing id="a"><span id="start"></span></x-thing><x-thing id="b"></x-thing><span id="end"></span></x-thing>
            <script>
              window.log = [];
              class Thing extends HTMLElement { constructor() { super(); window.log.push(this.id); } }
              customElements.define('x-thing', Thing);
              function range() {
                const r = new Range();
                r.setStart(document.getElementById('start'), 0);
                r.setEnd(document.getElementById('end'), 0);
                return r;
              }
              window.log = [];
              range().cloneContents();
              window.cloned = window.log.join(',');
              window.log = [];
              range().extractContents();
              window.extracted = window.log.join(',');
            </script>
            """);

        (await page.EvaluateAsync<string>("window.cloned")).Should().Be("a,b");
        (await page.EvaluateAsync<string>("window.extracted")).Should().Be("a");
        page.Errors.Should().BeEmpty();
    }
}
