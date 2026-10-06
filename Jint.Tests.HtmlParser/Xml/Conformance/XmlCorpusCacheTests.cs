#nullable enable
using System.Net;

namespace Jint.Tests.HtmlParser.Xml.Conformance;

public class XmlCorpusCacheTests
{
    [Test]
    public async Task DeadlineCancelsStalledBodyAfterSuccessfulHeaders()
    {
        using var body = new StalledBody();
        using var handler = new CorpusHandler(body);
        using var client = new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan };

        // The short deadline is the behavior under test; the outer wait only prevents a broken
        // cancellation implementation from wedging the test host.
        var download = XmlCorpusCache.DownloadAsync(client, "https://corpus.invalid/archive", TimeSpan.FromMilliseconds(100));
        try
        {
            await body.ReadStarted.Task.WaitAsync(TimeSpan.FromSeconds(30));
            await Assert.CatchAsync<OperationCanceledException>(async () => await download.WaitAsync(TimeSpan.FromSeconds(30)));
            body.ReadToken.IsCancellationRequested.Should().BeTrue();
            body.Disposed.Should().BeTrue();
            handler.HeadersReturned.Should().BeTrue();
        }
        finally
        {
            body.Release.TrySetResult();
            try { await download; }
            catch (OperationCanceledException) { }
        }
    }

    [Test]
    public async Task DeadlineAlsoCancelsStalledHeaders()
    {
        using var handler = new CorpusHandler(null);
        using var client = new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan };
        var download = XmlCorpusCache.DownloadAsync(client, "https://corpus.invalid/archive", TimeSpan.FromMilliseconds(100));
        await Assert.CatchAsync<OperationCanceledException>(async () => await download.WaitAsync(TimeSpan.FromSeconds(30)));
        handler.RequestToken.IsCancellationRequested.Should().BeTrue();
        handler.HeadersReturned.Should().BeFalse();
    }

    [Test]
    public async Task CompletedBodyPreservesExactArchiveBytes()
    {
        byte[] bytes = [0, 1, 2, 13, 10, 255];
        using var handler = new CorpusHandler(new MemoryStream(bytes));
        using var client = new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan };
        var actual = await XmlCorpusCache.DownloadAsync(client, "https://corpus.invalid/archive", TimeSpan.FromSeconds(30));
        actual.Should().Equal(bytes);
    }

    [Test]
    public void CachedArchiveMustStillMatchItsDigest()
    {
        var directory = Path.Combine(Path.GetTempPath(), "jint-xml-cache-" + Guid.NewGuid().ToString("N"));
        var path = Path.Combine(directory, XmlCorpusCache.ArchiveFileName);
        byte[] bytes = [0, 1, 2, 255];
        var digest = XmlCorpusCache.Hash(bytes);
        try
        {
            XmlCorpusCache.TryReadVerified(path, digest).Should().BeNull();
            XmlCorpusCache.WriteAtomically(path, bytes);
            XmlCorpusCache.TryReadVerified(path, digest).Should().Equal(bytes);
            File.WriteAllBytes(path, [0, 1]);
            XmlCorpusCache.TryReadVerified(path, digest).Should().BeNull();
            XmlCorpusCache.WriteAtomically(path, bytes);
            XmlCorpusCache.TryReadVerified(path, digest).Should().Equal(bytes);
            System.IO.Directory.GetFiles(directory).Should().ContainSingle();
        }
        finally
        {
            if (System.IO.Directory.Exists(directory)) System.IO.Directory.Delete(directory, recursive: true);
        }
    }

    private sealed class CorpusHandler(Stream? body) : HttpMessageHandler
    {
        internal CancellationToken RequestToken { get; private set; }
        internal bool HeadersReturned { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestToken = cancellationToken;
            if (body is null) await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            HeadersReturned = true;
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(body!) };
        }
    }

    private sealed class StalledBody : MemoryStream
    {
        internal TaskCompletionSource ReadStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal CancellationToken ReadToken { get; private set; }
        internal bool Disposed { get; private set; }

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            ReadToken = cancellationToken;
            ReadStarted.TrySetResult();
            await Release.Task.WaitAsync(cancellationToken);
            return 0;
        }

        public override Task CopyToAsync(Stream destination, int bufferSize, CancellationToken cancellationToken)
            => CopyBodyAsync(destination, cancellationToken);

        private async Task CopyBodyAsync(Stream destination, CancellationToken cancellationToken)
        {
            var buffer = new byte[16];
            while (await ReadAsync(buffer.AsMemory(), cancellationToken) is var count && count != 0)
                await destination.WriteAsync(buffer.AsMemory(0, count), cancellationToken);
        }

        protected override void Dispose(bool disposing)
        {
            Disposed = true;
            base.Dispose(disposing);
        }
    }
}
