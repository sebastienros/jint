using System.Text.Json;
using Jint.DevTools;
using Jint.Runtime.Debugger;

namespace Jint.Tests.DevTools.Session;

/// <summary>
/// <c>Debugger.setPauseOnExceptions</c>: which throws stop the engine, and what the client is told when one
/// does.
/// </summary>
/// <remarks>
/// <para>
/// The four states are not two booleans. <c>caught</c> and <c>uncaught</c> are the interesting pair, and a
/// suite that only tested <c>none</c> and <c>all</c> would pass with the filter inverted — so every state is
/// exercised against both a throw something catches and a throw nothing catches.
/// </para>
/// <para>
/// A test asserting that a throw does <i>not</i> stop the engine is a test that hangs when it is wrong, so
/// those bound the pause at a couple of seconds: the failure then reads as an unexpected pause rather than as
/// a continuous-integration leg that never finished.
/// </para>
/// </remarks>
[NonParallelizable]
public class DebuggerExceptionPauseTests
{
    private const string Source = """
        function thrower() {
            throw new Error("boom");
        }

        function guarded() {
            try {
                thrower();
            } catch (error) {
                return "caught " + error.message;
            }
        }

        function unguarded() {
            thrower();
        }
        """;

    /// <summary>The default: a throw is reported and nothing stops for it.</summary>
    [Test]
    public async Task NoneStopsForNeitherKindOfThrow()
    {
        await using var session = await CreateAsync();
        await session.ResultAsync("Debugger.setPauseOnExceptions", """{"state":"none"}""");

        (await RunAsync(session, "guarded()")).Should().Be("caught boom");
        (await RunAsync(session, "unguarded()")).Should().Be("threw boom");

        session.EventsOf("Debugger.paused").Should().BeEmpty("'none' is what a front end sends on connect");
    }

    /// <summary>
    /// <c>all</c> stops at every throw, and says of each whether anything is waiting to catch it.
    /// </summary>
    [Test]
    public async Task AllStopsAtBothAndSaysWhichIsUncaught()
    {
        await using var session = await CreateAsync();
        await session.ResultAsync("Debugger.setPauseOnExceptions", """{"state":"all"}""");

        var guarded = await PauseAsync(session, "guarded()");
        Uncaught(guarded).Should().BeFalse("a catch clause is executing on the stack");

        var unguarded = await PauseAsync(session, "unguarded()", index: 1);
        Uncaught(unguarded).Should().BeTrue("nothing on the stack will catch it");
    }

    /// <summary>The state a front end offers as "pause on uncaught exceptions", and the one it means.</summary>
    [Test]
    public async Task UncaughtStopsOnlyAtTheThrowNothingCatches()
    {
        await using var session = await CreateAsync();
        await session.ResultAsync("Debugger.setPauseOnExceptions", """{"state":"uncaught"}""");

        (await RunAsync(session, "guarded()")).Should().Be("caught boom");
        session.EventsOf("Debugger.paused").Should().BeEmpty("something catches it, so it is not what the client asked for");

        var paused = await PauseAsync(session, "unguarded()");
        Uncaught(paused).Should().BeTrue();
    }

    /// <summary>
    /// <c>caught</c> is the mirror image, and it is an engine mode of its own: the engine decides it at the
    /// throw, so a throw nobody will catch never reaches the pause handler at all.
    /// </summary>
    [Test]
    public async Task CaughtStopsOnlyAtTheThrowSomethingCatches()
    {
        await using var session = await CreateAsync();
        await session.ResultAsync("Debugger.setPauseOnExceptions", """{"state":"caught"}""");

        var paused = await PauseAsync(session, "guarded()");
        Uncaught(paused).Should().BeFalse();

        (await RunAsync(session, "unguarded()")).Should().Be("threw boom");
        session.EventsOf("Debugger.paused").Should().HaveCount(1, "the engine was never asked about the uncaught throw");
    }

    /// <summary>
    /// Each of the protocol's four states is one engine mode, so no throw is raised for this domain to
    /// decline — which it could only do by answering with a <c>StepMode</c>, and every one of those but
    /// <c>Unchanged</c> sets the mode and cancels a step in flight.
    /// </summary>
    [Test]
    public async Task EachProtocolStateIsAnEngineModeOfItsOwn()
    {
        await using var session = await CreateAsync();

        var states = new (string State, ExceptionPauseMode Mode)[]
        {
            ("none", ExceptionPauseMode.None),
            ("caught", ExceptionPauseMode.Caught),
            ("uncaught", ExceptionPauseMode.Uncaught),
            ("all", ExceptionPauseMode.All),
        };

        foreach (var (state, mode) in states)
        {
            await session.ResultAsync("Debugger.setPauseOnExceptions", $$"""{"state":"{{state}}"}""");

            (await session.Target.PostAsync(engine => engine.Debugger.PauseOnExceptions))
                .Should().Be(mode, "'{0}' is what the client asked the engine for", state);
        }
    }

