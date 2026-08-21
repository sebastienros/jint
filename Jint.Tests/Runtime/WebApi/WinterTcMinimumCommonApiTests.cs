#if NET8_0_OR_GREATER
#nullable enable

namespace Jint.Tests.Runtime.WebApi;

/// <summary>
/// The member index of WinterTC's Minimum Common Web Platform API
/// (https://min-common-api.proposal.wintertc.org/), §5.1 <i>Common interfaces</i> and §5.2 <i>Common methods
/// and properties</i> of the 2025 snapshot, transcribed member for member and asserted against the engine.
/// </summary>
/// <remarks>
/// <para>
/// This is the pin behind the conformance table in <c>README.md</c>. The table claims, row by row, which
/// <see cref="WebApiFeatures"/> flag provides each member and which ones are absent; a change that installs a
/// global WinterTC does not name, stops installing one it does, or quietly closes one of the documented gaps
/// fails here, so the table cannot go stale without the build saying so.
/// </para>
/// <para>
/// Membership is tested with <c>in</c> rather than <c>typeof</c>, so a property that exists and holds
/// <see langword="undefined"/> counts as present — which is exactly the distinction the three
/// <c>on*</c> handlers turn on, since a browser's are <see langword="null"/>.
/// </para>
/// </remarks>
public class WinterTcMinimumCommonApiTests
{
    /// <summary>§5.1 Common interfaces, in the standard's own order.</summary>
    private static readonly string[] CommonInterfaces =
    [
        // [DOM]
        "AbortController", "AbortSignal", "Event", "EventTarget",
        // [HTML] (CustomEvent is the DOM Standard's; §5.1 groups it here)
        "CustomEvent", "ErrorEvent", "MessageChannel", "MessageEvent", "MessagePort", "PromiseRejectionEvent",
        // [WEBIDL]
        "DOMException",
        // [FETCH]
        "Headers", "Request", "Response",
        // [XHR]
        "FormData",
        // [FILEAPI]
        "Blob", "File",
        // [COMPRESSION]
        "CompressionStream", "DecompressionStream",
        // [STREAMS]
        "ByteLengthQueuingStrategy", "CountQueuingStrategy", "ReadableByteStreamController", "ReadableStream",
        "ReadableStreamBYOBReader", "ReadableStreamBYOBRequest", "ReadableStreamDefaultController",
        "ReadableStreamDefaultReader", "TransformStream", "TransformStreamDefaultController", "WritableStream",
        "WritableStreamDefaultController", "WritableStreamDefaultWriter",
        // [ENCODING]
        "TextDecoder", "TextDecoderStream", "TextEncoder", "TextEncoderStream",
        // [URL]
        "URL", "URLSearchParams",
        // [URLPATTERN]
        "URLPattern",
        // [WEBCRYPTO]
        "Crypto", "CryptoKey", "SubtleCrypto",
        // [HR-TIME]
        "Performance",
        // [WASM-JS-API-2]
        "WebAssembly.Global", "WebAssembly.Instance", "WebAssembly.Memory", "WebAssembly.Module",
        "WebAssembly.Table", "WebAssembly.Tag", "WebAssembly.Exception", "WebAssembly.CompileError",
        "WebAssembly.LinkError", "WebAssembly.RuntimeError",
    ];

    /// <summary>§5.2 Common methods and properties, in the standard's own order.</summary>
    private static readonly string[] CommonMethodsAndProperties =
    [
        // [ECMASCRIPT]
        "globalThis",
        // [HTML]
        "atob", "btoa", "clearTimeout", "clearInterval", "navigator.userAgent", "onerror",
        "onunhandledrejection", "onrejectionhandled", "queueMicrotask", "reportError", "self", "setTimeout",
        "setInterval", "structuredClone",
        // [FETCH]
        "fetch",
        // [CONSOLE]
        "console",
        // [WEBCRYPTO]
        "crypto",
        // [HR-TIME]
        "performance",
        // [WASM-JS-API-2] and [WASM-WEB-API-2]
        "WebAssembly.compile", "WebAssembly.compileStreaming", "WebAssembly.instantiate",
        "WebAssembly.instantiateStreaming", "WebAssembly.JSTag", "WebAssembly.validate",
    ];

    /// <summary>
    /// Everything the README's table records as absent from a <see cref="WebApiFeatures.Default"/> engine,
    /// with the reason each one is on this list.
    /// </summary>
    private static readonly string[] AbsentFromDefault =
    [
        // Outbound network access is a grant a host names, so these four sit behind WebApiFeatures.Fetch
        // (the three interfaces also arrive with CacheApi and FetchEvents).
        "Headers", "Request", "Response", "fetch",

        // Implemented, and their interface objects are real; only the five interfaces a script constructs by
        // name are installed on the global object.
        "ReadableByteStreamController", "ReadableStreamBYOBReader", "ReadableStreamBYOBRequest",
        "ReadableStreamDefaultController", "ReadableStreamDefaultReader", "TransformStreamDefaultController",
        "WritableStreamDefaultController", "WritableStreamDefaultWriter",

        // The instances exist and carry every member; the interface objects do not exist at all.
        "Crypto", "SubtleCrypto", "Performance",

        // §6 The global scope: a runtime whose global is not an EventTarget "shall not support" these three.
        "onerror", "onunhandledrejection", "onrejectionhandled",

        // Declined: a second virtual machine, sharing nothing with a tree-walking AST interpreter.
        "WebAssembly.Global", "WebAssembly.Instance", "WebAssembly.Memory", "WebAssembly.Module",
        "WebAssembly.Table", "WebAssembly.Tag", "WebAssembly.Exception", "WebAssembly.CompileError",
        "WebAssembly.LinkError", "WebAssembly.RuntimeError",
        "WebAssembly.compile", "WebAssembly.compileStreaming", "WebAssembly.instantiate",
        "WebAssembly.instantiateStreaming", "WebAssembly.JSTag", "WebAssembly.validate",
    ];

