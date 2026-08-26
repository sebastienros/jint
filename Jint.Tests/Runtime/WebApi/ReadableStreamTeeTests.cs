#if NET8_0_OR_GREATER
#nullable enable

using Jint.Runtime;

namespace Jint.Tests.Runtime.WebApi;

/// <summary>
/// <c>ReadableStream.prototype.tee()</c> — https://streams.spec.whatwg.org/#readable-stream-default-tee.
/// </summary>
public class ReadableStreamTeeTests
{
    private static Engine StreamEngine()
    {
        var engine = new Engine(options => options.UseWebApis(WebApiFeatures.Streams));
        engine.Execute("""
            var log = [];
            async function drain(stream, label) {
              const reader = stream.getReader();
              for (;;) {
                let result;
                try { result = await reader.read(); }
                catch (e) { log.push(label + ':error:' + e.message); return; }
                if (result.done) { log.push(label + ':done'); return; }
                log.push(label + ':' + result.value);
              }
            }
            """);
        return engine;
    }

    private static string Log(Engine engine) => engine.Evaluate("log.join(',')").AsString();

    [Test]
    public void ReturnsTwoBranchesAndLocksTheOriginal()
    {
        var engine = StreamEngine();
        engine.Execute("""
            var stream = new ReadableStream();
            var branches = stream.tee();
            """);

        engine.Evaluate("Array.isArray(branches)").AsBoolean().Should().BeTrue();
        engine.Evaluate("branches.length").AsNumber().Should().Be(2);
        engine.Evaluate("branches[0] instanceof ReadableStream && branches[1] instanceof ReadableStream").AsBoolean().Should().BeTrue();
        engine.Evaluate("branches[0] !== branches[1]").AsBoolean().Should().BeTrue();
        engine.Evaluate("stream.locked").AsBoolean().Should().BeTrue();

        Assert.Throws<JavaScriptException>(() => engine.Evaluate("stream.getReader()"))!
            .Error.Get("name").AsString().Should().Be("TypeError");
    }

    [Test]
    public void RefusesToTeeALockedStream()
    {
        var engine = StreamEngine();
        engine.Execute("var stream = new ReadableStream(); stream.getReader();");

        Assert.Throws<JavaScriptException>(() => engine.Evaluate("stream.tee()"))!
            .Error.Get("name").AsString().Should().Be("TypeError");
    }

    [Test]
    public void BothBranchesSeeEveryChunkAndTheClose()
    {
        var engine = StreamEngine();
        engine.Execute("""
            var stream = new ReadableStream({ start(c) { c.enqueue('a'); c.enqueue('b'); c.close(); } });
            var [one, two] = stream.tee();
            (async () => { await drain(one, 'one'); await drain(two, 'two'); })();
            """);

        Log(engine).Should().Be("one:a,one:b,one:done,two:a,two:b,two:done");
    }

    [Test]
    public void HandsBothBranchesTheVerySameChunkObject()
    {
        // The non-byte tee never clones: "the chunks seen in each branch will be the same object."
        var engine = StreamEngine();
        engine.Execute("""
            var chunk = { id: 1 };
            var [one, two] = new ReadableStream({ start(c) { c.enqueue(chunk); c.close(); } }).tee();
            var a, b;
            one.getReader().read().then(r => { a = r.value; });
            two.getReader().read().then(r => { b = r.value; });
            """);

        engine.Evaluate("a === chunk && b === chunk").AsBoolean().Should().BeTrue();
    }

    [Test]
    public void OneBranchMayRunFarAheadOfTheOther()
    {
        // Each branch has its own queue, so a consumer that never reads does not stall the other.
        var engine = StreamEngine();
        engine.Execute("""
            var [one, two] = new ReadableStream({ start(c) { c.enqueue('a'); c.enqueue('b'); c.close(); } }).tee();
            drain(one, 'one');
            """);

        Log(engine).Should().Be("one:a,one:b,one:done");
        engine.Evaluate("two.locked").AsBoolean().Should().BeFalse();

        // The slow branch still has everything waiting for it.
        engine.Execute("drain(two, 'two');");
        Log(engine).Should().Be("one:a,one:b,one:done,two:a,two:b,two:done");
    }

    [Test]
    public void AnErrorInTheOriginalErrorsBothBranchesImmediately()
    {
        var engine = StreamEngine();
        engine.Execute("""
            var controller;
            var stream = new ReadableStream({ start(c) { controller = c; } });
            var [one, two] = stream.tee();
            drain(one, 'one');
            drain(two, 'two');
            controller.error(new Error('bad'));
            """);

        Log(engine).Should().Be("one:error:bad,two:error:bad");
    }

