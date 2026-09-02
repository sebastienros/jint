using System.Text.Json;
using Jint.DevTools;

namespace Jint.Tests.DevTools.Session;

/// <summary>
/// What one attachment can do with an engine: hear about its execution context, and evaluate in it.
/// </summary>
/// <remarks>
/// Every target here is <see cref="ThreadMode.LibraryOwned"/>, because that is what makes the test a test:
/// the command is answered on a thread that is not this one, which is the arrangement every real client is in
/// and the one a mistake in the mailbox shows up in.
/// </remarks>
public class RuntimeSessionTests
{
    [Test]
    public async Task EnableReplaysTheOneExecutionContext()
    {
        await using var session = await AttachedAsync();

        var reply = await session.SendAsync("Runtime.enable", sessionId: session.SessionId);
        reply.TryGetProperty("error", out _).Should().BeFalse();

        var created = session.EventsOf("Runtime.executionContextCreated");
        created.Should().HaveCount(1);
        created[0].GetProperty("sessionId").GetString().Should().Be(session.SessionId);

        var context = created[0].GetProperty("params").GetProperty("context");
        context.GetProperty("id").GetInt32().Should().Be(1);
        context.GetProperty("origin").GetString().Should().BeEmpty();
        context.GetProperty("name").GetString().Should().BeEmpty();
        context.GetProperty("uniqueId").GetString().Should().Be(session.Target.TargetId + ".1");
        context.GetProperty("auxData").GetProperty("isDefault").GetBoolean().Should().BeTrue();
        context.GetProperty("auxData").GetProperty("type").GetString().Should().Be("default");
    }

    [Test]
    public async Task EnablingTwiceAnnouncesTheContextOnce()
    {
        await using var session = await AttachedAsync();

        await session.SendAsync("Runtime.enable", sessionId: session.SessionId);
        await session.SendAsync("Runtime.enable", sessionId: session.SessionId);

        session.EventsOf("Runtime.executionContextCreated").Should().HaveCount(1);
    }

    [Test]
    public async Task DisableSucceedsAndSaysNothing()
    {
        await using var session = await AttachedAsync();

        await session.SendAsync("Runtime.enable", sessionId: session.SessionId);
        var reply = await session.SendAsync("Runtime.disable", sessionId: session.SessionId);

        reply.GetProperty("result").GetRawText().Should().Be("{}");
    }

    [Test]
    public async Task EvaluateAnswersANumber()
    {
        await using var session = await AttachedAsync();

        var result = await session.EvaluateAsync("6 * 7");

        result.GetProperty("type").GetString().Should().Be("number");
        result.GetProperty("value").GetInt32().Should().Be(42);
        result.GetProperty("description").GetString().Should().Be("42");
    }

    [TestCase("undefined", "undefined", null, TestName = "undefined")]
    [TestCase("null", "object", "null", TestName = "null")]
    [TestCase("true", "boolean", null, TestName = "a boolean")]
    [TestCase("'text'", "string", null, TestName = "a string")]
    [TestCase("({ a: 1 })", "object", null, TestName = "a plain object")]
    [TestCase("[1, 2]", "object", "array", TestName = "an array")]
    [TestCase("(function f() {})", "function", null, TestName = "a function")]
    [TestCase("new Date(0)", "object", "date", TestName = "a date")]
    [TestCase("/x/g", "object", "regexp", TestName = "a regular expression")]
    [TestCase("new Map()", "object", "map", TestName = "a map")]
    [TestCase("new Error('boom')", "object", "error", TestName = "an error object")]
    [TestCase("Promise.resolve(1)", "object", "promise", TestName = "a promise")]
    public async Task EvaluateNamesTheTypeAndSubtypeTheProtocolUses(string expression, string type, string? subtype)
    {
        await using var session = await AttachedAsync();

        var result = await session.EvaluateAsync(expression);

        result.GetProperty("type").GetString().Should().Be(type);

        if (subtype is null)
        {
            result.TryGetProperty("subtype", out _).Should().BeFalse();
        }
        else
        {
            result.GetProperty("subtype").GetString().Should().Be(subtype);
        }
    }

    [TestCase("NaN", "NaN")]
    [TestCase("Infinity", "Infinity")]
    [TestCase("-Infinity", "-Infinity")]
    [TestCase("-0", "-0")]
    public async Task ANumberWithNoJsonFormIsUnserializable(string expression, string expected)
    {
        await using var session = await AttachedAsync();

        var result = await session.EvaluateAsync(expression);

        result.GetProperty("unserializableValue").GetString().Should().Be(expected);
        result.TryGetProperty("value", out _).Should().BeFalse("a client that read 0 back for -0 has been told something false");
    }

    [Test]
    public async Task EvaluateWithoutReturnByValueDescribesAnObjectAndHandsOutNoHandle()
    {
        await using var session = await AttachedAsync();

        var result = await session.EvaluateAsync("({ a: 1, b: [1, 2] })");

        result.GetProperty("type").GetString().Should().Be("object");
        result.GetProperty("className").GetString().Should().Be("Object");
        result.GetProperty("description").GetString().Should().Be("Object");
        result.TryGetProperty("objectId", out _).Should().BeFalse(
            "a handle is a promise to keep the value alive, and there is no table keeping it yet");
    }

