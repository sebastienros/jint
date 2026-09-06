namespace Jint.Tests.Browser.Dom;

/// <summary>
/// The interfaces WebIDL really does give a constructor, and the ones it does not.
/// </summary>
/// <remarks>
/// AngleSharp puts <c>[DomConstructor]</c> on no <c>[DomName]</c> interface, so the generator can never
/// learn that an interface is constructible and <c>DomConstructors</c> is the table it is written in by
/// hand. The point of these rows is that the table is short and that everything outside it is still
/// <c>Illegal constructor</c>, which is what a browser answers too.
/// </remarks>
public sealed class DomConstructorTests
{
    private const string Page = """
        <!doctype html>
        <html><body><div id="a">hello</div></body></html>
        """;

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/embedded-content.html#dom-image and
    /// https://webidl.spec.whatwg.org/#legacy-factory-functions: <c>Image</c> is the legacy factory function
    /// for <c>HTMLImageElement</c>, with that interface's prototype rather than a prototype of its own.
    /// </summary>
    [Test]
    public async Task ImageIsTheHtmlImageElementLegacyFactoryFunction()
    {
        await using var browser = new global::Jint.Browser.Browser();
        var page = await browser.NewPageAsync();

        await page.SetContentAsync("<!doctype html><body></body>");

        (await page.EvaluateAsync<string>("""
            const image = new Image();
            class DerivedImage extends Image {}
            const derived = new DerivedImage();
            const globalDescriptor = Object.getOwnPropertyDescriptor(window, 'Image');
            const prototypeDescriptor = Object.getOwnPropertyDescriptor(Image, 'prototype');
            let callError;
            try { Image(); } catch (error) { callError = error.name; }
            [
              typeof Image,
              Image.name,
              Image.length,
              Image.prototype === HTMLImageElement.prototype,
              Image.prototype.constructor === HTMLImageElement,
              Object.getPrototypeOf(image) === HTMLImageElement.prototype,
              image instanceof HTMLImageElement,
              image.tagName,
              Object.prototype.toString.call(image),
              Object.getPrototypeOf(derived) === DerivedImage.prototype,
              derived instanceof DerivedImage,
              derived instanceof HTMLImageElement,
              callError,
              globalDescriptor.writable,
              globalDescriptor.enumerable,
              globalDescriptor.configurable,
              prototypeDescriptor.writable,
              prototypeDescriptor.enumerable,
              prototypeDescriptor.configurable,
            ].join('|')
            """)).Should().Be(
            "function|Image|0|true|true|true|true|IMG|[object HTMLImageElement]|true|true|true|TypeError|true|false|true|false|false|false");
    }

    /// <summary>
    /// The two arguments are optional, and only arguments that were supplied create the corresponding
    /// content attributes.
    /// </summary>
    [Test]
    public async Task ImageAppliesOnlyTheDimensionsThatWereSupplied()
    {
        await using var browser = new global::Jint.Browser.Browser();
        var page = await browser.NewPageAsync();

        await page.SetContentAsync("<!doctype html><body></body>");

        (await page.EvaluateAsync<string>("""
            const omitted = new Image();
            const widthOnly = new Image(320);
            const sized = new Image(320, 200);
            [
              omitted.width,
              omitted.height,
              omitted.hasAttribute('width'),
              omitted.hasAttribute('height'),
              widthOnly.width,
              widthOnly.height,
              widthOnly.getAttribute('width'),
              widthOnly.hasAttribute('height'),
              sized.width,
              sized.height,
              sized.getAttribute('width'),
              sized.getAttribute('height'),
            ].join('|')
            """)).Should().Be("0|0|false|false|320|0|320|false|320|200|320|200");
    }

    /// <summary>
    /// WebIDL converts each supplied argument to <c>unsigned long</c> before HTML writes the attribute.
    /// </summary>
    [Test]
    public async Task ImageDimensionsUseUnsignedLongConversion()
    {
        await using var browser = new global::Jint.Browser.Browser();
        var page = await browser.NewPageAsync();

        await page.SetContentAsync("<!doctype html><body></body>");

        (await page.EvaluateAsync<string>("""
            const wrapped = new Image(-1, 4294967297);
            const converted = new Image('12.9', NaN);
            const explicitUndefined = new Image(undefined, 9);
            const object = new Image({ valueOf() { return 17; } }, 4);
            const errorNames = [];
            for (const value of [Symbol('width'), 1n]) {
              try { new Image(value); }
              catch (error) { errorNames.push(error.name); }
            }
            [
              wrapped.getAttribute('width'),
              wrapped.getAttribute('height'),
              converted.getAttribute('width'),
              converted.getAttribute('height'),
              explicitUndefined.getAttribute('width'),
              explicitUndefined.getAttribute('height'),
              object.getAttribute('width'),
              object.getAttribute('height'),
              ...errorNames,
            ].join('|')
            """)).Should().Be("4294967295|1|12|0|0|9|17|4|TypeError|TypeError");
    }

