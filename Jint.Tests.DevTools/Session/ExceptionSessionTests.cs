using System.Text.Json;
using Jint.Runtime;

namespace Jint.Tests.DevTools.Session;

/// <summary>
/// What a client hears about script that failed with nobody to catch it:
/// <c>Runtime.exceptionThrown</c>, its revocation, and the <c>Log</c> domain's own entry stream.
/// </summary>
/// <remarks>
/// Two sources, and they are different things. An <b>uncaught exception</b> is script that escaped the
/// engine's own pump — a timer callback, an event listener — which a library-owned target reports itself and
/// a host-owned one reports through <see cref="Jint.DevTools.EngineTarget.ReportUncaughtException"/>. An
/// <b>unhandled rejection</b> is the engine's own rejection tracker, which every target subscribes to.
/// </remarks>
public class ExceptionSessionTests
{
    [Test]
    public async Task AnUnhandledRejectionIsReportedAsAnException()
    {
        await using var session = await AttachedSession.CreateAsync();

        await session.ResultAsync("Runtime.enable");
        await session.EvaluateAsync("Promise.reject(new RangeError('nope'))");

        var thrown = session.EventsOf("Runtime.exceptionThrown");
        thrown.Should().HaveCount(1);

        var details = thrown[0].GetProperty("params").GetProperty("exceptionDetails");
        details.GetProperty("text").GetString().Should().Be("Uncaught (in promise)");
        details.GetProperty("executionContextId").GetInt32().Should().Be(1);
        details.GetProperty("exception").GetProperty("className").GetString().Should().Be("RangeError");
        details.GetProperty("exception").GetProperty("objectId").GetString().Should().NotBeNullOrEmpty(
            "a client renders the reason and then asks what is inside it");

        thrown[0].GetProperty("params").GetProperty("timestamp").GetDouble().Should().BeGreaterThan(0);
    }

    /// <summary>
    /// The engine decides a rejection is unhandled the moment it happens, where V8 waits for the end of the
    /// microtask checkpoint. A handler attached afterwards therefore produces the pair
    /// <c>exceptionRevoked</c> exists for, and the identifiers match.
    /// </summary>
    [Test]
    public async Task AHandlerAddedLaterRevokesTheExceptionItReported()
    {
        await using var session = await AttachedSession.CreateAsync();

        await session.ResultAsync("Runtime.enable");
        await session.EvaluateAsync("globalThis.rejected = Promise.reject(new Error('late'))");
        await session.EvaluateAsync("rejected.catch(function () {})");

        var thrown = session.EventsOf("Runtime.exceptionThrown");
        thrown.Should().HaveCount(1);

        var revoked = session.EventsOf("Runtime.exceptionRevoked");
        revoked.Should().HaveCount(1);

        revoked[0].GetProperty("params").GetProperty("reason").GetString().Should().Be("Handler added to rejected promise");
        revoked[0].GetProperty("params").GetProperty("exceptionId").GetInt32()
            .Should().Be(thrown[0].GetProperty("params").GetProperty("exceptionDetails").GetProperty("exceptionId").GetInt32());
    }

    /// <summary>
    /// A rejection handled on the very same line is reported and immediately revoked, where Chrome reports
    /// neither. That is the engine's rejection tracker firing at the moment of rejection rather than at the
    /// end of the microtask checkpoint, and it is why the revocation exists: a client that acts on the pair
    /// ends up where Chrome's would, one event later.
    /// </summary>
    [Test]
    public async Task ARejectionHandledOnTheSameLineIsReportedAndAtOnceRevoked()
    {
        await using var session = await AttachedSession.CreateAsync();

        await session.ResultAsync("Runtime.enable");
        await session.EvaluateAsync("Promise.reject(new Error('caught')).catch(function () {})", awaitPromise: true);

        var thrown = session.EventsOf("Runtime.exceptionThrown");
        var revoked = session.EventsOf("Runtime.exceptionRevoked");

        thrown.Should().HaveCount(1);
        revoked.Should().HaveCount(1);
        revoked[0].GetProperty("params").GetProperty("exceptionId").GetInt32()
            .Should().Be(thrown[0].GetProperty("params").GetProperty("exceptionDetails").GetProperty("exceptionId").GetInt32());
    }

    /// <summary>A promise that never rejects is never reported, which is the base case worth pinning.</summary>
    [Test]
    public async Task APromiseThatSettlesWellIsNeverReported()
    {
        await using var session = await AttachedSession.CreateAsync();

        await session.ResultAsync("Runtime.enable");
        await session.EvaluateAsync("Promise.resolve(1).then(function () {})", awaitPromise: true);

        session.EventsOf("Runtime.exceptionThrown").Should().BeEmpty();
        session.EventsOf("Runtime.exceptionRevoked").Should().BeEmpty();
    }

