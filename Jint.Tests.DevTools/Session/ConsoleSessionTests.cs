using System.Text.Json;
using Jint.DevTools;
using Jint.WebApi;

namespace Jint.Tests.DevTools.Session;

/// <summary>
/// What a script logged, as a client hears it: <c>Runtime.consoleAPICalled</c> with handles and previews,
/// and the legacy <c>Console</c> domain's flat lines.
/// </summary>
/// <remarks>
/// Every engine here is built with <see cref="WebApiFeatures.Console"/>, because without it there is no
/// <c>console</c> object to call and nothing to report — which is itself one of the tests.
/// </remarks>
public class ConsoleSessionTests
{
    [Test]
    public async Task AConsoleCallReachesTheClientWithHandlesAndPreviews()
    {
        await using var session = await ConsoleSessionAsync();

        await session.ResultAsync("Runtime.enable");
        await session.EvaluateAsync("console.log('answer', { a: 1 }, 42)");

        var called = session.EventsOf("Runtime.consoleAPICalled");
        called.Should().HaveCount(1);

        var parameters = called[0].GetProperty("params");
        parameters.GetProperty("type").GetString().Should().Be("log");
        parameters.GetProperty("executionContextId").GetInt32().Should().Be(1);
        parameters.GetProperty("timestamp").GetDouble().Should().BeGreaterThan(0);

        var args = parameters.GetProperty("args").EnumerateArray().ToArray();
        args.Should().HaveCount(3);

        args[0].GetProperty("type").GetString().Should().Be("string");
        args[0].GetProperty("value").GetString().Should().Be("answer");
        args[0].Optional("objectId").Should().BeNull("a primitive is sent whole, not held");

        args[1].GetProperty("type").GetString().Should().Be("object");
        args[1].GetProperty("objectId").GetString().Should().NotBeNullOrEmpty();
        args[1].GetProperty("preview").GetProperty("properties").EnumerateArray().Single()
            .GetProperty("name").GetString().Should().Be("a");

        args[2].GetProperty("value").GetInt32().Should().Be(42);
    }

    [Test]
    public async Task AConsoleArgumentIsAddressableUntilItsGroupIsReleased()
    {
        await using var session = await ConsoleSessionAsync();

        await session.ResultAsync("Runtime.enable");
        await session.EvaluateAsync("console.log({ a: 1 })");

        var objectId = session.EventsOf("Runtime.consoleAPICalled")[0]
            .GetProperty("params").GetProperty("args").EnumerateArray().Single()
            .GetProperty("objectId").GetString()!;

        var properties = await session.PropertiesAsync(objectId, ownProperties: true);
        properties.Property("a").GetProperty("value").GetProperty("value").GetInt32().Should().Be(1);

        // Which is what a client's own "clear console" sends.
        await session.ResultAsync("Runtime.releaseObjectGroup", """{"objectGroup":"console"}""");

        (await session.ErrorAsync("Runtime.getProperties", $$"""{"objectId":"{{objectId}}"}"""))
            .GetProperty("message").GetString().Should().Be("Could not find object with given id");
    }

    [TestCase("console.debug('x')", "debug")]
    [TestCase("console.info('x')", "info")]
    [TestCase("console.warn('x')", "warning")]
    [TestCase("console.error('x')", "error")]
    [TestCase("console.dir({})", "dir")]
    [TestCase("console.table([1])", "table")]
    [TestCase("console.trace('x')", "trace")]
    [TestCase("console.assert(false, 'x')", "assert")]
    [TestCase("console.group('x')", "startGroup")]
    [TestCase("console.groupCollapsed('x')", "startGroupCollapsed")]
    [TestCase("console.group('x'); console.groupEnd()", "endGroup")]
    [TestCase("console.count('x')", "count")]
    [TestCase("console.time('x'); console.timeEnd('x')", "timeEnd")]
    public async Task EachConsoleMethodArrivesAsTheProtocolsOwnType(string script, string type)
    {
        await using var session = await ConsoleSessionAsync();

        await session.ResultAsync("Runtime.enable");
        await session.EvaluateAsync(script);

        var types = session.EventsOf("Runtime.consoleAPICalled")
            .Select(called => called.GetProperty("params").GetProperty("type").GetString());

        types.Should().Contain(type);
    }

