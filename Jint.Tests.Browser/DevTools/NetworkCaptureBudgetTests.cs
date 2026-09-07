using Jint.Browser.Runtime;
using Jint.WebApi.Fetch;

namespace Jint.Tests.Browser.DevTools;

#pragma warning disable JINT0002 // Exercise the same observer callbacks as the page's transport.

public sealed class NetworkCaptureBudgetTests
{
    [Test]
    public async Task InProgressCapturesEvictTheOldestBeforeEitherCompletes()
    {
        var recorder = Recorder();
        await Start(recorder, 1);
        recorder.OnData(new(1), "aaaaaaaa"u8);
        await Start(recorder, 2);
        recorder.OnData(new(2), "bbbbbbbb"u8);

        recorder.OnCompleted(new(1), 8);
        recorder.Body("1").Should().BeNull("both unfinished captures already exceeded the page's allowance");
        recorder.OnCompleted(new(2), 8);
        recorder.Body("2")!.Value.Bytes.ToArray().Should().Equal("bbbbbbbb"u8.ToArray());
    }

    [Test]
    public async Task ANewChunkEvictsCompletedBodiesBeforeItCompletes()
    {
        var recorder = Recorder();
        await Start(recorder, 1);
        recorder.OnData(new(1), "aaaaaaaa"u8);
        recorder.OnCompleted(new(1), 8);
        await Start(recorder, 2);
        recorder.OnData(new(2), "bbbbbbbb"u8);

        recorder.Body("1").Should().BeNull();
        recorder.OnCompleted(new(2), 8);
        recorder.Body("2")!.Value.Bytes.ToArray().Should().Equal("bbbbbbbb"u8.ToArray());
    }

    [Test]
    public async Task EvictedCapturesNeverRestartFromALaterChunk()
    {
        var recorder = Recorder();
        await Start(recorder, 1);
        recorder.OnData(new(1), "aaaaaaaa"u8);
        await Start(recorder, 2);
        recorder.OnData(new(2), "bbbbbbbb"u8);
        recorder.OnData(new(1), "aaaa"u8);
        recorder.OnCompleted(new(1), 12);
        recorder.OnData(new(2), "bb"u8);
        recorder.OnCompleted(new(2), 10);

        recorder.Body("1").Should().BeNull();
        recorder.Body("2")!.Value.Bytes.ToArray().Should().Equal("bbbbbbbbbb"u8.ToArray());
    }

    [Test]
    public async Task DisablingCaptureReleasesBytesAndNeverCapturesAnOldResponsesSuffix()
    {
        var recorder = Recorder();
        await Start(recorder, 1);
        recorder.OnData(new(1), "aaaaaaaa"u8);
        recorder.CaptureBodies = false;
        recorder.CaptureBodies = true;
        recorder.OnData(new(1), "aa"u8);
        recorder.OnCompleted(new(1), 10);
        recorder.Body("1").Should().BeNull();

        await Start(recorder, 2);
        recorder.OnData(new(2), "bbbbbbbbbb"u8);
        recorder.OnCompleted(new(2), 10);
        recorder.Body("2")!.Value.Bytes.ToArray().Should().Equal("bbbbbbbbbb"u8.ToArray());
    }

    [Test]
    public async Task FailedResponsesDoNotPublishTheirPrefixAsACompleteBody()
    {
        var recorder = Recorder();
        await Start(recorder, 1);
        recorder.OnData(new(1), "aaaaaaaa"u8);
        recorder.OnFailed(new(1), "connection reset", null);
        recorder.Body("1").Should().BeNull();

        await Start(recorder, 2);
        recorder.OnData(new(2), "bbbbbbbbbb"u8);
        recorder.OnCompleted(new(2), 10);
        recorder.Body("2")!.Value.Bytes.ToArray().Should().Equal("bbbbbbbbbb"u8.ToArray());
    }

    [Test]
    public async Task EnablingCaptureMidResponseDoesNotPublishItsSuffix()
    {
        var recorder = Recorder();
        recorder.CaptureBodies = false;
        await Start(recorder, 1);
        recorder.OnData(new(1), "aaaaaaaa"u8);
        recorder.CaptureBodies = true;
        recorder.OnData(new(1), "aa"u8);
        recorder.OnCompleted(new(1), 10);
        recorder.Body("1").Should().BeNull();
    }

    [Test]
    public async Task TheOldestCaptureCanEvictItselfWhenItGrows()
    {
        var recorder = Recorder();
        await Start(recorder, 1);
        recorder.OnData(new(1), "aaaaaa"u8);
        await Start(recorder, 2);
        recorder.OnData(new(2), "bbbb"u8);
        recorder.OnData(new(1), "a"u8);
        recorder.OnCompleted(new(1), 7);
        recorder.OnCompleted(new(2), 4);

        recorder.Body("1").Should().BeNull();
        recorder.Body("2")!.Value.Bytes.ToArray().Should().Equal("bbbb"u8.ToArray());
    }

    [TestCase(0)]
    [TestCase(7)]
    public async Task AnOversizedResponseIsNotKept(long cap)
    {
        var recorder = Recorder(cap);
        await Start(recorder, 1);
        recorder.OnData(new(1), "aaaaaaaa"u8);
        recorder.OnCompleted(new(1), 8);
        recorder.Body("1").Should().BeNull();
    }

    private static PageNetworkRecorder Recorder(long cap = 10)
        => new(100, cap, static () => "loader", static () => "https://example.test/") { CaptureBodies = true };

    private static async Task Start(PageNetworkRecorder recorder, long id)
    {
        await recorder.OnRequestAsync(new ObservedFetchRequest
        {
            Id = new(id),
            Initiator = FetchInitiator.Script,
            Url = new Uri("https://example.test/body"),
            Method = "GET",
            Headers = [],
        }, default);
    }
}

#pragma warning restore JINT0002
