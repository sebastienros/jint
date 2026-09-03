#if NET8_0_OR_GREATER
#nullable enable

using Jint.Runtime;

namespace Jint.Tests.Runtime.WebApi;

/// <summary>
/// <c>FileReader</c> — the four <c>readAs*</c> operations, the event sequence they produce, and
/// <c>abort()</c>.
/// <para>
/// https://w3c.github.io/FileAPI/#dfn-filereader
/// </para>
/// </summary>
/// <remarks>
/// A read runs as tasks on the engine's event loop, so every test that reaches a result pumps. What the tests
/// assert about the events is their <i>order</i> and the state visible from each: that is the whole content of
/// the read operation once the bytes are already in memory.
/// </remarks>
public class FileReaderTests
{
    private static Engine ReaderEngine() => new(options => options.UseWebApis(WebApiFeatures.Files));

    /// <summary>
    /// Runs the read to completion. Four tasks per read plus the checkpoints between them, and a handler that
    /// starts another read adds four more, so this pumps generously rather than exactly.
    /// </summary>
    private static void Pump(Engine engine)
    {
        for (var i = 0; i < 12; i++)
        {
            engine.Tasks.ProcessTasks();
        }
    }

    /// <summary>
    /// https://w3c.github.io/FileAPI/#readOperation fires <c>load</c> and <c>loadend</c> from one task, and
    /// what separates them is the microtask checkpoint the dispatch performs when a listener returns to an
    /// empty JavaScript execution context stack — so an <c>await</c> the <c>load</c> handler resumed runs
    /// first, which is the <c>EventWatcher</c> shape
    /// <c>FileAPI/reading-data-section/filereader_events.any.js</c> is written in.
    /// </summary>
    /// <remarks>
    /// Both halves are pinned here because until sebastienros/jint#3668 the engine had no such checkpoint and
    /// <c>loadend</c> was fired from a task of its own to stand in for one; this order has to survive the
    /// workaround's removal.
    /// </remarks>
    [Test]
    public void AnAwaitResumedByTheLoadListenerRunsBeforeLoadend()
    {
        var engine = ReaderEngine();

        engine.Execute("""
            var log = [];
            var reader = new FileReader();
            var arrived;
            var waiting = new Promise(function (resolve) { arrived = resolve; });
            (async function () { await waiting; log.push('resumed'); })();
            reader.addEventListener('load', function () { log.push('load'); arrived(); });
            reader.addEventListener('loadend', function () { log.push('loadend'); });
            reader.readAsText(new Blob(['x']));
            """);

        Pump(engine);

        engine.Evaluate("log.join('|')").AsString().Should().Be("load|resumed|loadend");
    }

    [Test]
    public void TheInterfaceArrivesWithTheFilesFlagAndNotWithout()
    {
        ReaderEngine().Evaluate("typeof FileReader").AsString().Should().Be("function");

        new Engine(options => options.UseWebApis(WebApiFeatures.Console))
            .Evaluate("typeof FileReader").AsString().Should().Be("undefined");
    }

    /// <summary>
    /// The File API cannot be had without the event interfaces, because a <c>FileReader</c> is an
    /// <c>EventTarget</c> that fires <c>ProgressEvent</c>s.
    /// </summary>
    [Test]
    public void TheFilesFlagBringsTheEventsAndTheProgressEvent()
    {
        var engine = ReaderEngine();

        engine.Evaluate("new FileReader() instanceof EventTarget").AsBoolean().Should().BeTrue();
        engine.Evaluate("typeof ProgressEvent").AsString().Should().Be("function");
        engine.Evaluate("typeof EventTarget").AsString().Should().Be("function");
    }

    [Test]
    public void TheThreeStateConstantsAreOnBothTheInterfaceAndItsPrototype()
    {
        var engine = ReaderEngine();

        engine.Evaluate("[FileReader.EMPTY, FileReader.LOADING, FileReader.DONE].join(',')").AsString().Should().Be("0,1,2");
        engine.Evaluate("[FileReader.prototype.EMPTY, FileReader.prototype.LOADING, FileReader.prototype.DONE].join(',')")
            .AsString().Should().Be("0,1,2");
        engine.Evaluate("new FileReader().readyState").AsNumber().Should().Be(0);
    }

    [Test]
    public void AFreshReaderHasNoResultNoErrorAndNoHandlers()
    {
        var engine = ReaderEngine();

        engine.Execute("var r = new FileReader();");
        engine.Evaluate("r.result").IsNull().Should().BeTrue();
        engine.Evaluate("r.error").IsNull().Should().BeTrue();

        foreach (var handler in new[] { "onloadstart", "onprogress", "onload", "onabort", "onerror", "onloadend" })
        {
            engine.Evaluate($"r.{handler}").IsNull().Should().BeTrue($"{handler} starts out null");
        }
    }

