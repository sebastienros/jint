using System.Text.Json;

namespace Jint.Tests.Browser.DevTools;

/// <summary>
/// The <c>Accessibility</c> domain over a real document: the tree, a part of it, a query, and the identity
/// it shares with the <c>DOM</c> domain.
/// </summary>
/// <remarks>
/// What the computation itself answers — the roles, the names, the ignored verdicts — is
/// <c>Accessibility/</c>'s own suite and its golden files. What is asserted here is the half only a session
/// can supply: Chrome's <c>AXNode</c> shape on the wire, the <c>backendDOMNodeId</c> that ties a node back to
/// one the <c>DOM</c> domain can measure and click, and the four ways a client asks for part of the tree.
/// </remarks>
[NonParallelizable]
public class AccessibilityDomainTests
{
    private const string Document =
        """
        <html><head><title>Accessible</title></head>
        <body>
          <h1>Heading</h1>
          <button id="save">Save changes</button>
          <input id="name" aria-label="Your name">
          <div id="plain">text</div>
        </body></html>
        """;

    [Test]
    public async Task TheFullTreeIsChromesShapeWithARootWebAreaAndABackendNodeOnEveryNode()
    {
        await using var session = await PageSession.CreateAsync();
        var attachment = await OpenAsync(session);

        var nodes = (await session.ResultAsync("Accessibility.getFullAXTree", null, attachment))
            .GetProperty("nodes")
            .EnumerateArray()
            .ToArray();

        nodes.Should().NotBeEmpty();

        var root = nodes[0];
        root.GetProperty("role").GetProperty("value").GetString().Should().Be("RootWebArea");
        root.GetProperty("name").GetProperty("value").GetString().Should().Be("Accessible");
        root.GetProperty("frameId").GetString().Should().NotBeNullOrEmpty("Chrome names the frame on the tree's root");
        root.TryGetProperty("parentId", out _).Should().BeFalse("the root has no parent");

        // Every node carries the identifier the DOM domain addresses the same node by, and the values are
        // written as the JSON primitives their type calls for rather than as their text.
        foreach (var node in nodes)
        {
            node.GetProperty("nodeId").GetString().Should().NotBeNullOrEmpty();
            node.GetProperty("ignored").ValueKind.Should().BeOneOf(JsonValueKind.True, JsonValueKind.False);
            node.GetProperty("backendDOMNodeId").GetInt32().Should().BeGreaterThan(0);
        }

        var button = nodes.Single(n => Role(n) == "button");
        button.GetProperty("name").GetProperty("value").GetString().Should().Be("Save changes");
        button.GetProperty("name").GetProperty("type").GetString().Should().Be("computedString");
        button.GetProperty("parentId").GetString().Should().NotBeNullOrEmpty();

        var textbox = nodes.Single(n => Role(n) == "textbox");
        textbox.GetProperty("name").GetProperty("value").GetString().Should().Be("Your name");

        // A property whose value is a boolean is a JSON boolean, which is what a front end reads it as.
        var focusable = nodes
            .Where(n => n.TryGetProperty("properties", out _))
            .SelectMany(n => n.GetProperty("properties").EnumerateArray())
            .Where(p => p.GetProperty("name").GetString() == "focusable")
            .ToArray();

        focusable.Should().NotBeEmpty();
        focusable[0].GetProperty("value").GetProperty("value").ValueKind
            .Should().BeOneOf(JsonValueKind.True, JsonValueKind.False);
    }

    [Test]
    public async Task DepthStopsTheWalkAndNoNodeNamesAChildTheReplyDoesNotCarry()
    {
        await using var session = await PageSession.CreateAsync();
        var attachment = await OpenAsync(session);

        var shallow = (await session.ResultAsync("Accessibility.getFullAXTree", """{"depth":0}""", attachment))
            .GetProperty("nodes")
            .EnumerateArray()
            .ToArray();

        shallow.Should().HaveCount(1, "depth 0 is the root alone");
        shallow[0].GetProperty("childIds").EnumerateArray().Should().BeEmpty(
            "a client walks childIds without asking whether it is there, so a truncated node names nobody");

        var whole = (await session.ResultAsync("Accessibility.getFullAXTree", null, attachment))
            .GetProperty("nodes")
            .EnumerateArray()
            .ToArray();

        whole.Length.Should().BeGreaterThan(1);

        // And in a whole tree every named child really is in the reply, which is what stops a client's own
        // tree builder from dereferencing an identifier it was never sent.
        var ids = whole.Select(n => n.GetProperty("nodeId").GetString()).ToHashSet(StringComparer.Ordinal);

        foreach (var node in whole)
        {
            foreach (var child in node.GetProperty("childIds").EnumerateArray())
            {
                ids.Should().Contain(child.GetString());
            }
        }
    }