    private static string[] AllMembers => [.. CommonInterfaces, .. CommonMethodsAndProperties];

    [Fact]
    public void ADefaultEngineIsMissingExactlyTheMembersTheReadmeRecordsAsAbsent()
    {
        var engine = new Engine(options => options.UseWebApis());

        MissingFrom(engine, AllMembers).Should().BeEquivalentTo(AbsentFromDefault);
    }

    [Fact]
    public void TheFetchGrantAddsTheFourNetworkMembersAndNothingElse()
    {
        var engine = new Engine(options => options.UseWebApis().UseFetch());

        string[] stillAbsent = [.. AbsentFromDefault.Where(static m => m is not ("Headers" or "Request" or "Response" or "fetch"))];

        MissingFrom(engine, AllMembers).Should().BeEquivalentTo(stillAbsent);
    }

    [Fact]
    public void AnEngineThatAskedForNoFeatureCarriesNoneOfThem()
    {
        var engine = new Engine();

        // Everything but `globalThis`, which is the language's and was never ours to install.
        string[] everythingElse = [.. AllMembers.Where(static m => m is not "globalThis")];

        MissingFrom(engine, AllMembers).Should().BeEquivalentTo(everythingElse);
    }

    [Theory]
    [InlineData("ReadableStreamDefaultReader", "new ReadableStream().getReader()")]
    [InlineData("ReadableStreamBYOBReader", "new ReadableStream({ type: 'bytes' }).getReader({ mode: 'byob' })")]
    [InlineData("WritableStreamDefaultWriter", "new WritableStream().getWriter()")]
    [InlineData("ReadableStreamDefaultController", "captured(c => new ReadableStream({ start: c }))")]
    [InlineData("ReadableByteStreamController", "captured(c => new ReadableStream({ type: 'bytes', start: c }))")]
    [InlineData("WritableStreamDefaultController", "captured(c => new WritableStream({ start: c }))")]
    [InlineData("TransformStreamDefaultController", "captured(c => new TransformStream({ start: c }))")]
    public void AStreamInterfaceObjectWithoutAGlobalIsStillTheRealThing(string name, string instance)
    {
        var engine = new Engine(options => options.UseWebApis());

        // The point of the reduction: not a global, but reachable and genuine through its instances.
        engine.Evaluate($"'{name}' in globalThis").AsBoolean().Should().BeFalse();

        engine.Execute("function captured(build) { var seen; build(c => { seen = c; }); return seen; }");
        engine.Evaluate($"Object.getPrototypeOf({instance}).constructor.name").AsString().Should().Be(name);
    }

    [Fact]
    public void TheByobRequestInterfaceObjectIsTheRealThingToo()
    {
        var engine = new Engine(options => options.UseWebApis());

        engine.Evaluate("'ReadableStreamBYOBRequest' in globalThis").AsBoolean().Should().BeFalse();

        engine.Execute("""
            var seen = '';
            var stream = new ReadableStream({
                type: 'bytes',
                autoAllocateChunkSize: 16,
                pull(controller) {
                    var request = controller.byobRequest;
                    if (request) {
                        seen = Object.getPrototypeOf(request).constructor.name;
                        request.view[0] = 1;
                        request.respond(1);
                    }
                }
            });
            stream.getReader().read();
            """);

        engine.Evaluate("seen").AsString().Should().Be("ReadableStreamBYOBRequest");
    }

    [Fact]
    public void CryptoSubtleCryptoAndPerformanceHaveNoInterfaceObjectAtAll()
    {
        var engine = new Engine(options => options.UseWebApis());

        // Not merely unnameable: there is no interface prototype either, so the members are own properties
        // and the object inherits straight from Object.prototype.
        foreach (var instance in new[] { "crypto", "crypto.subtle", "performance" })
        {
            engine.Evaluate($"Object.getPrototypeOf({instance}) === Object.prototype")
                .AsBoolean().Should().BeTrue(instance);
        }

        // What a script can observe of that is still what a browser shows, because there the members live one
        // level up on the prototype.
        engine.Evaluate("Object.keys(crypto).length + Object.keys(performance).length").AsNumber().Should().Be(0);

        // And the members themselves are all there.
        engine.Evaluate("typeof crypto.randomUUID").AsString().Should().Be("function");
        engine.Evaluate("typeof crypto.subtle.digest").AsString().Should().Be("function");
        engine.Evaluate("typeof performance.now").AsString().Should().Be("function");
    }

    /// <summary>
    /// Which of <paramref name="members"/> the engine does not have, resolved as dotted paths so that
    /// <c>navigator.userAgent</c> and <c>WebAssembly.Module</c> are asked the way WinterTC writes them.
    /// </summary>
    private static string[] MissingFrom(Engine engine, string[] members)
    {
        var script = $$"""
            (function (names) {
                var missing = [];
                for (var i = 0; i < names.length; i++) {
                    var parts = names[i].split('.');
                    var current = globalThis;
                    var found = true;
                    for (var j = 0; j < parts.length; j++) {
                        if (current === null || current === undefined || !(parts[j] in Object(current))) {
                            found = false;
                            break;
                        }
                        current = current[parts[j]];
                    }
                    if (!found) { missing.push(names[i]); }
                }
                return missing.join(' ');
            })(['{{string.Join("','", members)}}'])
            """;

        var joined = engine.Evaluate(script).AsString();
        return joined.Length == 0 ? [] : joined.Split(' ');
    }
}
#endif
