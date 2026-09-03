using System.Text.Json;
using Jint.Browser;

namespace Jint.Tests.Browser.DevTools;

/// <summary>
/// The <c>DOM</c> domain over a real page: the document a client walks, the identifiers it addresses nodes
/// by, and the boxes it measures.
/// </summary>
/// <remarks>
/// <para>
/// These assert the envelope as text, for the reason <c>Jint.DevTools/AGENTS.md</c> gives: a client library
/// matches on <c>result</c> shapes and on <c>error.code</c>, and a test that called a domain method directly
/// would pass with the envelope broken.
/// </para>
/// <para>
/// The fixture document is always <c>html &gt; head, body</c> plus whatever the test puts in the body, and
/// <c>&lt;head&gt;</c> has no box — so the first two rows of every layout assertion here are <c>html</c> and
/// <c>body</c>.
/// </para>
/// </remarks>
[NonParallelizable]
public class DomDomainTests
{
    private const int Row = 16;

    [Test]
    public async Task GetDocumentAnswersTheDocumentAndTheDepthTheClientAskedFor()
    {
        await using var session = await PageSession.CreateAsync();
        var attachment = await session.OpenPageAsync();
        await Content(session, attachment, "<div id='a'><span id='b'>hi</span></div>");

        await session.ResultAsync("DOM.enable", "{}", attachment);
        var root = (await session.ResultAsync("DOM.getDocument", """{"depth":1}""", attachment)).GetProperty("root");

        root.GetProperty("nodeType").GetInt32().Should().Be(9, "a document is node type 9");
        root.GetProperty("nodeName").GetString().Should().Be("#document");
        root.GetProperty("frameId").GetString().Should().NotBeNullOrEmpty("a document names the frame it is in");
        root.GetProperty("documentURL").GetString().Should().NotBeNullOrEmpty();
        root.GetProperty("backendNodeId").GetInt32().Should().BeGreaterThan(0);

        // depth 1 stops at the document element: it is there, and its own children are not.
        var html = root.GetProperty("children")[0];
        html.GetProperty("nodeName").GetString().Should().Be("HTML");
        html.GetProperty("childNodeCount").GetInt32().Should().Be(2, "head and body");
        html.TryGetProperty("children", out _).Should().BeFalse("depth 1 was asked for");

        // …and -1 is the whole tree, attributes and all.
        var whole = (await session.ResultAsync("DOM.getDocument", """{"depth":-1}""", attachment)).GetProperty("root");
        var div = Find(whole, "DIV");
        div.GetProperty("localName").GetString().Should().Be("div");
        Flat(div.GetProperty("attributes")).Should().Be("id=a");
        Find(whole, "SPAN").GetProperty("nodeId").GetInt32().Should().BeGreaterThan(0);
    }

