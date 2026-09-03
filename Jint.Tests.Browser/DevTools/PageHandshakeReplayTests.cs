using System.Globalization;
using System.Text.Json;
using Jint.Browser;
using Jint.Tests.Browser.Navigation;

namespace Jint.Tests.Browser.DevTools;

/// <summary>
/// Every command a real client sends up to and including its first click, replayed against a real page.
/// </summary>
/// <remarks>
/// <para>
/// The recordings in <c>tools/devtools-protocol/handshakes/</c> are what Puppeteer, PuppeteerSharp,
/// Playwright and Playwright for .NET actually put on the wire while driving one Chrome through one scenario.
/// <c>matrix.md</c>'s "minimum must-answer set" is the first five steps of it — <c>connect</c>,
/// <c>newContext</c>, <c>newPage</c>, <c>goto</c>, the two evaluations — and its "what <c>$</c>,
/// <c>$$</c>, <c>click</c> and <c>waitForSelector</c> add on top" is the four after them. <c>type</c> — six
/// <c>Input.dispatchKeyEvent</c> in every one of the five recordings — and the cookie and interception steps
/// are the four after <i>those</i>. This replays all of them, in the order each client sent them, on the
/// session each belongs on.
/// </para>
/// <para>
/// <b>Two properties, and the second is the interesting one.</b> Nothing may answer <c>-32601</c> or
/// <c>-32602</c> except the methods <see cref="Absent"/> names with a reason; and the events the client then
/// waited on — <c>attachedToTarget</c>, <c>executionContextCreated</c>, <c>frameNavigated</c>,
/// <c>lifecycleEvent</c> for <c>load</c>, <c>loadEventFired</c> — have to have arrived, on the attachment
/// rather than on the browser conversation, in the recorded relative order.
/// </para>
/// <para>
/// A replay is not a client: it does not know a command's parameters, so <see cref="Parameters"/> supplies
/// the ones a command cannot be answered without and the rest go out empty. What it does know is what a real
/// client sent and in what order, which is the thing no compatibility table can tell you.
/// </para>
/// </remarks>
[NonParallelizable]
public class PageHandshakeReplayTests
{
    /// <summary>
    /// Why each recorded method this server does not answer is expected to fail, and how.
    /// </summary>
    /// <remarks>
    /// Every one of these is a campaign item rather than a decision, and each names it. <c>WebMCP.enable</c>
    /// is the exception: Chrome itself answered <c>-32601</c> to it in the very recording, so a client that
    /// sends it already handles not getting it.
    /// </remarks>
    private static readonly Dictionary<string, string> Absent = new(StringComparer.Ordinal)
    {
        ["WebMCP.enable"] = "Chrome answered -32601 in the recording itself",
    };

    /// <summary>
    /// Every step of the scenario this replay covers but one, in the order the recording has them.
    /// </summary>
    /// <remarks>
    /// A page's cookies are read and written through <c>Storage</c> by every recorded client, <c>interception</c>
    /// is where <c>Fetch.enable</c> and <c>Fetch.continueRequest</c> appear, <c>type</c> is six
    /// <c>Input.dispatchKeyEvent</c> in every one of the five recordings, and <c>screenshot</c> is the refusal
    /// this browser answers by design. The one step left out is <c>pdf</c>: it needs the <c>IO</c> stream a
    /// <c>printToPDF</c> would have written into, and that command is refused by design.
    /// </remarks>
    private static readonly string[] ReplayedSteps =
    [
        "connect", "newContext", "newPage", "goto", "evaluateTitle", "evaluateObject",
        "querySelector", "querySelectorAll", "click", "waitForSelector", "type",
        "clickCheckbox", "selectOption", "content", "title", "evaluateLocalStorage", "consoleEvent",
        "gotoPage2", "goBack", "screenshot", "cookies", "setCookie", "interception",
    ];