    [Test]
    public void AnErrorDoesNotOvertakeASynchronouslyAvailableChunk()
    {
        // The tee's chunk steps are deliberately delayed by a microtask, because an error only reaches the
        // branches through the reader's closed promise, which also takes a microtask. Without the delay a
        // chunk enqueued and then errored in the same turn would reach the branches out of order.
        var engine = StreamEngine();
        engine.Execute("""
            var controller;
            var stream = new ReadableStream({ start(c) { controller = c; c.enqueue('a'); } });
            var [one, two] = stream.tee();
            drain(one, 'one');
            controller.error(new Error('bad'));
            """);

        Log(engine).Should().Be("one:error:bad");
    }

    [Test]
    public void CancellingOneBranchDoesNotCancelTheOriginal()
    {
        var engine = StreamEngine();
        engine.Execute("""
            var stream = new ReadableStream({
              start(c) { c.enqueue('a'); },
              cancel(reason) { log.push('source:' + JSON.stringify(reason)); }
            });
            var [one, two] = stream.tee();
            one.cancel('first').then(() => log.push('one:cancelled'));
            """);

        // The underlying source has not been told anything, and the branch's own cancel promise is still
        // pending: it settles only once the other branch is done with the stream too.
        Log(engine).Should().Be("");

        // Meanwhile the other branch keeps working, chunk and all.
        engine.Execute("drain(two, 'two');");
        Log(engine).Should().Be("two:a");
    }

    [Test]
    public void ALoneBranchCancelSettlesOnceTheOtherBranchReachesTheEnd()
    {
        var engine = StreamEngine();
        engine.Execute("""
            var controller;
            var stream = new ReadableStream({ start(c) { controller = c; } });
            var [one, two] = stream.tee();
            one.cancel('first').then(() => log.push('one:cancelled'));
            drain(two, 'two');
            log.push('mid');
            controller.close();
            """);

        Log(engine).Should().Be("mid,two:done,one:cancelled");
    }

    [Test]
    public void ALoneBranchCancelSettlesOnceTheOriginalErrors()
    {
        var engine = StreamEngine();
        engine.Execute("""
            var controller;
            var stream = new ReadableStream({ start(c) { controller = c; } });
            var [one, two] = stream.tee();
            one.cancel('first').then(() => log.push('one:cancelled'), e => log.push('one:rejected'));
            controller.error(new Error('bad'));
            drain(two, 'two');
            """);

        Log(engine).Should().Be("two:error:bad,one:cancelled");
    }

    [Test]
    public void CancellingBothBranchesCancelsTheOriginalWithACompositeReason()
    {
        var engine = StreamEngine();
        engine.Execute("""
            var stream = new ReadableStream({ cancel(reason) { log.push('source:' + JSON.stringify(reason)); } });
            var [one, two] = stream.tee();
            one.cancel('first').then(() => log.push('one:cancelled'));
            two.cancel('second').then(() => log.push('two:cancelled'));
            """);

        Log(engine).Should().Be("""source:["first","second"],one:cancelled,two:cancelled""");
    }

    [Test]
    public void ACompositeCancelReportsTheUnderlyingSourceFailureToBothBranches()
    {
        var engine = StreamEngine();
        engine.Execute("""
            var stream = new ReadableStream({ cancel() { return Promise.reject(new Error('no')); } });
            var [one, two] = stream.tee();
            one.cancel().catch(e => log.push('one:' + e.message));
            two.cancel().catch(e => log.push('two:' + e.message));
            """);

        Log(engine).Should().Be("one:no,two:no");
    }

    [Test]
    public void ClosingTheOriginalClosesEveryBranchThatIsStillInterested()
    {
        var engine = StreamEngine();
        engine.Execute("""
            var controller;
            var stream = new ReadableStream({ start(c) { controller = c; } });
            var [one, two] = stream.tee();
            drain(one, 'one');
            drain(two, 'two');
            controller.close();
            """);

        Log(engine).Should().Be("one:done,two:done");
    }

    [Test]
    public void PullsFromTheOriginalOnlyWhenABranchWantsMore()
    {
        var engine = StreamEngine();
        engine.Execute("""
            var pulls = 0;
            var stream = new ReadableStream({
              pull(c) { pulls++; c.enqueue('chunk' + pulls); },
              cancel() {}
            }, { highWaterMark: 0 });
            var [one, two] = stream.tee();
            log.push('teed:' + pulls);
            one.getReader().read().then(r => log.push('one:' + r.value + ':pulls=' + pulls));
            """);

        // Teeing itself reads nothing; each branch's own high water mark of 1 is what starts a read. The
        // second branch's request arrives while the first read is outstanding, so it is remembered and
        // served once that read completes — which is the second pull.
        Log(engine).Should().Be("teed:0,one:chunk1:pulls=2");
    }
}
#endif