    [Test]
    public async Task DescribeResolveAndRequestNodeRoundTrip()
    {
        await using var session = await PageSession.CreateAsync();
        var attachment = await session.OpenPageAsync();
        await Content(session, attachment, "<div id='a' class='one two'>hi</div>");

        // A node reaches a client as a handle whose subtype says what it is -- which is what makes a client
        // library build an element handle out of it rather than a plain object handle.
        var handle = await Handle(session, attachment, "document.getElementById('a')");
        handle.Subtype.Should().Be("node");
        handle.ClassName.Should().Be("HTMLDivElement");
        handle.Description.Should().Be("div#a.one.two");

        var described = (await session.ResultAsync(
            "DOM.describeNode", $$"""{"objectId":"{{handle.ObjectId}}"}""", attachment)).GetProperty("node");

        described.GetProperty("nodeName").GetString().Should().Be("DIV");
        described.GetProperty("nodeId").GetInt32().Should().Be(0, "describing a node is not sending one");

        var backendNodeId = described.GetProperty("backendNodeId").GetInt32();

        var nodeId = (await session.ResultAsync(
            "DOM.requestNode", $$"""{"objectId":"{{handle.ObjectId}}"}""", attachment)).GetProperty("nodeId").GetInt32();

        nodeId.Should().BeGreaterThan(0);

        // The identifier is stable, and describing again now reports it.
        (await session.ResultAsync("DOM.describeNode", $$"""{"nodeId":{{nodeId}}}""", attachment))
            .GetProperty("node").GetProperty("nodeId").GetInt32().Should().Be(nodeId);

        // Back the other way: both identifiers resolve to a handle for the same element.
        foreach (var parameters in new[] { $$"""{"nodeId":{{nodeId}}}""", $$"""{"backendNodeId":{{backendNodeId}}}""" })
        {
            var resolved = (await session.ResultAsync("DOM.resolveNode", parameters, attachment)).GetProperty("object");
            resolved.GetProperty("subtype").GetString().Should().Be("node");
            resolved.GetProperty("description").GetString().Should().Be("div#a.one.two");
        }

        // …and a group a client names is one it can release the whole of.
        var grouped = (await session.ResultAsync(
            "DOM.resolveNode", $$"""{"nodeId":{{nodeId}},"objectGroup":"probe"}""", attachment)).GetProperty("object");
        grouped.GetProperty("objectId").GetString().Should().NotBeNullOrEmpty();
        await session.ResultAsync("Runtime.releaseObjectGroup", """{"objectGroup":"probe"}""", attachment);
    }

    [Test]
    public async Task ANavigationClearsEveryNodeIdAndSaysSo()
    {
        using var origin = new Navigation.LoopbackServer();
        origin.MapHtml("/one", "<html><body><div id='a'>one</div></body></html>");
        origin.MapHtml("/two", "<html><body><div id='b'>two</div></body></html>");

        await using var session = await PageSession.CreateAsync(new BrowserContextOptions { UrlFilter = origin.Owns });
        var attachment = await session.OpenPageAsync();
        await session.EnablePageAsync(attachment);
        await session.ResultAsync("DOM.enable", "{}", attachment);

        await session.ResultAsync("Page.navigate", $$"""{"url":"{{origin.Url("/one")}}"}""", attachment);
        await session.EventAsync("Page.loadEventFired", sessionId: attachment);

        var before = (await session.ResultAsync("DOM.getDocument", """{"depth":-1}""", attachment)).GetProperty("root");
        var stale = Find(before, "DIV").GetProperty("nodeId").GetInt32();

        await session.ResultAsync("Page.navigate", $$"""{"url":"{{origin.Url("/two")}}"}""", attachment);
        await session.EventAsync("Page.loadEventFired", 1, attachment);

        session.EventsOf("DOM.documentUpdated", attachment).Should().NotBeEmpty("a commit throws every node identifier away");

        var error = await session.ErrorAsync("DOM.describeNode", $$"""{"nodeId":{{stale}}}""", attachment);
        error.GetProperty("code").GetInt32().Should().Be(-32000);
        error.GetProperty("message").GetString().Should().Be("Could not find node with given id");
    }

    [Test]
    public async Task MutationEventsReachOnlyTheNodesTheClientWasSent()
    {
        await using var session = await PageSession.CreateAsync();
        var attachment = await session.OpenPageAsync();
        await Content(session, attachment, "<ul id='list'><li id='first'>one</li></ul><div id='other'></div>");

        await session.ResultAsync("DOM.enable", "{}", attachment);

        // The client walks the whole tree, so every node in it is one it has been sent.
        await session.ResultAsync("DOM.getDocument", """{"depth":-1}""", attachment);

        await session.EvaluateAsync(
            """
            (() => {
              const list = document.getElementById('list');
              const li = document.createElement('li');
              li.id = 'second';
              list.appendChild(li);
              document.getElementById('first').setAttribute('data-x', '1');
              document.getElementById('first').removeAttribute('id');
              document.getElementById('other').firstChild;
              list.removeChild(list.firstElementChild);
              true;
            })()
            """,
            attachment);

        var inserted = await session.EventAsync("DOM.childNodeInserted", sessionId: attachment);
        inserted.GetProperty("node").GetProperty("nodeName").GetString().Should().Be("LI");

        (await session.EventAsync("DOM.attributeModified", sessionId: attachment))
            .GetProperty("name").GetString().Should().Be("data-x");

        (await session.EventAsync("DOM.attributeRemoved", sessionId: attachment))
            .GetProperty("name").GetString().Should().Be("id");

        (await session.EventAsync("DOM.childNodeRemoved", sessionId: attachment))
            .GetProperty("parentNodeId").GetInt32().Should().Be(inserted.GetProperty("parentNodeId").GetInt32());
    }