    [TestCase("puppeteer-node")]
    [TestCase("puppeteersharp-dotnet")]
    [TestCase("playwright-node")]
    [TestCase("playwright-dotnet")]
    public async Task EveryMethodAClientSendsUpToItsFirstClickIsAnswered(string client)
    {
        var methods = RecordedMethods(client);
        methods.Should().NotBeEmpty("the recording is the specification these tests are written against");

        using var server = new LoopbackServer();
        server.MapHtml("/page", "<html><head><title>Handshake</title></head><body><p>ready</p></body></html>");

        await using var session = await PageSession.CreateAsync(new BrowserContextOptions { UrlFilter = server.Owns });

        // The two commands the replay cannot make up, because everything after them addresses what they
        // answered: the context a page is created in, and the attachment every page-level command rides.
        var contextId = (await session.ResultAsync("Target.createBrowserContext", "{}"))
            .GetProperty("browserContextId").GetString()!;

        await session.ResultAsync("Target.setAutoAttach", """{"autoAttach":true,"waitForDebuggerOnStart":true,"flatten":true}""");
        await session.ResultAsync("Target.setDiscoverTargets", """{"discover":true}""");

        var targetId = (await session.ResultAsync("Target.createTarget", $$"""{"url":"about:blank","browserContextId":"{{contextId}}"}"""))
            .GetProperty("targetId").GetString()!;

        var attached = await session.EventAsync("Target.attachedToTarget");
        attached.GetProperty("targetInfo").GetProperty("targetId").GetString().Should().Be(targetId);

        var attachment = attached.GetProperty("sessionId").GetString()!;
        var refused = new List<string>();
        string? handle = null;
        string? node = null;
        var nodeId = 0;

        foreach (var method in methods)
        {
            // The one command that cannot be sent without something the server minted moments earlier: a
            // client calls a function *on* a handle it was just given, so the replay takes one at the point
            // in the sequence the client would have had one.
            if (method is "Runtime.callFunctionOn" or "Runtime.getProperties" or "Runtime.releaseObject")
            {
                handle = await HandleAsync(session, attachment);
            }

            // The DOM half is the same shape of dependency, and it is what the four steps this replay added
            // are about: a client turns an element handle into a node it can measure and click.
            if (method.StartsWith("DOM.", StringComparison.Ordinal) || method == "Input.dispatchMouseEvent")
            {
                node ??= await NodeHandleAsync(session, attachment);
                nodeId = nodeId != 0 ? nodeId : await NodeIdAsync(session, attachment, node);
            }

            var reply = await BestAnswerAsync(session, method, attachment, targetId, contextId, server.Url("/page"), handle, node, nodeId);
            if (!reply.TryGetProperty("error", out var error))
            {
                Absent.Should().NotContainKey(method, "'{0}' is answered now, so the reason it is excused is stale", method);
                continue;
            }

            var code = error.GetProperty("code").GetInt32();
            if (code != -32601 && code != -32602)
            {
                // A -32000 is a refusal with a reason, which is a different thing from not being there:
                // captureScreenshot and printToPDF are the two, and both say why in their message.
                continue;
            }

            if (!Absent.ContainsKey(method))
            {
                refused.Add($"{method} -> {code}: {error.GetProperty("message").GetString()}");
            }
        }

        Assert.That(
            refused.Count == 0,
            $"""
            {refused.Count} method(s) the '{client}' recording sends up to and including its first click are
            not answered. Every one of them is a command that client needs to find an element and click it,
            so each is either implemented or accounted for in PageHandshakeReplayTests.Absent with the reason:

            {string.Join(Environment.NewLine, refused.Select(entry => "  " + entry))}
            """);

        // And the events the client then waits on, on the attachment, in the recorded relative order.
        await session.EventAsync("Page.loadEventFired", sessionId: attachment);

        session.EventsOf("Runtime.executionContextCreated", attachment).Should().NotBeEmpty();
        session.EventsOf("Page.frameNavigated", attachment).Should().NotBeEmpty();

        // The document's own request, which is what a client builds its `goto` response object out of: its
        // requestId is the loaderId, and every client tells the navigation apart by exactly that.
        var sent = session.EventsOf("Network.requestWillBeSent", attachment);
        sent.Should().NotBeEmpty();

        var documentRequest = sent[0].GetProperty("params");
        documentRequest.GetProperty("requestId").GetString()
            .Should().Be(documentRequest.GetProperty("loaderId").GetString());

        var received = session.EventsOf("Network.responseReceived", attachment);
        received.Should().NotBeEmpty("a client's goto answers a response object built from this event");
        received[0].GetProperty("params").GetProperty("response").GetProperty("status").GetInt32().Should().Be(200);

        session.EventsOf("Page.lifecycleEvent", attachment)
            .Select(e => e.GetProperty("params").GetProperty("name").GetString())
            .Should().Contain("load");

        session.Ordinal("Target.attachedToTarget").Should().BeLessThan(session.Ordinal("Page.frameNavigated"));
        session.Ordinal("Page.frameNavigated").Should().BeLessThan(session.Ordinal("Page.loadEventFired"));
    }

