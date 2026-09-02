using System.Text.Json;
using Jint.DevTools;

namespace Jint.Tests.DevTools.Session;

/// <summary>
/// One conversation, one engine, and the identifier that addresses the second through the first.
/// </summary>
/// <remarks>
/// Every target here is <see cref="ThreadMode.LibraryOwned"/>, because that is what makes a test a test: the
/// command is answered on a thread that is not this one, which is the arrangement every real client is in
/// and the one a mistake in the mailbox shows up in.
/// </remarks>
internal sealed class AttachedSession : IAsyncDisposable
{
    private readonly ProtocolSession _session;

    private AttachedSession(ProtocolSession session, EngineTarget target, string sessionId)
    {
        _session = session;
        Target = target;
        SessionId = sessionId;
    }

    /// <summary>Gets the engine this attachment speaks to.</summary>
    internal EngineTarget Target { get; }

    /// <summary>Gets the identifier every message of this attachment carries.</summary>
    internal string SessionId { get; }

    /// <summary>Gets the conversation the attachment was minted on.</summary>
    internal ProtocolSession Protocol => _session;

    /// <summary>Attaches to a target over one engine, built the way a host that means to attach builds one.</summary>
    /// <param name="configure">What to do to the engine once it is built.</param>
    /// <param name="options">How the target presents itself, or the defaults.</param>
    /// <param name="configureOptions">
    /// What the host asked for before <c>UseDevTools</c> ran, which is the order a host writes: its own
    /// console sink, its own web-API features, and then the call that makes the engine attachable.
    /// </param>
    /// <param name="serverOptions">
    /// What the server is configured with, or the defaults. The bounds a target reads — the command timeout
    /// and the pause timeout — are the server's, so a test about either sets them here.
    /// </param>
    internal static async Task<AttachedSession> CreateAsync(
        Action<Engine>? configure = null,
        EngineTargetOptions? options = null,
        Action<Options>? configureOptions = null,
        DevToolsServerOptions? serverOptions = null)
    {
        var session = ProtocolSession.Create(options: serverOptions);
        var engine = new Engine(o =>
        {
            configureOptions?.Invoke(o);
            o.UseDevTools();
        });

        configure?.Invoke(engine);

        options ??= new EngineTargetOptions();
        options.ThreadMode = ThreadMode.LibraryOwned;

        var target = session.AddTarget(options, engine);
        var sessionId = await session.AttachAsync(target).ConfigureAwait(false);

        return new AttachedSession(session, target, sessionId);
    }

    /// <summary>Sends one command on this attachment.</summary>
    internal Task<JsonElement> SendAsync(string method, string? parameters = null)
        => _session.SendAsync(method, parameters, SessionId);

    /// <summary>Sends one command and hands back its <c>result</c>, asserting that it succeeded.</summary>
    internal async Task<JsonElement> ResultAsync(string method, string? parameters = null)
    {
        var reply = await SendAsync(method, parameters).ConfigureAwait(false);
        reply.TryGetProperty("error", out var error).Should().BeFalse("'{0}' was expected to succeed, and it answered {1}", method, error);
        return reply.GetProperty("result");
    }

    /// <summary>Sends one command and hands back its <c>error</c>, asserting that it failed.</summary>
    internal async Task<JsonElement> ErrorAsync(string method, string? parameters = null)
    {
        var reply = await SendAsync(method, parameters).ConfigureAwait(false);
        reply.TryGetProperty("error", out var error).Should().BeTrue("'{0}' was expected to fail, and it answered {1}", method, reply);
        return error;
    }

    /// <summary>Evaluates <paramref name="expression"/> and hands back the remote object it answered with.</summary>
    internal async Task<JsonElement> EvaluateAsync(
        string expression,
        bool returnByValue = false,
        bool awaitPromise = false,
        bool generatePreview = false,
        string? objectGroup = null)
    {
        var parameters = JsonSerializer.Serialize(new Dictionary<string, object>
        {
            ["expression"] = expression,
            ["returnByValue"] = returnByValue,
            ["awaitPromise"] = awaitPromise,
            ["generatePreview"] = generatePreview,
            ["objectGroup"] = objectGroup ?? "",
        });

        var result = await ResultAsync("Runtime.evaluate", parameters).ConfigureAwait(false);
        result.TryGetProperty("exceptionDetails", out var details).Should().BeFalse("the expression was expected to succeed, and it threw {0}", details);
        return result.GetProperty("result");
    }