    [Test]
    public async Task AClientThatWasSentNothingHearsNothing()
    {
        await using var session = await PageSession.CreateAsync();
        var attachment = await session.OpenPageAsync();
        await Content(session, attachment, "<div id='a'></div>");

        await session.ResultAsync("DOM.enable", "{}", attachment);

        await session.EvaluateAsync("document.getElementById('a').appendChild(document.createElement('p')) && true", attachment);

        // One more round trip, which is a turn of the page loop: whatever the mutation was going to produce
        // has been produced by the time this answers.
        await session.EvaluateAsync("1", attachment);

        session.EventsOf("DOM.childNodeInserted", attachment)
            .Should().BeEmpty("Chrome tells a client only about the nodes it has been given, and this one has none");
    }

    [Test]
    public async Task TheBoxModelAndTheHitTestAgreeAtABoxsCentre()
    {
        await using var session = await PageSession.CreateAsync();
        var attachment = await session.OpenPageAsync();
        await Content(session, attachment, "<div id='a'>one</div><div id='b'>two</div>");

        var handle = await Handle(session, attachment, "document.getElementById('b')");
        var model = (await session.ResultAsync(
            "DOM.getBoxModel", $$"""{"objectId":"{{handle.ObjectId}}"}""", attachment)).GetProperty("model");

        model.GetProperty("width").GetInt32().Should().Be(1280);
        model.GetProperty("height").GetInt32().Should().Be(Row);

        // html, body, #a, #b: the fourth row.
        var quad = model.GetProperty("content");
        quad.GetArrayLength().Should().Be(8);
        quad[1].GetDouble().Should().Be(3 * Row);

        // The four boxes are one box, because a flat box has no padding, border or margin to tell apart.
        foreach (var name in new[] { "padding", "border", "margin" })
        {
            model.GetProperty(name)[1].GetDouble().Should().Be(quad[1].GetDouble());
        }

        var quads = (await session.ResultAsync(
            "DOM.getContentQuads", $$"""{"objectId":"{{handle.ObjectId}}"}""", attachment)).GetProperty("quads");
        quads.GetArrayLength().Should().Be(1);

        // And the centre of that box hits the element the box came from.
        var centreX = (quad[0].GetDouble() + quad[2].GetDouble()) / 2;
        var centreY = (quad[1].GetDouble() + quad[5].GetDouble()) / 2;

        var located = await session.ResultAsync(
            "DOM.getNodeForLocation",
            $$"""{"x":{{(int) centreX}},"y":{{(int) centreY}}}""",
            attachment);

        var describedAgain = (await session.ResultAsync(
            "DOM.describeNode",
            $$"""{"backendNodeId":{{located.GetProperty("backendNodeId").GetInt32()}}}""",
            attachment)).GetProperty("node");

        Flat(describedAgain.GetProperty("attributes")).Should().Be("id=b");
        located.GetProperty("frameId").GetString().Should().NotBeNullOrEmpty();
    }

