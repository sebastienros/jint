#if NET8_0_OR_GREATER
#nullable enable
#pragma warning disable JINT0002 // the fetch observer is a preview surface; this suite is what pins its shape

using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Jint;
using Jint.WebApi.Fetch;

namespace Jint.Tests.PublicInterface;

/// <summary>
/// The one capability a response-stage pause needs that metadata cannot give it: reading the body before the
/// caller does, against an allowance the host owns, without the caller ever noticing.
/// </summary>
/// <remarks>
/// <para>
/// This project has no <c>InternalsVisibleTo</c>, so everything here is reachable by a third party — which is
/// the point: <c>Fetch.getResponseBody</c> is implemented on top of exactly this surface and nothing else.
/// </para>
/// <para>
/// <b>Every test asserts both halves.</b> What the observer got, and what the script got. A read that
/// succeeded, one the budget refused and one that failed must all leave the page receiving every original
/// byte exactly once, because a body read is a debugging capability and a page is not a debugger.
/// </para>
/// </remarks>
public class WebApiFetchResponseBodyTests
{
    private static readonly TimeSpan TransportSignalCeiling = TimeSpan.FromMinutes(2);

    // ---- harness ----

    /// <summary>An allowance with a ceiling, counting what it granted and what it refused.</summary>
    private sealed class Allowance : IFetchResponseBodyBudget
    {
        private readonly long _max;
        private readonly object _gate = new();
        private long _used;

        internal Allowance(long max) => _max = max;

        internal long Used
        {
            get
            {
                lock (_gate)
                {
                    return _used;
                }
            }
        }

        internal long Peak { get; private set; }

        internal int Grants { get; private set; }

        internal int Refusals { get; private set; }

        public bool TryReserve(int bytes, out IDisposable? lease)
        {
            lock (_gate)
            {
                if (bytes < 0 || _used + bytes > _max)
                {
                    Refusals++;
                    lease = null;
                    return false;
                }

                _used += bytes;
                Peak = Math.Max(Peak, _used);
                Grants++;
                lease = new Lease(this, bytes);
                return true;
            }
        }

        private void Release(int bytes)
        {
            lock (_gate)
            {
                _used -= bytes;
            }
        }

        private sealed class Lease(Allowance allowance, int bytes) : IDisposable
        {
            private int _disposed;

            public void Dispose()
            {
                if (Interlocked.Exchange(ref _disposed, 1) == 0)
                {
                    allowance.Release(bytes);
                }
            }
        }
    }

    /// <summary>
    /// A body that arrives in fixed-size pieces, optionally failing part-way and optionally telling the test
    /// how far it has got — which is how a cancellation is armed mid-read.
    /// </summary>
    private sealed class ScriptedStream : Stream
    {
        private readonly byte[] _bytes;
        private readonly int _chunk;
        private readonly int _failAt;
        private readonly Action<int>? _reached;
        private readonly bool _deferred;
        private int _position;

        internal ScriptedStream(byte[] bytes, int chunk = int.MaxValue, int failAt = -1, Action<int>? reached = null, bool deferred = false)
        {
            _bytes = bytes;
            _chunk = chunk;
            _failAt = failAt;
            _reached = reached;
            _deferred = deferred;
        }