    /// <summary>
    /// The pause carries the thrown value itself, as a handle a client inspects the way it inspects any other.
    /// </summary>
    [Test]
    public async Task ThePauseCarriesTheThrownValueAsARemoteObject()
    {
        await using var session = await CreateAsync();
        await session.ResultAsync("Debugger.setPauseOnExceptions", """{"state":"all"}""");

        var paused = await PauseAsync(session, "unguarded()", resume: false);

        paused.GetProperty("reason").GetString().Should().Be("exception");
        paused.GetProperty("hitBreakpoints").EnumerateArray().Should().BeEmpty("no breakpoint was hit; the throw was");

        var data = paused.GetProperty("data");
        data.GetProperty("type").GetString().Should().Be("object");
        data.GetProperty("subtype").GetString().Should().Be("error");
        data.GetProperty("className").GetString().Should().Be("Error");
        data.GetProperty("description").GetString().Should().StartWith("Error: boom");

        // The handle is live for the pause, so the client can read the error's own properties from it.
        var objectId = data.GetProperty("objectId").GetString()!;
        var properties = await session.PropertiesAsync(objectId, ownProperties: true);
        properties.Property("message").GetProperty("value").GetProperty("value").GetString().Should().Be("boom");

        // The engine stopped at the throw rather than after it, so the frame that threw is still on the stack.
        var frames = paused.GetProperty("callFrames").EnumerateArray().ToArray();
        frames[0].GetProperty("functionName").GetString().Should().Be("thrower");

        await session.ResultAsync("Debugger.resume");
    }

    /// <summary>A throw of something that is not an object is still a value, and is described as one.</summary>
    [Test]
    public async Task ThrowingAPrimitiveIsDescribedAsThatPrimitive()
    {
        await using var session = await CreateAsync();
        await session.ResultAsync("Debugger.setPauseOnExceptions", """{"state":"all"}""");

        var paused = await PauseAsync(session, "(function () { throw 42; })()");

        var data = paused.GetProperty("data");
        data.GetProperty("type").GetString().Should().Be("number");
        data.GetProperty("value").GetInt32().Should().Be(42);
    }

    /// <summary>
    /// The mode is the attachment's, so a client that goes away leaves the engine exactly as it found it.
    /// </summary>
    [Test]
    public async Task DisablingGivesTheEngineBackItsOwnPauseMode()
    {
        await using var session = await CreateAsync();
        await session.ResultAsync("Debugger.setPauseOnExceptions", """{"state":"all"}""");

        await session.ResultAsync("Debugger.disable");

        var mode = await session.Target.PostAsync(engine => engine.Debugger.PauseOnExceptions);
        mode.Should().Be(Jint.Runtime.Debugger.ExceptionPauseMode.None);

        (await RunAsync(session, "unguarded()")).Should().Be("threw boom");
        session.EventsOf("Debugger.paused").Should().BeEmpty();
    }

    /// <summary>A state the protocol does not define is refused rather than quietly read as <c>none</c>.</summary>
    [Test]
    public async Task AnUnknownStateIsRefused()
    {
        await using var session = await CreateAsync();

        var error = await session.ErrorAsync("Debugger.setPauseOnExceptions", """{"state":"sometimes"}""");
        error.GetProperty("code").GetInt32().Should().Be(-32000);
        error.GetProperty("message").GetString().Should().Contain("sometimes");
    }

    /// <summary>An attachment with the functions defined and the engine ready to throw them.</summary>
    private static async Task<AttachedSession> CreateAsync()
    {
        var session = await AttachedSession.CreateAsync(
            serverOptions: new DevToolsServerOptions { PauseTimeout = TimeSpan.FromSeconds(2) }).ConfigureAwait(false);

        await session.EnableDebuggerAsync().ConfigureAwait(false);
        await session.Target.PostAsync(engine => engine.Execute(Source, "main.js")).ConfigureAwait(false);
        return session;
    }

    /// <summary>
    /// Runs <paramref name="expression"/> as host work and answers what it did, throw included.
    /// </summary>
    /// <remarks>
    /// Host work rather than <c>Runtime.evaluate</c>, so that a pause happens inside the engine's own job and
    /// not nested inside the command that is waiting for it. An uncaught throw reaches the host as a
    /// <c>JavaScriptException</c>, which is the whole point of calling it uncaught.
    /// </remarks>
    private static Task<string> RunAsync(AttachedSession session, string expression) => session.Target.PostAsync(engine =>
    {
        try
        {
            return engine.Evaluate(expression).ToString();
        }
        catch (Jint.Runtime.JavaScriptException exception)
        {
            return "threw " + exception.Message;
        }
    });

    /// <summary>Runs <paramref name="expression"/>, waits for the pause it causes, and resumes.</summary>
    private static async Task<JsonElement> PauseAsync(
        AttachedSession session,
        string expression,
        int index = 0,
        bool resume = true)
    {
        var running = RunAsync(session, expression);
        var paused = await session.EventAsync("Debugger.paused", index).ConfigureAwait(false);

        paused.GetProperty("reason").GetString().Should().Be("exception");

        if (resume)
        {
            await session.ResultAsync("Debugger.resume").ConfigureAwait(false);
            await running.ConfigureAwait(false);
        }

        return paused;
    }

    /// <summary>Whether the pause said nothing was waiting to catch the throw.</summary>
    private static bool Uncaught(JsonElement paused) => paused.GetProperty("data").GetProperty("uncaught").GetBoolean();
}