    /// <summary>
    /// Every parameter <b>shape</b> a client was recorded sending, replayed against a real page.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The name-only pin missed two defects, and this is the one that would not have.</b> The replay above
    /// asks whether a method is <i>reachable</i>; a client sends a method with particular members, and two of
    /// the four defects Playwright found were about exactly that — <c>Target.getTargetInfo</c> with
    /// <b>no</b> parameters at all, and a page of the <i>default</i> browser context, which Playwright
    /// refuses unless the target says which context it is in. Both are shapes rather than names, and both
    /// passed a pin that only counted methods.
    /// </para>
    /// <para>
    /// So this rebuilds each recorded call out of its own <c>paramsKeys</c> — the recorded
    /// <c>paramsValues</c> where the recording kept one, the live identifier where the key names one, and a
    /// value of the right JSON type otherwise — and asserts the answer is never <c>-32602</c>. A method this
    /// server does not implement answers <c>-32601</c> before it looks at the parameters, which is Chrome's
    /// own order and not a failure here; <see cref="Absent"/> is where a name is excused.
    /// </para>
    /// </remarks>
    [TestCase("puppeteer-node")]
    [TestCase("puppeteersharp-dotnet")]
    [TestCase("playwright-node")]
    [TestCase("playwright-dotnet")]
    public async Task EveryParameterShapeAClientSendsIsUnderstood(string client)
    {
        var shapes = RecordedShapes(client);
        shapes.Should().NotBeEmpty("the recording is the specification these tests are written against");

        using var server = new LoopbackServer();
        server.MapHtml("/page", "<html><head><title>Handshake</title></head><body><p>ready</p></body></html>");

        await using var session = await PageSession.CreateAsync(new BrowserContextOptions { UrlFilter = server.Owns });

        var contextId = (await session.ResultAsync("Target.createBrowserContext", "{}"))
            .GetProperty("browserContextId").GetString()!;

        await session.ResultAsync("Target.setAutoAttach", """{"autoAttach":true,"waitForDebuggerOnStart":true,"flatten":true}""");

        var targetId = (await session.ResultAsync("Target.createTarget", $$"""{"url":"about:blank","browserContextId":"{{contextId}}"}"""))
            .GetProperty("targetId").GetString()!;

        var attached = await session.EventAsync("Target.attachedToTarget");
        var attachment = attached.GetProperty("sessionId").GetString()!;

        // What every client enables before it drives a page, and what makes the load event arrive: the
        // replay needs a real document under it, because half of these shapes address a node in one.
        await session.EnablePageAsync(attachment);

        await session.ResultAsync("Page.navigate", $$"""{"url":"{{server.Url("/page")}}"}""", attachment);
        await session.EventAsync("Page.loadEventFired", sessionId: attachment);

        var node = await NodeHandleAsync(session, attachment);
        var nodeId = await NodeIdAsync(session, attachment, node);
        var handle = await HandleAsync(session, attachment);

        var identifiers = new Identifiers(targetId, contextId, attachment, server.Url("/page"), handle, node, nodeId);
        var misread = new List<string>();

        foreach (var shape in shapes)
        {
            var parameters = Shape(shape, identifiers);
            var reply = await BestAnswerAsync(session, shape.Method, attachment, parameters).ConfigureAwait(false);

            if (!reply.TryGetProperty("error", out var error) || error.GetProperty("code").GetInt32() != -32602)
            {
                continue;
            }

            var detail = error.TryGetProperty("data", out var data) ? ": " + data.GetString() : "";
            misread.Add($"{shape.Method}({string.Join(", ", shape.Keys)}) -> {error.GetProperty("message").GetString()}{detail}");
        }

        Assert.That(
            misread.Count == 0,
            $"""
            {misread.Count} call(s) the '{client}' recording makes are answered -32602 — the parameters were
            read and refused. Each is a member of the shape that client really sends, so each is either
            accepted or a deliberate change to what this server takes:

            {string.Join(Environment.NewLine, misread.Select(entry => "  " + entry))}
            """);
    }

    /// <summary>
    /// The two shapes the name-only pin let through, asserted by themselves so that a regression names itself.
    /// </summary>
    [Test]
    public async Task TheTwoShapesTheNameOnlyPinMissedAreAnsweredExplicitly()
    {
        await using var session = await PageSession.CreateAsync();

        await session.ResultAsync("Target.setAutoAttach", """{"autoAttach":true,"waitForDebuggerOnStart":true,"flatten":true}""");

        // No browserContextId: the page belongs to the browser's default context, which is the case
        // Playwright's connectOverCDP adopts and the one that used to name no context at all.
        var targetId = (await session.ResultAsync("Target.createTarget", """{"url":"about:blank"}"""))
            .GetProperty("targetId").GetString()!;

        var attached = await session.EventAsync("Target.attachedToTarget");
        var attachment = attached.GetProperty("sessionId").GetString()!;

        attached.GetProperty("targetInfo").TryGetProperty("browserContextId", out var announced).Should().BeTrue(
            "Playwright drops a page target that names no browser context, and a default-context page has one too");

        announced.GetString().Should().NotBeNullOrEmpty();

        // Target.getTargetInfo with no parameters at all: the target is the session's own, which is the only
        // thing a client sending an empty object can mean.
        var implied = await session.ResultAsync("Target.getTargetInfo", "{}", attachment);
        implied.GetProperty("targetInfo").GetProperty("targetId").GetString().Should().Be(targetId);

        // And with the identifier, which is the spelling the other three clients use.
        var named = await session.ResultAsync("Target.getTargetInfo", $$"""{"targetId":"{{targetId}}"}""");
        named.GetProperty("targetInfo").GetProperty("targetId").GetString().Should().Be(targetId);
        named.GetProperty("targetInfo").GetProperty("browserContextId").GetString().Should().Be(
            implied.GetProperty("targetInfo").GetProperty("browserContextId").GetString(),
            "both spellings answer about one target");
    }