        internal int Reads { get; private set; }

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));

        public override int Read(Span<byte> buffer)
        {
            Reads++;

            if (_failAt >= 0 && _position >= _failAt)
            {
                throw new IOException("the response ended prematurely");
            }

            var count = Math.Min(Math.Min(buffer.Length, _chunk), _bytes.Length - _position);
            if (count <= 0)
            {
                return 0;
            }

            _bytes.AsSpan(_position, count).CopyTo(buffer);
            _position += count;
            _reached?.Invoke(_position);
            return count;
        }

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            => ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (_deferred)
            {
                // Nothing completes synchronously, which is what lets a test start a second read while the
                // first is genuinely still in flight.
                await Task.Yield();
            }

            cancellationToken.ThrowIfCancellationRequested();
            return Read(buffer.Span);
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    /// <summary>An observer that reads the body a given number of times and then answers.</summary>
    private sealed class Reader : FetchObserver
    {
        internal IFetchResponseBodyBudget? Budget { get; init; }

        internal int Reads { get; init; } = 1;

        internal Func<FetchResponseInterceptionContext, FetchResponseInterception?>? Answer { get; init; }

        internal CancellationToken ReadToken { get; init; }

        internal List<byte[]?> Bodies { get; } = [];

        internal List<ReadOnlyMemory<byte>?> Memories { get; } = [];

        internal Exception? Failure { get; private set; }

        internal FetchResponseInterceptionContext? Context { get; private set; }

        public override async ValueTask<FetchResponseInterception?> OnInterceptedResponseAsync(
            FetchResponseInterceptionContext context,
            CancellationToken cancellationToken)
        {
            Context = context;

            if (Budget is { } budget)
            {
                var token = ReadToken.CanBeCanceled ? ReadToken : cancellationToken;

                for (var i = 0; i < Reads; i++)
                {
                    try
                    {
                        var body = await context.TryReadBodyAsync(budget, token).ConfigureAwait(false);
                        Memories.Add(body);
                        Bodies.Add(body?.ToArray());
                    }
#pragma warning disable CA1031 // the test is the thing that decides what a read failure means
                    catch (Exception exception)
#pragma warning restore CA1031
                    {
                        Failure ??= exception;
                        break;
                    }
                }
            }

            return Answer?.Invoke(context);
        }
    }

    /// <summary>An observer that overrides only the metadata callback, i.e. every observer written so far.</summary>
    private sealed class MetadataOnly : FetchObserver
    {
        internal int Asks { get; private set; }

        internal Func<ObservedFetchResponse, FetchResponseInterception?>? Answer { get; init; }

        public override ValueTask<FetchResponseInterception?> OnResponseAsync(
            ObservedFetchResponse response,
            CancellationToken cancellationToken)
        {
            Asks++;
            return new ValueTask<FetchResponseInterception?>(Answer?.Invoke(response));
        }
    }

    private sealed class StubHandler(Func<HttpResponseMessage> responder) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(responder());
    }

    private static HttpResponseMessage Body(
        byte[] bytes,
        int chunk = int.MaxValue,
        long? contentLength = null,
        int failAt = -1,
        Action<int>? reached = null,
        bool deferred = false)
    {
        var content = new StreamContent(new ScriptedStream(bytes, chunk, failAt, reached, deferred));
        content.Headers.TryAddWithoutValidation("content-type", "application/octet-stream");

        if (contentLength is { } length)
        {
            content.Headers.TryAddWithoutValidation("content-length", length.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }

        return new HttpResponseMessage(HttpStatusCode.OK) { Content = content };
    }

    private static Engine WebEngine(HttpMessageHandler handler, FetchObserver? observer)
        => new(options => options.UseWebApis(WebApiFeatures.Timers).UseFetch(fetch =>
        {
            fetch.HttpClient = new HttpClient(handler);
            fetch.Observer = observer;
        }));

    /// <summary>Runs one fetch and answers what the script saw of the body, as a latin-1 string of bytes.</summary>
    private static string FetchBytes(HttpResponseMessage response, FetchObserver? observer, string expression = "r.arrayBuffer()")
    {
        using var handler = new StubHandler(() => response);
        var engine = WebEngine(handler, observer);

        return engine.Evaluate($$"""
            fetch('https://example.org/a')
                .then(r => {{expression}})
                .then(b => Array.from(new Uint8Array(b)).join(','), e => 'rejected:' + e.constructor.name)
            """)
            .UnwrapIfPromise(TransportSignalCeiling)
            .AsString();
    }

    private static string Joined(byte[] bytes) => string.Join(",", bytes);

    private static byte[] Pattern(int length)
    {
        var bytes = new byte[length];
        for (var i = 0; i < length; i++)
        {
            bytes[i] = (byte) ((i * 7) % 251);
        }

        return bytes;
    }

    // ---- reading ----

    [Test]
    public void AnEmptyBodyIsReadAsANonNullEmptyValue()
    {
        var allowance = new Allowance(1024);
        var observer = new Reader { Budget = allowance };

        FetchBytes(Body([]), observer).Should().BeEmpty();

        observer.Bodies.Should().ContainSingle();
        observer.Bodies[0].Should().NotBeNull().And.BeEmpty();
        observer.Failure.Should().BeNull();
        allowance.Used.Should().Be(0, "every lease is released once the response has been delivered");
    }

    [Test]
    public void ATextBodyIsReadWholeAndThePageStillReceivesIt()
    {
        var bytes = Encoding.UTF8.GetBytes("héllo — wörld");
        var allowance = new Allowance(1024);
        var observer = new Reader { Budget = allowance };

        FetchBytes(Body(bytes), observer).Should().Be(Joined(bytes));

        observer.Bodies[0].Should().Equal(bytes);
        allowance.Used.Should().Be(0);
    }

    [Test]
    public void ABinaryBodyIsReadByteForByte()
    {
        // Every byte value, including the ones a charset would mangle: the read is bytes and stays bytes.
        var bytes = new byte[256];
        for (var i = 0; i < bytes.Length; i++)
        {
            bytes[i] = (byte) i;
        }

        var observer = new Reader { Budget = new Allowance(4096) };

        FetchBytes(Body(bytes, chunk: 17), observer).Should().Be(Joined(bytes));
        observer.Bodies[0].Should().Equal(bytes);
    }

    [Test]
    public void AnUnknownLengthChunkedBodyIsReadWhole()
    {
        var bytes = Pattern(20_000);
        var response = Body(bytes, chunk: 1_000);
        response.Content.Headers.ContentLength.Should().BeNull("the test body must be unknown-length");

        var allowance = new Allowance(1 << 20);
        var observer = new Reader { Budget = allowance };

        FetchBytes(response, observer).Should().Be(Joined(bytes));
        observer.Bodies[0].Should().Equal(bytes);
        allowance.Used.Should().Be(0);
    }

    [Test]
    public void ARepeatedReadAnswersTheSameMemoryWithoutTouchingTheSocketAgain()
    {
        var bytes = Pattern(64);
        var observer = new Reader { Budget = new Allowance(4096), Reads = 3 };

        FetchBytes(Body(bytes), observer).Should().Be(Joined(bytes));

        observer.Bodies.Should().HaveCount(3);
        observer.Bodies[1].Should().Equal(bytes);
        observer.Bodies[2].Should().Equal(bytes);
        observer.Memories[0]!.Value.Equals(observer.Memories[1]!.Value).Should().BeTrue("the body is immutable and reused");
        observer.Memories[1]!.Value.Equals(observer.Memories[2]!.Value).Should().BeTrue();
    }

    // ---- the allowance ----

    [Test]
    public void ABodyExactlyAtTheAllowanceIsRead()
    {
        // The case an unknown-length body cannot answer without one byte of lookahead: the buffer is full and
        // the allowance is spent, and the body has nevertheless ended.
        var bytes = Pattern(24);
        var allowance = new Allowance(24);
        var observer = new Reader { Budget = allowance };

        FetchBytes(Body(bytes, chunk: 5), observer).Should().Be(Joined(bytes));

        observer.Bodies[0].Should().Equal(bytes);
        observer.Failure.Should().BeNull();
        allowance.Peak.Should().Be(24);
        allowance.Used.Should().Be(0);
    }

    [Test]
    public void ABodyOneByteOverTheAllowanceIsRefusedAndThePageStillReceivesEveryByte()
    {
        var bytes = Pattern(25);
        var allowance = new Allowance(24);
        var observer = new Reader { Budget = allowance };

        FetchBytes(Body(bytes, chunk: 5), observer).Should().Be(Joined(bytes), "a refusal must be invisible to the page");

        observer.Bodies.Should().ContainSingle();
        observer.Bodies[0].Should().BeNull("null is what a refusal answers");
        observer.Failure.Should().BeNull();
        allowance.Refusals.Should().BeGreaterThan(0);
        allowance.Peak.Should().BeLessThanOrEqualTo(24);
        allowance.Used.Should().Be(0);
    }

    [Test]
    public void ARefusalIsStickyForThatResponse()
    {
        var bytes = Pattern(25);
        var allowance = new Allowance(24);
        var observer = new Reader { Budget = allowance, Reads = 3 };

        FetchBytes(Body(bytes, chunk: 5), observer).Should().Be(Joined(bytes));

        observer.Bodies.Should().HaveCount(3);
        observer.Bodies.Should().AllSatisfy(body => body.Should().BeNull());

        // The later reads answered from the refusal, not from the socket: nothing more was ever asked for.
        allowance.Peak.Should().BeLessThanOrEqualTo(24);
    }

    [Test]
    public void AZeroAllowanceRefusesEveryBodyButTheEmptyOne()
    {
        var allowance = new Allowance(0);

        var refused = new Reader { Budget = allowance };
        FetchBytes(Body("x"u8.ToArray()), refused).Should().Be("120");
        refused.Bodies[0].Should().BeNull();

        var empty = new Reader { Budget = allowance };
        FetchBytes(Body([]), empty).Should().BeEmpty();
        empty.Bodies[0].Should().NotBeNull().And.BeEmpty("an empty body retains nothing, so nothing was refused");
    }

    [Test]
    public void AnAllowanceAlreadySpentElsewhereRefusesTheReadAndIsGivenBackAfterwards()
    {
        // One ledger, many holders: what a sibling capture or a still-unfinished pause is holding is exactly
        // what this read does not get, and it waits for none of them.
        var bytes = Pattern(4_000);
        var allowance = new Allowance(8_192);

        allowance.TryReserve(8_000, out var held).Should().BeTrue();

        var refused = new Reader { Budget = allowance };
        FetchBytes(Body(bytes, chunk: 500), refused).Should().Be(Joined(bytes));
        refused.Bodies[0].Should().BeNull("the allowance was spent by somebody else");

        held!.Dispose();
        allowance.Used.Should().Be(0);

        var granted = new Reader { Budget = allowance };
        FetchBytes(Body(bytes, chunk: 500), granted).Should().Be(Joined(bytes));
        granted.Bodies[0].Should().Equal(bytes, "the same ledger admits the same body once the holder let go");
        allowance.Used.Should().Be(0);
    }

    // ---- a body that lies, fails or is cancelled ----

    [Test]
    public void AKnownLengthBodyChargesAboutItsOwnSizeAndNotAWholeStep()
    {
        // The declared length may only make the first reservation smaller. A small body charging a whole
        // growth step of a shared allowance is a sibling refused for no reason.
        var bytes = Pattern(40);
        var allowance = new Allowance(1 << 20);
        var observer = new Reader { Budget = allowance };

        using var handler = new StubHandler(() =>
        {
            var response = Body(bytes);
            response.Content.Headers.ContentLength = bytes.Length;
            return response;
        });

        var engine = WebEngine(handler, observer);

        engine.Evaluate("fetch('https://example.org/a').then(r => r.arrayBuffer()).then(b => b.byteLength)")
            .UnwrapIfPromise(TransportSignalCeiling)
            .AsNumber().Should().Be(40);

        observer.Bodies[0].Should().Equal(bytes);
        allowance.Peak.Should().Be(41, "the declared length plus the one byte that finds its end");
        allowance.Used.Should().Be(0);
    }

    [TestCase(1_000L)]
    [TestCase(3L)]
    public void ADeceptiveContentLengthChangesNeitherWhatIsReadNorWhatIsSpent(long declared)
    {
        // Content-Length is never trusted for sizing: the reservation follows the bytes that actually land.
        var bytes = Pattern(40);
        var allowance = new Allowance(4_096);
        var observer = new Reader { Budget = allowance };

        FetchBytes(Body(bytes, chunk: 9, contentLength: declared), observer).Should().Be(Joined(bytes));

        observer.Bodies[0].Should().Equal(bytes);
        allowance.Peak.Should().BeLessThanOrEqualTo(8 * 1024 + 1, "the growth is the reader's, not the header's");
        allowance.Used.Should().Be(0);
    }

    [Test]
    public void AReadThatFailsIsThrownRatherThanBecomingAShortBody()
    {
        var bytes = Pattern(4_000);
        var allowance = new Allowance(1 << 20);
        var observer = new Reader
        {
            Budget = allowance,

            // Continue after the failure: the page then meets the same broken stream, and must be told so
            // rather than handed the prefix as though it were the response.
            Answer = _ => null,
        };

        FetchBytes(Body(bytes, chunk: 500, failAt: 1_500), observer)
            .Should().Be("rejected:TypeError", "a failed transfer is a failed transfer on both sides");

        observer.Failure.Should().BeOfType<IOException>();
        observer.Bodies.Should().BeEmpty();
        allowance.Used.Should().Be(0);
    }

    [Test]
    public void ACancelledReadIsThrownAndThePageStillReceivesEveryByte()
    {
        var bytes = Pattern(4_000);
        var allowance = new Allowance(1 << 20);
        using var cancellation = new CancellationTokenSource();

        var observer = new Reader { Budget = allowance, ReadToken = cancellation.Token };

        // Cancel once the read is under way, so a prefix has been taken and has to be given back.
        var response = Body(bytes, chunk: 500, reached: position =>
        {
            if (position >= 1_000)
            {
                cancellation.Cancel();
            }
        });

        FetchBytes(response, observer).Should().Be(Joined(bytes), "the prefix is replayed ahead of the rest");

        observer.Failure.Should().BeAssignableTo<OperationCanceledException>();
        allowance.Used.Should().Be(0);
    }

    // ---- what the read does not change ----

    [TestCase(false)]
    [TestCase(true)]
    public void AResponseNobodyReadsIsNotTouchedBeforeItsConsumerAsks(bool read)
    {
        // "Still streaming" stated as something a third party can see: with the headers in hand and nobody
        // having asked for the body, not one byte has come off the socket unless the observer read it.
        var bytes = Pattern(8_000);
        var stream = new ScriptedStream(bytes, chunk: 500);
        var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(stream) };

        var observer = new Reader { Budget = read ? new Allowance(1 << 20) : null };

        using var handler = new StubHandler(() => response);
        var engine = WebEngine(handler, observer);

        engine.Evaluate("globalThis.held = null; fetch('https://example.org/a').then(r => { globalThis.held = r; return r.status; })")
            .UnwrapIfPromise(TransportSignalCeiling)
            .AsNumber().Should().Be(200);

        observer.Context.Should().NotBeNull("the context is handed over whether or not it is used");
        (stream.Reads == 0).Should().Be(!read, "only a body read takes bytes before the consumer asks for them");

        engine.Evaluate("held.arrayBuffer().then(b => b.byteLength)")
            .UnwrapIfPromise(TransportSignalCeiling)
            .AsNumber().Should().Be(8_000, "the consumer still receives the whole body");
    }

    [Test]
    public void AnObserverThatOnlyOverridesOnResponseAsyncIsUnaffected()
    {
        var bytes = Pattern(64);
        var observer = new MetadataOnly();

        FetchBytes(Body(bytes), observer).Should().Be(Joined(bytes));
        observer.Asks.Should().Be(1, "the default forwards, so the existing override is still the one asked");
    }

    [Test]
    public void AReadIsAvailableOnTheXmlHttpRequestLaneToo()
    {
        // The ask lives in the one place every lane passes through, so the lane that does not take fetch()'s
        // buffered path reads exactly the same way.
        var bytes = Encoding.UTF8.GetBytes("xhr body");
        var allowance = new Allowance(1024);
        var observer = new Reader { Budget = allowance };

        using var handler = new StubHandler(() => Body(bytes));
        var engine = new Engine(options => options
            .UseWebApis(WebApiFeatures.Timers | WebApiFeatures.XmlHttpRequest)
            .UseFetch(fetch =>
            {
                fetch.HttpClient = new HttpClient(handler);
                fetch.Observer = observer;
            }));

        engine.Evaluate("""
            (() => {
                const x = new XMLHttpRequest();
                x.open('GET', 'https://example.org/a', false);
                x.send();
                return x.responseText;
            })()
            """)
            .AsString().Should().Be("xhr body");

        observer.Bodies[0].Should().Equal(bytes);
        allowance.Used.Should().Be(0);
    }

    [TestCase("fulfill")]
    [TestCase("fail")]
    public void AResponseNobodyWillReceiveReleasesWhatWasRead(string decision)
    {
        var bytes = Pattern(4_000);
        var allowance = new Allowance(1 << 20);
        var observer = new Reader
        {
            Budget = allowance,
            Answer = _ => decision == "fulfill"
                ? FetchResponseInterception.Fulfill(201, body: "substitute"u8.ToArray())
                : FetchResponseInterception.Fail("no"),
        };

        var seen = FetchBytes(Body(bytes, chunk: 500), observer);

        seen.Should().Be(decision == "fulfill" ? Joined("substitute"u8.ToArray()) : "rejected:TypeError");
        observer.Bodies[0].Should().Equal(bytes);
        allowance.Used.Should().Be(0, "the bytes were discarded rather than replayed");
    }

    [Test]
    public void AContinuedResponseCarriesTheRewrittenHeadersOverTheReplay()
    {
        var bytes = Encoding.UTF8.GetBytes("continued");
        var observer = new Reader
        {
            Budget = new Allowance(1024),
            Answer = _ => FetchResponseInterception.Continue(
                status: 203,
                headers: [new FetchHeader("content-type", "text/plain; charset=utf-8"), new FetchHeader("x-mark", "1")]),
        };

        using var handler = new StubHandler(() => Body(bytes));
        var engine = WebEngine(handler, observer);

        engine.Evaluate("""
            fetch('https://example.org/a')
                .then(r => r.text().then(t => [r.status, r.headers.get('content-type'), r.headers.get('x-mark'), t].join('|')))
            """)
            .UnwrapIfPromise(TransportSignalCeiling)
            .AsString().Should().Be("203|text/plain; charset=utf-8|1|continued");
    }

    // ---- the shape of the capability ----

    [Test]
    public void TheContextIsSealedOnceTheCallbackHasReturned()
    {
        var observer = new Reader { Budget = new Allowance(1024) };

        FetchBytes(Body("abc"u8.ToArray()), observer).Should().Be("97,98,99");

        var context = observer.Context;
        context.Should().NotBeNull();

        var caught = Caught.Exception(() => context!.TryReadBodyAsync(new Allowance(1024), CancellationToken.None).AsTask().GetAwaiter().GetResult());
        caught.Should().BeOfType<InvalidOperationException>();
    }

    [Test]
    public void ASecondReadInFlightAtOnceIsRefused()
    {
        var observer = new Concurrent();

        FetchBytes(Body(Pattern(2_000), chunk: 100, deferred: true), observer).Should().Be(Joined(Pattern(2_000)));

        observer.Second.Should().BeOfType<InvalidOperationException>();
    }

    /// <summary>Starts a second read without awaiting the first, which the per-response state must refuse.</summary>
    private sealed class Concurrent : FetchObserver
    {
        internal Exception? Second { get; private set; }

        public override async ValueTask<FetchResponseInterception?> OnInterceptedResponseAsync(
            FetchResponseInterceptionContext context,
            CancellationToken cancellationToken)
        {
            var allowance = new Allowance(1 << 20);
            var first = context.TryReadBodyAsync(allowance, cancellationToken);

            try
            {
                await context.TryReadBodyAsync(allowance, cancellationToken).ConfigureAwait(false);
            }
#pragma warning disable CA1031 // the test is what decides what a concurrent read means
            catch (Exception exception)
#pragma warning restore CA1031
            {
                Second = exception;
            }

            await first.ConfigureAwait(false);
            return null;
        }
    }

    [Test]
    public void TheBudgetIsRequired()
    {
        var observer = new NullBudget();

        FetchBytes(Body("abc"u8.ToArray()), observer).Should().Be("97,98,99");
        observer.Caught.Should().BeOfType<ArgumentNullException>();
    }

    private sealed class NullBudget : FetchObserver
    {
        internal Exception? Caught { get; private set; }

        public override async ValueTask<FetchResponseInterception?> OnInterceptedResponseAsync(
            FetchResponseInterceptionContext context,
            CancellationToken cancellationToken)
        {
            try
            {
                await context.TryReadBodyAsync(null!, cancellationToken).ConfigureAwait(false);
            }
#pragma warning disable CA1031 // the test is what decides what a missing budget means
            catch (Exception exception)
#pragma warning restore CA1031
            {
                Caught = exception;
            }

            return null;
        }
    }
}
#endif
