using System.Text.Json;

namespace Jint.Tests.DevTools.Session;

/// <summary>
/// The commands a client drives an engine with once it holds handles: calling a function on one, waiting for
/// a promise, compiling and running a script, and installing the binding a page answers through.
/// </summary>
/// <remarks>
/// <c>Runtime.callFunctionOn</c> is the busiest command any recorded client sends — between 32 and 53 times
/// in one scenario, with <c>returnByValue</c> both ways and <c>awaitPromise</c> throughout — so the
/// arrangements here are the ones out of
/// <c>tools/devtools-protocol/handshakes/matrix.md</c> rather than ones invented for the test.
/// </remarks>
public class RuntimeCallTests
{
    [Test]
    public async Task CallFunctionOnBindsThisToTheHandle()
    {
        await using var session = await AttachedSession.CreateAsync();

        var handle = await session.HandleAsync("({ answer: 42 })");
        var result = await session.ResultAsync(
            "Runtime.callFunctionOn",
            $$"""{"functionDeclaration":"function () { return this.answer; }","objectId":"{{handle}}","returnByValue":true}""");

        result.GetProperty("result").GetProperty("value").GetInt32().Should().Be(42);
    }

    [Test]
    public async Task CallFunctionOnWithoutAHandleRunsAgainstTheGlobal()
    {
        await using var session = await AttachedSession.CreateAsync(engine => engine.SetValue("answer", 42));

        var result = await session.ResultAsync(
            "Runtime.callFunctionOn",
            """{"functionDeclaration":"function () { return this.answer; }","executionContextId":1,"returnByValue":true}""");

        result.GetProperty("result").GetProperty("value").GetInt32().Should().Be(42);
    }

    /// <summary>
    /// Every client appends a <c>//# sourceURL=</c> comment to the declaration it sends, so the wrapper that
    /// turns a declaration into an expression has to survive one.
    /// </summary>
    [Test]
    public async Task ADeclarationEndingInALineCommentStillParses()
    {
        await using var session = await AttachedSession.CreateAsync();

        var result = await session.ResultAsync(
            "Runtime.callFunctionOn",
            """{"functionDeclaration":"function () { return 1; }\n//# sourceURL=__puppeteer_evaluation_script__","executionContextId":1,"returnByValue":true}""");

        result.GetProperty("result").GetProperty("value").GetInt32().Should().Be(1);
    }

    [Test]
    public async Task AnArrowDeclarationWorksToo()
    {
        await using var session = await AttachedSession.CreateAsync();

        var result = await session.ResultAsync(
            "Runtime.callFunctionOn",
            """{"functionDeclaration":"(a, b) => a + b","executionContextId":1,"arguments":[{"value":2},{"value":3}],"returnByValue":true}""");

        result.GetProperty("result").GetProperty("value").GetInt32().Should().Be(5);
    }

    [Test]
    public async Task AJsonArgumentBecomesANativeValue()
    {
        await using var session = await AttachedSession.CreateAsync();

        var result = await session.ResultAsync(
            "Runtime.callFunctionOn",
            """
            {"functionDeclaration":"function (v) { return [typeof v, Array.isArray(v.list), v.list[1], v.nested.deep]; }",
             "executionContextId":1,
             "arguments":[{"value":{"list":[1,2],"nested":{"deep":"yes"}}}],
             "returnByValue":true}
            """);

        result.GetProperty("result").GetProperty("value").EnumerateArray().Select(item => item.ToString())
            .Should().Equal("object", "True", "2", "yes");
    }

    [TestCase("NaN", "Number.isNaN(v)")]
    [TestCase("Infinity", "v === Infinity")]
    [TestCase("-Infinity", "v === -Infinity")]
    [TestCase("-0", "Object.is(v, -0)")]
    [TestCase("7n", "typeof v === 'bigint' && v === 7n")]
    public async Task AnUnserializableArgumentIsUnderstood(string unserializable, string test)
    {
        await using var session = await AttachedSession.CreateAsync();

        var result = await session.ResultAsync(
            "Runtime.callFunctionOn",
            $$"""
            {"functionDeclaration":"function (v) { return {{test}}; }",
             "executionContextId":1,
             "arguments":[{"unserializableValue":"{{unserializable}}"}],
             "returnByValue":true}
            """);

        result.GetProperty("result").GetProperty("value").GetBoolean().Should().BeTrue();
    }