    /// <summary>
    /// <c>console.trace</c> is the one call that carries frames, and the two countings differ: the engine
    /// counts lines and columns from one and the protocol counts both from zero.
    /// </summary>
    [Test]
    public async Task ConsoleTraceCarriesTheCapturedFrames()
    {
        await using var session = await ConsoleSessionAsync();

        await session.ResultAsync("Runtime.enable");
        await session.EvaluateAsync("(function outer() { (function inner() { console.trace('here'); })(); })()");

        var parameters = session.EventsOf("Runtime.consoleAPICalled")[0].GetProperty("params");
        parameters.GetProperty("type").GetString().Should().Be("trace");

        var frames = parameters.GetProperty("stackTrace").GetProperty("callFrames").EnumerateArray().ToArray();
        frames.Should().NotBeEmpty();
        frames.Select(frame => frame.GetProperty("functionName").GetString()).Should().Contain("inner");
        frames[0].GetProperty("lineNumber").GetInt32().Should().BeGreaterThanOrEqualTo(0);
    }

    /// <summary>
    /// Every console call carries its call site, not just <c>console.trace</c>: it is the source anchor a
    /// front end prints on the right of each line, and without it a message links to nothing.
    /// </summary>
    [Test]
    public async Task EveryConsoleCallCarriesItsCallSite()
    {
        await using var session = await ConsoleSessionAsync();

        await session.ResultAsync("Runtime.enable");
        await session.EnableDebuggerAsync();
        await session.Target.PostAsync(engine => engine.Execute(
            """
            function speak() {
                console.log('spoken');
            }
            speak();
            """,
            "app.js"));

        var parameters = session.EventsOf("Runtime.consoleAPICalled")
            .Select(call => call.GetProperty("params"))
            .Single(call => call.GetProperty("type").GetString() == "log");

        var frames = parameters.GetProperty("stackTrace").GetProperty("callFrames").EnumerateArray().ToArray();

        // The console method's own frame is not one of them: the anchor is where the script called it.
        frames[0].GetProperty("functionName").GetString().Should().Be("speak");
        frames[0].GetProperty("url").GetString().Should().Be("app.js");
        frames[0].GetProperty("lineNumber").GetInt32().Should().Be(1);

        // Resolved back to the script, so the front end can make the anchor clickable rather than printing
        // a URL it cannot open.
        frames[0].GetProperty("scriptId").GetString().Should().NotBe("0");
    }

    /// <summary>
    /// A client that enables the domain after the fact is replayed the journal, which is what makes a front
    /// end opened halfway through a run useful rather than empty. V8 does the same.
    /// </summary>
    [Test]
    public async Task EnablingAfterTheFactReplaysWhatWasLogged()
    {
        await using var session = await ConsoleSessionAsync();

        await session.EvaluateAsync("console.log('before'); console.warn('also before')");
        session.EventsOf("Runtime.consoleAPICalled").Should().BeEmpty("a domain nobody enabled says nothing");

        await session.ResultAsync("Runtime.enable");

        var replayed = session.EventsOf("Runtime.consoleAPICalled");
        replayed.Should().HaveCount(2);
        replayed[0].GetProperty("params").GetProperty("args").EnumerateArray().Single()
            .GetProperty("value").GetString().Should().Be("before");
        replayed[1].GetProperty("params").GetProperty("type").GetString().Should().Be("warning");
    }

