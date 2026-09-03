namespace Jint.Tests.Browser.Views;

// The test namespace sits under Jint.Tests.Browser, so the bare name Browser binds to that namespace rather
// than to the type. The alias belongs inside the namespace declaration, where it wins that lookup.
using Browser = global::Jint.Browser.Browser;

/// <summary>
/// DOM §7's XPath: <c>XPathEvaluator</c>, <c>XPathExpression</c>, <c>XPathResult</c> and
/// <c>document.evaluate</c>.
/// </summary>
/// <remarks>
/// The engine is <c>System.Xml.XPath</c> over <c>AngleSharp.XPath</c>'s navigator, so what these assert is
/// the <i>binding</i>: the interfaces exist, a page can construct the one it is meant to, the ten result
/// types are answered and coerced the way the standard says, and the two documented divergences (namespaces
/// are ignored, and a node set is materialized) hold. <c>Jint.Browser/Dom/Views/JsXPath</c> argues both.
/// </remarks>
public sealed class XPathTests
{
    private const string Page =
        """
        <div id="root">
          <p class="note">first</p>
          <p class="note" data-tag="x">second</p>
          <span id="only">alone</span>
        </div>
        """;

    private static async Task<global::Jint.Browser.Page> OpenAsync(Browser browser)
    {
        var page = await browser.NewPageAsync();
        await page.SetContentAsync(Page);
        return page;
    }

    [Test]
    public async Task TheThreeInterfacesAreGlobalsAndOnlyTheEvaluatorIsConstructible()
    {
        await using var browser = new Browser();
        var page = await OpenAsync(browser);

        (await page.EvaluateAsync<string>("typeof XPathEvaluator")).Should().Be("function");
        (await page.EvaluateAsync<string>("typeof XPathExpression")).Should().Be("function");
        (await page.EvaluateAsync<string>("typeof XPathResult")).Should().Be("function");

        (await page.EvaluateAsync<bool>("new XPathEvaluator() instanceof XPathEvaluator")).Should().BeTrue();
        (await page.EvaluateAsync<string>("Object.prototype.toString.call(new XPathEvaluator())"))
            .Should().Be("[object XPathEvaluator]");

        (await page.EvaluateAsync<string>(
            "(() => { try { new XPathResult(); return 'no throw' } catch (e) { return e.message } })()"))
            .Should().Be("Illegal constructor", "only XPathEvaluator has a constructor in DOM");
    }

    /// <summary>The two spellings of a constant WebIDL requires, and the ten values.</summary>
    [Test]
    public async Task TheResultTypeConstantsAreOnTheInterfaceObjectAndOnThePrototype()
    {
        await using var browser = new Browser();
        var page = await OpenAsync(browser);

        (await page.EvaluateAsync<int>("XPathResult.FIRST_ORDERED_NODE_TYPE")).Should().Be(9);
        (await page.EvaluateAsync<int>("XPathResult.prototype.FIRST_ORDERED_NODE_TYPE")).Should().Be(9);

        (await page.EvaluateAsync<string>(
            """
            [
              'ANY_TYPE', 'NUMBER_TYPE', 'STRING_TYPE', 'BOOLEAN_TYPE',
              'UNORDERED_NODE_ITERATOR_TYPE', 'ORDERED_NODE_ITERATOR_TYPE',
              'UNORDERED_NODE_SNAPSHOT_TYPE', 'ORDERED_NODE_SNAPSHOT_TYPE',
              'ANY_UNORDERED_NODE_TYPE', 'FIRST_ORDERED_NODE_TYPE'
            ].map(name => XPathResult[name]).join(',')
            """))
            .Should().Be("0,1,2,3,4,5,6,7,8,9");
    }

    /// <summary>The shape htmx uses: compile once at the top level, evaluate against a node, iterate.</summary>
    [Test]
    public async Task ACompiledExpressionIteratesTheNodesItMatched()
    {
        await using var browser = new Browser();
        var page = await OpenAsync(browser);

        (await page.EvaluateAsync<string>(
            """
            (() => {
              const expression = (new XPathEvaluator).createExpression('.//p[@class="note"]');
              const result = expression.evaluate(document.getElementById('root'));
              const seen = [];
              let node = null;
              while (node = result.iterateNext()) { seen.push(node.textContent) }
              return seen.join('|');
            })()
            """))
            .Should().Be("first|second");

        (await page.EvaluateAsync<bool>(
            "(new XPathEvaluator).createExpression('//p') instanceof XPathExpression")).Should().BeTrue();
    }

