#if NET8_0_OR_GREATER
#nullable enable

using System.Threading;
using Jint.Runtime.Modules;
using Jint.WebApi;

namespace Jint.Tests.Wpt;

/// <summary>
/// The driver's <see cref="WorkerProvider"/>: it builds the engine that runs one worker-scoped
/// <c>.any.js</c> file, and hands the driver a live list of the connections to pump.
/// </summary>
/// <remarks>
/// <para>
/// <b>This is test infrastructure, not shipped code.</b> It is the simplest provider that can be correct for
/// a single-threaded driver, and it is deliberately the <i>cooperative</i> shape rather than the
/// thread-per-worker one: <see cref="OnWorkerStarted"/> starts nothing, and the driver's own pump loop calls
/// <c>ProcessTasks</c> on the parent and on every live worker in turn. That keeps the whole run on one thread,
/// so a suite's outcome cannot depend on how two schedulers happened to interleave — which is what a
/// conformance corpus needs and what <c>Tasks.WaitForScheduledWork</c> would only obscure here, since
/// nothing in this driver blocks waiting for a worker.
/// </para>
/// <para>
/// <b>Why the file runs inside a worker at all.</b> Every <c>.any.js</c> in wpt's <c>workers/</c> directory
/// that this corpus can reach carries <c>// META: global=worker</c> or <c>global=dedicatedworker</c> — there
/// is no window variant, because the file's whole subject is the worker global. Running such a file in the
/// driver's top-level engine, the way every other vendored suite is run, would assert nothing about workers:
/// <c>Worker-custom-event.any.js</c> would test the driver engine's own <c>addEventListener</c> and pass
/// without a worker existing. <see cref="WptHarness"/> therefore reads the <c>global=</c> key it otherwise
/// ignores and routes those files here — see its class remarks for the rule and for why no previously
/// vendored file changes lane.
/// </para>
/// <para>
/// <b>The file becomes the worker's module body.</b> Jint runs module workers only (the design's divergence
/// #2), so there is no classic script to evaluate and no <c>importScripts</c> to pull the harness in with.
/// The loader below serves one module whose source is the shim, then the file's <c>// META: script=</c>
/// helpers, then the file itself — the same three-step composition
/// <see cref="WptHarness"/> performs with three <c>Execute</c> calls for a top-level suite, and legitimate
/// because the shim installs everything it exports onto <c>globalThis</c> explicitly rather than relying on a
/// script's own <c>var</c> bindings. The one behavioural difference from a browser's classic worker is that
/// module code is strict, which is recorded in <c>Vendor/README.md</c>; no vendored file depends on sloppy
/// mode today.
/// </para>
/// <para>
/// <b>The second shape: a corpus worker.</b> <c>workers/modules/dedicated-worker-import.any.js</c> runs in the
/// <i>top-level</i> lane — it is a file about a page creating workers, so it carries no
/// <c>// META: global=</c> key — and the workers it creates run vendored corpus modules of their own, whose
/// whole subject is <c>import</c>. Constructed with a <see langword="null"/> module source, this provider
/// serves those: the loader resolves a specifier against the vendored tree exactly as the shim's
/// <c>fetch</c> does, so a worker's <c>./export-on-load-script.js</c> reaches the file beside it and a
/// specifier the corpus does not hold is a startup failure the parent hears as an <c>error</c> event — which
/// is what lets the two cases needing a wpt server be per-test exclusions rather than a dead file.
/// </para>
/// </remarks>
internal sealed class WptWorkerProvider : WorkerProvider
{
    private readonly string? _moduleSource;
    private readonly string _directory;
    private readonly WptDiagnosticsSink _sink;
    private readonly List<WorkerConnection> _live = [];

    /// <param name="moduleSource">
    /// The shim, the META helpers and the test file, already concatenated — or <see langword="null"/> when the
    /// workers run vendored corpus modules instead, which is the <c>workers/modules/</c> shape.
    /// </param>
    /// <param name="directory">The directory a worker-side <c>fetch()</c> resolves a corpus file against.</param>
    /// <param name="sink">The run's diagnostics recorder, shared with the parent engine.</param>
    internal WptWorkerProvider(string? moduleSource, string directory, WptDiagnosticsSink sink)
    {
        _moduleSource = moduleSource;
        _directory = directory;
        _sink = sink;
    }

    /// <summary>
    /// The connections that have started and not ended, in start order. Single-threaded by construction —
    /// every callback on this provider runs on the driver's own thread, because nothing else ever pumps.
    /// </summary>
    internal IReadOnlyList<WorkerConnection> Live => _live;

    /// <summary>
    /// Every connection that has started, live or not. The driver reads a file's results off the worker
    /// engine <i>after</i> the run, and a file that ends its own connection with <c>close()</c> would
    /// otherwise take the engine holding those results out of <see cref="Live"/> with it.
    /// </summary>
    internal List<WorkerConnection> Started { get; } = [];

    /// <summary>Every connection that has ended, with the reason, so a suite's teardown can be asserted.</summary>
    internal List<(string Name, WorkerEndReason Reason)> Ended { get; } = [];