    /// <summary>
    /// <c>loadstart</c>, one <c>progress</c>, <c>load</c>, <c>loadend</c> — and no <c>progress</c> at all for
    /// a blob with no bytes to have read.
    /// </summary>
    [TestCase("''", "loadstart,load,loadend")]
    [TestCase("'abc'", "loadstart,progress,load,loadend")]
    public void TheEventsArriveInOrderAndProgressOnlyWhenThereAreBytes(string contents, string expected)
    {
        var engine = ReaderEngine();

        engine.Execute($$"""
            var order = [];
            var r = new FileReader();
            for (const type of ['loadstart', 'progress', 'load', 'abort', 'error', 'loadend']) {
              r.addEventListener(type, () => order.push(type));
            }
            r.readAsText(new Blob([{{contents}}]));
            var deliveredDuringTheScript = order.length;
            """);

        Pump(engine);

        engine.Evaluate("deliveredDuringTheScript").AsNumber().Should().Be(0, "every event of a read is a task");
        engine.Evaluate("order.join(',')").AsString().Should().Be(expected);
    }

    [Test]
    public void ReadyStateAndResultAreWhatEachEventSees()
    {
        var engine = ReaderEngine();

        engine.Execute("""
            var seen = [];
            var r = new FileReader();
            for (const type of ['loadstart', 'progress', 'load', 'loadend']) {
              r.addEventListener(type, () => seen.push(type + ':' + r.readyState + ':' + (r.result === null ? 'null' : r.result)));
            }
            r.readAsText(new Blob(['hi']));
            var syncReadyState = r.readyState;
            """);

        Pump(engine);

        engine.Evaluate("syncReadyState").AsNumber().Should().Be(1, "readyState moves to LOADING synchronously");
        engine.Evaluate("seen.join(' | ')").AsString()
            .Should().Be("loadstart:1:null | progress:1:null | load:2:hi | loadend:2:hi");
    }

    [TestCase("readAsText(new Blob(['TEST']))", "TEST")]
    [TestCase("readAsText(new Blob(['TEST']), 'UTF-16')", "䕔呓")]
    [TestCase("readAsBinaryString(new Blob(['σ']))", "Ï")]
    [TestCase("readAsDataURL(new Blob(['TEST'], { type: 'text/plain' }))", "data:text/plain;base64,VEVTVA==")]
    [TestCase("readAsDataURL(new Blob(['TEST']))", "data:application/octet-stream;base64,VEVTVA==")]
    [TestCase("readAsDataURL(new Blob([]))", "data:application/octet-stream;base64,")]
    public void EachReadPackagesTheBytesItsOwnWay(string call, string expected)
    {
        var engine = ReaderEngine();

        engine.Execute($"var r = new FileReader(); r.{call};");
        Pump(engine);

        engine.Evaluate("r.result").AsString().Should().Be(expected);
    }

    [Test]
    public void ReadAsArrayBufferHandsOutAFreshBuffer()
    {
        var engine = ReaderEngine();

        engine.Execute("var r = new FileReader(); r.readAsArrayBuffer(new Blob(['TEST']));");
        Pump(engine);

        engine.Evaluate("r.result instanceof ArrayBuffer").AsBoolean().Should().BeTrue();
        engine.Evaluate("Array.from(new Uint8Array(r.result)).join(',')").AsString().Should().Be("84,69,83,84");
    }

    /// <summary>
    /// The encoding is decided by the argument, then by the blob type's <c>charset</c>, then by UTF-8 — and a
    /// byte order mark overrides all three, which is the Encoding standard's <i>decode</i> rather than
    /// <c>TextDecoder</c>'s BOM stripping.
    /// </summary>
    [TestCase("new Blob([new Uint8Array([0xFE, 0xFF, 0, 104, 0, 105])])", "undefined", "hi")]
    [TestCase("new Blob([new Uint8Array([0xFF, 0xFE, 104, 0, 105, 0])])", "undefined", "hi")]
    [TestCase("new Blob([new Uint8Array([0xEF, 0xBB, 0xBF, 104, 105])])", "undefined", "hi")]
    [TestCase("new Blob([new Uint8Array([0x80])], { type: 'text/plain;charset=windows-1252' })", "undefined", "€")]
    [TestCase("new Blob([new Uint8Array([0x80])], { type: 'text/plain;charset=UTF-8' })", "'windows-1252'", "€")]
    [TestCase("new Blob([new Uint8Array([0x80])])", "'not-an-encoding'", "�")]
    public void ReadAsTextResolvesItsEncodingInOrder(string blob, string encoding, string expected)
    {
        var engine = ReaderEngine();

        engine.Execute($"var r = new FileReader(); r.readAsText({blob}, {encoding});");
        Pump(engine);

        engine.Evaluate("r.result").AsString().Should().Be(expected);
    }

