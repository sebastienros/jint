#if NET8_0_OR_GREATER
using Jint;
using Jint.WebApi;

namespace Jint.Tests.PublicInterface;

/// <summary>
/// Blob URLs seen from outside the assembly: what a host has to enable, and that fetching one needs no
/// network at all.
/// </summary>
/// <remarks>
/// The engines here have <c>WebApiFeatures.Fetch</c> and no <c>HttpClient</c>, which is a host that granted
/// the interface and nothing to reach with it. A <c>blob:</c> URL is answered anyway, because it names bytes
/// the script itself created and scheme fetch never gets as far as a transport.
/// </remarks>
public class WebApiBlobUrlTests
{
    private static Engine BlobUrlEngine()
        => new(options => options.UseWebApis(WebApiFeatures.Default | WebApiFeatures.Fetch));

    private static void Pump(Engine engine)
    {
        for (var i = 0; i < 8; i++)
        {
            engine.Tasks.ProcessTasks();
        }
    }

    [Test]
    public void ADefaultEngineHasNoBlobUrls()
    {
        new Engine().Evaluate("typeof URL").AsString().Should().Be("undefined");

        // The two statics need both halves of the File API's URL extension, so an engine with only the URL
        // standard has the interface and not them.
        new Engine(options => options.UseWebApis(WebApiFeatures.Url))
            .Evaluate("typeof URL.createObjectURL").AsString().Should().Be("undefined");
    }

    [Test]
    public void TheDefaultFeatureSetHasThem()
    {
        var engine = new Engine(options => options.UseWebApis());

        engine.Evaluate("typeof URL.createObjectURL").AsString().Should().Be("function");
        engine.Evaluate("typeof URL.revokeObjectURL").AsString().Should().Be("function");
        engine.Evaluate("URL.createObjectURL(new Blob(['x'])).startsWith('blob:null/')").AsBoolean()
            .Should().BeTrue("an engine with no document has an opaque origin, which serializes to null");
    }

    /// <summary>
    /// The round trip an embedder writes: mint a URL, fetch it, read the bytes back — with no
    /// <c>HttpClient</c> anywhere, because a blob URL reaches no transport.
    /// </summary>
    [Test]
    public void FetchingABlobUrlNeedsNoHttpClient()
    {
        var engine = BlobUrlEngine();

        engine.Execute("""
            var result = null;
            var contentType = null;
            var blob = new Blob(['hello from a blob'], { type: 'text/plain' });
            fetch(URL.createObjectURL(blob)).then(response => {
              contentType = response.headers.get('Content-Type');
              return response.text();
            }).then(text => { result = text; });
            """);

        Pump(engine);

        engine.Evaluate("result").AsString().Should().Be("hello from a blob");
        engine.Evaluate("contentType").AsString().Should().Be("text/plain");
    }

    [Test]
    public void ARevokedUrlFailsTheFetchWithATypeError()
    {
        var engine = BlobUrlEngine();

        engine.Execute("""
            var failure = null;
            var url = URL.createObjectURL(new Blob(['gone']));
            URL.revokeObjectURL(url);
            fetch(url).then(() => { failure = 'resolved'; }, error => { failure = error.constructor.name; });
            """);

        Pump(engine);
        engine.Evaluate("failure").AsString().Should().Be("TypeError");
    }

    /// <summary>
    /// The request holds the blob from the moment it is built, so revoking the URL afterwards does not take
    /// the bytes away — which is the difference between a blob URL and a network one.
    /// </summary>
    [Test]
    public void ARequestBuiltBeforeTheRevokeStillFetches()
    {
        var engine = BlobUrlEngine();

        engine.Execute("""
            var result = null;
            var url = URL.createObjectURL(new Blob(['still here']));
            var request = new Request(url);
            URL.revokeObjectURL(url);
            fetch(request).then(response => response.text()).then(text => { result = text; });
            """);

        Pump(engine);
        engine.Evaluate("result").AsString().Should().Be("still here");
    }

    [Test]
    public void OnlyGetIsAnswered()
    {
        var engine = BlobUrlEngine();

        engine.Execute("""
            var outcomes = [];
            var url = URL.createObjectURL(new Blob(['x']));
            for (const method of ['POST', 'PUT', 'DELETE', 'HEAD']) {
              fetch(url, { method }).then(() => outcomes.push(method + ':ok'), () => outcomes.push(method + ':failed'));
            }
            """);

        Pump(engine);
        engine.Evaluate("outcomes.slice().sort().join(',')").AsString()
            .Should().Be("DELETE:failed,HEAD:failed,POST:failed,PUT:failed");
    }

    /// <summary>
    /// A <c>Range</c> gets a 206 with a <c>Content-Range</c>, exactly as it would from a server — which is
    /// what makes a blob URL usable as a stand-in for one.
    /// </summary>
    [Test]
    public void ARangeRequestAnswersPartialContent()
    {
        var engine = BlobUrlEngine();

        engine.Execute("""
            var status = null;
            var contentRange = null;
            var body = null;
            var url = URL.createObjectURL(new Blob(['A simple Hello, World! example'], { type: 'text/plain' }));
            fetch(url, { headers: { Range: 'bytes=9-21' } }).then(response => {
              status = response.status;
              contentRange = response.headers.get('Content-Range');
              return response.text();
            }).then(text => { body = text; });
            """);

        Pump(engine);

        engine.Evaluate("status").AsNumber().Should().Be(206);
        engine.Evaluate("contentRange").AsString().Should().Be("bytes 9-21/30");
        engine.Evaluate("body").AsString().Should().Be("Hello, World!");
    }

    /// <summary>
    /// <c>XMLHttpRequest</c> takes the same path, so a blob URL is readable on an engine that has the
    /// interface and no network grant of any kind.
    /// </summary>
    [Test]
    public void XmlHttpRequestReadsABlobUrlWithoutANetworkGrant()
    {
        var engine = new Engine(options => options.UseWebApis(WebApiFeatures.Default | WebApiFeatures.XmlHttpRequest));

        engine.Execute("""
            var status = null;
            var text = null;
            var length = null;
            var xhr = new XMLHttpRequest();
            xhr.open('GET', URL.createObjectURL(new Blob(['blah'])));
            xhr.onload = function () {
              status = xhr.status;
              text = xhr.responseText;
              length = xhr.getResponseHeader('Content-Length');
            };
            xhr.send();
            """);

        Pump(engine);

        engine.Evaluate("status").AsNumber().Should().Be(200);
        engine.Evaluate("text").AsString().Should().Be("blah");
        engine.Evaluate("length").AsString().Should().Be("4");
    }
}
#endif
