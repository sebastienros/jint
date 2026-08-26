#if NET8_0_OR_GREATER
#nullable enable

using Jint.Runtime;

namespace Jint.Tests.Runtime.WebApi;

/// <summary>
/// https://fetch.spec.whatwg.org/#concept-body-clone — the tee a <c>Response.clone()</c> or a
/// <c>Request.clone()</c> runs, and the one property that separates it from
/// <c>ReadableStream.prototype.tee()</c>: it is
/// https://streams.spec.whatwg.org/#readablestream-tee, which is
/// <c>ReadableStreamTee(stream, <b>true</b>)</c>, so every chunk the clone receives is a
/// <a href="https://html.spec.whatwg.org/multipage/structured-data.html#structured-cloning">StructuredClone</a>
/// of the chunk the original receives.
/// </summary>
/// <remarks>
/// <para>
/// The engines here are built with <see cref="WebApiOptionsExtensions.UseFetch"/> and nothing else, which
/// deliberately does <b>not</b> include <see cref="WebApiFeatures.StructuredClone"/>: the serializer the tee
/// reaches is machinery, and the flag governs only the <c>structuredClone</c> global.
/// <see cref="TheCloneStillClonesOnAnEngineWithNoStructuredCloneGlobal"/> is what pins that.
/// </para>
/// <para>
/// A buffered body — <c>new Response('hi')</c> — is cloned by sharing its source and has no stream to tee,
/// so none of this applies to it; <c>ResponseTests.CloneSharesTheBytesAndNotTheUsedFlag</c> covers that half.
/// </para>
/// </remarks>
public class BodyCloneTeeTests
{
    private static Engine FetchEngine() => new(options => options.UseFetch());

    /// <summary>
    /// A sixteen-byte buffer whose bytes are all different, so that comparing content means something.
    /// </summary>
    private const string Fixture = """
        var arrayBuffer = new ArrayBuffer(16);
        new Uint8Array(arrayBuffer).set([1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16]);
        function bytesOf(v) {
          const buffer = v instanceof ArrayBuffer ? v : v.buffer;
          const offset = v instanceof ArrayBuffer ? 0 : v.byteOffset;
          return Array.from(new Uint8Array(buffer, offset, v.byteLength)).join('-');
        }
        function bodyOf(chunk) {
          return new Response(new ReadableStream({ start(c) { c.enqueue(chunk); c.close(); } }));
        }
        """;

    private const string AllTrue =
        "first-is-the-original:true,second-is-not:true,same-prototype:true,same-byteLength:true," +
        "same-byteOffset:true,different-buffer:true,same-bytes:true";

    /// <summary>
    /// Every chunk kind <c>fetch/api/response/response-clone.any.js</c> covers. The assertions are the
    /// corpus's own: identity, not merely equal content.
    /// </summary>
    [TestCase("new Int8Array(arrayBuffer, 1)")]
    [TestCase("new Int16Array(arrayBuffer, 2, 2)")]
    [TestCase("new Int32Array(arrayBuffer)")]
    [TestCase("arrayBuffer")]
    [TestCase("new Uint8Array(arrayBuffer)")]
    [TestCase("new Uint8ClampedArray(arrayBuffer)")]
    [TestCase("new Uint16Array(arrayBuffer, 2)")]
    [TestCase("new Uint32Array(arrayBuffer)")]
    [TestCase("new BigInt64Array(arrayBuffer)")]
    [TestCase("new BigUint64Array(arrayBuffer)")]
    [TestCase("new Float16Array(arrayBuffer)")]
    [TestCase("new Float32Array(arrayBuffer)")]
    [TestCase("new Float64Array(arrayBuffer)")]
    [TestCase("new DataView(arrayBuffer, 2, 8)")]
    public void TheClonedResponseGetsAStructuredCloneOfEveryChunk(string chunk)
    {
        var engine = FetchEngine();
        engine.Execute(Fixture);

        engine.Evaluate($$"""
            (async () => {
              const initial = {{chunk}};
              const response = bodyOf(initial);
              const clone = response.clone();

              const first = (await response.body.getReader().read()).value;
              const second = (await clone.body.getReader().read()).value;
              const isBuffer = initial instanceof ArrayBuffer;

              return [
                'first-is-the-original:' + (first === initial),
                'second-is-not:' + (second !== initial),
                'same-prototype:' + (Object.getPrototypeOf(second) === Object.getPrototypeOf(initial)),
                'same-byteLength:' + (second.byteLength === initial.byteLength),
                'same-byteOffset:' + (isBuffer ? true : second.byteOffset === initial.byteOffset),
                'different-buffer:' + (isBuffer ? second !== initial : second.buffer !== initial.buffer),
                'same-bytes:' + (bytesOf(second) === bytesOf(initial)),
              ].join(',');
            })()
            """).UnwrapIfPromise().AsString().Should().Be(AllTrue);
    }