    [Test]
    public async Task EvaluateWithReturnByValueSerializesTheValue()
    {
        await using var session = await AttachedAsync();

        var result = await session.EvaluateAsync("({ a: 1, b: [1, 2], c: 'x', d: null, e: undefined, f: function () {} })", returnByValue: true);

        result.GetProperty("type").GetString().Should().Be("object");

        var value = result.GetProperty("value");
        value.GetProperty("a").GetInt32().Should().Be(1);
        value.GetProperty("b").EnumerateArray().Select(item => item.GetInt32()).Should().Equal(1, 2);
        value.GetProperty("c").GetString().Should().Be("x");
        value.GetProperty("d").ValueKind.Should().Be(JsonValueKind.Null);
        value.TryGetProperty("e", out _).Should().BeFalse("JSON.stringify omits an undefined member and so does this");
        value.TryGetProperty("f", out _).Should().BeFalse("JSON.stringify omits a function member and so does this");
    }

    [Test]
    public async Task ByValueRefusesAValueThatRefersToItself()
    {
        await using var session = await AttachedAsync();

        var reply = await session.SendAsync(
            "Runtime.evaluate",
            """{"expression":"(function () { const a = {}; a.self = a; return a; })()","returnByValue":true}""",
            session.SessionId);

        var error = reply.GetProperty("error");
        error.GetProperty("code").GetInt32().Should().Be(-32000);
        error.GetProperty("message").GetString().Should().Be("Object couldn't be returned by value");
        error.GetProperty("data").GetString().Should().Be("the value refers to itself");
    }

    [Test]
    public async Task AThrownErrorBecomesExceptionDetailsAndNotAProtocolError()
    {
        await using var session = await AttachedAsync();

        var reply = await session.SendAsync(
            "Runtime.evaluate",
            """{"expression":"\n\nthrow new TypeError('boom')"}""",
            session.SessionId);

        reply.TryGetProperty("error", out _).Should().BeFalse("the command was answered; it is the expression that failed");

        var details = reply.GetProperty("result").GetProperty("exceptionDetails");
        details.GetProperty("text").GetString().Should().Be("Uncaught");
        details.GetProperty("lineNumber").GetInt32().Should().Be(2, "the protocol counts lines from zero and the parser counts them from one");
        details.GetProperty("executionContextId").GetInt32().Should().Be(1);
        details.GetProperty("exception").GetProperty("subtype").GetString().Should().Be("error");
        details.GetProperty("exception").GetProperty("description").GetString().Should().Contain("boom");

        reply.GetProperty("result").GetProperty("result").GetProperty("subtype").GetString().Should().Be("error");
    }

    /// <summary>
    /// A compile failure travels the same path as a throw, because the engine raises a <c>SyntaxError</c>
    /// for one with the location filled in. There is one path rather than two, and neither is a protocol
    /// error: the command was well formed and it is the client's expression that was not.
    /// </summary>
    [Test]
    public async Task ASyntaxErrorBecomesExceptionDetailsToo()
    {
        await using var session = await AttachedAsync();

        var reply = await session.SendAsync("Runtime.evaluate", """{"expression":"function ("}""", session.SessionId);

        reply.TryGetProperty("error", out _).Should().BeFalse();

        var details = reply.GetProperty("result").GetProperty("exceptionDetails");
        details.GetProperty("text").GetString().Should().Be("Uncaught");
        details.GetProperty("exception").GetProperty("className").GetString().Should().Be("SyntaxError");
        reply.GetProperty("result").GetProperty("result").GetProperty("subtype").GetString().Should().Be("error");
    }

    /// <summary>
    /// The promise is awaited by attaching reactions, never by draining: the command runs inside an
    /// event-loop job and a nested drain is exactly what the pump's re-entrancy guard refuses.
    /// </summary>
    [Test]
    public async Task AwaitPromiseAnswersWhatThePromiseSettledWith()
    {
        await using var session = await AttachedAsync();

        var result = await session.EvaluateAsync("Promise.resolve(41 + 1)", returnByValue: true, awaitPromise: true);

        result.GetProperty("type").GetString().Should().Be("number");
        result.GetProperty("value").GetInt32().Should().Be(42);
    }

    [Test]
    public async Task AwaitPromiseWaitsForOneThatSettlesOnALaterTurn()
    {
        await using var session = await AttachedAsync();

        var result = await session.EvaluateAsync(
            "new Promise(function (resolve) { Promise.resolve().then(function () { resolve('later'); }); })",
            returnByValue: true,
            awaitPromise: true);

        result.GetProperty("value").GetString().Should().Be("later");
    }