    /// <summary>The live identifiers a recorded parameter key is rewritten to.</summary>
    /// <param name="TargetId">The page target this replay created.</param>
    /// <param name="ContextId">The browser context it was created in.</param>
    /// <param name="SessionId">The attachment every page-level command rides.</param>
    /// <param name="Url">A URL on the loopback origin the page is allowed to reach.</param>
    /// <param name="ObjectId">A remote object handle the server minted.</param>
    /// <param name="NodeObjectId">A handle for an element of the page.</param>
    /// <param name="NodeId">That element's node identifier.</param>
    private readonly record struct Identifiers(
        string TargetId,
        string ContextId,
        string SessionId,
        string Url,
        string ObjectId,
        string NodeObjectId,
        int NodeId);

    /// <summary>One recorded call: the method, the members it carried, and any values kept with them.</summary>
    private sealed record RecordedShape(string Method, IReadOnlyList<string> Keys, IReadOnlyDictionary<string, string> Values);

    /// <summary>
    /// The JSON one recorded call becomes: every key it carried, with a value of the right kind.
    /// </summary>
    private static string Shape(RecordedShape shape, in Identifiers ids)
    {
        var members = new List<string>(shape.Keys.Count);

        foreach (var key in shape.Keys)
        {
            // A value the recording kept is the truest one there is, and it is what carries a client's own
            // world name, its device metrics and its auto-attach flags.
            var value = shape.Values.TryGetValue(key, out var recorded)
                ? Recorded(shape.Method, key, recorded) ?? ValueFor(shape.Method, key, ids)
                : ValueFor(shape.Method, key, ids);

            members.Add(Quote(key) + ":" + value);
        }

        return "{" + string.Join(",", members) + "}";
    }

    /// <summary>
    /// The value one parameter takes: the live identifier where the key names one, and otherwise a value of
    /// the kind the <b>vendored protocol</b> declares for it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The type comes from <c>tools/devtools-protocol/</c> rather than from a table written here, which is
    /// what keeps this a shape replay rather than a second guess at the protocol: a bump that changes a
    /// member from a string to an enumeration changes what this sends, in the same pull request, without
    /// anybody remembering to. A member the vendored files do not declare — a domain this server has never
    /// heard of — is a flag, which is what most of them are.
    /// </para>
    /// <para>
    /// The identifiers are the exception and have to be: a command addressed to a target, a frame, a node or
    /// a handle that does not exist would be refused for the wrong reason, and the two Playwright shapes this
    /// test exists for are both about identifiers.
    /// </para>
    /// </remarks>
    private static string ValueFor(string method, string key, in Identifiers ids) => (method, key) switch
    {
        // A pattern that matches nothing, deliberately: a real interception here would leave the page's next
        // request paused with nobody to release it.
        ("Fetch.enable", "patterns") => """[{"urlPattern":"http://handshake.invalid/*"}]""",
        (_, "requestId") => Quote("interception-job-0"),

        (_, "targetId") => Quote(ids.TargetId),
        (_, "browserContextId") => Quote(ids.ContextId),
        (_, "sessionId") => Quote(ids.SessionId),
        (_, "frameId") => Quote(ids.TargetId),
        (_, "objectId") => Quote(method.StartsWith("DOM.", StringComparison.Ordinal) ? ids.NodeObjectId : ids.ObjectId),
        (_, "nodeId") or (_, "backendNodeId") => ids.NodeId.ToString(CultureInfo.InvariantCulture),
        (_, "url") => Quote(ids.Url),
        (_, "expression") => Quote("document.title"),
        (_, "functionDeclaration") => Quote("function () { return this.answer; }"),
        (_, "selector") => Quote("p"),

        _ => Declared(method, key),
    };

    /// <summary>A value of the kind the vendored protocol declares for one command's parameter.</summary>
    private static string Declared(string method, string key)
        => ProtocolParameters.TryGetValue(method + "." + key, out var value) ? value : "true";