    [Test]
    public async Task TheRootAndItsChildrenAreReachableOneStepAtATime()
    {
        await using var session = await PageSession.CreateAsync();
        var attachment = await OpenAsync(session);

        var root = (await session.ResultAsync("Accessibility.getRootAXNode", null, attachment)).GetProperty("node");
        root.GetProperty("role").GetProperty("value").GetString().Should().Be("RootWebArea");

        var rootId = root.GetProperty("nodeId").GetString()!;
        var children = (await session.ResultAsync("Accessibility.getChildAXNodes", $$"""{"id":"{{rootId}}"}""", attachment))
            .GetProperty("nodes")
            .EnumerateArray()
            .ToArray();

        children.Should().NotBeEmpty();
        children.Should().OnlyContain(child => child.GetProperty("parentId").GetString() == rootId);

        var unknown = await session.ErrorAsync("Accessibility.getChildAXNodes", """{"id":"999999999"}""", attachment);
        unknown.GetProperty("message").GetString().Should().Contain("Could not find node with given id");
    }

    [Test]
    public async Task APartialTreeIsTheNodeItsAncestorsAndItsChildren()
    {
        await using var session = await PageSession.CreateAsync();
        var attachment = await OpenAsync(session);

        var nodeId = await NodeIdAsync(session, attachment, "#save");

        var partial = (await session.ResultAsync("Accessibility.getPartialAXTree", $$"""{"nodeId":{{nodeId}}}""", attachment))
            .GetProperty("nodes")
            .EnumerateArray()
            .ToArray();

        partial.Select(Role).Should().Contain("RootWebArea", "fetchRelatives defaults to true, so the ancestors come too");
        partial.Select(Role).Should().Contain("button");

        var alone = (await session.ResultAsync(
                "Accessibility.getPartialAXTree",
                $$"""{"nodeId":{{nodeId}},"fetchRelatives":false}""",
                attachment))
            .GetProperty("nodes")
            .EnumerateArray()
            .ToArray();

        alone.Should().HaveCount(1);
        Role(alone[0]).Should().Be("button");
    }

    [Test]
    public async Task ANodeAndItsAncestorsIsTheChainFromTheRootDown()
    {
        await using var session = await PageSession.CreateAsync();
        var attachment = await OpenAsync(session);

        var nodeId = await NodeIdAsync(session, attachment, "#save");

        var chain = (await session.ResultAsync("Accessibility.getAXNodeAndAncestors", $$"""{"nodeId":{{nodeId}}}""", attachment))
            .GetProperty("nodes")
            .EnumerateArray()
            .ToArray();

        Role(chain[0]).Should().Be("RootWebArea", "the chain reads from the root down");
        Role(chain[^1]).Should().Be("button");

        // Each entry names the one before it, which is what makes the list a chain rather than a set.
        for (var i = 1; i < chain.Length; i++)
        {
            chain[i].GetProperty("parentId").GetString().Should().Be(chain[i - 1].GetProperty("nodeId").GetString());
        }
    }

    [Test]
    public async Task AQueryFindsANodeByItsRoleAndItsComputedName()
    {
        await using var session = await PageSession.CreateAsync();
        var attachment = await OpenAsync(session);

        var byBoth = (await session.ResultAsync(
                "Accessibility.queryAXTree",
                """{"accessibleName":"Save changes","role":"button"}""",
                attachment))
            .GetProperty("nodes")
            .EnumerateArray()
            .ToArray();

        byBoth.Should().HaveCount(1);
        byBoth[0].GetProperty("backendDOMNodeId").GetInt32().Should().BeGreaterThan(0);

        var byRole = (await session.ResultAsync("Accessibility.queryAXTree", """{"role":"textbox"}""", attachment))
            .GetProperty("nodes")
            .EnumerateArray()
            .ToArray();

        byRole.Should().HaveCount(1);
        byRole[0].GetProperty("name").GetProperty("value").GetString().Should().Be("Your name");

        var missing = (await session.ResultAsync("Accessibility.queryAXTree", """{"role":"treegrid"}""", attachment))
            .GetProperty("nodes")
            .EnumerateArray()
            .ToArray();

        missing.Should().BeEmpty("a query that matches nothing is an empty list rather than an error");
    }