    [Test]
    public async Task DiscardConsoleEntriesEmptiesTheJournalAndReleasesItsHandles()
    {
        await using var session = await ConsoleSessionAsync();

        await session.ResultAsync("Runtime.enable");
        await session.EvaluateAsync("console.log({ a: 1 })");

        session.Target.Runtime.RemoteObjects.Count.Should().BeGreaterThan(0);

        await session.ResultAsync("Runtime.discardConsoleEntries");

        session.Target.Runtime.RemoteObjects.Count.Should().Be(0, "the handles the journal minted go with it");

        // And a client enabling now is replayed nothing.
        await session.ResultAsync("Runtime.disable");
        var before = session.EventsOf("Runtime.consoleAPICalled").Count;
        await session.ResultAsync("Runtime.enable");
        session.EventsOf("Runtime.consoleAPICalled").Should().HaveCount(before);
    }

    /// <summary>
    /// The journal is bounded, and the bound is a memory bound before it is a replay bound: every entry
    /// holds its arguments alive, so falling out of it has to release the handles minted for them.
    /// </summary>
    [Test]
    public async Task TheJournalIsBoundedAndEvictionReleasesWhatItHeld()
    {
        await using var session = await ConsoleSessionAsync();

        await session.ResultAsync("Runtime.enable");
        await session.EvaluateAsync("console.log({ first: true })");

        var first = session.EventsOf("Runtime.consoleAPICalled")[0]
            .GetProperty("params").GetProperty("args").EnumerateArray().Single()
            .GetProperty("objectId").GetString()!;

        await session.EvaluateAsync("for (var i = 0; i < 200; i++) { console.log({ i: i }); }");

        session.Target.Runtime.RemoteObjects.Count.Should().BeLessThan(
            201,
            "a page that logs an object every turn must not keep every one of them alive for as long as a client stays attached");

        (await session.ErrorAsync("Runtime.getProperties", $$"""{"objectId":"{{first}}"}"""))
            .GetProperty("message").GetString().Should().Be("Could not find object with given id");
    }

    [Test]
    public async Task TheConsoleDomainCarriesTheFlatLines()
    {
        await using var session = await ConsoleSessionAsync();

        await session.EvaluateAsync("console.log('before enabling')");
        await session.ResultAsync("Console.enable");

        var replayed = session.EventsOf("Console.messageAdded");
        replayed.Should().HaveCount(1);
        replayed[0].GetProperty("params").GetProperty("message").GetProperty("text").GetString().Should().Be("before enabling");
        replayed[0].GetProperty("params").GetProperty("message").GetProperty("source").GetString().Should().Be("console-api");
        replayed[0].GetProperty("params").GetProperty("message").GetProperty("level").GetString().Should().Be("log");

        await session.EvaluateAsync("console.error('after enabling')");
        var all = session.EventsOf("Console.messageAdded");
        all.Should().HaveCount(2);
        all[1].GetProperty("params").GetProperty("message").GetProperty("level").GetString().Should().Be("error");
    }

    /// <summary>
    /// The <c>Console</c> domain reports lines, so a call that printed nothing is absent from it — while
    /// <c>Runtime.consoleAPICalled</c> carries it, because a front end draws a group from it.
    /// </summary>
    [Test]
    public async Task ACallThatPrintedNothingIsAbsentFromTheFlatLines()
    {
        await using var session = await ConsoleSessionAsync();

        await session.ResultAsync("Console.enable");
        await session.ResultAsync("Runtime.enable");
        await session.EvaluateAsync("console.group('open'); console.groupEnd()");

        session.EventsOf("Console.messageAdded").Should().HaveCount(1, "groupEnd prints nothing");
        session.EventsOf("Runtime.consoleAPICalled").Should().HaveCount(2);
    }

    [Test]
    public async Task ConsoleClearMessagesEmptiesTheSameJournal()
    {
        await using var session = await ConsoleSessionAsync();

        await session.EvaluateAsync("console.log('gone')");
        await session.ResultAsync("Console.clearMessages");
        await session.ResultAsync("Console.enable");

        session.EventsOf("Console.messageAdded").Should().BeEmpty();
    }

