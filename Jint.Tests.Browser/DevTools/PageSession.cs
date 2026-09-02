using System.Text.Json;
using Jint.Browser;
using Jint.Browser.DevTools;
using Jint.DevTools;
using Jint.DevTools.Session;
using Jint.DevTools.Transport;

namespace Jint.Tests.Browser.DevTools;

/// <summary>
/// A browser published on a server, a client attached to one of its pages, and the two operations every test
/// here performs: send a command, wait for an event.
/// </summary>
/// <remarks>
/// <para>
/// The server is real and simply never started, which is what makes these tests worth writing: they drive
/// exactly the code a socket drives, minus the socket. The page is real too — its own thread, its own engine,
/// its own document — so a command that touches the DOM really does cross to the page loop.
/// </para>
/// <para>
/// <b>Every wait is bounded.</b> A protocol test that can hang is a continuous-integration leg that can hang,
/// and an event that never arrives is exactly the defect these tests look for.
/// </para>
/// </remarks>
internal sealed class PageSession : IAsyncDisposable
{
    private readonly InProcessConnection _connection = new();
    private readonly DevToolsServer _server;
    private readonly global::Jint.Browser.Browser _browser;
    private readonly BrowserContext _context;

    private int _nextId = 1000;

    private PageSession(global::Jint.Browser.Browser browser, BrowserContext context, DevToolsServer server)
    {
        _browser = browser;
        _context = context;
        _server = server;
        Browser = server.OpenBrowserSession(_connection);
    }

    /// <summary>The conversation under test.</summary>
    internal BrowserSession Browser { get; }

    /// <summary>The browser whose pages are published.</summary>
    internal global::Jint.Browser.Browser Pages => _browser;

    /// <summary>The server the browser is published on.</summary>
    internal DevToolsServer Server => _server;

    /// <summary>Every message the session has sent, oldest first.</summary>
    internal IReadOnlyList<string> Sent => _connection.Sent;

    /// <summary>Opens a browser, publishes it, and answers a session over one context of it.</summary>
    /// <param name="contextOptions">
    /// What the pages' context keeps to itself, or <see langword="null"/> for the browser's default context.
    /// A test that navigates anywhere real passes a <c>UrlFilter</c> here, for the reason
    /// <c>LoopbackPage</c> gives: a test that could reach anything else is a test that could hang on
    /// somebody's name server.
    /// </param>
    /// <param name="options">What every page is built from, or the defaults.</param>
    internal static async Task<PageSession> CreateAsync(BrowserContextOptions? contextOptions = null, BrowserOptions? options = null)
    {
        var browser = new global::Jint.Browser.Browser(options);
        var context = contextOptions is null
            ? browser.DefaultContext
            : await browser.NewContextAsync(contextOptions).ConfigureAwait(false);

        var server = new DevToolsServer();
        await server.AddBrowser(browser).ConfigureAwait(false);

        return new PageSession(browser, context, server);
    }

    /// <summary>The context this session's pages are opened in.</summary>
    internal BrowserContext Context => _context;

    /// <summary>Opens a page and answers the attachment addressing its target.</summary>
    internal async Task<string> OpenPageAsync()
    {
        var page = await NewPageAsync().ConfigureAwait(false);
        var target = await TargetForAsync(page).ConfigureAwait(false);
        return await AttachAsync(target).ConfigureAwait(false);
    }

    /// <summary>Opens a page in this session's context.</summary>
    internal Task<Page> NewPageAsync() => _context.NewPageAsync();

    /// <summary>The target published for <paramref name="page"/>, waiting for it to be adopted.</summary>
    internal async Task<DevToolsTarget> TargetForAsync(Page page)
    {
        var deadline = Environment.TickCount64 + 30_000L;

        while (Environment.TickCount64 < deadline)
        {
            foreach (var target in _server.AllTargets)
            {
                if (string.Equals(target.Type, "page", StringComparison.Ordinal) && IsFor(target, page))
                {
                    return target;
                }
            }

            await Task.Delay(5).ConfigureAwait(false);
        }

        Assert.Fail("the page was never published as a target");
        return null!;
    }

    /// <summary>Attaches to <paramref name="target"/> the way a client does.</summary>
    internal async Task<string> AttachAsync(DevToolsTarget target)
    {
        var reply = await SendAsync(
            "Target.attachToTarget",
            $$"""{"targetId":"{{target.TargetId}}","flatten":true}""").ConfigureAwait(false);

        reply.TryGetProperty("error", out var error).Should().BeFalse("attaching was expected to succeed, and it answered {0}", error);
        return reply.GetProperty("result").GetProperty("sessionId").GetString()!;
    }

    /// <summary>Enables what a client enables before it drives a page, in the order a client sends them.</summary>
    internal async Task EnablePageAsync(string sessionId)
    {
        await ResultAsync("Page.enable", null, sessionId).ConfigureAwait(false);
        await ResultAsync("Page.setLifecycleEventsEnabled", """{"enabled":true}""", sessionId).ConfigureAwait(false);
        await ResultAsync("Runtime.enable", null, sessionId).ConfigureAwait(false);
    }

    /// <summary>Sends one command, addressed to an attachment or to the conversation itself.</summary>
    internal Task<JsonElement> SendAsync(string method, string? parameters = null, string? sessionId = null)
    {
        var identifier = Interlocked.Increment(ref _nextId);
        var payload = parameters is null ? "" : ",\"params\":" + parameters;
        var session = sessionId is null ? "" : ",\"sessionId\":\"" + sessionId + "\"";

        return RoundTripAsync($$"""{"id":{{identifier}},"method":"{{method}}"{{payload}}{{session}}}""");
    }

