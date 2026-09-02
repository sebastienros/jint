namespace Jint.Tests.Browser.Views;

// The test namespace sits under Jint.Tests.Browser, so the bare name Browser binds to that namespace rather
// than to the type. The alias belongs inside the namespace declaration, where it wins that lookup.
using Browser = global::Jint.Browser.Browser;

/// <summary>
/// <c>Range</c>, <c>TreeWalker</c> and <c>NodeIterator</c>: the generated interfaces, plus the members whose
/// signatures the binding could not cross.
/// </summary>
public sealed class TraversalTests
{
    [Test]
    public async Task ARangeSelectsMovesAndExtracts()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();

        await page.SetContentAsync(
            """
            <div id="host"><b>one</b><i>two</i><u>three</u></div>
            <script>
              const host = document.getElementById('host');
              const range = document.createRange();
              range.selectNodeContents(host);

              window.log = [
                range instanceof Range,
                range.startContainer === host,
                range.startOffset,
                range.endOffset,
                range.collapsed,
                range.commonAncestorContainer === host,
                range.toString(),
              ].join('|');

              const fragment = range.extractContents();
              window.after = [fragment.childNodes.length, host.childNodes.length].join('|');
            </script>
            """);

        (await page.EvaluateAsync<string>("window.log")).Should().Be("true|true|0|3|false|true|onetwothree");
        (await page.EvaluateAsync<string>("window.after")).Should().Be("3|0");
    }

    [Test]
    public async Task ARangeClonesInsertsSurroundsAndDeletes()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();

        await page.SetContentAsync(
            """
            <div id="host"><b>one</b><i>two</i></div>
            <script>
              const host = document.getElementById('host');
              const range = document.createRange();

              range.selectNode(host.querySelector('b'));
              window.cloned = range.cloneContents().childNodes.length;

              const wrapper = document.createElement('em');
              range.surroundContents(wrapper);
              window.surrounded = host.innerHTML;

              const insertion = document.createRange();
              insertion.setStart(host, 0);
              insertion.collapse(true);
              insertion.insertNode(document.createElement('s'));
              window.inserted = host.firstChild.tagName;

              const removal = document.createRange();
              removal.selectNode(host.querySelector('i'));
              removal.deleteContents();
              window.deleted = host.querySelector('i') === null;
            </script>
            """);

        (await page.EvaluateAsync<int>("window.cloned")).Should().Be(1);
        (await page.EvaluateAsync<string>("window.surrounded")).Should().Be("<em><b>one</b></em><i>two</i>");
        (await page.EvaluateAsync<string>("window.inserted")).Should().Be("S");
        (await page.EvaluateAsync<bool>("window.deleted")).Should().BeTrue();
    }

    [Test]
    public async Task ARangeAnswersZeroRectanglesBecauseThereIsNoLayout()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();

        await page.SetContentAsync("<p id='p'>text</p>");

        (await page.EvaluateAsync<string>(
            """
            (() => {
              const range = document.createRange();
              range.selectNodeContents(document.getElementById('p'));
              const rect = range.getBoundingClientRect();
              return [rect.x, rect.y, rect.width, rect.height, range.getClientRects().length].join('|');
            })()
            """))
            .Should().Be("0|0|0|0|0");
    }

    [Test]
    public async Task ATreeWalkerTakesAFunctionFilter()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();

        await page.SetContentAsync(
            """
            <div id="host"><a></a><b><i></i></b><u></u></div>
            <script>
              const host = document.getElementById('host');
              const walker = document.createTreeWalker(host, NodeFilter.SHOW_ELEMENT, node =>
                node.tagName === 'B' ? NodeFilter.FILTER_REJECT : NodeFilter.FILTER_ACCEPT);

              const seen = [];
              let node;
              while ((node = walker.nextNode())) { seen.push(node.tagName) }

              window.log = seen.join(',');
              window.meta = [walker instanceof TreeWalker, walker.root === host, walker.whatToShow].join('|');
            </script>
            """);

        // FILTER_REJECT on an element skips its subtree, so <i> never appears.
        (await page.EvaluateAsync<string>("window.log")).Should().Be("A,U");
        (await page.EvaluateAsync<string>("window.meta")).Should().Be("true|true|1");
    }

    [Test]
    public async Task ATreeWalkerTakesAnObjectFilterAndReportsItBack()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();

        await page.SetContentAsync(
            """
            <div id="host"><a></a><b></b><u></u></div>
            <script>
              const filter = { acceptNode: node => node.tagName === 'B' ? NodeFilter.FILTER_SKIP : NodeFilter.FILTER_ACCEPT };
              const walker = document.createTreeWalker(document.getElementById('host'), NodeFilter.SHOW_ELEMENT, filter);

              const seen = [];
              let node;
              while ((node = walker.nextNode())) { seen.push(node.tagName) }

              window.log = seen.join(',');
              window.filterIsTheSameObject = walker.filter === filter;
            </script>
            """);

        // FILTER_SKIP passes over the node but keeps its subtree, unlike FILTER_REJECT.
        (await page.EvaluateAsync<string>("window.log")).Should().Be("A,U");
        (await page.EvaluateAsync<bool>("window.filterIsTheSameObject")).Should().BeTrue();
    }

    [Test]
    public async Task ATreeWalkerWalksInEveryDirection()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();

        await page.SetContentAsync(
            """
            <div id="host"><a><i></i></a><b></b></div>
            <script>
              const host = document.getElementById('host');
              const walker = document.createTreeWalker(host, NodeFilter.SHOW_ELEMENT);
              const seen = [];
              seen.push(walker.firstChild().tagName);
              seen.push(walker.firstChild().tagName);
              seen.push(walker.parentNode().tagName);
              seen.push(walker.nextSibling().tagName);
              seen.push(walker.previousSibling().tagName);
              seen.push(walker.lastChild().tagName);
              window.log = seen.join(',');
              window.current = walker.currentNode.tagName;
            </script>
            """);

        (await page.EvaluateAsync<string>("window.log")).Should().Be("A,I,A,B,A,I");
        (await page.EvaluateAsync<string>("window.current")).Should().Be("I");
    }

    [Test]
    public async Task ANodeIteratorWalksEveryNodeTypeItWasAskedFor()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();

        await page.SetContentAsync(
            """
            <div id="host"><b>one</b><!--note--><i>two</i></div>
            <script>
              const host = document.getElementById('host');
              const iterator = document.createNodeIterator(host);
              const seen = [];
              let node;
              while ((node = iterator.nextNode())) { seen.push(node.nodeName) }
              window.all = seen.join(',');

              const elements = document.createNodeIterator(host, NodeFilter.SHOW_ELEMENT);
              const tags = [];
              while ((node = elements.nextNode())) { tags.push(node.nodeName) }
              window.elements = tags.join(',');
              window.meta = [elements instanceof NodeIterator, elements.root === host, elements.whatToShow, elements.filter].join('|');
            </script>
            """);

        (await page.EvaluateAsync<string>("window.all")).Should().Be("DIV,B,#text,#comment,I,#text");
        (await page.EvaluateAsync<string>("window.elements")).Should().Be("DIV,B,I");

        // whatToShow defaults to 0xFFFFFFFF and is an unsigned long: a signed read would answer -1.
        (await page.EvaluateAsync<string>("document.createNodeIterator(document.body).whatToShow.toString()")).Should().Be("4294967295");
        (await page.EvaluateAsync<string>("window.meta")).Should().Be("true|true|1|");
    }

    [Test]
    public async Task ANodeIteratorGoesBackwards()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();

        await page.SetContentAsync(
            """
            <div id="host"><a></a><b></b></div>
            <script>
              const iterator = document.createNodeIterator(document.getElementById('host'), NodeFilter.SHOW_ELEMENT);
              iterator.nextNode();
              iterator.nextNode();
              const back = iterator.previousNode();
              window.log = [back.nodeName, iterator.referenceNode.nodeName, iterator.pointerBeforeReferenceNode].join('|');
            </script>
            """);

        // DOM's traverse(previous) moves the *pointer* from after the reference node to before it without
        // moving the node, so the first previousNode() after a nextNode() answers the same node again.
        (await page.EvaluateAsync<string>("window.log")).Should().Be("A|A|true");
    }

    [Test]
    public async Task AFilterThatThrowsPropagatesOutOfNextNode()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();

        await page.SetContentAsync("<div id='host'><a></a></div>");

        (await page.EvaluateAsync<string>(
            """
            (() => {
              const walker = document.createTreeWalker(document.getElementById('host'), NodeFilter.SHOW_ELEMENT, () => { throw new Error('from a filter') });
              try { walker.nextNode(); return 'no throw' } catch (e) { return e.message }
            })()
            """))
            .Should().Be("from a filter");
    }

    [Test]
    public async Task NodeFilterCarriesItsConstantsAndABadFilterIsATypeError()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();

        (await page.EvaluateAsync<string>("[NodeFilter.FILTER_ACCEPT, NodeFilter.FILTER_REJECT, NodeFilter.FILTER_SKIP].join(',')"))
            .Should().Be("1,2,3");
        (await page.EvaluateAsync<string>("[NodeFilter.SHOW_ALL, NodeFilter.SHOW_ELEMENT, NodeFilter.SHOW_TEXT, NodeFilter.SHOW_COMMENT].join(',')"))
            .Should().Be("4294967295,1,4,128");
        (await page.EvaluateAsync<string>("Object.prototype.toString.call(NodeFilter)")).Should().Be("[object NodeFilter]");

        (await page.EvaluateAsync<string>(
            "(() => { try { document.createTreeWalker(document.body, NodeFilter.SHOW_ALL, 7) } catch (e) { return e.constructor.name } return 'no throw' })()"))
            .Should().Be("TypeError");
    }
}