    /// <summary>
    /// The recorded value for one parameter, or <see langword="null"/> when the recording kept a <i>set</i>
    /// of them that this cannot take as it stands.
    /// </summary>
    /// <remarks>
    /// <b>A recording keeps every distinct value a client sent for a member.</b> One value is stored as
    /// itself; several become an array — <c>"returnByValue": [false, true]</c> is a client that sent both,
    /// not a client that sent a list. So a recorded value is used where its JSON kind is the one the protocol
    /// declares, and otherwise the first member of the set that is. An array-typed member seen twice becomes
    /// an array of arrays, which is the one case the kinds cannot tell apart on their own.
    /// </remarks>
    private static string? Recorded(string method, string key, string raw)
    {
        if (!ProtocolKinds.TryGetValue(method + "." + key, out var kind))
        {
            return raw;
        }

        using var document = JsonDocument.Parse(raw);
        var value = document.RootElement;

        var isSet = value.ValueKind == JsonValueKind.Array
            && (kind != "array" || (value.GetArrayLength() > 0 && value[0].ValueKind == JsonValueKind.Array));

        if (!isSet)
        {
            return Matches(value, kind) ? raw : null;
        }

        foreach (var member in value.EnumerateArray())
        {
            if (Matches(member, kind))
            {
                return member.GetRawText();
            }
        }

        return null;
    }

    /// <summary>Whether one JSON value is of the kind the protocol declares.</summary>
    private static bool Matches(JsonElement value, string kind) => kind switch
    {
        "array" => value.ValueKind == JsonValueKind.Array,
        "object" => value.ValueKind == JsonValueKind.Object,
        "boolean" => value.ValueKind is JsonValueKind.True or JsonValueKind.False,
        "integer" or "number" => value.ValueKind == JsonValueKind.Number,

        // Everything else the protocol declares is a string: a plain one, an alias, or an enumeration.
        _ => value.ValueKind == JsonValueKind.String,
    };

    /// <summary>
    /// Every command parameter the vendored protocol declares, as a JSON value of its declared type.
    /// </summary>
    /// <remarks>
    /// A <c>$ref</c> is resolved, and an object is built out of its own <i>required</i> members — which is
    /// what a client sends and what a data transfer object with required members needs: an empty object for
    /// <c>Emulation.setDeviceMetricsOverride</c>'s <c>screenOrientation</c> would be refused here for the
    /// same reason Chrome refuses it, and that refusal would say nothing about this server. An enumeration
    /// answers its first member, because a fixed set is the only string those commands accept.
    /// </remarks>
    /// <summary>The same parameters, as the JSON kind each takes rather than as a value.</summary>
    /// <remarks>
    /// Declared <i>before</i> the values it is filled alongside, because a static initializer runs in
    /// source order and the one below writes into this.
    /// </remarks>
    private static Dictionary<string, string> ProtocolKinds { get; } = new(StringComparer.Ordinal);

    private static Dictionary<string, string> ProtocolParameters { get; } = ReadProtocolParameters();

    private static Dictionary<string, string> ReadProtocolParameters()
    {
        var declared = new Dictionary<string, string>(StringComparer.Ordinal);
        var types = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        var documents = new List<JsonDocument>();

        foreach (var name in new[] { "browser_protocol.json", "js_protocol.json", "jint_protocol.json" })
        {
            var path = Path.Combine(RepositoryPaths.Root, "tools", "devtools-protocol", name);
            File.Exists(path).Should().BeTrue("the protocol is vendored at {0}", path);
            documents.Add(JsonDocument.Parse(File.ReadAllBytes(path)));
        }

        // The type declarations first, because a parameter's $ref may name one in another domain.
        foreach (var domain in documents.SelectMany(Domains))
        {
            var domainName = domain.GetProperty("domain").GetString()!;

            if (!domain.TryGetProperty("types", out var domainTypes))
            {
                continue;
            }

            foreach (var type in domainTypes.EnumerateArray())
            {
                types[domainName + "." + type.GetProperty("id").GetString()] = type;
            }
        }

        foreach (var domain in documents.SelectMany(Domains))
        {
            var domainName = domain.GetProperty("domain").GetString()!;

            if (!domain.TryGetProperty("commands", out var commands))
            {
                continue;
            }

            foreach (var command in commands.EnumerateArray())
            {
                if (!command.TryGetProperty("parameters", out var parameters))
                {
                    continue;
                }

                var method = domainName + "." + command.GetProperty("name").GetString();

                foreach (var parameter in parameters.EnumerateArray())
                {
                    var name = method + "." + parameter.GetProperty("name").GetString();

                    declared[name] = ValueOf(parameter, types, domainName, depth: 0);
                    ProtocolKinds[name] = KindOf(parameter, types, domainName, depth: 0);
                }
            }
        }

        foreach (var document in documents)
        {
            document.Dispose();
        }

        return declared;
    }