    [Test]
    public async Task AnUnserializableArgumentNobodyRecognizesIsRefused()
    {
        await using var session = await AttachedSession.CreateAsync();

        var error = await session.ErrorAsync(
            "Runtime.callFunctionOn",
            """{"functionDeclaration":"function (v) { return v; }","executionContextId":1,"arguments":[{"unserializableValue":"wat"}]}""");

        error.GetProperty("code").GetInt32().Should().Be(-32000);
        error.GetProperty("message").GetString().Should().Be("Invalid CallArgument: wat");
    }

    [Test]
    public async Task AHandleArgumentResolvesBackToTheSameObject()
    {
        await using var session = await AttachedSession.CreateAsync();

        await session.EvaluateAsync("globalThis.subject = { a: 1 }");
        var handle = await session.HandleAsync("subject");

        var result = await session.ResultAsync(
            "Runtime.callFunctionOn",
            $$"""
            {"functionDeclaration":"function (v) { return v === globalThis.subject; }",
             "executionContextId":1,
             "arguments":[{"objectId":"{{handle}}"}],
             "returnByValue":true}
            """);

        result.GetProperty("result").GetProperty("value").GetBoolean().Should().BeTrue();
    }

    [Test]
    public async Task AnAbsentArgumentIsUndefined()
    {
        await using var session = await AttachedSession.CreateAsync();

        var result = await session.ResultAsync(
            "Runtime.callFunctionOn",
            """{"functionDeclaration":"function (v) { return typeof v; }","executionContextId":1,"arguments":[{}],"returnByValue":true}""");

        result.GetProperty("result").GetProperty("value").GetString().Should().Be("undefined");
    }

    [Test]
    public async Task CallFunctionOnAwaitsThePromiseItWasAskedTo()
    {
        await using var session = await AttachedSession.CreateAsync();

        var result = await session.ResultAsync(
            "Runtime.callFunctionOn",
            """
            {"functionDeclaration":"function () { return new Promise(function (resolve) { Promise.resolve().then(function () { resolve('later'); }); }); }",
             "executionContextId":1,
             "awaitPromise":true,
             "returnByValue":true}
            """);

        result.GetProperty("result").GetProperty("value").GetString().Should().Be("later");
    }

    [Test]
    public async Task CallFunctionOnReportsARejectionAsExceptionDetails()
    {
        await using var session = await AttachedSession.CreateAsync();

        var result = await session.ResultAsync(
            "Runtime.callFunctionOn",
            """
            {"functionDeclaration":"function () { return Promise.reject(new RangeError('nope')); }",
             "executionContextId":1,
             "awaitPromise":true,
             "returnByValue":true}
            """);

        var details = result.GetProperty("exceptionDetails");
        details.GetProperty("text").GetString().Should().Be("Uncaught (in promise)");
        details.GetProperty("exception").GetProperty("className").GetString().Should().Be("RangeError");

        result.GetProperty("result").GetProperty("subtype").GetString().Should().Be("error");
        result.GetProperty("result").GetProperty("objectId").GetString().Should().NotBeNullOrEmpty(
            "a client that asked for the result by value did not ask for the error object by value");
    }

    [Test]
    public async Task AThrowingCallIsExceptionDetailsAndNotAProtocolError()
    {
        await using var session = await AttachedSession.CreateAsync();

        var reply = await session.SendAsync(
            "Runtime.callFunctionOn",
            """{"functionDeclaration":"function () { throw new TypeError('boom'); }","executionContextId":1}""");

        reply.TryGetProperty("error", out _).Should().BeFalse("the command was answered; it is the client's function that failed");
        reply.GetProperty("result").GetProperty("exceptionDetails").GetProperty("exception")
            .GetProperty("description").GetString().Should().Contain("boom");
    }

