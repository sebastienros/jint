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
        await using var session = await AttachedSession.CreateAsync();

        var reply = await session.SendAsync("Runtime.enable");
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
        await using var session = await AttachedSession.CreateAsync();

        await session.SendAsync("Runtime.enable");
        await session.SendAsync("Runtime.enable");

        session.EventsOf("Runtime.executionContextCreated").Should().HaveCount(1);
    }

    [Test]
    public async Task DisableSucceedsAndSaysNothing()
    {
        await using var session = await AttachedSession.CreateAsync();

        await session.SendAsync("Runtime.enable");
        var reply = await session.SendAsync("Runtime.disable");

        reply.GetProperty("result").GetRawText().Should().Be("{}");
    }

    [Test]
    public async Task EvaluateAnswersANumber()
    {
        await using var session = await AttachedSession.CreateAsync();

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
        await using var session = await AttachedSession.CreateAsync();

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
        await using var session = await AttachedSession.CreateAsync();

        var result = await session.EvaluateAsync(expression);

        result.GetProperty("unserializableValue").GetString().Should().Be(expected);
        result.TryGetProperty("value", out _).Should().BeFalse("a client that read 0 back for -0 has been told something false");
    }

    [Test]
    public async Task EvaluateWithoutReturnByValueDescribesAnObjectAndHandsOutAHandle()
    {
        await using var session = await AttachedSession.CreateAsync();

        var result = await session.EvaluateAsync("({ a: 1, b: [1, 2] })");

        result.GetProperty("type").GetString().Should().Be("object");
        result.GetProperty("className").GetString().Should().Be("Object");
        result.GetProperty("description").GetString().Should().Be("Object");
        result.GetProperty("objectId").GetString().Should().NotBeNullOrEmpty(
            "a value that cannot be sent by value is addressable, which is what every later command about it needs");
    }

    [Test]
    public async Task EvaluateWithReturnByValueSerializesTheValue()
    {
        await using var session = await AttachedSession.CreateAsync();

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
        await using var session = await AttachedSession.CreateAsync();

        var reply = await session.SendAsync(
            "Runtime.evaluate", """{"expression":"(function () { const a = {}; a.self = a; return a; })()","returnByValue":true}""");

        var error = reply.GetProperty("error");
        error.GetProperty("code").GetInt32().Should().Be(-32000);
        error.GetProperty("message").GetString().Should().Be("Object couldn't be returned by value");
        error.GetProperty("data").GetString().Should().Contain("Cyclic reference",
            "the refusal carries what JSON.stringify itself said, which is the whole reason the engine's own serializer does the work");
    }

    [Test]
    public async Task AThrownErrorBecomesExceptionDetailsAndNotAProtocolError()
    {
        await using var session = await AttachedSession.CreateAsync();

        var reply = await session.SendAsync("Runtime.evaluate", """{"expression":"\n\nthrow new TypeError('boom')"}""");

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
        await using var session = await AttachedSession.CreateAsync();

        var reply = await session.SendAsync("Runtime.evaluate", """{"expression":"function ("}""");

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
        await using var session = await AttachedSession.CreateAsync();

        var result = await session.EvaluateAsync("Promise.resolve(41 + 1)", returnByValue: true, awaitPromise: true);

        result.GetProperty("type").GetString().Should().Be("number");
        result.GetProperty("value").GetInt32().Should().Be(42);
    }

    [Test]
    public async Task AwaitPromiseWaitsForOneThatSettlesOnALaterTurn()
    {
        await using var session = await AttachedSession.CreateAsync();

        var result = await session.EvaluateAsync(
            "new Promise(function (resolve) { Promise.resolve().then(function () { resolve('later'); }); })",
            returnByValue: true,
            awaitPromise: true);

        result.GetProperty("value").GetString().Should().Be("later");
    }

    [Test]
    public async Task AwaitPromiseReportsARejectionAsExceptionDetails()
    {
        await using var session = await AttachedSession.CreateAsync();

        var reply = await session.SendAsync("Runtime.evaluate", """{"expression":"Promise.reject(new RangeError('nope'))","awaitPromise":true}""");

        reply.TryGetProperty("error", out _).Should().BeFalse();

        var details = reply.GetProperty("result").GetProperty("exceptionDetails");
        details.GetProperty("text").GetString().Should().Be("Uncaught (in promise)");
        details.GetProperty("exception").GetProperty("subtype").GetString().Should().Be("error");
        details.GetProperty("exception").GetProperty("className").GetString().Should().Be("RangeError");
    }

    [Test]
    public async Task WithoutAwaitPromiseAPromiseIsDescribedRatherThanWaitedFor()
    {
        await using var session = await AttachedSession.CreateAsync();

        var result = await session.EvaluateAsync("Promise.resolve(1)");

        result.GetProperty("subtype").GetString().Should().Be("promise");
        result.TryGetProperty("value", out _).Should().BeFalse();
    }

    [Test]
    public async Task EvaluateInAContextThatIsNotThereIsRefusedInChromesWording()
    {
        await using var session = await AttachedSession.CreateAsync();

        var reply = await session.SendAsync("Runtime.evaluate", """{"expression":"1","contextId":7}""");

        reply.GetProperty("error").GetProperty("code").GetInt32().Should().Be(-32000);
        reply.GetProperty("error").GetProperty("message").GetString().Should().Be("Cannot find context with specified id");
    }

    [Test]
    public async Task EvaluateSeesWhatTheHostPutOnTheEngine()
    {
        await using var session = await AttachedSession.CreateAsync(engine => engine.SetValue("answer", 42));

        var result = await session.EvaluateAsync("answer + 1");
        result.GetProperty("value").GetInt32().Should().Be(43);
    }

    [Test]
    public async Task GetIsolateIdIsStableForOneEngine()
    {
        await using var session = await AttachedSession.CreateAsync();

        var first = (await session.SendAsync("Runtime.getIsolateId")).GetProperty("result").GetProperty("id").GetString();
        var second = (await session.SendAsync("Runtime.getIsolateId")).GetProperty("result").GetProperty("id").GetString();

        first.Should().NotBeNullOrEmpty();
        second.Should().Be(first);
    }

    [Test]
    public async Task DiscardConsoleEntriesSucceeds()
    {
        await using var session = await AttachedSession.CreateAsync();

        var reply = await session.SendAsync("Runtime.discardConsoleEntries");
        reply.GetProperty("result").GetRawText().Should().Be("{}");
    }

    /// <summary>
    /// The <c>Runtime</c> commands that need engine surface this package cannot reach say so with the one
    /// error a client feature-detects on rather than with a made-up success.
    /// </summary>
    /// <remarks>
    /// <c>globalLexicalScopeNames</c> would need the realm's global declarative record to publish its
    /// binding <i>names</i>, which it does not — <c>engine.Diagnostics.GetMemoryReport()</c> answers a count
    /// and nothing else. <c>queryObjects</c> would need the heap enumerated by prototype, which is the CLR's
    /// heap. <c>getExceptionDetails</c> would need an exception's details retained past the command that
    /// reported them. <c>terminateExecution</c> would let a client stop a host's script at will, which is a
    /// different security posture from the one the constraints define.
    /// </remarks>
    [TestCase("Runtime.globalLexicalScopeNames")]
    [TestCase("Runtime.queryObjects")]
    [TestCase("Runtime.getExceptionDetails")]
    [TestCase("Runtime.terminateExecution")]
    public async Task TheCommandsThisPackageCannotAnswerAreMethodNotFound(string method)
    {
        await using var session = await AttachedSession.CreateAsync();

        var error = (await session.SendAsync(method, "{}")).GetProperty("error");
        error.GetProperty("code").GetInt32().Should().Be(-32601);
        error.GetProperty("message").GetString().Should().Be($"'{method}' wasn't found");
    }
}