    public override Engine? CreateWorkerEngine(WorkerRequest request)
    {
        var options = request.CreateDefaultOptions();
        options.Modules.ModuleLoader = _moduleSource is { } source
            ? new WptWorkerModuleLoader(request.Specifier, source)
            : new WptCorpusModuleLoader();

        // CreateDefaultOptions already installs DiagnosticsSink.Null, which is what makes a worker's callback
        // errors report-and-continue; this replaces it with the run's recorder so that the driver can hold a
        // worker-lane file to the same rule a top-level one is held to — see WptDiagnosticsSink.
        options.WebApi.Diagnostics.Sink = _sink;

        var engine = new Engine(options);

        // The same three things the driver gives its top-level engine, so a file's environment does not depend
        // on which lane it ran in: the fetch object model (Headers/Request/Response/FormData, which no feature
        // flag names on its own — see WptHarness), the shim's resource reader, and the file's own name, which
        // is what `setup({single_test: true})` names its one test after. The specifier *is* that name, which
        // is what makes this a one-liner rather than another constructor parameter.
        WebApiRegistration.InstallFetchModel(engine);
        WptHarness.InstallResourceReader(engine, _directory);
        engine.SetValue("__wptTestFile", request.Specifier);

        return engine;
    }

    public override void OnWorkerStarted(WorkerConnection connection)
    {
        Started.Add(connection);
        _live.Add(connection);
    }

    public override void OnWorkerEnded(WorkerConnection connection, WorkerEndReason reason)
    {
        _live.Remove(connection);
        Ended.Add((connection.Name, reason));
    }

    /// <summary>
    /// Serves the one module the worker loads, and refuses everything else.
    /// </summary>
    /// <remarks>
    /// A specifier this loader does not know is a vendoring bug rather than a test result — no vendored
    /// worker-scoped file imports anything — so it raises <see cref="ModuleResolutionException"/>, which the
    /// worker reports as a startup failure and the driver turns into a harness error naming the specifier.
    /// </remarks>
    private sealed class WptWorkerModuleLoader : IModuleLoader
    {
        private readonly string _specifier;
        private readonly string _source;

        internal WptWorkerModuleLoader(string specifier, string source)
        {
            _specifier = specifier;
            _source = source;
        }

        public ResolvedSpecifier Resolve(string? referencingModuleLocation, ModuleRequest moduleRequest)
        {
            if (!string.Equals(moduleRequest.Specifier, _specifier, StringComparison.Ordinal))
            {
                throw new ModuleResolutionException(
                    "The vendored web-platform-tests corpus has no such worker module",
                    moduleRequest.Specifier,
                    referencingModuleLocation,
                    filePath: null);
            }

            return new ResolvedSpecifier(moduleRequest, moduleRequest.Specifier, Uri: null, SpecifierType.RelativeOrAbsolute);
        }

        public Jint.Runtime.Modules.ModuleRecord LoadModule(Engine engine, ResolvedSpecifier resolved)
            => ModuleFactory.BuildSourceTextModule(engine, resolved, _source);
    }

    /// <summary>
    /// Serves modules out of the vendored corpus, which is what a <c>workers/modules/</c> worker runs.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Resolution is the shim's <c>fetch</c> rule, reused verbatim: a leading slash is the wpt root and
    /// anything else is relative to the importing module's own directory, with dot segments resolved. That is
    /// what makes <c>/workers/modules/resources/static-import-worker.js</c> — the specifier the test file hands
    /// <c>new Worker()</c> — and the <c>./export-on-load-script.js</c> it then imports both land on real
    /// vendored files, and it is the same <see cref="WptCorpus.ResolveReference"/> every other reference in the
    /// driver goes through, so a path that climbs out of the tree is refused here too.
    /// </para>
    /// <para>
    /// A specifier the corpus does not hold raises <see cref="ModuleResolutionException"/>, which the worker
    /// reports as a startup failure and the parent hears as an <c>error</c> event on its <c>Worker</c> object.
    /// That is deliberately <i>not</i> a harness error: two of the nine cases in
    /// <c>dedicated-worker-import.any.js</c> name fixtures that need a wpt server — a cross-origin
    /// <c>.sub.js</c> worker and a <c>redirect.py</c> handler — and reaching the test's own <c>onerror</c>
    /// reject path is what lets those two be named individually in the exclusion table while the other seven
    /// run.
    /// </para>
    /// </remarks>
    private sealed class WptCorpusModuleLoader : IModuleLoader
    {
        public ResolvedSpecifier Resolve(string? referencingModuleLocation, ModuleRequest moduleRequest)
        {
            string path;
            try
            {
                path = WptCorpus.ResolveReference(DirectoryOf(referencingModuleLocation), moduleRequest.Specifier);
            }
            catch (InvalidOperationException ex)
            {
                throw new ModuleResolutionException(
                    ex.Message, moduleRequest.Specifier, referencingModuleLocation, filePath: null);
            }

            if (!WptCorpus.Contains(path))
            {
                throw new ModuleResolutionException(
                    "The vendored web-platform-tests corpus has no such module",
                    moduleRequest.Specifier,
                    referencingModuleLocation,
                    filePath: null);
            }

            return new ResolvedSpecifier(moduleRequest, path, Uri: null, SpecifierType.RelativeOrAbsolute);
        }

        public Jint.Runtime.Modules.ModuleRecord LoadModule(Engine engine, ResolvedSpecifier resolved)
            => ModuleFactory.BuildSourceTextModule(engine, resolved, WptCorpus.Read(resolved.Key));

        /// <summary>
        /// The directory a relative specifier is resolved against: the importing module's own. The wpt root
        /// for the entry module, whose specifier the test file wrote as an absolute path.
        /// </summary>
        private static string DirectoryOf(string? location)
        {
            if (location is null)
            {
                return string.Empty;
            }

            var slash = location.LastIndexOf('/');
            return slash < 0 ? string.Empty : location.Substring(0, slash);
        }
    }
}
#endif
