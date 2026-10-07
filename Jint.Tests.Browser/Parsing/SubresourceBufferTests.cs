using System.Net.Http;
using Jint.Browser.Runtime;

namespace Jint.Tests.Browser.Parsing;

public class SubresourceBufferTests
{
    [TestCase(0, 0)]
    [TestCase(17, -1)]
    [TestCase(17, 0)]
    [TestCase(17, 1)]
    [TestCase(17, 100)]
    [TestCase(65_536, 65_536)]
    [TestCase(65_536, -1)]
    [TestCase(70_001, 70_001)]
    [TestCase(70_001, 1)]
    public async Task ReadsThroughEofAndReturnsOnlyTheActualBody(int length, int declared)
    {
        var bytes = Enumerable.Range(0, length).Select(i => (byte) (i % 251)).ToArray();
        using var response = Response(bytes, declared);
        var body = await SubresourceFetch.ReadBoundedAsync(response, Math.Max(length, declared), CancellationToken.None);
        body.Should().Equal(bytes);

        // Later reads reuse the scratch pool but must not change the returned, independently owned body.
        using var second = Response(new byte[65_536], 65_536);
        await SubresourceFetch.ReadBoundedAsync(second, 65_536, CancellationToken.None);
        body.Should().Equal(bytes);
    }

    [TestCase(-1)]
    [TestCase(0)]
    [TestCase(1)]
    [TestCase(33)]
    public async Task RejectsBodiesOverTheCapEvenWhenTheLengthIsMissingOrUnderstated(int declared)
    {
        using var response = Response(new byte[33], declared);
        var exception = await Caught.ExceptionAsync(() => SubresourceFetch.ReadBoundedAsync(response, 32, CancellationToken.None));
        exception.Should().BeOfType<SubresourceFetchException>();
    }

    [Test]
    public async Task CancellationStopsTheBodyRead()
    {
        using var response = Response(new byte[65_536], -1);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var exception = await Caught.ExceptionAsync(() => SubresourceFetch.ReadBoundedAsync(response, 65_536, cancellation.Token));
        exception.Should().BeAssignableTo<OperationCanceledException>();
    }

    private static HttpResponseMessage Response(byte[] bytes, int declared)
    {
        var response = new HttpResponseMessage { Content = new StreamContent(new ChunkedBody(bytes)) };
        response.Content.Headers.ContentLength = declared >= 0 ? declared : null;
        return response;
    }

    private sealed class ChunkedBody(byte[] bytes) : MemoryStream(bytes, writable: false)
    {
        public override bool CanSeek => false;
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
            => base.ReadAsync(buffer[..Math.Min(buffer.Length, 997)], cancellationToken);
    }
}
