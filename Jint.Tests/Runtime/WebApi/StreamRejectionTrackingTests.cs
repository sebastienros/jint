#if NET8_0_OR_GREATER
#nullable enable

using Jint.Native;
using Jint.Native.Object;
using Jint.Native.Promise;
using Jint.Runtime;
using Jint.WebApi;

namespace Jint.Tests.Runtime.WebApi;

/// <summary>
/// What a host's <see cref="Engine.TaskOperations.PromiseRejectionTracker"/> hears while the streams
/// algorithms do their own bookkeeping — which is nothing, because every rejection they make for themselves
/// is one the specification accounts for in the very next step.
/// </summary>
/// <remarks>
/// <para>
/// Five of the Streams Standard's operations end with "reject <c>p</c> …" immediately followed by "set
/// <c>p.[[PromiseIsHandled]]</c> to true": <c>ReadableStreamError</c>,
/// <c>ReadableStreamReaderGenericRelease</c>, <c>WritableStreamRejectCloseAndClosedPromiseIfNeeded</c> and
/// the writer's <c>EnsureReadyPromiseRejected</c> / <c>EnsureClosedPromiseRejected</c>. In a browser that
/// pair raises nothing, because HTML's <i>notify about rejected promises</i> re-reads
/// <c>[[PromiseIsHandled]]</c> in a queued task — "If p.[[PromiseIsHandled]] is true, then continue" — long
/// after the flag has been set. Jint's tracker fires at <c>HostPromiseRejectionTracker</c>'s own cadence and
/// cannot re-read anything, so the flag is set before the rejection instead; the two orders are
/// indistinguishable in every other way, since <c>[[PromiseIsHandled]]</c> gates the tracker and nothing
/// else.
/// </para>
/// <para>
/// The tests are therefore in two halves: the algorithms' own rejections must be silent, and every rejection
/// that is <i>not</i> theirs — a promise handed to the script, a promise the script derived — must still be
/// reported exactly as it was.
/// </para>
/// <para>
/// https://streams.spec.whatwg.org/ ·
/// https://html.spec.whatwg.org/multipage/webappapis.html#unhandled-promise-rejections
/// </para>
/// </remarks>
public class StreamRejectionTrackingTests
{
    /// <summary>One <c>HostPromiseRejectionTracker</c> call, as a host hears it.</summary>
    private sealed record Rejection(JsValue Promise, PromiseRejectionOperation Operation, string Reason)
    {
        public override string ToString() => $"{Operation}:{Reason}";
    }

    private static (Engine Engine, List<Rejection> Rejections) TrackingEngine(
        WebApiFeatures features = WebApiFeatures.Streams | WebApiFeatures.Events)
    {
        var rejections = new List<Rejection>();
        var engine = new Engine(options => options.UseWebApis(features));

        engine.Tasks.PromiseRejectionTracker += (_, e) =>
            rejections.Add(new Rejection(e.Promise, e.Operation, Describe(e.Value)));

        engine.Execute("""
            var log = [];
            function sink(name) {
              return new WritableStream({
                write(chunk) { log.push(name + ':' + chunk); },
                close() { log.push(name + ':close'); },
                abort(reason) { log.push(name + ':abort:' + reason); }
              });
            }
            function source(chunks) {
              return new ReadableStream({
                start(c) { for (const chunk of chunks) { c.enqueue(chunk); } c.close(); },
                cancel(reason) { log.push('source:cancel:' + reason); }
              });
            }
            """);

        return (engine, rejections);
    }

    private static string Describe(JsValue? value)
    {
        if (value is ObjectInstance o && o.Get("name") is JsString name && o.Get("message") is JsString message)
        {
            return $"{name}: {message}";
        }

        return value?.ToString() ?? "<null>";
    }

    private static string Log(Engine engine) => engine.Evaluate("log.join(',')").AsString();