    [Test]
    public async Task AnAccessibilityNodeAndADomNodeAreTheSameNode()
    {
        await using var session = await PageSession.CreateAsync();
        var attachment = await OpenAsync(session);

        var found = (await session.ResultAsync(
                "Accessibility.queryAXTree",
                """{"role":"button"}""",
                attachment))
            .GetProperty("nodes")
            .EnumerateArray()
            .Single();

        var backendNodeId = found.GetProperty("backendDOMNodeId").GetInt32();

        // The identifier the accessibility tree minted resolves in the DOM domain, and names the element the
        // page can be driven through.
        var described = (await session.ResultAsync(
                "DOM.describeNode",
                $$"""{"backendNodeId":{{backendNodeId}}}""",
                attachment))
            .GetProperty("node");

        described.GetProperty("nodeName").GetString().Should().Be("BUTTON");

        // …and the other way: a node the DOM domain gave a client is one the accessibility tree answers about.
        var nodeId = await NodeIdAsync(session, attachment, "#name");
        var partial = (await session.ResultAsync("Accessibility.getPartialAXTree", $$"""{"nodeId":{{nodeId}},"fetchRelatives":false}""", attachment))
            .GetProperty("nodes")
            .EnumerateArray()
            .Single();

        Role(partial).Should().Be("textbox");
    }

    [Test]
    public async Task AFrameThatIsNotThePagesIsRefusedInChromesWording()
    {
        await using var session = await PageSession.CreateAsync();
        var attachment = await OpenAsync(session);

        var error = await session.ErrorAsync("Accessibility.getFullAXTree", """{"frameId":"NOT-A-FRAME"}""", attachment);
        error.GetProperty("message").GetString().Should().Contain("Frame with the given id was not found.");
    }

    [Test]
    public async Task EnableAndDisableAreAnswered()
    {
        await using var session = await PageSession.CreateAsync();
        var attachment = await OpenAsync(session);

        await session.ResultAsync("Accessibility.enable", null, attachment);
        await session.ResultAsync("Accessibility.disable", null, attachment);
    }

    private static string? Role(JsonElement node)
        => node.TryGetProperty("role", out var role) ? role.GetProperty("value").GetString() : null;

    /// <summary>A page showing the fixture, attached and with the <c>DOM</c> domain enabled.</summary>
    private static async Task<string> OpenAsync(PageSession session)
    {
        var attachment = await session.OpenPageAsync().ConfigureAwait(false);
        await session.EnablePageAsync(attachment).ConfigureAwait(false);
        await session.ResultAsync("DOM.enable", null, attachment).ConfigureAwait(false);

        var tree = await session.ResultAsync("Page.getFrameTree", null, attachment).ConfigureAwait(false);
        var frameId = tree.GetProperty("frameTree").GetProperty("frame").GetProperty("id").GetString()!;

        var payload = JsonSerializer.Serialize(new Dictionary<string, string>
        {
            ["frameId"] = frameId,
            ["html"] = Document,
        });

        await session.ResultAsync("Page.setDocumentContent", payload, attachment).ConfigureAwait(false);
        return attachment;
    }

    /// <summary>The <c>nodeId</c> the <c>DOM</c> domain addresses one element by.</summary>
    private static async Task<int> NodeIdAsync(PageSession session, string attachment, string selector)
    {
        var document = await session.ResultAsync("DOM.getDocument", """{"depth":0}""", attachment).ConfigureAwait(false);
        var rootId = document.GetProperty("root").GetProperty("nodeId").GetInt32();

        var found = await session.ResultAsync(
            "DOM.querySelector",
            $$"""{"nodeId":{{rootId}},"selector":"{{selector}}"}""",
            attachment).ConfigureAwait(false);

        return found.GetProperty("nodeId").GetInt32();
    }
}
