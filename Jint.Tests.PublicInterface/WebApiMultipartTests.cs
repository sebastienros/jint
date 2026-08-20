#if NET8_0_OR_GREATER
#nullable enable

using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Jint;
using Jint.Runtime;

namespace Jint.Tests.PublicInterface;

/// <summary>
/// A <c>FormData</c> body seen from the wire: what a server actually receives when a script posts one, and
/// what a script reads back when a server answers with one.
/// </summary>
/// <remarks>
/// The stub goes in through <c>Options.WebApi.Fetch.HttpClient</c>, which is the door a host uses for its own
/// <c>HttpClient</c>; nothing here reaches into the assembly. The request payload is asserted <b>byte for
/// byte</b> against a hand-written expectation, because a serializer only its own parser agrees with would
/// pass a round trip and still be unreadable to every server on the internet.
/// </remarks>
public class WebApiMultipartTests
{
    private const string TypePrefix = "multipart/form-data; boundary=";

    /// <summary>A handler that answers immediately and keeps what it was sent.</summary>
    private sealed class CapturingHandler : HttpMessageHandler
    {
        internal List<string> ContentTypes { get; } = new();

        internal List<byte[]> Bodies { get; } = new();

        internal Func<HttpResponseMessage> Responder { get; init; } =
            static () => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("ok") };

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var content = request.Content;
            if (content is not null)
            {
                Bodies.Add(await content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false));
                ContentTypes.Add(content.Headers.TryGetValues("Content-Type", out var values) ? string.Join(", ", values) : string.Empty);
            }

            return Responder();
        }
    }

    private static Engine WebEngine(HttpMessageHandler handler)
        => new(options => options.UseFetch(fetch => fetch.HttpClient = new HttpClient(handler)));

    /// <summary>One character per byte, so an expectation can be written as text and compared exactly.</summary>
    private static string Bytes(string text) => Encoding.Latin1.GetString(Encoding.UTF8.GetBytes(text));

    private static string BoundaryOf(string contentType)
    {
        contentType.Should().StartWith(TypePrefix);
        return contentType.Substring(TypePrefix.Length);
    }

    [Fact]
    public void PostsAFormDataBodyAServerCanRead()
    {
        var handler = new CapturingHandler();
        var engine = WebEngine(handler);

        engine.Evaluate(@"
            const fd = new FormData();
            fd.append('field', 'value');
            fd.append('quoted""name', 'and\r\na value');
            fd.append('upload', new File(['id,name\r\n1,a'], 'report.csv', { type: 'text/csv' }));
            fetch('https://api.example.org/upload', { method: 'POST', body: fd }).then(r => r.status)")
            .UnwrapIfPromise().AsNumber().Should().Be(200);

        handler.Bodies.Should().ContainSingle();
        var boundary = BoundaryOf(handler.ContentTypes[0]);

        Encoding.Latin1.GetString(handler.Bodies[0]).Should().Be(Bytes(
            $"--{boundary}\r\n"
            + "Content-Disposition: form-data; name=\"field\"\r\n"
            + "\r\n"
            + "value\r\n"
            + $"--{boundary}\r\n"
            + "Content-Disposition: form-data; name=\"quoted%22name\"\r\n"
            + "\r\n"
            + "and\r\na value\r\n"
            + $"--{boundary}\r\n"
            + "Content-Disposition: form-data; name=\"upload\"; filename=\"report.csv\"\r\n"
            + "Content-Type: text/csv\r\n"
            + "\r\n"
            + "id,name\r\n1,a\r\n"
            + $"--{boundary}--\r\n"));
    }

    [Fact]
    public void GivesEveryRequestItsOwnBoundary()
    {
        var handler = new CapturingHandler();
        var engine = WebEngine(handler);

        engine.Execute("function post() { return fetch('https://api.example.org/', { method: 'POST', body: new FormData() }).then(r => r.status); }");
        engine.Evaluate("post()").UnwrapIfPromise().AsNumber().Should().Be(200);
        engine.Evaluate("post()").UnwrapIfPromise().AsNumber().Should().Be(200);

        var first = BoundaryOf(handler.ContentTypes[0]);
        var second = BoundaryOf(handler.ContentTypes[1]);

        // Unguessable is the only way a writer that never inspects the payload can honour "the boundary
        // delimiter MUST NOT appear inside any of the encapsulated parts".
        first.Should().NotBe(second);

        // Each body is framed by its own boundary and nothing else.
        Encoding.Latin1.GetString(handler.Bodies[0]).Should().Be($"--{first}--\r\n");
        Encoding.Latin1.GetString(handler.Bodies[1]).Should().Be($"--{second}--\r\n");
    }

    [Fact]
    public void ReadsAMultipartResponseBackAsFormData()
    {
        var handler = new CapturingHandler
        {
            Responder = static () =>
            {
                var content = new StringContent(
                    "--sep\r\n"
                    + "Content-Disposition: form-data; name=\"status\"\r\n"
                    + "\r\n"
                    + "done\r\n"
                    + "--sep\r\n"
                    + "Content-Disposition: form-data; name=\"receipt\"; filename=\"r.txt\"\r\n"
                    + "Content-Type: text/plain\r\n"
                    + "\r\n"
                    + "thanks\r\n"
                    + "--sep--\r\n");

                content.Headers.ContentType = new MediaTypeHeaderValue("multipart/form-data")
                {
                    Parameters = { new NameValueHeaderValue("boundary", "sep") },
                };

                return new HttpResponseMessage(HttpStatusCode.OK) { Content = content };
            },
        };

        var engine = WebEngine(handler);

        engine.Evaluate(@"fetch('https://api.example.org/')
                .then(r => r.formData())
                .then(fd => fd.get('status') + '|' + fd.get('receipt').name + '|' + fd.get('receipt').type)")
            .UnwrapIfPromise().AsString().Should().Be("done|r.txt|text/plain");
    }

    [Fact]
    public void RejectsAMalformedMultipartResponseWithATypeError()
    {
        var handler = new CapturingHandler
        {
            Responder = static () =>
            {
                var content = new StringContent("this is not a multipart body");
                content.Headers.ContentType = new MediaTypeHeaderValue("multipart/form-data")
                {
                    Parameters = { new NameValueHeaderValue("boundary", "sep") },
                };

                return new HttpResponseMessage(HttpStatusCode.OK) { Content = content };
            },
        };

        var engine = WebEngine(handler);

        engine.Evaluate(@"fetch('https://api.example.org/')
                .then(r => r.formData())
                .then(() => 'resolved', e => e.constructor.name)")
            .UnwrapIfPromise().AsString().Should().Be("TypeError");
    }
}
#endif