    private static IEnumerable<JsonElement> Domains(JsonDocument document)
        => document.RootElement.GetProperty("domains").EnumerateArray();

    /// <summary>The JSON kind one declaration takes, resolving a <c>$ref</c> the way <see cref="ValueOf"/> does.</summary>
    private static string KindOf(JsonElement declaration, Dictionary<string, JsonElement> types, string domain, int depth)
    {
        if (depth > 4)
        {
            return "object";
        }

        if (declaration.TryGetProperty("type", out var type))
        {
            return type.GetString()!;
        }

        if (!declaration.TryGetProperty("$ref", out var reference))
        {
            return "object";
        }

        var name = reference.GetString()!;
        var qualified = name.Contains('.', StringComparison.Ordinal) ? name : domain + "." + name;

        return types.TryGetValue(qualified, out var resolved)
            ? KindOf(resolved, types, qualified[..qualified.IndexOf('.', StringComparison.Ordinal)], depth + 1)
            : "object";
    }

    /// <summary>The JSON one declaration takes: its own type, its first enumeration member, or its <c>$ref</c>.</summary>
    private static string ValueOf(JsonElement declaration, Dictionary<string, JsonElement> types, string domain, int depth)
    {
        if (depth > 4)
        {
            return "{}";
        }

        if (declaration.TryGetProperty("enum", out var members) && members.GetArrayLength() > 0)
        {
            return JsonSerializer.Serialize(members[0].GetString());
        }

        if (declaration.TryGetProperty("$ref", out var reference))
        {
            var name = reference.GetString()!;
            var qualified = name.Contains('.', StringComparison.Ordinal) ? name : domain + "." + name;

            return types.TryGetValue(qualified, out var resolved)
                ? ValueOf(resolved, types, qualified[..qualified.IndexOf('.', StringComparison.Ordinal)], depth + 1)
                : "{}";
        }

        return declaration.TryGetProperty("type", out var type) ? type.GetString() switch
        {
            "string" => JsonSerializer.Serialize("replay"),
            "integer" or "number" => "1",
            "boolean" => "true",
            "array" => "[]",
            "object" => Object(declaration, types, domain, depth),
            _ => "true",
        } : "{}";
    }

    /// <summary>An object built from the members its declaration does not call optional.</summary>
    private static string Object(JsonElement declaration, Dictionary<string, JsonElement> types, string domain, int depth)
    {
        if (!declaration.TryGetProperty("properties", out var properties))
        {
            return "{}";
        }

        var members = new List<string>();

        foreach (var property in properties.EnumerateArray())
        {
            if (property.TryGetProperty("optional", out var optional) && optional.GetBoolean())
            {
                continue;
            }

            members.Add(
                JsonSerializer.Serialize(property.GetProperty("name").GetString())
                + ":"
                + ValueOf(property, types, domain, depth + 1));
        }

        return "{" + string.Join(",", members) + "}";
    }

    /// <summary>One recorded value as a JSON string.</summary>
    private static string Quote(string value) => JsonSerializer.Serialize(value);

    /// <summary>Sends one call where it belongs, keeping whichever session answered better.</summary>
    private static async Task<JsonElement> BestAnswerAsync(PageSession session, string method, string attachment, string parameters)
    {
        var onAttachment = await session.SendAsync(method, parameters, attachment).ConfigureAwait(false);
        if (!onAttachment.TryGetProperty("error", out _))
        {
            return onAttachment;
        }

        var onBrowser = await session.SendAsync(method, parameters).ConfigureAwait(false);
        return onBrowser.TryGetProperty("error", out _) ? onAttachment : onBrowser;
    }

    /// <summary>Every distinct call shape one client was recorded making, across every step.</summary>
    /// <remarks>
    /// Every step rather than the fourteen the name replay covers: a shape is answered or refused whatever
    /// the command does afterwards, and the two steps that replay leaves out — the screenshot and the PDF —
    /// are refused with <c>-32000</c>, which this does not count.
    /// </remarks>
    private static IReadOnlyList<RecordedShape> RecordedShapes(string client)
    {
        var path = Path.Combine(RepositoryPaths.Root, "tools", "devtools-protocol", "handshakes", client + ".json");
        File.Exists(path).Should().BeTrue("the recorded handshakes are checked in at {0}", path);

        using var document = JsonDocument.Parse(File.ReadAllBytes(path));
        var shapes = new List<RecordedShape>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var step in document.RootElement.GetProperty("scenarioSteps").EnumerateArray())
        {
            if (!step.TryGetProperty("methods", out var recorded))
            {
                continue;
            }

            foreach (var entry in recorded.EnumerateArray())
            {
                var method = entry.GetProperty("method").GetString()!;

                // The four the replay itself already sent, and which would take the ground out from under it.
                if (method is "Target.createTarget" or "Target.createBrowserContext" or "Target.closeTarget" or "Target.detachFromTarget")
                {
                    continue;
                }

                var keys = entry.TryGetProperty("paramsKeys", out var recordedKeys)
                    ? recordedKeys.EnumerateArray().Select(key => key.GetString()!).ToArray()
                    : [];

                if (!seen.Add(method + "(" + string.Join(",", keys) + ")"))
                {
                    continue;
                }

                var values = new Dictionary<string, string>(StringComparer.Ordinal);

                if (entry.TryGetProperty("paramsValues", out var recordedValues))
                {
                    foreach (var value in recordedValues.EnumerateObject())
                    {
                        // The raw text rather than the element: the document is disposed with this method,
                        // and a JsonElement does not outlive the one it was read from.
                        values[value.Name] = value.Value.GetRawText();
                    }
                }

                shapes.Add(new RecordedShape(method, keys, values));
            }
        }