    /// <summary>An unprefixed name test matches an HTML element, which is the whole point of the choice.</summary>
    [Test]
    public async Task AnUnprefixedNameTestMatchesAnHtmlElement()
    {
        await using var browser = new Browser();
        var page = await OpenAsync(browser);

        (await page.EvaluateAsync<string>(
            """
            document.evaluate('//span[@id="only"]', document, null, XPathResult.FIRST_ORDERED_NODE_TYPE, null)
              .singleNodeValue.textContent
            """))
            .Should().Be("alone");

        // And the node that comes back is the page's own wrapper, not a copy.
        (await page.EvaluateAsync<bool>(
            """
            document.evaluate('//span[@id="only"]', document, null, XPathResult.FIRST_ORDERED_NODE_TYPE, null)
              .singleNodeValue === document.getElementById('only')
            """))
            .Should().BeTrue("wrapper identity is the cache's, so an XPath result is the same object querySelector gives");
    }

    /// <summary>The result types other than a node set, and the coercions between them.</summary>
    [Test]
    public async Task ANumberAStringAndABooleanAreAnsweredAndCoerced()
    {
        await using var browser = new Browser();
        var page = await OpenAsync(browser);

        var evaluate = "((expression, type) => document.evaluate(expression, document, null, type, null))";

        (await page.EvaluateAsync<double>(evaluate + "('count(//p)', XPathResult.NUMBER_TYPE).numberValue"))
            .Should().Be(2);

        (await page.EvaluateAsync<string>(evaluate + "('string(//span)', XPathResult.STRING_TYPE).stringValue"))
            .Should().Be("alone");

        (await page.EvaluateAsync<bool>(evaluate + "('//p', XPathResult.BOOLEAN_TYPE).booleanValue"))
            .Should().BeTrue("a non-empty node set is true");

        (await page.EvaluateAsync<bool>(evaluate + "('//table', XPathResult.BOOLEAN_TYPE).booleanValue"))
            .Should().BeFalse("an empty one is false");

        // ANY_TYPE infers, which is what a page that passes nothing gets.
        (await page.EvaluateAsync<int>(evaluate + "('count(//p)', XPathResult.ANY_TYPE).resultType"))
            .Should().Be(1);
        (await page.EvaluateAsync<int>(evaluate + "('//p', XPathResult.ANY_TYPE).resultType"))
            .Should().Be(4, "a node set with no type asked for is an unordered iterator");
    }

    /// <summary>A snapshot is indexed, and reading the wrong member off a result is a TypeError.</summary>
    [Test]
    public async Task ASnapshotIsIndexedAndTheWrongAccessorIsATypeError()
    {
        await using var browser = new Browser();
        var page = await OpenAsync(browser);

        var snapshot = "document.evaluate('//p', document, null, XPathResult.ORDERED_NODE_SNAPSHOT_TYPE, null)";

        (await page.EvaluateAsync<int>(snapshot + ".snapshotLength")).Should().Be(2);
        (await page.EvaluateAsync<string>(snapshot + ".snapshotItem(1).textContent")).Should().Be("second");
        (await page.EvaluateAsync<string>("String(" + snapshot + ".snapshotItem(9))")).Should().Be("null");

        (await page.EvaluateAsync<string>(
            "(() => { try { return String(" + snapshot + ".numberValue) } catch (e) { return e.constructor.name } })()"))
            .Should().Be("TypeError", "a snapshot has no number value");
    }

    /// <summary>An expression the parser refuses is the standard's <c>SyntaxError</c>, not a CLR exception.</summary>
    [Test]
    public async Task AnUnparseableExpressionIsASyntaxError()
    {
        await using var browser = new Browser();
        var page = await OpenAsync(browser);

        (await page.EvaluateAsync<string>(
            """
            (() => {
              try { document.evaluate('//p[', document, null, 0, null); return 'no throw' }
              catch (e) { return e.name }
            })()
            """))
            .Should().Be("SyntaxError");

        page.Errors.Should().BeEmpty();
    }

    /// <summary><c>createNSResolver</c> is legacy, and its whole definition is "return the node".</summary>
    [Test]
    public async Task CreateNsResolverAnswersTheNodeItWasGiven()
    {
        await using var browser = new Browser();
        var page = await OpenAsync(browser);

        (await page.EvaluateAsync<bool>("document.createNSResolver(document) === document")).Should().BeTrue();
        (await page.EvaluateAsync<bool>("(new XPathEvaluator).createNSResolver(document) === document")).Should().BeTrue();
    }

    /// <summary>
    /// The documented divergence: the node set is taken whole, so a mutation cannot invalidate an iterator.
    /// </summary>
    [Test]
    public async Task AnIteratorSurvivesAMutationRatherThanBecomingInvalid()
    {
        await using var browser = new Browser();
        var page = await OpenAsync(browser);

        (await page.EvaluateAsync<string>(
            """
            (() => {
              const result = document.evaluate('//p', document, null, XPathResult.ORDERED_NODE_ITERATOR_TYPE, null);
              const first = result.iterateNext();
              document.getElementById('root').appendChild(document.createElement('p'));
              const second = result.iterateNext();
              return [result.invalidIteratorState, first.textContent, second.textContent].join('|');
            })()
            """))
            .Should().Be("false|first|second",
                "the set is materialized at evaluation, so a browser's InvalidStateError cannot arise here");
    }
}