    /// <summary>
    /// What a host is left holding once the pairs cancel out: the rejections reported with no later
    /// <c>"handle"</c> for the same promise. A <c>Reject</c> followed by a <c>Handle</c> means a handler was
    /// attached in a later turn — the promise was announced and then handled, which is what
    /// <see cref="DiagnosticEvent.RejectionHandled"/> is for — and is not what this defect was about.
    /// </summary>
    private static List<string> Standing(List<Rejection> rejections)
    {
        var handled = new HashSet<object>(
            rejections.Where(r => r.Operation == PromiseRejectionOperation.Handle).Select(r => (object) r.Promise),
            ReferenceEqualityComparer.Instance);

        return rejections
            .Where(r => r.Operation == PromiseRejectionOperation.Reject && !handled.Contains(r.Promise))
            .Select(r => r.Reason)
            .ToList();
    }

    // ---- The algorithms' own bookkeeping: silent ----

    [Test]
    public void ASuccessfulPipeToReportsNothingAtAll()
    {
        var (engine, rejections) = TrackingEngine();

        engine.Execute("source(['a', 'b']).pipeTo(sink('dest')).then(() => log.push('piped'));");

        Log(engine).Should().Be("dest:a,dest:b,dest:close,piped");
        rejections.Should().BeEmpty();
    }

    [Test]
    public void ASuccessfulPipeThroughReportsNothingAtAll()
    {
        var (engine, rejections) = TrackingEngine();

        engine.Execute("""
            source(['a', 'b'])
              .pipeThrough(new TransformStream())
              .pipeTo(sink('dest'))
              .then(() => log.push('piped'));
            """);

        Log(engine).Should().Be("dest:a,dest:b,dest:close,piped");
        rejections.Should().BeEmpty();
    }

    [Test]
    public void ATextDecoderStreamPipeThroughReportsNothingAtAll()
    {
        var (engine, rejections) = TrackingEngine(
            WebApiFeatures.Streams | WebApiFeatures.Events | WebApiFeatures.Encoding);

        engine.Execute("""
            new ReadableStream({ start(c) { c.enqueue(new Uint8Array([104, 105])); c.close(); } })
              .pipeThrough(new TextDecoderStream())
              .pipeTo(sink('dest'))
              .then(() => log.push('piped'));
            """);

        Log(engine).Should().Be("dest:hi,dest:close,piped");
        rejections.Should().BeEmpty();
    }

    [Test]
    public void ABlobStreamPipeThroughReportsNothingAtAll()
    {
        // The shape the defect was found in: Blob.stream() feeding a transform.
        var (engine, rejections) = TrackingEngine(
            WebApiFeatures.Streams | WebApiFeatures.Events | WebApiFeatures.Encoding | WebApiFeatures.Files);

        engine.Execute("""
            new Blob(['hi'])
              .stream()
              .pipeThrough(new TextDecoderStream())
              .pipeTo(sink('dest'))
              .then(() => log.push('piped'));
            """);

        Log(engine).Should().Be("dest:hi,dest:close,piped");
        rejections.Should().BeEmpty();
    }

    [Test]
    public void APlainDrainReportsNothingAtAll()
    {
        // The control the issue reported: no pipe, no phantom. It has to stay at zero too.
        var (engine, rejections) = TrackingEngine();

        engine.Execute("(async () => { for await (const chunk of source(['a', 'b'])) { log.push('read:' + chunk); } })();");

        Log(engine).Should().Be("read:a,read:b");
        rejections.Should().BeEmpty();
    }

    [Test]
    public void ScriptCalledReleaseLockOnAReaderReportsNothing()
    {
        // ReadableStreamReaderGenericRelease, reached by the script rather than by a pipe.
        var (engine, rejections) = TrackingEngine();

        engine.Execute("source(['a']).getReader().releaseLock();");

        rejections.Should().BeEmpty();
    }

    [Test]
    public void ScriptCalledReleaseLockOnAWriterReportsNothing()
    {
        // WritableStreamDefaultWriterRelease, and with it both Ensure*PromiseRejected operations.
        var (engine, rejections) = TrackingEngine();

        engine.Execute("sink('dest').getWriter().releaseLock();");

        rejections.Should().BeEmpty();
    }