        return shapes;
    }

    /// <summary>
    /// Sends one method where it belongs: the browser conversation first, and the attachment when the
    /// conversation does not carry that domain.
    /// </summary>
    /// <remarks>
    /// A real client knows which session each command belongs on because the protocol tells it; a replay does
    /// not, so it tries both and keeps the better answer — which is exactly the question being asked: is this
    /// method reachable at all?
    /// </remarks>
    private static async Task<JsonElement> BestAnswerAsync(
        PageSession session,
        string method,
        string attachment,
        string targetId,
        string contextId,
        string url,
        string? handle,
        string? node,
        int nodeId)
    {
        var parameters = Parameters(method, targetId, contextId, url, handle, node, nodeId);

        var onAttachment = await session.SendAsync(method, parameters, attachment).ConfigureAwait(false);
        if (!onAttachment.TryGetProperty("error", out _))
        {
            return onAttachment;
        }

        var onBrowser = await session.SendAsync(method, parameters).ConfigureAwait(false);
        return onBrowser.TryGetProperty("error", out _) ? onAttachment : onBrowser;
    }

    /// <summary>Evaluates one object on the attachment and hands back the handle the server minted for it.</summary>
    private static async Task<string> HandleAsync(PageSession session, string attachment)
    {
        var result = await session.ResultAsync(
            "Runtime.evaluate",
            """{"expression":"({ answer: 42 })"}""",
            attachment).ConfigureAwait(false);

        return result.GetProperty("result").GetProperty("objectId").GetString()!;
    }

    /// <summary>The handle for an element of the page, which is what every DOM command here addresses.</summary>
    private static async Task<string> NodeHandleAsync(PageSession session, string attachment)
    {
        var result = await session.ResultAsync(
            "Runtime.evaluate",
            """{"expression":"document.querySelector('p')"}""",
            attachment).ConfigureAwait(false);

        var value = result.GetProperty("result");
        value.GetProperty("subtype").GetString().Should().Be("node", "a client builds an element handle out of the subtype");

        return value.GetProperty("objectId").GetString()!;
    }

    /// <summary>The identifier that handle is addressed by, which is what resolveNode takes.</summary>
    private static async Task<int> NodeIdAsync(PageSession session, string attachment, string node)
    {
        var result = await session.ResultAsync(
            "DOM.requestNode",
            $$"""{"objectId":"{{node}}"}""",
            attachment).ConfigureAwait(false);

        return result.GetProperty("nodeId").GetInt32();
    }

    /// <summary>The parameters a command cannot be answered without.</summary>
    private static string? Parameters(
        string method,
        string targetId,
        string contextId,
        string url,
        string? handle,
        string? node,
        int nodeId) => method switch
    {
        "DOM.describeNode" => $$"""{"objectId":"{{node}}"}""",
        "DOM.resolveNode" => $$"""{"nodeId":{{nodeId}}}""",
        "DOM.getContentQuads" => $$"""{"objectId":"{{node}}"}""",
        "DOM.scrollIntoViewIfNeeded" => $$"""{"objectId":"{{node}}"}""",
        "Input.dispatchMouseEvent" => """{"type":"mouseMoved","x":4,"y":4,"button":"none"}""",

        // Every member of it is one a real client sends: `paramsKeys` in the recording's `type` step names
        // `type`, `modifiers`, `windowsVirtualKeyCode`, `code`, `commands`, `key`, `text`, `unmodifiedText`,
        // `autoRepeat`, `location` and `isKeypad`, and Playwright is the one that adds `commands`.
        "Input.dispatchKeyEvent" => """
            {"type":"keyDown","modifiers":0,"windowsVirtualKeyCode":72,"code":"KeyH","commands":[],
             "key":"h","text":"h","unmodifiedText":"h","autoRepeat":false,"location":0,"isKeypad":false}
            """,
        "Runtime.callFunctionOn" => $$"""{"functionDeclaration":"function () { return this.answer; }","objectId":"{{handle}}","returnByValue":true}""",
        "Runtime.getProperties" => $$"""{"objectId":"{{handle}}"}""",
        "Runtime.releaseObject" => $$"""{"objectId":"{{handle}}"}""",
        "Page.navigate" => $$"""{"url":"{{url}}"}""",
        "Page.setLifecycleEventsEnabled" => """{"enabled":true}""",
        "Page.addScriptToEvaluateOnNewDocument" => """{"source":"void 0"}""",
        "Page.createIsolatedWorld" => $$"""{"frameId":"{{targetId}}","worldName":"utility"}""",
        "Page.setFontFamilies" => """{"fontFamilies":{}}""",
        "Page.setBypassCSP" => """{"enabled":true}""",
        "Page.setDocumentContent" => $$"""{"frameId":"{{targetId}}","html":"<html></html>"}""",
        "Page.handleJavaScriptDialog" => """{"accept":true}""",
        "Page.navigateToHistoryEntry" => """{"entryId":0}""",
        "Emulation.setDeviceMetricsOverride" => """{"width":800,"height":600,"deviceScaleFactor":1,"mobile":false}""",
        "Emulation.setTouchEmulationEnabled" => """{"enabled":true}""",
        "Emulation.setFocusEmulationEnabled" => """{"enabled":true}""",
        "Emulation.setUserAgentOverride" => """{"userAgent":"replay"}""",
        "Emulation.setEmulatedMedia" => """{"media":"screen"}""",
        "Network.setCacheDisabled" => """{"cacheDisabled":true}""",
        "Network.setExtraHTTPHeaders" => """{"headers":{}}""",
        "Browser.setDownloadBehavior" => """{"behavior":"deny"}""",
        "Browser.setWindowBounds" => """{"windowId":1,"bounds":{"width":800,"height":600}}""",
        "Browser.getWindowForTarget" => $$"""{"targetId":"{{targetId}}"}""",
        "Target.setAutoAttach" => """{"autoAttach":false,"waitForDebuggerOnStart":false,"flatten":true}""",
        "Target.setDiscoverTargets" => """{"discover":true}""",
        "Target.getTargetInfo" => $$"""{"targetId":"{{targetId}}"}""",
        "Target.attachToTarget" => $$"""{"targetId":"{{targetId}}","flatten":true}""",
        "Runtime.evaluate" => """{"expression":"document.title","returnByValue":true}""",
        "Runtime.addBinding" => """{"name":"__jintHandshakeBinding"}""",
        "Storage.setCookies" => $$"""{"cookies":[{"name":"replay","value":"1","url":"{{url}}"}]}""",

        // A pattern that matches nothing, deliberately: the question this replay asks is whether the method
        // is answered, and a client that really enabled interception here would leave the page's next request
        // paused with nobody to release it.
        "Fetch.enable" => """{"patterns":[{"urlPattern":"http://handshake.invalid/*"}]}""",
        "Fetch.continueRequest" => """{"requestId":"interception-job-0"}""",
        _ => null,
    };

    /// <summary>
    /// The methods one client sent in the fourteen steps this replay covers.
    /// </summary>
    /// <remarks>
    /// Taken from the recording's own per-step breakdown rather than from its whole method list, because the
    /// two steps after these — the screenshot and the PDF — are refused by design, and a replay that included
    /// them would be asserting that a refusal is an answer.
    /// </remarks>
    private static IReadOnlyList<string> RecordedMethods(string client)
    {
        var path = Path.Combine(RepositoryPaths.Root, "tools", "devtools-protocol", "handshakes", client + ".json");
        File.Exists(path).Should().BeTrue("the recorded handshakes are checked in at {0}", path);

        using var document = JsonDocument.Parse(File.ReadAllBytes(path));
        var methods = new List<string>();

        foreach (var step in document.RootElement.GetProperty("scenarioSteps").EnumerateArray())
        {
            if (!Array.Exists(ReplayedSteps, name => name == step.GetProperty("step").GetString()))
            {
                continue;
            }

            if (!step.TryGetProperty("methods", out var recorded))
            {
                continue;
            }

            foreach (var entry in recorded.EnumerateArray())
            {
                var method = entry.GetProperty("method").GetString()!;

                // The two the replay itself already sent, and which would take the ground out from under it:
                // a second createTarget is a second page, and a second createBrowserContext a second context.
                if (method is "Target.createTarget" or "Target.createBrowserContext" or "Target.closeTarget" or "Target.detachFromTarget")
                {
                    continue;
                }

                if (!methods.Contains(method, StringComparer.Ordinal))
                {
                    methods.Add(method);
                }
            }
        }

        return methods;
    }
}