    /// <summary>
    /// Image fetching is intentionally outside this headless browser's rendering-free model, but the
    /// constructed element still reflects <c>src</c> and participates in the normal DOM event path.
    /// </summary>
    [Test]
    public async Task ImageSupportsSourceReflectionAndLoadEvents()
    {
        await using var browser = new global::Jint.Browser.Browser();
        var page = await browser.NewPageAsync();

        await page.SetContentAsync("<!doctype html><body></body>");

        (await page.EvaluateAsync<string>("""
            const image = new Image();
            let loads = 0;
            image.addEventListener('load', () => loads++);
            image.src = 'data:image/gif;base64,R0lGODlhAQABAIAAAAAAAP///ywAAAAAAQABAAACAUwAOw==';
            const beforeDispatch = loads;
            image.dispatchEvent(new Event('load'));
            [
              image.getAttribute('src'),
              image.src,
              beforeDispatch,
              loads,
            ].join('|')
            """)).Should().Be(
            "data:image/gif;base64,R0lGODlhAQABAIAAAAAAAP///ywAAAAAAQABAAACAUwAOw==|data:image/gif;base64,R0lGODlhAQABAIAAAAAAAP///ywAAAAAAQABAAACAUwAOw==|0|1");
    }

    /// <summary>
    /// https://dom.spec.whatwg.org/#dom-comment-comment and
    /// https://dom.spec.whatwg.org/#dom-text-text: <c>constructor(optional DOMString data = "")</c>, whose
    /// node document is the current global object's associated <c>Document</c>.
    /// </summary>
    [TestCase("new Comment('x').data", "x")]
    [TestCase("new Comment().data", "")]
    [TestCase("new Comment('x').nodeType", "8")]
    [TestCase("new Comment('x') instanceof Comment", "true")]
    [TestCase("new Comment('x') instanceof CharacterData", "true")]
    [TestCase("Object.prototype.toString.call(new Comment('x'))", "[object Comment]")]
    [TestCase("new Comment('x').ownerDocument !== null", "true")]
    [TestCase("new Text('x').data", "x")]
    [TestCase("new Text().data", "")]
    [TestCase("new Text('x').nodeType", "3")]
    [TestCase("new Text('x') instanceof Text", "true")]
    [TestCase("Object.prototype.toString.call(new Text('x'))", "[object Text]")]
    [TestCase("new Text('x').ownerDocument !== null", "true")]
    [TestCase("document.getElementById('a').appendChild(new Text('!')).data", "!")]
    public void CommentAndTextTakeTheirData(string source, string expected)
    {
        Answer(source).Should().Be(expected);
    }

    /// <summary>
    /// https://dom.spec.whatwg.org/#dom-range-range: the new range's start and end are that document at
    /// offset 0, so it is collapsed on the document node.
    /// </summary>
    [TestCase("new Range() instanceof Range", "true")]
    [TestCase("Object.prototype.toString.call(new Range())", "[object Range]")]
    [TestCase("new Range().collapsed", "true")]
    [TestCase("new Range().startOffset", "0")]
    [TestCase("new Range().startContainer.nodeType", "9")]
    [TestCase("new Range().endContainer.nodeType", "9")]
    public void ARangeStartsCollapsedOnTheDocument(string source, string expected)
    {
        Answer(source).Should().Be(expected);
    }

    /// <summary>
    /// And everything else still refuses, which is what keeps the table meaningful rather than a habit.
    /// </summary>
    [TestCase("new Node()")]
    [TestCase("new Element()")]
    [TestCase("new CharacterData()")]
    [TestCase("new NodeList()")]
    [TestCase("new HTMLDivElement()")]
    public void EveryOtherInterfaceObjectIsStillIllegal(string source)
    {
        Answer(source).Should().Be("TypeError: Illegal constructor");
    }

    /// <summary>
    /// On a real page the associated <c>Document</c> is the page's, which is the answer the standard names
    /// and the one every wpt row asserts. The fixture above is the binding on its own, where there is no
    /// page and the node gets an empty document of its own instead — the same fallback
    /// <c>new DocumentFragment()</c> has always taken.
    /// </summary>
    [Test]
    public async Task OnAPageTheNodeDocumentIsThePagesOwn()
    {
        await using var browser = new global::Jint.Browser.Browser();
        var page = await browser.NewPageAsync();

        await page.SetContentAsync("<div id=\"a\"></div>");

        (await page.EvaluateAsync<string>("""
            [
              new Comment('x').ownerDocument === document,
              new Text('x').ownerDocument === document,
              new Range().startContainer === document,
              new Range().endContainer === document,
            ].join('|')
            """)).Should().Be("true|true|true|true");
    }

    private static string? Answer(string source)
    {
        using var fixture = DomTestFixture.Create(Page);

        return fixture.Text($$"""
            (function () {
              try { return String({{source}}); }
              catch (e) { return e.constructor.name + ': ' + e.message; }
            })()
            """);
    }
}
