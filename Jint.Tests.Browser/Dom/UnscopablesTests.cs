namespace Jint.Tests.Browser.Dom;

/// <summary>
/// WebIDL's <a href="https://webidl.spec.whatwg.org/#es-unscopable">unscopable object</a>: the
/// <c>@@unscopables</c> a DOM interface prototype object carries, and what it keeps out of an object
/// environment.
/// </summary>
public sealed class UnscopablesTests
{
    private const string Page = """<!doctype html><html><body><div id="d"></div></body></html>""";

    [TestCase("Element", "after,append,before,prepend,remove,replaceChildren,replaceWith,slot")]
    [TestCase("Document", "append,prepend,replaceChildren")]
    [TestCase("DocumentFragment", "append,prepend,replaceChildren")]
    [TestCase("DocumentType", "after,before,remove,replaceWith")]
    [TestCase("CharacterData", "after,before,remove,replaceWith")]
    public void EachInterfacePrototypeListsItsOwnUnscopableMembers(string iface, string expected)
    {
        using var fixture = DomTestFixture.Create(Page);

        // https://dom.spec.whatwg.org/ marks eight members [Unscopable]: ParentNode's three, ChildNode's
        // four and Element's `slot`. The object lists the interface's OWN members, so an inherited one is
        // not on it — Element's list is not on Node.prototype and Document's is not Element's.
        fixture.Text("Object.keys(" + iface + ".prototype[Symbol.unscopables]).sort().join(',')")
            .Should().Be(expected);
    }

    [Test]
    public void TheUnscopableObjectHasTheAttributesWebIdlGivesIt()
    {
        using var fixture = DomTestFixture.Create(Page);

        fixture.Text(
            """
            var d = Object.getOwnPropertyDescriptor(Element.prototype, Symbol.unscopables);
            [d.writable, d.enumerable, d.configurable, Object.getPrototypeOf(d.value),
             Element.prototype[Symbol.unscopables] === Element.prototype[Symbol.unscopables]].join(',');
            """)
            .Should().Be("false,false,true,,true");
    }

    [Test]
    public async Task AnUnscopableMemberDoesNotShadowAGlobalInAHandlerScope()
    {
        await using var browser = new global::Jint.Browser.Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync(Page);

        // https://html.spec.whatwg.org/multipage/webappapis.html#getting-the-current-value-of-the-event-handler
        // builds object environments over the document and the element with the *withEnvironment* flag, so
        // @@unscopables is what decides that `remove` in the attribute is the global's and `this.remove` is
        // still the element's. This is `dom/nodes/remove-unscopable.html`'s subject.
        (await page.EvaluateAsync<string>(
            """
            window.remove = 'from the global';
            window.result = null;
            var d = document.getElementById('d');
            d.setAttribute('onclick', "window.result = remove + '|' + typeof this.remove");
            d.dispatchEvent(new Event('click'));
            window.result;
            """))
            .Should().Be("from the global|function");
    }

    [Test]
    public async Task APageCanWriteToTheUnscopableObject()
    {
        await using var browser = new global::Jint.Browser.Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync(Page);

        // The object is one per interface prototype object and mutable, which is what
        // `compile-event-handler-symbol-unscopables.html` relies on: it makes a name of its own unscopable
        // and then asserts the handler reads the global.
        (await page.EvaluateAsync<string>(
            """
            window.testVariable = 'from the global';
            window.result = null;
            document[Symbol.unscopables].testVariable = true;
            document.testVariable = 'from the document';
            var d = document.getElementById('d');
            d.setAttribute('onclick', 'window.result = testVariable');
            d.dispatchEvent(new Event('click'));
            window.result;
            """))
            .Should().Be("from the global");
    }
}