    [Test]
    public async Task ANodeWithNoBoxIsRefusedRatherThanMeasuredAsZero()
    {
        await using var session = await PageSession.CreateAsync();
        var attachment = await session.OpenPageAsync();
        await Content(session, attachment, "<div id='a' hidden>gone</div>");

        var handle = await Handle(session, attachment, "document.getElementById('a')");

        var box = await session.ErrorAsync("DOM.getBoxModel", $$"""{"objectId":"{{handle.ObjectId}}"}""", attachment);
        box.GetProperty("message").GetString().Should().Be("Could not compute box model.");

        var quads = await session.ErrorAsync("DOM.getContentQuads", $$"""{"objectId":"{{handle.ObjectId}}"}""", attachment);
        quads.GetProperty("message").GetString().Should().Be("Could not compute content quads.");

        var scroll = await session.ErrorAsync("DOM.scrollIntoViewIfNeeded", $$"""{"objectId":"{{handle.ObjectId}}"}""", attachment);
        scroll.GetProperty("message").GetString().Should().Be("Node does not have a layout object");

        // And nothing is at the point where its row would have been.
        var nowhere = await session.ErrorAsync("DOM.getNodeForLocation", """{"x":10,"y":700}""", attachment);
        nowhere.GetProperty("message").GetString().Should().Be("No node found at given location");
    }

    [Test]
    public async Task ScrollIntoViewIfNeededMovesTheVirtualScroll()
    {
        await using var session = await PageSession.CreateAsync(
            options: new BrowserOptions { Viewport = new Viewport(800, 64) });

        var attachment = await session.OpenPageAsync();
        await Content(session, attachment, string.Concat(Enumerable.Range(0, 20).Select(i => $"<p id='p{i}'>{i}</p>")));

        var handle = await Handle(session, attachment, "document.getElementById('p15')");
        await session.ResultAsync("DOM.scrollIntoViewIfNeeded", $$"""{"objectId":"{{handle.ObjectId}}"}""", attachment);

        // 'if needed' is the nearest alignment, so a row below the window comes to the bottom of it.
        var scrolled = await session.EvaluateAsync("window.scrollY", attachment);
        scrolled.GetProperty("value").GetDouble().Should().Be((18 * Row) - 64);
    }

    [Test]
    public async Task QuerySelectorAttributesAndOuterHtmlEditTheDocument()
    {
        await using var session = await PageSession.CreateAsync();
        var attachment = await session.OpenPageAsync();
        await Content(session, attachment, "<div id='a' class='x'>one</div><div class='x'>two</div>");

        await session.ResultAsync("DOM.enable", "{}", attachment);
        var root = (await session.ResultAsync("DOM.getDocument", """{"depth":-1}""", attachment)).GetProperty("root");
        var documentId = root.GetProperty("nodeId").GetInt32();

        var one = (await session.ResultAsync(
            "DOM.querySelector", $$"""{"nodeId":{{documentId}},"selector":"#a"}""", attachment)).GetProperty("nodeId").GetInt32();
        one.Should().BeGreaterThan(0);

        (await session.ResultAsync(
            "DOM.querySelectorAll", $$"""{"nodeId":{{documentId}},"selector":".x"}""", attachment))
            .GetProperty("nodeIds").GetArrayLength().Should().Be(2);

        Flat((await session.ResultAsync("DOM.getAttributes", $$"""{"nodeId":{{one}}}""", attachment)).GetProperty("attributes"))
            .Should().Be("id=a,class=x");

        await session.ResultAsync("DOM.setAttributeValue", $$"""{"nodeId":{{one}},"name":"title","value":"t"}""", attachment);
        await session.ResultAsync("DOM.removeAttribute", $$"""{"nodeId":{{one}},"name":"class"}""", attachment);
        await session.ResultAsync("DOM.setAttributesAsText", $$"""{"nodeId":{{one}},"text":"data-k=\"v\""}""", attachment);
        await session.ResultAsync("DOM.markUndoableState", "{}", attachment);

        Flat((await session.ResultAsync("DOM.getAttributes", $$"""{"nodeId":{{one}}}""", attachment)).GetProperty("attributes"))
            .Should().Be("id=a,title=t,data-k=v");

        (await session.ResultAsync("DOM.getOuterHTML", $$"""{"nodeId":{{one}}}""", attachment))
            .GetProperty("outerHTML").GetString().Should().Contain("data-k=\"v\"");

        // Markup a client sends is parsed and not executed, which is what innerHTML's own rule says.
        await session.ResultAsync(
            "DOM.setOuterHTML",
            $$"""{"nodeId":{{one}},"outerHTML":"<section id=\"replaced\"><script>window.ran = true;<\/script></section>"}""",
            attachment);

        (await session.EvaluateAsync("document.getElementById('replaced').tagName", attachment))
            .GetProperty("value").GetString().Should().Be("SECTION");
        (await session.EvaluateAsync("typeof window.ran", attachment))
            .GetProperty("value").GetString().Should().Be("undefined");
    }