    /// <summary>Sends one command and hands back its <c>result</c>, asserting that it succeeded.</summary>
    internal async Task<JsonElement> ResultAsync(string method, string? parameters = null, string? sessionId = null)
    {
        var reply = await SendAsync(method, parameters, sessionId).ConfigureAwait(false);
        reply.TryGetProperty("error", out var error).Should().BeFalse("'{0}' was expected to succeed, and it answered {1}", method, error);
        return reply.GetProperty("result");
    }

    /// <summary>Sends one command and hands back its <c>error</c>, asserting that it failed.</summary>
    internal async Task<JsonElement> ErrorAsync(string method, string? parameters = null, string? sessionId = null)
    {
        var reply = await SendAsync(method, parameters, sessionId).ConfigureAwait(false);
        reply.TryGetProperty("error", out var error).Should().BeTrue("'{0}' was expected to fail, and it answered {1}", method, reply);
        return error;
    }

    /// <summary>Evaluates <paramref name="expression"/> on an attachment and answers its remote object.</summary>
    internal async Task<JsonElement> EvaluateAsync(string expression, string sessionId, bool returnByValue = true)
    {
        var parameters = JsonSerializer.Serialize(new Dictionary<string, object>
        {
            ["expression"] = expression,
            ["returnByValue"] = returnByValue,
        });

        var result = await ResultAsync("Runtime.evaluate", parameters, sessionId).ConfigureAwait(false);
        result.TryGetProperty("exceptionDetails", out var details).Should().BeFalse("the expression was expected to succeed, and it threw {0}", details);
        return result.GetProperty("result");
    }

    /// <summary>Sends one message and hands back the reply, parsed.</summary>
    internal async Task<JsonElement> RoundTripAsync(string message)
    {
        var before = _connection.Sent.Count;
        await _connection.PostAsync(message).ConfigureAwait(false);

        var sent = _connection.Sent;
        sent.Count.Should().BeGreaterThan(before, "a session answers every message with exactly one reply, or the client hangs");

        var identifier = Identifier(message);
        for (var i = sent.Count - 1; i >= before; i--)
        {
            using var candidate = JsonDocument.Parse(sent[i]);
            if (candidate.RootElement.TryGetProperty("id", out var id) && id.GetInt64() == identifier)
            {
                return candidate.RootElement.Clone();
            }
        }

        using var document = JsonDocument.Parse(sent[^1]);
        return document.RootElement.Clone();
    }

    /// <summary>Every event of <paramref name="method"/> the conversation has sent, oldest first.</summary>
    internal IReadOnlyList<JsonElement> EventsOf(string method, string? sessionId = null)
    {
        var events = new List<JsonElement>();

        foreach (var message in _connection.Sent)
        {
            using var document = JsonDocument.Parse(message);
            if (!document.RootElement.TryGetProperty("method", out var name) || name.GetString() != method)
            {
                continue;
            }

            if (sessionId is not null &&
                (!document.RootElement.TryGetProperty("sessionId", out var session) || session.GetString() != sessionId))
            {
                continue;
            }

            events.Add(document.RootElement.Clone());
        }

        return events;
    }

    /// <summary>Waits for the event at <paramref name="index"/>, failing rather than hanging.</summary>
    internal async Task<JsonElement> EventAsync(string method, int index = 0, string? sessionId = null, int timeoutSeconds = 60)
    {
        var deadline = Environment.TickCount64 + (timeoutSeconds * 1000L);

        while (Environment.TickCount64 < deadline)
        {
            var events = EventsOf(method, sessionId);
            if (events.Count > index)
            {
                return events[index].GetProperty("params");
            }

            await Task.Delay(5).ConfigureAwait(false);
        }

        Assert.Fail($"'{method}' number {index} never arrived within {timeoutSeconds} seconds.");
        return default;
    }

    /// <summary>Where one event sits among everything the connection has sent, for an ordering assertion.</summary>
    internal int Ordinal(string method, int index = 0)
    {
        var seen = 0;
        var sent = _connection.Sent;

        for (var i = 0; i < sent.Count; i++)
        {
            using var document = JsonDocument.Parse(sent[i]);
            if (document.RootElement.TryGetProperty("method", out var name) && name.GetString() == method && seen++ == index)
            {
                return i;
            }
        }

        return int.MaxValue;
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        await _browser.CloseAsync().ConfigureAwait(false);
        await _server.DisposeAsync().ConfigureAwait(false);
    }

    /// <summary>Whether <paramref name="target"/> is the target published for <paramref name="page"/>.</summary>
    /// <remarks>
    /// By the page's own identity rather than by its URL: two pages of one context both open on
    /// <c>about:blank</c>, and a test that matched on that would attach to whichever was published first.
    /// </remarks>
    private static bool IsFor(DevToolsTarget target, Page page)
        => target is PageTarget candidate && ReferenceEquals(candidate.Page, page);

    private static long Identifier(string message)
    {
        try
        {
            using var document = JsonDocument.Parse(message);
            return document.RootElement.ValueKind == JsonValueKind.Object &&
                   document.RootElement.TryGetProperty("id", out var id) &&
                   id.ValueKind == JsonValueKind.Number &&
                   id.TryGetInt64(out var value)
                ? value
                : long.MinValue;
        }
        catch (JsonException)
        {
            return long.MinValue;
        }
    }
}