    [Test]
    public async Task ByValueRefusesACycleTheSameWayEvaluateDoes()
    {
        await using var session = await AttachedSession.CreateAsync();

        var error = await session.ErrorAsync(
            "Runtime.callFunctionOn",
            """{"functionDeclaration":"function () { const a = {}; a.self = a; return a; }","executionContextId":1,"returnByValue":true}""");

        error.GetProperty("code").GetInt32().Should().Be(-32000);
        error.GetProperty("message").GetString().Should().Be("Object couldn't be returned by value");
    }

    [Test]
    public async Task ADeclarationThatIsNotAFunctionIsRefusedInChromesWording()
    {
        await using var session = await AttachedSession.CreateAsync();

        var notAFunction = await session.ErrorAsync(
            "Runtime.callFunctionOn",
            """{"functionDeclaration":"42","executionContextId":1}""");
        notAFunction.GetProperty("message").GetString().Should().Be("Given expression does not evaluate to a function");

        var unparseable = await session.ErrorAsync(
            "Runtime.callFunctionOn",
            """{"functionDeclaration":"function (","executionContextId":1}""");
        unparseable.GetProperty("message").GetString().Should().Be("Given expression does not evaluate to a function");
    }

    [Test]
    public async Task ACallInAContextThatIsNotThereIsRefused()
    {
        await using var session = await AttachedSession.CreateAsync();

        var error = await session.ErrorAsync(
            "Runtime.callFunctionOn",
            """{"functionDeclaration":"function () { return 1; }","executionContextId":7}""");

        error.GetProperty("message").GetString().Should().Be("Cannot find context with specified id");
    }

    [TestCase("Runtime.evaluate", """{"expression":"1","throwOnSideEffect":true}""")]
    [TestCase("Runtime.callFunctionOn", """{"functionDeclaration":"function () { return 1; }","executionContextId":1,"throwOnSideEffect":true}""")]
    public async Task ThrowOnSideEffectIsRefusedRatherThanIgnored(string method, string parameters)
    {
        await using var session = await AttachedSession.CreateAsync();

        var error = await session.ErrorAsync(method, parameters);

        error.GetProperty("code").GetInt32().Should().Be(-32000);
        error.GetProperty("message").GetString().Should().Be("Side-effect free evaluation is not supported");
    }

    [Test]
    public async Task AwaitPromiseAnswersWhatAHandleSettledTo()
    {
        await using var session = await AttachedSession.CreateAsync();

        var handle = await session.HandleAsync("new Promise(function (resolve) { Promise.resolve().then(function () { resolve(7); }); })");

        var result = await session.ResultAsync("Runtime.awaitPromise", $$"""{"promiseObjectId":"{{handle}}","returnByValue":true}""");
        result.GetProperty("result").GetProperty("value").GetInt32().Should().Be(7);
    }

    [Test]
    public async Task AwaitPromiseReportsARejection()
    {
        await using var session = await AttachedSession.CreateAsync();

        var handle = await session.HandleAsync("Promise.reject(new RangeError('nope'))");

        var result = await session.ResultAsync("Runtime.awaitPromise", $$"""{"promiseObjectId":"{{handle}}"}""");
        result.GetProperty("exceptionDetails").GetProperty("text").GetString().Should().Be("Uncaught (in promise)");
        result.GetProperty("exceptionDetails").GetProperty("exception").GetProperty("className").GetString().Should().Be("RangeError");
    }

    [Test]
    public async Task AwaitPromiseOnSomethingThatIsNotOneIsRefused()
    {
        await using var session = await AttachedSession.CreateAsync();

        var handle = await session.HandleAsync("({ a: 1 })");
        var error = await session.ErrorAsync("Runtime.awaitPromise", $$"""{"promiseObjectId":"{{handle}}"}""");

        error.GetProperty("code").GetInt32().Should().Be(-32000);
        error.GetProperty("message").GetString().Should().Be("Could not find promise with given id");
    }