    /// <summary>Evaluates <paramref name="expression"/> and hands back the handle the server minted for it.</summary>
    internal async Task<string> HandleAsync(string expression, string? objectGroup = null)
    {
        var result = await EvaluateAsync(expression, objectGroup: objectGroup).ConfigureAwait(false);
        var objectId = result.TryGetProperty("objectId", out var value) ? value.GetString() : null;

        objectId.Should().NotBeNullOrEmpty("'{0}' was expected to answer a handle, and it answered {1}", expression, result);
        return objectId!;
    }

    /// <summary>Reads the properties of one handle.</summary>
    internal Task<JsonElement> PropertiesAsync(
        string objectId,
        bool? ownProperties = null,
        bool? accessorPropertiesOnly = null,
        bool? generatePreview = null,
        bool? nonIndexedPropertiesOnly = null)
    {
        var parameters = new Dictionary<string, object> { ["objectId"] = objectId };
        Add(parameters, "ownProperties", ownProperties);
        Add(parameters, "accessorPropertiesOnly", accessorPropertiesOnly);
        Add(parameters, "generatePreview", generatePreview);
        Add(parameters, "nonIndexedPropertiesOnly", nonIndexedPropertiesOnly);

        return ResultAsync("Runtime.getProperties", JsonSerializer.Serialize(parameters));

        static void Add(Dictionary<string, object> parameters, string name, bool? value)
        {
            if (value is { } flag)
            {
                parameters[name] = flag;
            }
        }
    }

    /// <summary>Every event of <paramref name="method"/> the conversation has sent, oldest first.</summary>
    internal IReadOnlyList<JsonElement> EventsOf(string method) => _session.EventsOf(method);

    /// <summary>
    /// Waits for the event at <paramref name="index"/> of <paramref name="method"/>, failing rather than
    /// hanging.
    /// </summary>
    /// <remarks>
    /// An event is not a reply, so nothing hands one back: it arrives on the connection whenever the engine
    /// has something to say, and a test that wants one waits. <b>Every wait here is bounded</b> — a protocol
    /// test that can hang is a continuous-integration leg that can hang, and a pause that never arrives is
    /// exactly the defect these tests are looking for.
    /// </remarks>
    internal async Task<JsonElement> EventAsync(string method, int index = 0, int timeoutSeconds = 120)
    {
        var deadline = Environment.TickCount64 + (timeoutSeconds * 1000L);

        while (Environment.TickCount64 < deadline)
        {
            var events = EventsOf(method);
            if (events.Count > index)
            {
                return events[index].GetProperty("params");
            }

            await Task.Delay(5).ConfigureAwait(false);
        }

        Assert.Fail($"'{method}' number {index} never arrived within {timeoutSeconds} seconds.");
        return default;
    }

    /// <summary>Enables the domains a debugging client enables, in the order a client sends them.</summary>
    internal async Task EnableDebuggerAsync()
    {
        await ResultAsync("Runtime.enable").ConfigureAwait(false);
        await ResultAsync("Debugger.enable").ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public ValueTask DisposeAsync() => _session.DisposeAsync();
}

/// <summary>Reading the one shape every one of these tests picks apart.</summary>
internal static class RemoteObjectAssertions
{
    /// <summary>Answers the named property of a JSON object, or <see langword="null"/> when it is absent.</summary>
    internal static JsonElement? Optional(this JsonElement element, string name)
        => element.TryGetProperty(name, out var value) ? value : null;

    /// <summary>Answers the one property descriptor of <paramref name="name"/>, asserting there is one.</summary>
    internal static JsonElement Property(this JsonElement getPropertiesResult, string name)
    {
        var match = getPropertiesResult.GetProperty("result").EnumerateArray()
            .Where(property => property.GetProperty("name").GetString() == name)
            .ToArray();

        match.Should().HaveCount(1, "'{0}' was expected exactly once in {1}", name, getPropertiesResult);
        return match[0];
    }

    /// <summary>Answers every property name a <c>getProperties</c> reply lists, in order.</summary>
    internal static string[] Names(this JsonElement getPropertiesResult)
        => [.. getPropertiesResult.GetProperty("result").EnumerateArray().Select(property => property.GetProperty("name").GetString()!)];

    /// <summary>Answers the named internal property, asserting there is one.</summary>
    internal static JsonElement Internal(this JsonElement getPropertiesResult, string name)
    {
        var match = getPropertiesResult.GetProperty("internalProperties").EnumerateArray()
            .Where(property => property.GetProperty("name").GetString() == name)
            .ToArray();

        match.Should().HaveCount(1, "'{0}' was expected exactly once in {1}", name, getPropertiesResult);
        return match[0];
    }
}
