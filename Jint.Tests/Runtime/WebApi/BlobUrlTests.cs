#if NET8_0_OR_GREATER
#nullable enable

using Jint.Runtime;

namespace Jint.Tests.Runtime.WebApi;

/// <summary>
/// <c>URL.createObjectURL</c>, <c>URL.revokeObjectURL</c>, and the <c>blob:</c> arm of scheme fetch that
/// consumes what they mint.
/// <para>
/// https://w3c.github.io/FileAPI/#url
/// </para>
/// </summary>
/// <remarks>
/// The two statics are conditional members: they belong to the File API rather than to the URL standard, and
/// exist only where an engine has both features. Everything they store is per engine, so nothing here can
/// leak between two of them.
/// </remarks>
public class BlobUrlTests
{
    private static Engine BlobUrlEngine()
        => new(options => options.UseWebApis(WebApiFeatures.Url | WebApiFeatures.Files));

    /// <summary>
    /// How many URLs the store holds. Reached through the engine's internals because nothing script-visible
    /// reports it: a script can only tell a live URL from a revoked one by fetching it, and this engine has
    /// no fetch. The public-interface tests do it the other way, through <c>fetch</c>.
    /// </summary>
    private static int LiveUrlCount(Engine engine) => engine._webApi!.BlobUrls.Count;

    [Test]
    public void TheTwoStaticsNeedBothFeaturesAndAreAbsentWithout()
    {
        BlobUrlEngine().Evaluate("typeof URL.createObjectURL").AsString().Should().Be("function");

        new Engine(options => options.UseWebApis(WebApiFeatures.Url))
            .Evaluate("typeof URL.createObjectURL").AsString().Should().Be("undefined", "there is no Blob to name");

        // And the other way round: an engine with the File API and no URL has no URL object to hang them on.
        new Engine(options => options.UseWebApis(WebApiFeatures.Files))
            .Evaluate("typeof URL").AsString().Should().Be("undefined");
    }

    /// <summary>
    /// Static operations, so WebIDL's ordinary operation attributes — which is also what makes them show up
    /// in <c>Object.keys(URL)</c> beside <c>parse</c> and <c>canParse</c>.
    /// </summary>
    [Test]
    public void TheyAreEnumerableWritableConfigurableOperations()
    {
        var engine = BlobUrlEngine();

        engine.Evaluate("JSON.stringify(Object.keys(URL).sort())").AsString()
            .Should().Be("""["canParse","createObjectURL","parse","revokeObjectURL"]""");

        engine.Evaluate("""
            const d = Object.getOwnPropertyDescriptor(URL, 'createObjectURL');
            [d.writable, d.enumerable, d.configurable].join(',')
            """).AsString().Should().Be("true,true,true");

        engine.Evaluate("[URL.createObjectURL.length, URL.revokeObjectURL.length].join(',')").AsString().Should().Be("1,1");
    }

    /// <summary>
    /// The URL is <c>blob:null/&lt;uuid&gt;</c>: an engine with no document has an opaque origin, which
    /// serializes to <c>"null"</c>.
    /// </summary>
    [Test]
    public void AMintedUrlIsABlobUrlWithANullOriginAndAUuidPath()
    {
        var engine = BlobUrlEngine();

        engine.Execute("var url = URL.createObjectURL(new Blob(['x'])); var record = new URL(url);");

        engine.Evaluate("record.protocol").AsString().Should().Be("blob:");
        engine.Evaluate("record.origin").AsString().Should().Be("null");
        engine.Evaluate("record.host").AsString().Should().BeEmpty();
        engine.Evaluate("record.port").AsString().Should().BeEmpty();
        engine.Evaluate(@"/^null\/[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i.test(record.pathname)")
            .AsBoolean().Should().BeTrue();
    }

    [Test]
    public void EveryMintedUrlIsUnique()
    {
        var engine = BlobUrlEngine();

        engine.Execute("""
            var blob = new Blob(['x']);
            var seen = new Set();
            for (var i = 0; i < 500; i++) { seen.add(URL.createObjectURL(blob)); }
            """);

        engine.Evaluate("seen.size").AsNumber().Should().Be(500);
    }

    [Test]
    public void CreateObjectUrlRefusesAnythingThatIsNotABlob()
    {
        var engine = BlobUrlEngine();

        foreach (var argument in new[] { "'a string'", "42", "null", "undefined", "{}", "new Uint8Array(4)" })
        {
            Assert.Throws<JavaScriptException>(() => engine.Execute($"URL.createObjectURL({argument});"))!
                .Error.Get("name").AsString().Should().Be("TypeError", $"{argument} is not a Blob");
        }

        // A File is a Blob, so it is the one subclass that passes.
        engine.Evaluate("URL.createObjectURL(new File(['x'], 'name.txt')).startsWith('blob:')").AsBoolean().Should().BeTrue();
    }

    /// <summary>
    /// Revoking is by the serialization <i>with</i> the fragment and resolving is by the serialization
    /// <i>without</i> it, which is what makes a fragment appended to a blob URL still name the blob while
    /// revoking that same string revokes nothing.
    /// </summary>
    [Test]
    public void OnlyAnExactMatchRevokes()
    {
        var engine = BlobUrlEngine();

        engine.Execute("""
            var url = URL.createObjectURL(new Blob(['x']));
            URL.revokeObjectURL(url + '#fragment');
            URL.revokeObjectURL(url + '?query');
            URL.revokeObjectURL('https://example.org/');
            URL.revokeObjectURL('not a url at all');
            """);

        // Nothing above revoked anything, which is only observable through the store — so the check is that
        // revoking the exact string afterwards is what empties it.
        LiveUrlCount(engine).Should().Be(1);

        engine.Execute("URL.revokeObjectURL(url);");
        LiveUrlCount(engine).Should().Be(0);
    }

    [Test]
    public void ARestoreEmptiesTheStore()
    {
        var engine = BlobUrlEngine();
        var snapshot = engine.Advanced.CaptureGlobalSnapshot();

        engine.Execute("URL.createObjectURL(new Blob(['x'])); URL.createObjectURL(new Blob(['y']));");
        LiveUrlCount(engine).Should().Be(2);

        engine.Advanced.RestoreGlobalSnapshot(snapshot);
        LiveUrlCount(engine).Should().Be(0, "the URLs belonged to the cycle the restore ended");
    }
}
#endif