    [Test]
    public async Task DisablingStopsTheEvents()
    {
        await using var session = await ConsoleSessionAsync();

        await session.ResultAsync("Runtime.enable");
        await session.ResultAsync("Console.enable");
        await session.ResultAsync("Runtime.disable");
        await session.ResultAsync("Console.disable");

        var runtime = session.EventsOf("Runtime.consoleAPICalled").Count;
        await session.EvaluateAsync("console.log('nobody is listening')");

        session.EventsOf("Runtime.consoleAPICalled").Should().HaveCount(runtime);
        session.EventsOf("Console.messageAdded").Should().BeEmpty();
    }

    /// <summary>
    /// The wrapper is a wrapper: a host that set its own sink keeps every line it was getting, in the
    /// overload it was getting it in.
    /// </summary>
    [Test]
    public async Task TheHostsOwnSinkKeepsEverythingItWasGetting()
    {
        var recorded = new RecordingSink();

        await using var session = await AttachedSession.CreateAsync(configureOptions: options =>
        {
            options.WebApi.Features |= WebApiFeatures.Console;
            options.WebApi.Console.Sink = recorded;
        });

        await session.ResultAsync("Runtime.enable");
        await session.EvaluateAsync("console.log('to both'); console.groupEnd()");

        recorded.Lines.Should().Equal("to both");
        // The structured overload sees the calls that print nothing too, which is what it is for.
        recorded.Records.Should().Equal("Log", "GroupEnd");
        session.EventsOf("Runtime.consoleAPICalled").Should().HaveCount(2);
    }

    /// <summary>
    /// An engine that never enabled the console has nothing to log through, so nothing is reported and
    /// nothing costs anything.
    /// </summary>
    [Test]
    public async Task AnEngineWithoutTheConsoleFeatureReportsNothing()
    {
        await using var session = await AttachedSession.CreateAsync();

        await session.ResultAsync("Runtime.enable");

        var typeOf = await session.EvaluateAsync("typeof console", returnByValue: true);
        typeOf.GetProperty("value").GetString().Should().Be("undefined");

        session.EventsOf("Runtime.consoleAPICalled").Should().BeEmpty();
    }

    [Test]
    public async Task DetachingStopsTheConsoleEvents()
    {
        await using var session = await ConsoleSessionAsync();

        await session.ResultAsync("Runtime.enable");
        await session.Protocol.SendAsync("Target.detachFromTarget", $$"""{"sessionId":"{{session.SessionId}}"}""");

        var before = session.EventsOf("Runtime.consoleAPICalled").Count;
        await session.Target.PostAsync(engine => engine.Execute("console.log('after detaching')"));

        session.EventsOf("Runtime.consoleAPICalled").Should().HaveCount(before);
    }

    private static Task<AttachedSession> ConsoleSessionAsync()
        => AttachedSession.CreateAsync(configureOptions: options => options.WebApi.Features |= WebApiFeatures.Console);

    /// <summary>A host's own sink, which the wrapper must keep feeding in both of its overloads.</summary>
    private sealed class RecordingSink : ConsoleSink
    {
        private readonly List<string> _lines = [];
        private readonly List<string> _records = [];

        internal IReadOnlyList<string> Lines
        {
            get
            {
                lock (_lines)
                {
                    return [.. _lines];
                }
            }
        }

        internal IReadOnlyList<string> Records
        {
            get
            {
                lock (_lines)
                {
                    return [.. _records];
                }
            }
        }

        public override void Write(ConsoleLogLevel level, string message)
        {
            lock (_lines)
            {
                _lines.Add(message);
            }
        }

        public override void Write(in ConsoleRecord record)
        {
            lock (_lines)
            {
                _records.Add(record.Method.ToString());
            }

            base.Write(in record);
        }
    }
}