    [Test]
    public void ReleasingAReaderOnAnAlreadyClosedStreamReportsNothing()
    {
        // The other branch of the release steps: the closed promise has already settled, so the reader is
        // given a freshly rejected one instead.
        var (engine, rejections) = TrackingEngine();

        engine.Execute("""
            var reader = new ReadableStream({ start(c) { c.close(); } }).getReader();
            reader.closed.then(() => { reader.releaseLock(); log.push('released'); });
            """);

        Log(engine).Should().Be("released");
        rejections.Should().BeEmpty();
    }

    [Test]
    public void ErroringAStreamThatHasAReaderReportsNothing()
    {
        // ReadableStreamError: "Reject reader.[[closedPromise]] with e" then "set …[[PromiseIsHandled]] to
        // true". Nobody is watching reader.closed here, and a browser raises nothing either.
        var (engine, rejections) = TrackingEngine();

        engine.Execute("""
            var controller;
            var stream = new ReadableStream({ start(c) { controller = c; } });
            stream.getReader();
            controller.error(new Error('boom'));
            """);

        rejections.Should().BeEmpty();
    }

    [Test]
    public void ErroringAWritableStreamThatHasAWriterReportsNothing()
    {
        // WritableStreamRejectCloseAndClosedPromiseIfNeeded, plus the ready promise the erroring rejects.
        var (engine, rejections) = TrackingEngine();

        engine.Execute("""
            var controller;
            var stream = new WritableStream({ start(c) { controller = c; } });
            stream.getWriter();
            controller.error(new Error('boom'));
            """);

        rejections.Should().BeEmpty();
    }

    [Test]
    public void TeeReportsNothing()
    {
        var (engine, rejections) = TrackingEngine();

        engine.Execute("""
            var branches = source(['a']).tee();
            branches[0].pipeTo(sink('one')).then(() => log.push('one:piped'));
            branches[1].pipeTo(sink('two')).then(() => log.push('two:piped'));
            """);

        Log(engine).Should().Contain("one:piped").And.Contain("two:piped");
        rejections.Should().BeEmpty();
    }

    [Test]
    public void CancellingBothTeeBranchesReportsNothing()
    {
        var (engine, rejections) = TrackingEngine();

        engine.Execute("""
            var branches = new ReadableStream({
              start(c) { c.enqueue('a'); },
              cancel(reason) { log.push('source:cancel:' + reason); }
            }).tee();
            branches[0].cancel('done').then(() => log.push('one:cancelled'));
            branches[1].cancel('done').then(() => log.push('two:cancelled'));
            """);

        // The source is cancelled once, with the composite reason « 'done', 'done' » the tee's cancel
        // algorithms build.
        Log(engine).Should().Be("source:cancel:done,done,one:cancelled,two:cancelled");
        rejections.Should().BeEmpty();
    }

    [Test]
    public void AnAbortedPipeReportsOnlyTheAbortReason()
    {
        // The signal case: the pipe's own promise rejects and the script does not catch it, so exactly one
        // rejection is reported — the AbortError. The released-reader and released-writer TypeErrors that
        // Finalize makes on the way are the algorithms' own and stay silent.
        var (engine, rejections) = TrackingEngine();

        engine.Execute("""
            var controller = new AbortController();
            var to = new WritableStream({ write(chunk) { log.push('write:' + chunk); } });
            var from = new ReadableStream({ start(c) { c.enqueue('a'); } });
            from.pipeTo(to, { signal: controller.signal });
            controller.abort();
            """);

        Standing(rejections).Should().ContainSingle().Which.Should().StartWith("AbortError:");
    }

    // ---- Everything that is not the algorithms' own: still reported ----

    [Test]
    public void AnOrdinaryUnhandledRejectionIsStillReported()
    {
        var (engine, rejections) = TrackingEngine();

        engine.Execute("Promise.reject(new Error('boom'));");

        rejections.Select(r => r.ToString()).Should().Equal("Reject:Error: boom");
    }