    [Test]
    public async Task CompileScriptPersistsAndRunScriptRunsIt()
    {
        await using var session = await AttachedSession.CreateAsync();

        var compiled = await session.ResultAsync(
            "Runtime.compileScript",
            """{"expression":"6 * 7","sourceURL":"answer.js","persistScript":true}""");

        var scriptId = compiled.GetProperty("scriptId").GetString()!;
        scriptId.Should().NotBeNullOrEmpty();

        var ran = await session.ResultAsync("Runtime.runScript", $$"""{"scriptId":"{{scriptId}}","returnByValue":true}""");
        ran.GetProperty("result").GetProperty("value").GetInt32().Should().Be(42);
    }

    [Test]
    public async Task CompileScriptWithoutPersistingIsASyntaxCheck()
    {
        await using var session = await AttachedSession.CreateAsync();

        var ok = await session.ResultAsync(
            "Runtime.compileScript",
            """{"expression":"1 + 1","sourceURL":"","persistScript":false}""");
        ok.TryGetProperty("scriptId", out _).Should().BeFalse("nothing was persisted, so nothing is addressable");
        ok.TryGetProperty("exceptionDetails", out _).Should().BeFalse();

        var broken = await session.ResultAsync(
            "Runtime.compileScript",
            """{"expression":"function (","sourceURL":"broken.js","persistScript":true}""");
        broken.TryGetProperty("scriptId", out _).Should().BeFalse();
        broken.GetProperty("exceptionDetails").GetProperty("url").GetString().Should().Be("broken.js");
        broken.GetProperty("exceptionDetails").GetProperty("text").GetString().Should().NotBeNullOrEmpty();
    }

    [Test]
    public async Task RunScriptWithAnUnknownIdIsRefused()
    {
        await using var session = await AttachedSession.CreateAsync();

        var error = await session.ErrorAsync("Runtime.runScript", """{"scriptId":"nope"}""");

        error.GetProperty("code").GetInt32().Should().Be(-32000);
        error.GetProperty("message").GetString().Should().Be("No script with given id");
    }

    [Test]
    public async Task RunScriptReportsWhatTheScriptThrew()
    {
        await using var session = await AttachedSession.CreateAsync();

        var compiled = await session.ResultAsync(
            "Runtime.compileScript",
            """{"expression":"throw new TypeError('boom')","sourceURL":"boom.js","persistScript":true}""");

        var ran = await session.ResultAsync("Runtime.runScript", $$"""{"scriptId":"{{compiled.GetProperty("scriptId").GetString()}}"}""");
        ran.GetProperty("exceptionDetails").GetProperty("exception").GetProperty("description").GetString().Should().Contain("boom");
        ran.GetProperty("result").GetProperty("subtype").GetString().Should().Be("error");
    }

    [Test]
    public async Task GetHeapUsageAnswersTheManagedHeapTwice()
    {
        await using var session = await AttachedSession.CreateAsync();

        var usage = await session.ResultAsync("Runtime.getHeapUsage");

        usage.GetProperty("usedSize").GetDouble().Should().BeGreaterThan(0);
        usage.GetProperty("totalSize").GetDouble().Should().Be(usage.GetProperty("usedSize").GetDouble());
        usage.GetProperty("embedderHeapUsedSize").GetDouble().Should().Be(0);
        usage.GetProperty("backingStorageSize").GetDouble().Should().Be(0);
    }

    [TestCase("Runtime.setCustomObjectFormatterEnabled", """{"enabled":true}""")]
    [TestCase("Runtime.setMaxCallStackSizeToCapture", """{"size":200}""")]
    public async Task TheSwitchesThisTargetHasNothingToDoWithAreStillASuccess(string method, string parameters)
    {
        await using var session = await AttachedSession.CreateAsync();

        var reply = await session.SendAsync(method, parameters);
        reply.GetProperty("result").GetRawText().Should().Be("{}");
    }