    [Test]
    public void TheClonedRequestGetsAStructuredCloneOfEveryChunkToo()
    {
        // https://fetch.spec.whatwg.org/#dom-request-clone runs the very same clone-a-body algorithm, so the
        // sibling has to hold the same property even though no corpus row names it.
        var engine = FetchEngine();
        engine.Execute(Fixture);

        engine.Evaluate("""
            (async () => {
              const initial = new Uint8Array(arrayBuffer);
              const request = new Request('https://example.org/', {
                method: 'POST',
                body: new ReadableStream({ start(c) { c.enqueue(initial); c.close(); } }),
                duplex: 'half',
              });
              const clone = request.clone();

              const first = (await request.body.getReader().read()).value;
              const second = (await clone.body.getReader().read()).value;

              return [
                'first-is-the-original:' + (first === initial),
                'second-is-not:' + (second !== initial),
                'different-buffer:' + (second.buffer !== initial.buffer),
                'same-bytes:' + (bytesOf(second) === bytesOf(initial)),
              ].join(',');
            })()
            """).UnwrapIfPromise().AsString()
            .Should().Be("first-is-the-original:true,second-is-not:true,different-buffer:true,same-bytes:true");
    }

    [Test]
    public void WritingThroughOneBodysChunkIsInvisibleToTheOther()
    {
        // The whole reason the standard clones: a consumer of one branch may not reach the bytes the other
        // branch is about to read.
        var engine = FetchEngine();
        engine.Execute(Fixture);

        engine.Evaluate("""
            (async () => {
              const initial = new Uint8Array(arrayBuffer);
              const response = bodyOf(initial);
              const clone = response.clone();

              const second = (await clone.body.getReader().read()).value;
              second[0] = 99;

              const first = (await response.body.getReader().read()).value;
              return first[0] + ':' + second[0];
            })()
            """).UnwrapIfPromise().AsString().Should().Be("1:99");
    }

    [Test]
    public void TheCloneStillClonesOnAnEngineWithNoStructuredCloneGlobal()
    {
        // UseFetch() expands to Events | Url | Files | Streams and pointedly not StructuredClone, so the
        // global is absent — and the algorithm still runs, because the flag governs the global and not the
        // serializer behind it. A clone() whose correctness depended on an unrelated flag would be the bug
        // wearing a different hat.
        var engine = FetchEngine();
        engine.Execute(Fixture);

        engine.Evaluate("typeof structuredClone").AsString().Should().Be("undefined");

        engine.Evaluate("""
            (async () => {
              const initial = new Uint8Array(arrayBuffer);
              const response = bodyOf(initial);
              const clone = response.clone();
              const first = (await response.body.getReader().read()).value;
              const second = (await clone.body.getReader().read()).value;
              return second !== initial && second.buffer !== initial.buffer && bytesOf(second) === bytesOf(first);
            })()
            """).UnwrapIfPromise().AsBoolean().Should().BeTrue();
    }

    [Test]
    public void AChunkTheSerializerRefusesErrorsBothBodiesWithADataCloneError()
    {
        // https://streams.spec.whatwg.org/#readable-stream-default-tee step 3.2 of the chunk steps: the
        // original's own branch is errored too, even though its chunk was never in question.
        var engine = FetchEngine();

        engine.Evaluate("""
            (async () => {
              const response = new Response(new ReadableStream({
                start(c) { c.enqueue(function notSerializable() {}); c.close(); },
              }));
              const clone = response.clone();

              const failure = async stream => {
                try { await stream.getReader().read(); return 'resolved'; }
                catch (e) { return e.name; }
              };

              return (await failure(response.body)) + ',' + (await failure(clone.body));
            })()
            """).UnwrapIfPromise().AsString().Should().Be("DataCloneError,DataCloneError");
    }