    [Test]
    public void AFailedPipeStillReportsItsOwnRejection()
    {
        // The signal a host actually wants: pipeTo()'s promise is the script's, so nothing marks it handled.
        var (engine, rejections) = TrackingEngine();

        engine.Execute("""
            var to = new WritableStream({ write() { throw new Error('dest failed'); } });
            source(['a']).pipeTo(to);
            """);

        // Exactly one rejection is left standing, and it is the pipe's own promise. The released-writer and
        // released-reader TypeErrors the shutdown makes on the way are gone entirely.
        Standing(rejections).Should().Equal("Error: dest failed");

        // And it is the only report of any kind. Two algorithms here build an already-rejected promise and
        // attach their own reaction one statement later — the WebIDL wrapper around the sink's throwing
        // write(), and WritableStreamDefaultWriterCloseWithErrorPropagation's "a promise rejected with
        // stream's stored error". Neither promise is the specification's to mark handled, and neither needs
        // to be: the reaction lands inside the same microtask checkpoint, so HTML's deferred notification
        // never announces it at all. That used to be a Reject followed by its own Handle.
        rejections.Select(r => r.ToString()).Should().Equal("Reject:Error: dest failed");
    }

    [Test]
    public void AWriteThatFailsStillReportsItsOwnRejection()
    {
        // writer.write()'s promise is handed to the script and the specification never marks it handled, so
        // it is reported like any other. This is the sibling the release steps are deliberately not.
        var (engine, rejections) = TrackingEngine();

        engine.Execute("""
            var writer = new WritableStream({ write() { throw new Error('write failed'); } }).getWriter();
            writer.write('a');
            """);

        Standing(rejections).Should().Contain("Error: write failed");
    }

    [Test]
    public void APromiseDerivedFromAReleasedReadersClosedPromiseIsStillReported()
    {
        // The over-suppression check. The reader's own closed promise is accounted for by the release steps;
        // a promise the script derived from it is the script's own and is reported exactly as before — which
        // is only possible because the release really does reject, rather than being made to settle some
        // other way.
        var (engine, rejections) = TrackingEngine();

        engine.Execute("""
            var reader = source(['a']).getReader();
            reader.closed.then(() => log.push('never'));
            reader.releaseLock();
            """);

        Log(engine).Should().Be("");
        rejections.Select(r => r.ToString()).Should().Equal(
            "Reject:TypeError: Reader was released and can no longer be used to monitor the stream's closedness");
    }

    [Test]
    public void AReleasedReadersClosedPromiseIsRejectedEvenThoughNothingIsReported()
    {
        // The other half of the same check: the flag suppresses the report, never the rejection. The script
        // holds the promise across the release, nothing is reported, and the promise is nevertheless rejected
        // with the release steps' TypeError — which is the state a browser leaves it in too, since
        // ReadableStreamReaderGenericRelease sets [[PromiseIsHandled]] unconditionally.
        var (engine, rejections) = TrackingEngine();

        engine.Execute("""
            var reader = source(['a']).getReader();
            var closed = reader.closed;
            reader.releaseLock();
            """);

        rejections.Should().BeEmpty();

        engine.Execute("closed.catch(e => log.push('caught:' + e.name));");
        Log(engine).Should().Be("caught:TypeError");
        rejections.Should().BeEmpty();
    }

    [Test]
    public void AReleasedWritersReadyAndClosedPromisesAreRejectedEvenThoughNothingIsReported()
    {
        var (engine, rejections) = TrackingEngine();

        // A destination with no room applies backpressure, so `ready` is still pending when the script takes
        // it — which is the case worth asserting: the release rejects the very promise the script is
        // holding, not a fresh one it will never see.
        engine.Execute("""
            var writer = new WritableStream({}, { highWaterMark: 0 }).getWriter();
            var ready = writer.ready;
            var closed = writer.closed;
            writer.releaseLock();
            ready.catch(e => log.push('ready:' + e.name));
            closed.catch(e => log.push('closed:' + e.name));
            """);

        Log(engine).Should().Be("ready:TypeError,closed:TypeError");
        rejections.Should().BeEmpty();
    }
}
#endif