    /// <summary>
    /// The binding path Puppeteer's <c>exposeFunction</c> and its wait helpers run on: a global function the
    /// client installs, and an event every time script calls it.
    /// </summary>
    [Test]
    public async Task ABindingScriptCallsReachesTheClient()
    {
        await using var session = await AttachedSession.CreateAsync();

        await session.ResultAsync("Runtime.addBinding", """{"name":"report"}""");
        await session.EvaluateAsync("typeof report === 'function' && report('first') === undefined && report('second')");

        var events = session.EventsOf("Runtime.bindingCalled");
        events.Should().HaveCount(2);

        events[0].GetProperty("sessionId").GetString().Should().Be(session.SessionId);
        events[0].GetProperty("params").GetProperty("name").GetString().Should().Be("report");
        events[0].GetProperty("params").GetProperty("payload").GetString().Should().Be("first");
        events[0].GetProperty("params").GetProperty("executionContextId").GetInt32().Should().Be(1);
        events[1].GetProperty("params").GetProperty("payload").GetString().Should().Be("second");
    }

    [Test]
    public async Task AddingTheSameBindingTwiceInstallsOneFunction()
    {
        await using var session = await AttachedSession.CreateAsync();

        await session.ResultAsync("Runtime.addBinding", """{"name":"report"}""");
        var first = await session.EvaluateAsync("report", returnByValue: false);

        await session.ResultAsync("Runtime.addBinding", """{"name":"report"}""");
        await session.EvaluateAsync("report('once')");

        first.GetProperty("type").GetString().Should().Be("function");
        session.EventsOf("Runtime.bindingCalled").Should().HaveCount(1, "one function, one subscriber, one event");
    }

    [Test]
    public async Task ABindingScopedToTheOneContextThisTargetHasIsInstalled()
    {
        await using var session = await AttachedSession.CreateAsync();

        // Every Puppeteer client sends executionContextName, because it means to reach an isolated world.
        // An engine target has one context and no worlds, so the binding lands on the one there is.
        await session.ResultAsync("Runtime.addBinding", """{"name":"report","executionContextName":"puppeteer_utility_world"}""");
        await session.EvaluateAsync("report('reached')");

        session.EventsOf("Runtime.bindingCalled").Should().HaveCount(1);
    }

    [Test]
    public async Task ABindingScopedToAContextThatIsNotThereIsRefused()
    {
        await using var session = await AttachedSession.CreateAsync();

        var error = await session.ErrorAsync("Runtime.addBinding", """{"name":"report","executionContextId":7}""");

        error.GetProperty("message").GetString().Should().Be("Cannot find context with specified id");
    }

    [Test]
    public async Task RemoveBindingTakesTheFunctionWithIt()
    {
        await using var session = await AttachedSession.CreateAsync();

        await session.ResultAsync("Runtime.addBinding", """{"name":"report"}""");
        await session.ResultAsync("Runtime.removeBinding", """{"name":"report"}""");

        var gone = await session.EvaluateAsync("typeof report", returnByValue: true);
        gone.GetProperty("value").GetString().Should().Be("undefined");
    }

    /// <summary>
    /// A detached attachment hears nothing more, and the global it installed answers rather than throwing:
    /// removing one is engine work and detaching happens on a transport thread.
    /// </summary>
    [Test]
    public async Task DetachingStopsTheEventsAndLeavesTheGlobalHarmless()
    {
        await using var session = await AttachedSession.CreateAsync();

        await session.ResultAsync("Runtime.addBinding", """{"name":"report"}""");
        await session.Protocol.SendAsync("Target.detachFromTarget", $$"""{"sessionId":"{{session.SessionId}}"}""");

        var again = await session.Protocol.SendAsync("Target.attachToTarget", $$"""{"targetId":"{{session.Target.TargetId}}","flatten":true}""");
        var reattached = again.GetProperty("result").GetProperty("sessionId").GetString();

        await session.Protocol.SendAsync("Runtime.evaluate", """{"expression":"report('nobody is listening')"}""", reattached);

        session.EventsOf("Runtime.bindingCalled").Should().BeEmpty();
    }
}