    [Test]
    public void TeeItselfIsUntouchedAndStillHandsBothBranchesOneChunk()
    {
        // https://streams.spec.whatwg.org/#rs-tee passes cloneForBranch2 false, and that is normative: the
        // chunks seen in each branch are the same object. Fixing clone() must not "fix" this.
        var engine = FetchEngine();
        engine.Execute(Fixture);

        engine.Evaluate("""
            (async () => {
              const initial = new Uint8Array(arrayBuffer);
              const [one, two] = new ReadableStream({ start(c) { c.enqueue(initial); c.close(); } }).tee();
              const a = (await one.getReader().read()).value;
              const b = (await two.getReader().read()).value;
              return (a === initial) + ',' + (b === initial) + ',' + (a === b);
            })()
            """).UnwrapIfPromise().AsString().Should().Be("true,true,true");
    }

    [Test]
    public void ARequestBuiltFromARequestProxiesItsBodyAndDoesNotCloneTheChunks()
    {
        // https://fetch.spec.whatwg.org/#dom-request step 42 is "create a proxy for inputBody", which
        // https://streams.spec.whatwg.org/#readablestream-create-a-proxy defines as a pipe through an
        // identity TransformStream — chunks pass through unchanged. It is not the clone-a-body algorithm,
        // so it must not start cloning.
        var engine = FetchEngine();
        engine.Execute(Fixture);

        engine.Evaluate("""
            (async () => {
              const initial = new Uint8Array(arrayBuffer);
              const source = new Request('https://example.org/', {
                method: 'POST',
                body: new ReadableStream({ start(c) { c.enqueue(initial); c.close(); } }),
                duplex: 'half',
              });
              const proxy = new Request(source);

              const a = (await source.body.getReader().read()).value;
              const b = (await proxy.body.getReader().read()).value;
              return (a === initial) + ',' + (b === initial);
            })()
            """).UnwrapIfPromise().AsString().Should().Be("true,true");
    }

    [Test]
    public void ANetworkStyleByteStreamBodyWasAlreadyCloningAndStillIs()
    {
        // A body whose stream is a byte stream reaches ReadableByteStreamTee, which clones every chunk for
        // its second branch whatever cloneForBranch2 says — https://streams.spec.whatwg.org/#readable-stream-tee
        // step 3 does not pass the argument on. `new Response(bytes).body` is such a stream.
        var engine = FetchEngine();

        engine.Evaluate("""
            (async () => {
              const response = new Response(new Uint8Array([1, 2, 3]));
              const clone = response.clone();
              const first = (await response.body.getReader().read()).value;
              const second = (await clone.body.getReader().read()).value;
              return (first !== second) + ',' + (first.buffer !== second.buffer) + ',' + second.join('-');
            })()
            """).UnwrapIfPromise().AsString().Should().Be("true,true,1-2-3");
    }

    [Test]
    public void CloningTwiceGivesThreeIndependentBuffers()
    {
        // clone() replaces the original's stream with the tee's first branch, so a second clone() tees that
        // branch in turn. Each hop clones again, which is what keeps the third body independent too.
        var engine = FetchEngine();
        engine.Execute(Fixture);

        engine.Evaluate("""
            (async () => {
              const initial = new Uint8Array(arrayBuffer);
              const response = bodyOf(initial);
              const a = response.clone();
              const b = response.clone();

              const read = async body => (await body.getReader().read()).value;
              const one = await read(response.body);
              const two = await read(a.body);
              const three = await read(b.body);

              const distinct = new Set([one.buffer, two.buffer, three.buffer]).size;
              return distinct + ':' + [one, two, three].map(bytesOf).join('|');
            })()
            """).UnwrapIfPromise().AsString().Should().Be("3:" + string.Join("|", Enumerable.Repeat(string.Join("-", Enumerable.Range(1, 16)), 3)));
    }
}
#endif