    /// <summary>Chrome's three arms: a selector, an XPath expression, and a text substring.</summary>
    [Test]
    public async Task PerformSearchFindsBySelectorByXPathAndByText()
    {
        await using var session = await PageSession.CreateAsync();
        var attachment = await session.OpenPageAsync();
        await Content(session, attachment, "<div class='needle'>alpha</div><p>a haystack sentence</p>");

        await session.ResultAsync("DOM.enable", "{}", attachment);

        var bySelector = await session.ResultAsync("DOM.performSearch", """{"query":".needle"}""", attachment);
        bySelector.GetProperty("resultCount").GetInt32().Should().Be(1);

        var results = await session.ResultAsync(
            "DOM.getSearchResults",
            $$"""{"searchId":"{{bySelector.GetProperty("searchId").GetString()}}","fromIndex":0,"toIndex":1}""",
            attachment);

        results.GetProperty("nodeIds").GetArrayLength().Should().Be(1);

        // The arm this browser had none of until DOM XPath arrived: a front end's search box sends one of
        // these for anything beginning `//`, and it is the same evaluator document.evaluate answers from.
        var byXPath = await session.ResultAsync("DOM.performSearch", """{"query":"//div[@class='needle']"}""", attachment);
        byXPath.GetProperty("resultCount").GetInt32().Should().Be(1);

        // A query that is not an expression contributes nothing rather than failing the command: a search
        // box is typed into one character at a time.
        var nonsense = await session.ResultAsync("DOM.performSearch", """{"query":"//div["}""", attachment);
        nonsense.GetProperty("resultCount").GetInt32().Should().Be(0);

        var byText = await session.ResultAsync("DOM.performSearch", """{"query":"haystack"}""", attachment);
        byText.GetProperty("resultCount").GetInt32().Should().Be(1);

        await session.ResultAsync(
            "DOM.discardSearchResults",
            $$"""{"searchId":"{{byText.GetProperty("searchId").GetString()}}"}""",
            attachment);

        var gone = await session.ErrorAsync(
            "DOM.getSearchResults",
            $$"""{"searchId":"{{byText.GetProperty("searchId").GetString()}}","fromIndex":0,"toIndex":1}""",
            attachment);

        gone.GetProperty("code").GetInt32().Should().Be(-32000);
    }