    /// <summary>
    /// <c>abort()</c> fires its two events synchronously, which is what the algorithm says and what a script
    /// that arms a listener after calling it would miss.
    /// </summary>
    [Test]
    public void AbortIsSynchronousAndLeavesNoResult()
    {
        var engine = ReaderEngine();

        engine.Execute("""
            var order = [];
            var r = new FileReader();
            for (const type of ['loadstart', 'progress', 'load', 'abort', 'error', 'loadend']) {
              r.addEventListener(type, () => order.push(type + ':' + r.readyState));
            }
            r.readAsText(new Blob(['abc']));
            r.abort();
            var afterAbort = order.join(',');
            """);

        Pump(engine);

        engine.Evaluate("afterAbort").AsString().Should().Be("abort:2,loadend:2");
        engine.Evaluate("order.join(',')").AsString().Should().Be("abort:2,loadend:2", "the queued tasks were removed");
        engine.Evaluate("r.result").IsNull().Should().BeTrue();
    }

    [Test]
    public void AbortingAnIdleOrFinishedReaderFiresNothing()
    {
        var engine = ReaderEngine();

        engine.Execute("""
            var order = [];
            var r = new FileReader();
            for (const type of ['abort', 'loadend']) { r.addEventListener(type, () => order.push(type)); }
            r.abort();
            """);

        Pump(engine);
        engine.Evaluate("order.length").AsNumber().Should().Be(0);
        engine.Evaluate("r.readyState").AsNumber().Should().Be(0);
    }

    /// <summary>
    /// Both reads are in one <c>Execute</c> because every host entry point drains the event loop on its way
    /// out: split in two, the first read would have finished before the second was asked for.
    /// </summary>
    [Test]
    public void ASecondReadWhileLoadingIsRefusedByName()
    {
        var engine = ReaderEngine();

        var exception = Assert.Throws<JavaScriptException>(() => engine.Execute("""
            var r = new FileReader();
            r.readAsText(new Blob(['a']));
            r.readAsText(new Blob(['b']));
            """))!;

        exception.Error.Get("name").AsString().Should().Be("InvalidStateError");
    }

    /// <summary>
    /// A read started from inside a handler replaces the one that was running, and the tasks still queued for
    /// the old one fire nothing — which is <c>abort()</c>'s "remove those tasks" step reached the other way.
    /// </summary>
    [Test]
    public void AbortingAndRestartingFromAHandlerRunsOnlyTheSecondRead()
    {
        var engine = ReaderEngine();

        engine.Execute("""
            var order = [];
            var r = new FileReader();
            r.onloadstart = function () {
              order.push('loadstart-1');
              r.abort();
              r.onloadstart = null;
              r.onloadend = function () { order.push('loadend:' + r.result); };
              r.readAsText(new Blob(['second']));
            };
            r.readAsText(new Blob(['first']));
            """);

        Pump(engine);
        engine.Evaluate("order.join(',')").AsString().Should().Be("loadstart-1,loadend:second");
    }

    [Test]
    public void ANonBlobArgumentIsARefusal()
    {
        var engine = ReaderEngine();
        engine.Execute("var r = new FileReader();");

        Assert.Throws<JavaScriptException>(() => engine.Execute("r.readAsText('not a blob');"))!
            .Error.Get("name").AsString().Should().Be("TypeError");
    }

    /// <summary>
    /// A restore ends the cycle the read belongs to, so the events never arrive and the reader is left as it
    /// was — the same contract a promise registered before a restore has.
    /// </summary>
    [Test]
    public void ARestoreDropsAReadInFlight()
    {
        var engine = ReaderEngine();
        var snapshot = engine.Advanced.CaptureGlobalSnapshot();

        engine.Execute("""
            globalThis.order = [];
            globalThis.reader = new FileReader();
            reader.onloadend = function () { globalThis.order.push('loadend'); };
            reader.readAsText(new Blob(['abc']));
            """);

        engine.Advanced.RestoreGlobalSnapshot(snapshot);

        engine.Execute("globalThis.order = [];");
        Pump(engine);
        engine.Evaluate("order.length").AsNumber().Should().Be(0);
    }
}
#endif