    /// <summary>
    /// The host's own door: a host-owned target catches script that escaped its loop and tells whoever is
    /// attached, in the shape a client already understands.
    /// </summary>
    [Test]
    public async Task AnUncaughtExceptionTheHostReportsReachesTheClient()
    {
        await using var session = await AttachedSession.CreateAsync();

        await session.ResultAsync("Runtime.enable");

        await session.Target.PostAsync(engine =>
        {
            try
            {
                engine.Execute("\n\nthrow new TypeError('escaped')");
            }
            catch (JavaScriptException exception)
            {
                session.Target.ReportUncaughtException(exception);
            }
        });

        var thrown = session.EventsOf("Runtime.exceptionThrown");
        thrown.Should().HaveCount(1);

        var details = thrown[0].GetProperty("params").GetProperty("exceptionDetails");
        details.GetProperty("text").GetString().Should().Be("Uncaught");
        details.GetProperty("lineNumber").GetInt32().Should().Be(2, "the protocol counts lines from zero and the parser counts them from one");
        details.GetProperty("exception").GetProperty("description").GetString().Should().Contain("escaped");
    }

    /// <summary>
    /// Script that escapes the library-owned loop is reported by the loop itself, which is the whole reason
    /// that mode exists: nobody else is holding the <c>try</c>.
    /// </summary>
    [Test]
    public async Task ScriptThatEscapesTheLibraryOwnedLoopIsReported()
    {
        await using var session = await AttachedSession.CreateAsync();

        await session.ResultAsync("Runtime.enable");

        session.Target.Post(engine => engine.Execute("throw new TypeError('out of the pump')"));

        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(30);
        while (session.EventsOf("Runtime.exceptionThrown").Count == 0 && DateTime.UtcNow < deadline)
        {
            await Task.Delay(25);
        }

        var thrown = session.EventsOf("Runtime.exceptionThrown");
        thrown.Should().HaveCount(1);
        thrown[0].GetProperty("params").GetProperty("exceptionDetails").GetProperty("exception")
            .GetProperty("description").GetString().Should().Contain("out of the pump");
    }

    [Test]
    public async Task ADomainNobodyEnabledReportsNothing()
    {
        await using var session = await AttachedSession.CreateAsync();

        await session.EvaluateAsync("Promise.reject(new Error('unheard'))");

        session.EventsOf("Runtime.exceptionThrown").Should().BeEmpty();
        session.EventsOf("Log.entryAdded").Should().BeEmpty();
    }

    [Test]
    public async Task TheLogDomainCarriesTheSameFailures()
    {
        await using var session = await AttachedSession.CreateAsync();

        await session.ResultAsync("Log.enable");
        await session.EvaluateAsync("Promise.reject(new RangeError('logged'))");

        var entries = session.EventsOf("Log.entryAdded");
        entries.Should().HaveCount(1);

        var entry = entries[0].GetProperty("params").GetProperty("entry");
        entry.GetProperty("source").GetString().Should().Be("javascript");
        entry.GetProperty("level").GetString().Should().Be("error");
        entry.GetProperty("text").GetString().Should().Contain("Uncaught (in promise)").And.Contain("logged");
        entry.GetProperty("timestamp").GetDouble().Should().BeGreaterThan(0);
    }

    [Test]
    public async Task AnUncaughtExceptionCarriesItsLocationIntoTheLog()
    {
        await using var session = await AttachedSession.CreateAsync();

        await session.ResultAsync("Log.enable");

        await session.Target.PostAsync(engine =>
        {
            try
            {
                engine.Execute("throw new TypeError('located')", "somewhere.js");
            }
            catch (JavaScriptException exception)
            {
                session.Target.ReportUncaughtException(exception);
            }
        });

        var entry = session.EventsOf("Log.entryAdded")[0].GetProperty("params").GetProperty("entry");
        entry.GetProperty("text").GetString().Should().Contain("located");
        entry.GetProperty("url").GetString().Should().Be("somewhere.js");
        entry.GetProperty("lineNumber").GetInt32().Should().Be(1, "a log entry counts lines from one, unlike a call frame");
    }

    [TestCase("Log.clear", null)]
    [TestCase("Log.disable", null)]
    [TestCase("Log.startViolationsReport", """{"config":[]}""")]
    [TestCase("Log.stopViolationsReport", null)]
    public async Task TheLogCommandsAnEngineTargetHasNothingToDoWithStillSucceed(string method, string? parameters)
    {
        await using var session = await AttachedSession.CreateAsync();

        var reply = await session.SendAsync(method, parameters);
        reply.GetProperty("result").GetRawText().Should().Be("{}");
    }

    /// <summary>
    /// A rejection reason is any value, and a client watching a log stream must not be what makes a page's
    /// code run: the text comes from the engine's getter-free describer.
    /// </summary>
    [Test]
    public async Task ARejectionReasonIsDescribedWithoutRunningItsToString()
    {
        await using var session = await AttachedSession.CreateAsync();

        await session.ResultAsync("Log.enable");
        await session.ResultAsync("Runtime.enable");
        await session.EvaluateAsync(
            """
            (function () {
                globalThis.reads = 0;
                const reason = { toString: function () { globalThis.reads++; return 'ran'; } };
                Promise.reject(reason);
            })()
            """);

        session.EventsOf("Log.entryAdded").Should().HaveCount(1);

        var reads = await session.EvaluateAsync("reads", returnByValue: true);
        reads.GetProperty("value").GetInt32().Should().Be(0);
    }
}