    [Test]
    public async Task RequestChildNodesFocusRemoveNodeAndSetNodeValue()
    {
        await using var session = await PageSession.CreateAsync();
        var attachment = await session.OpenPageAsync();
        await Content(session, attachment, "<ul id='list'><li>one</li><li>two</li></ul><input id='field'>");

        await session.ResultAsync("DOM.enable", "{}", attachment);
        var root = (await session.ResultAsync("DOM.getDocument", """{"depth":1}""", attachment)).GetProperty("root");
        var html = root.GetProperty("children")[0].GetProperty("nodeId").GetInt32();

        await session.ResultAsync("DOM.requestChildNodes", $$"""{"nodeId":{{html}},"depth":-1}""", attachment);

        var sent = await session.EventAsync("DOM.setChildNodes", sessionId: attachment);
        sent.GetProperty("parentId").GetInt32().Should().Be(html);

        var list = Find(sent.GetProperty("nodes")[1], "UL");
        list.GetProperty("childNodeCount").GetInt32().Should().Be(2);

        var text = Find(sent.GetProperty("nodes")[1], "LI").GetProperty("children")[0];
        text.GetProperty("nodeValue").GetString().Should().Be("one");

        await session.ResultAsync(
            "DOM.setNodeValue", $$"""{"nodeId":{{text.GetProperty("nodeId").GetInt32()}},"value":"edited"}""", attachment);

        (await session.EvaluateAsync("document.querySelector('li').textContent", attachment))
            .GetProperty("value").GetString().Should().Be("edited");

        var field = await Handle(session, attachment, "document.getElementById('field')");
        await session.ResultAsync("DOM.focus", $$"""{"objectId":"{{field.ObjectId}}"}""", attachment);
        (await session.EvaluateAsync("document.activeElement.id", attachment))
            .GetProperty("value").GetString().Should().Be("field");

        await session.ResultAsync(
            "DOM.removeNode", $$"""{"nodeId":{{list.GetProperty("nodeId").GetInt32()}}}""", attachment);
        (await session.EvaluateAsync("document.querySelector('ul') === null", attachment))
            .GetProperty("value").GetBoolean().Should().BeTrue();
    }

    [Test]
    public async Task GetFrameOwnerRefusesTheOnlyFrameThereIs()
    {
        await using var session = await PageSession.CreateAsync();
        var attachment = await session.OpenPageAsync();

        var unknown = await session.ErrorAsync("DOM.getFrameOwner", """{"frameId":"nothing"}""", attachment);
        unknown.GetProperty("message").GetString().Should().Be("Frame with the given id was not found.");
    }

    /// <summary>Replaces the page's document, on the attachment, and waits for it.</summary>
    private static async Task Content(PageSession session, string attachment, string body)
    {
        await session.ResultAsync(
            "Page.setDocumentContent",
            JsonSerializer.Serialize(new Dictionary<string, object> { ["frameId"] = "", ["html"] = body }),
            attachment);
    }

    /// <summary>Evaluates one expression and hands back the handle the server minted for it.</summary>
    private static async Task<NodeHandle> Handle(PageSession session, string attachment, string expression)
    {
        var result = await session.ResultAsync(
            "Runtime.evaluate",
            JsonSerializer.Serialize(new Dictionary<string, object> { ["expression"] = expression }),
            attachment);

        var value = result.GetProperty("result");

        return new NodeHandle(
            value.GetProperty("objectId").GetString()!,
            value.TryGetProperty("subtype", out var subtype) ? subtype.GetString() : null,
            value.TryGetProperty("className", out var className) ? className.GetString() : null,
            value.TryGetProperty("description", out var description) ? description.GetString() : null);
    }

    /// <summary>The first node of a subtree whose <c>nodeName</c> matches, depth first.</summary>
    private static JsonElement Find(JsonElement node, string nodeName)
    {
        if (node.GetProperty("nodeName").GetString() == nodeName)
        {
            return node;
        }

        if (node.TryGetProperty("children", out var children))
        {
            foreach (var child in children.EnumerateArray())
            {
                var found = Find(child, nodeName);
                if (found.ValueKind != JsonValueKind.Undefined)
                {
                    return found;
                }
            }
        }

        return default;
    }

    /// <summary>The protocol's flat attribute array, as <c>name=value</c> pairs, for a readable failure.</summary>
    private static string Flat(JsonElement attributes)
    {
        var pairs = new List<string>();
        var values = attributes.EnumerateArray().Select(entry => entry.GetString() ?? "").ToArray();

        for (var i = 0; i + 1 < values.Length; i += 2)
        {
            pairs.Add(values[i] + "=" + values[i + 1]);
        }

        return string.Join(",", pairs);
    }

    /// <summary>What a client holds a node by, and what the describer said about it.</summary>
    private sealed record NodeHandle(string ObjectId, string? Subtype, string? ClassName, string? Description);
}