    [Test]
    public async Task AwaitPromiseReportsARejectionAsExceptionDetails()
    {
        await using var session = await AttachedAsync();

        var reply = await session.SendAsync(
            "Runtime.evaluate",
            """{"expression":"Promise.reject(new RangeError('nope'))","awaitPromise":true}""",
            session.SessionId);

        reply.TryGetProperty("error", out _).Should().BeFalse();

        var details = reply.GetProperty("result").GetProperty("exceptionDetails");
        details.GetProperty("text").GetString().Should().Be("Uncaught (in promise)");
        details.GetProperty("exception").GetProperty("subtype").GetString().Should().Be("error");
        details.GetProperty("exception").GetProperty("className").GetString().Should().Be("RangeError");
    }

    [Test]
    public async Task WithoutAwaitPromiseAPromiseIsDescribedRatherThanWaitedFor()
    {
        await using var session = await AttachedAsync();

        var result = await session.EvaluateAsync("Promise.resolve(1)");

        result.GetProperty("subtype").GetString().Should().Be("promise");
        result.TryGetProperty("value", out _).Should().BeFalse();
    }

    [Test]
    public async Task EvaluateInAContextThatIsNotThereIsRefusedInChromesWording()
    {
        await using var session = await AttachedAsync();

        var reply = await session.SendAsync("Runtime.evaluate", """{"expression":"1","contextId":7}""", session.SessionId);

        reply.GetProperty("error").GetProperty("code").GetInt32().Should().Be(-32000);
        reply.GetProperty("error").GetProperty("message").GetString().Should().Be("Cannot find context with specified id");
    }

    [Test]
    public async Task EvaluateSeesWhatTheHostPutOnTheEngine()
    {
        await using var session = await AttachedAsync(engine =>
        {
            engine.SetValue("answer", 42);
        });

        var result = await session.EvaluateAsync("answer + 1");
        result.GetProperty("value").GetInt32().Should().Be(43);
    }

    [Test]
    public async Task GetIsolateIdIsStableForOneEngine()
    {
        await using var session = await AttachedAsync();

        var first = (await session.SendAsync("Runtime.getIsolateId", sessionId: session.SessionId)).GetProperty("result").GetProperty("id").GetString();
        var second = (await session.SendAsync("Runtime.getIsolateId", sessionId: session.SessionId)).GetProperty("result").GetProperty("id").GetString();

        first.Should().NotBeNullOrEmpty();
        second.Should().Be(first);
    }

    [Test]
    public async Task DiscardConsoleEntriesSucceeds()
    {
        await using var session = await AttachedAsync();

        var reply = await session.SendAsync("Runtime.discardConsoleEntries", sessionId: session.SessionId);
        reply.GetProperty("result").GetRawText().Should().Be("{}");
    }

    /// <summary>
    /// The <c>Runtime</c> commands that need the object table are not answered yet, and say so with the one
    /// error a client feature-detects on rather than with a made-up success.
    /// </summary>
    [TestCase("Runtime.getProperties")]
    [TestCase("Runtime.callFunctionOn")]
    [TestCase("Runtime.awaitPromise")]
    [TestCase("Runtime.releaseObject")]
    public async Task TheCommandsThatNeedAnObjectTableAreMethodNotFound(string method)
    {
        await using var session = await AttachedAsync();

        var error = (await session.SendAsync(method, "{}", session.SessionId)).GetProperty("error");
        error.GetProperty("code").GetInt32().Should().Be(-32601);
        error.GetProperty("message").GetString().Should().Be($"'{method}' wasn't found");
    }

    private static async Task<AttachedSession> AttachedAsync(Action<Engine>? configure = null)
    {
        var session = ProtocolSession.Create();
        var engine = new Engine(options => options.UseDevTools());
        configure?.Invoke(engine);

        var target = session.AddTarget(new EngineTargetOptions { ThreadMode = ThreadMode.LibraryOwned }, engine);
        var sessionId = await session.AttachAsync(target).ConfigureAwait(false);

        return new AttachedSession(session, target, sessionId);
    }

    /// <summary>One conversation, one engine, and the identifier that addresses the second through the first.</summary>
    private sealed class AttachedSession(ProtocolSession session, EngineTarget target, string sessionId) : IAsyncDisposable
    {
        internal EngineTarget Target { get; } = target;

        internal string SessionId { get; } = sessionId;

        internal Task<JsonElement> SendAsync(string method, string? parameters = null, string? sessionId = null)
            => session.SendAsync(method, parameters, sessionId);

        internal IReadOnlyList<JsonElement> EventsOf(string method) => session.EventsOf(method);

        internal async Task<JsonElement> EvaluateAsync(string expression, bool returnByValue = false, bool awaitPromise = false)
        {
            var parameters = JsonSerializer.Serialize(new Dictionary<string, object>
            {
                ["expression"] = expression,
                ["returnByValue"] = returnByValue,
                ["awaitPromise"] = awaitPromise,
            });

            var reply = await SendAsync("Runtime.evaluate", parameters, SessionId).ConfigureAwait(false);
            reply.TryGetProperty("error", out var error).Should().BeFalse("evaluating was expected to succeed, and it answered {0}", error);

            var result = reply.GetProperty("result");
            result.TryGetProperty("exceptionDetails", out var details).Should().BeFalse("the expression was expected to succeed, and it threw {0}", details);
            return result.GetProperty("result");
        }

        public ValueTask DisposeAsync() => session.DisposeAsync();
    }
}
