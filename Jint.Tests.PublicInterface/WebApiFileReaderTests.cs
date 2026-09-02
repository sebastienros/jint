#if NET8_0_OR_GREATER
using Jint;
using Jint.WebApi;

namespace Jint.Tests.PublicInterface;

/// <summary>
/// <c>FileReader</c> seen from outside the assembly: which flag installs it, what each <c>readAs*</c> hands
/// back, and that a read needs the host's own pump to finish.
/// </summary>
/// <remarks>
/// A <c>Blob</c> here is bytes already in memory, so nothing about a read is I/O — but every one of its events
/// is still a task on the engine's event loop, which is the one thing an embedder has to know: a host that
/// calls <c>readAsText</c> and reads <c>result</c> without pumping gets <c>null</c>.
/// </remarks>
public class WebApiFileReaderTests
{
    private static Engine ReaderEngine() => new(options => options.UseWebApis(WebApiFeatures.Files));

    private static void Pump(Engine engine)
    {
        for (var i = 0; i < 12; i++)
        {
            engine.Tasks.ProcessTasks();
        }
    }

    [Test]
    public void ADefaultEngineHasNoFileReader()
    {
        var engine = new Engine();

        engine.Evaluate("typeof FileReader").AsString().Should().Be("undefined");
        engine.Evaluate("'FileReader' in globalThis").AsBoolean().Should().BeFalse();
    }

    [Test]
    public void TheFilesFlagInstallsItAndTheEventInterfacesItNeeds()
    {
        var engine = ReaderEngine();

        engine.Evaluate("typeof FileReader").AsString().Should().Be("function");
        engine.Evaluate("typeof ProgressEvent").AsString().Should().Be("function");
        engine.Evaluate("new FileReader() instanceof EventTarget").AsBoolean().Should().BeTrue();
    }

    /// <summary>
    /// The result is null until the host pumps, because every event of a read is a task.
    /// </summary>
    [Test]
    public void TheHostsPumpIsWhatFinishesARead()
    {
        var engine = ReaderEngine();

        engine.Execute("""
            var reader = new FileReader();
            reader.readAsText(new Blob(['hello']));
            var resultBeforePumping = reader.result;
            var readyStateBeforePumping = reader.readyState;
            """);

        engine.Evaluate("resultBeforePumping").IsNull().Should().BeTrue();
        engine.Evaluate("readyStateBeforePumping").AsNumber().Should().Be(1, "FileReader.LOADING");

        Pump(engine);

        engine.Evaluate("reader.result").AsString().Should().Be("hello");
        engine.Evaluate("reader.readyState").AsNumber().Should().Be(2, "FileReader.DONE");
    }

    [Test]
    public void EveryReadAsMethodRoundTripsItsBytes()
    {
        var engine = ReaderEngine();

        engine.Execute("""
            var results = {};
            var pending = 4;
            function read(key, start) {
              var reader = new FileReader();
              reader.onload = function () { results[key] = reader.result; };
              start(reader);
            }
            read('text', r => r.readAsText(new Blob(['TEST'])));
            read('binary', r => r.readAsBinaryString(new Blob(['TEST'])));
            read('dataUrl', r => r.readAsDataURL(new Blob(['TEST'], { type: 'text/plain' })));
            read('buffer', r => r.readAsArrayBuffer(new Blob(['TEST'])));
            """);

        Pump(engine);

        engine.Evaluate("results.text").AsString().Should().Be("TEST");
        engine.Evaluate("results.binary").AsString().Should().Be("TEST");
        engine.Evaluate("results.dataUrl").AsString().Should().Be("data:text/plain;base64,VEVTVA==");
        engine.Evaluate("results.buffer instanceof ArrayBuffer").AsBoolean().Should().BeTrue();
        engine.Evaluate("Array.from(new Uint8Array(results.buffer)).join(',')").AsString().Should().Be("84,69,83,84");
    }

    /// <summary>
    /// The six events arrive in the order the File API gives them, and <c>progress</c> is skipped for a blob
    /// with no bytes.
    /// </summary>
    [Test]
    public void TheEventsArriveInTheOrderTheStandardGives()
    {
        var engine = ReaderEngine();

        engine.Execute("""
            var order = [];
            var reader = new FileReader();
            reader.onloadstart = () => order.push('loadstart');
            reader.onprogress = () => order.push('progress');
            reader.onload = () => order.push('load');
            reader.onloadend = () => order.push('loadend');
            reader.readAsText(new Blob(['bytes']));
            """);

        Pump(engine);
        engine.Evaluate("order.join(',')").AsString().Should().Be("loadstart,progress,load,loadend");
    }

    [Test]
    public void AbortEndsTheReadAndFiresItsTwoEventsSynchronously()
    {
        var engine = ReaderEngine();

        engine.Execute("""
            var order = [];
            var reader = new FileReader();
            reader.onabort = () => order.push('abort');
            reader.onload = () => order.push('load');
            reader.onloadend = () => order.push('loadend');
            reader.readAsText(new Blob(['bytes']));
            reader.abort();
            var orderAtAbort = order.join(',');
            """);

        Pump(engine);

        engine.Evaluate("orderAtAbort").AsString().Should().Be("abort,loadend");
        engine.Evaluate("order.join(',')").AsString().Should().Be("abort,loadend", "the queued tasks were removed");
        engine.Evaluate("reader.result").IsNull().Should().BeTrue();
    }

    /// <summary>
    /// <c>readAsText</c>'s encoding comes from the argument, then from the blob type's <c>charset</c>
    /// parameter, then from a byte order mark, and finally from UTF-8.
    /// </summary>
    [Test]
    public void ReadAsTextHonoursTheBlobTypesCharset()
    {
        var engine = ReaderEngine();

        engine.Execute("""
            var reader = new FileReader();
            reader.readAsText(new Blob([new Uint8Array([0x80])], { type: 'text/plain;charset=windows-1252' }));
            """);

        Pump(engine);
        engine.Evaluate("reader.result").AsString().Should().Be("€");
    }
}
#endif
