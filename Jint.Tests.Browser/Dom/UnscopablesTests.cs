namespace Jint.Tests.Browser.Dom;

using Browser = global::Jint.Browser.Browser;

/// <summary>
/// The <c>@@unscopables</c> object Web IDL puts on the interface prototype object of every interface with an
/// <c>[Unscopable]</c> member — which in DOM is every member of <c>ChildNode</c> (§4.2.8) and
/// <c>ParentNode</c> (§4.2.9).
/// </summary>
/// <remarks>
/// It exists for one statement, and the last test is that statement: HTML compiles an inline event handler
/// with the element, its form owner and the document on the scope chain, which is <c>with</c> semantics — so
/// without the object every one of those seven names shadows a global of the same name, and <c>remove</c>,
/// <c>append</c> and <c>before</c> are names pages really do use.
/// </remarks>
public sealed class UnscopablesTests
{
    private const string Page = """
        <!doctype html>
        <html><body><div id="a">hello</div></body></html>
        """;

    /// <summary>
    /// https://webidl.spec.whatwg.org/#es-unscopables — the object is
    /// <c>{ writable: false, enumerable: false, configurable: true }</c>, its <c>[[Prototype]]</c> is null,
    /// and every key is <see langword="true"/>.
    /// </summary>
    [Test]
    public void TheObjectIsWebIdlsAndItsPrototypeIsNull()
    {
        using var fixture = DomTestFixture.Create(Page);

        fixture.Text(
            """
            (() => {
              const d = Object.getOwnPropertyDescriptor(Element.prototype, Symbol.unscopables);
              const value = d.value;
              return [
                d.writable, d.enumerable, d.configurable,
                Object.getPrototypeOf(value),
                Object.keys(value).sort().join('+'),
                Object.values(value).every(v => v === true)
              ].map(String).join(',');
            })()
            """)
            .Should().Be(
                "false,false,true,null,"
                + "after+append+before+prepend+remove+replaceChildren+replaceWith,true");
    }

    /// <summary>
    /// Which interfaces carry one, and with which names. It is the mixin inclusion and nothing else:
    /// <c>Element</c> includes both mixins, <c>CharacterData</c> and <c>DocumentType</c> only
    /// <c>ChildNode</c>, <c>Document</c> and <c>DocumentFragment</c> only <c>ParentNode</c>, and no other
    /// interface has an <c>[Unscopable]</c> member at all.
    /// </summary>
    [TestCase("Element", "after+append+before+prepend+remove+replaceChildren+replaceWith")]
    [TestCase("CharacterData", "after+before+remove+replaceWith")]
    [TestCase("DocumentType", "after+before+remove+replaceWith")]
    [TestCase("Document", "append+prepend+replaceChildren")]
    [TestCase("DocumentFragment", "append+prepend+replaceChildren")]
    [TestCase("Node", "")]
    [TestCase("HTMLElement", "")]
    [TestCase("Attr", "")]
    public void EachInterfaceCarriesTheMembersItsMixinsMark(string interfaceName, string expected)
    {
        using var fixture = DomTestFixture.Create(Page);

        fixture.Text(
            $$"""
            (() => {
              const own = Object.getOwnPropertyDescriptor({{interfaceName}}.prototype, Symbol.unscopables);
              return own === undefined ? "" : Object.keys(own.value).sort().join('+');
            })()
            """)
            .Should().Be(expected);
    }

    /// <summary>
    /// Every name in the object has to be a member the interface really declares: Web IDL builds the object
    /// out of the interface's own members, so a key naming nothing would be the object claiming something the
    /// prototype does not have. The generator refuses an <c>unscopables</c> entry that names one; this is the
    /// same property asserted from the outside.
    /// </summary>
    [Test]
    public void EveryNameIsAMemberTheInterfaceDeclares()
    {
        using var fixture = DomTestFixture.Create(Page);

        fixture.Text(
            """
            ['Element', 'CharacterData', 'DocumentType', 'Document', 'DocumentFragment']
              .flatMap(name => {
                const proto = globalThis[name].prototype;
                return Object.keys(proto[Symbol.unscopables])
                  .filter(member => typeof proto[member] !== 'function')
                  .map(member => name + '.' + member);
              })
              .join(',')
            """)
            .Should().BeEmpty();
    }

    /// <summary>
    /// A page may add its own names, because nothing freezes the object — that is how a page opts one of its
    /// own expandos out of the same shadowing, and it is what
    /// <c>html/webappapis/scripting/events/compile-event-handler-symbol-unscopables.html</c> does.
    /// </summary>
    [Test]
    public void APageMayAddItsOwnNames()
    {
        using var fixture = DomTestFixture.Create(Page);

        fixture.Text(
            """
            (() => {
              Document.prototype[Symbol.unscopables].mine = true;
              return Object.keys(document[Symbol.unscopables]).sort().join('+');
            })()
            """)
            .Should().Be("append+mine+prepend+replaceChildren");
    }

    /// <summary>
    /// The whole point: an inline event handler is compiled with the element and the document on its scope
    /// chain, so <c>remove</c> inside one must be the page's global and <c>this.remove</c> must still be the
    /// method. This is <c>dom/nodes/remove-unscopable.html</c>'s own shape, run through the page runtime that
    /// compiles the handler.
    /// </summary>
    [Test]
    public async Task AnInlineHandlerSeesTheGlobalAndNotTheElementsMethod()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<div id='t'></div>");

        (await page.EvaluateAsync<string>(
            """
            (() => {
              const names = ['before', 'after', 'replaceWith', 'remove', 'prepend', 'append'];
              const out = [];
              const div = document.getElementById('t');
              for (const name of names) {
                window[name] = 'Hello there';
                window.result1 = window.result2 = undefined;
                div.setAttribute('onclick', 'result1 = ' + name + '; result2 = this.' + name + ';');
                div.dispatchEvent(new Event('click'));
                out.push(typeof window.result1 + '/' + typeof window.result2);
              }
              return out.join(',');
            })()
            """)).Should().Be(string.Join(',', Enumerable.Repeat("string/function", 6)));
    }
}
