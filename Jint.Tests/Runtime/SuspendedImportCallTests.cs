#nullable enable

using Jint.Native;
using Jint.Runtime.Modules;

namespace Jint.Tests.Runtime;

/// <summary>
/// A dynamic <c>import()</c> whose options argument suspends must resume in its options with the specifier it
/// already evaluated, and an <c>import()</c> whose operand suspends must not start a load at the suspension
/// (sebastienros/jint#4335).
/// <para>
/// EvaluateImportCall evaluates the specifier once, then the options, and only then converts the specifier to a
/// string and asks the host for the module (https://tc39.es/ecma262/#sec-evaluate-import-call). Jint resumes a
/// suspended body by replaying the statement it suspended in, and the import handler kept neither the evaluated
/// specifier nor where it was, so the replay evaluated the specifier again: every call and getter in it ran once
/// more per suspension in the options. And it stopped at a suspended operand only in a generator; in an async
/// function or an async generator it went on to start a load at the suspension, with the placeholder standing in
/// for the operand that had not been produced yet, so the host was asked for a module named <c>undefined</c>.
/// </para>
/// <para>
/// Every probe runs in a generator, an async function and an async generator (suspending at an <c>await</c>, and at
/// a <c>yield</c>), each <c>yield</c> resumed with its own operand, and returns
/// <c>&lt;report&gt;|&lt;log&gt;|&lt;requests&gt;</c>: the log is bracketed by <c>pre</c> and <c>post</c> so a body
/// that stops part-way shows, and the requests are the module requests the host's loader was handed, in order.
/// The expected report and log are what Node 26 prints. The tests named <c>…Control</c> pass on the unfixed tree
/// and bound the claim.
/// </para>
/// </summary>
public class SuspendedImportCallTests
{
    public enum Frame
    {
        Generator,
        Async,
        AsyncGeneratorAwait,
        AsyncGeneratorYield,
    }

    private const string Setup = """
        var log = [];
        var M = 'm';
        function spec(s) { log.push('spec'); return s === undefined ? 'm' : s; }
        function opts(o) { log.push('opts'); return o; }
        function f(x) { log.push('f'); return x; }
        var sp = { toString() { log.push('toString'); return 'm'; } };
        var bad = { toString() { log.push('toString'); throw new Error('bad specifier'); } };
        """;

    private const string ReportX = "p.then(function (ns) { return ns.x; })";

    /// <summary>
    /// Answers <c>m</c> with a source text module and <c>j</c> with a JSON module, and records every request it is
    /// handed, attributes included, so a load started with a placeholder shows. Like many hosts' loaders it throws
    /// for a name it does not know, and a synchronous loader's exception leaves the host's call rather than rejecting
    /// the import, so on the unfixed tree the request for <c>undefined</c> failed the whole <c>Evaluate</c>.
    /// </summary>
    private sealed class RecordingModuleLoader : IModuleLoader
    {
        public List<string> Requests { get; } = new();

        public ResolvedSpecifier Resolve(string? referencingModuleLocation, ModuleRequest moduleRequest)
        {
            var request = moduleRequest.Specifier;
            foreach (var attribute in moduleRequest.Attributes)
            {
                request += "{" + attribute.Key + "=" + attribute.Value + "}";
            }

            Requests.Add(request);
            return new ResolvedSpecifier(moduleRequest, moduleRequest.Specifier, Uri: null, SpecifierType.Bare);
        }

        public ModuleRecord LoadModule(Engine engine, ResolvedSpecifier resolved)
        {
            return resolved.ModuleRequest.Specifier switch
            {
                "m" => ModuleFactory.BuildSourceTextModule(engine, resolved, "export const x = 42;"),
                "j" => ModuleFactory.BuildJsonModule(engine, resolved, """{ "x": 42 }"""),
                _ => throw new InvalidOperationException("No module named '" + resolved.ModuleRequest.Specifier + "'."),
            };
        }
    }

    /// <summary>
    /// Runs <paramref name="body"/> in <paramref name="frame"/>, with every <c>SUSPEND</c> token replaced by that
    /// frame's suspension keyword, and returns <c>"&lt;report&gt;|&lt;log&gt;|&lt;requests&gt;"</c>. Both drivers
    /// stop after a fixed number of steps, so a body that never finishes fails the assertion rather than spinning.
    /// </summary>
    private static string Probe(Frame frame, string body, string report = ReportX)
    {
        var keyword = frame is Frame.Generator or Frame.AsyncGeneratorYield ? "yield" : "await";
        body = body.Replace("SUSPEND", keyword);

        var loader = new RecordingModuleLoader();
        var engine = new Engine(options => options.UseModules(loader));

        string script;
        if (frame == Frame.Generator)
        {
            script = $$"""
                {{Setup}}
                function* body() {
                  log.push('pre');
                  {{body}}
                  log.push('post');
                  return {{report}};
                }
                var it = body();
                var res = it.next();
                for (var i = 0; !res.done && i < 8; i++) res = it.next(res.value);
                var done = res.done;
                Promise.resolve(res.value).then(function (r) {
                  return String(r) + (done ? '' : ' (not done after 8 steps)') + '|' + log.join(',');
                });
                """;
        }
        else if (frame == Frame.Async)
        {
            script = $$"""
                {{Setup}}
                async function body() {
                  log.push('pre');
                  {{body}}
                  log.push('post');
                  return {{report}};
                }
                body().then(function (r) { return String(r) + '|' + log.join(','); });
                """;
        }
        else
        {
            script = $$"""
                {{Setup}}
                async function* body() {
                  log.push('pre');
                  {{body}}
                  log.push('post');
                  return {{report}};
                }
                async function drive() {
                  var it = body();
                  var res = await it.next();
                  for (var i = 0; !res.done && i < 8; i++) res = await it.next(res.value);
                  return String(res.value) + (res.done ? '' : ' (not done after 8 steps)') + '|' + log.join(',');
                }
                drive();
                """;
        }

        var result = engine.Evaluate(script).UnwrapIfPromise().AsString();
        return result + "|" + string.Join(",", loader.Requests);
    }

    // ------------------------------------------------------------------ the reported shape

    /// <summary>
    /// The issue's own shape. On the unfixed tree the replay evaluated the specifier again, and in an async frame
    /// the handler also went on past the suspended options and started a load with no options at all.
    /// </summary>
    [TestCase(Frame.Generator)]
    [TestCase(Frame.Async)]
    [TestCase(Frame.AsyncGeneratorAwait)]
    [TestCase(Frame.AsyncGeneratorYield)]
    public void TheSpecifierIsEvaluatedOnceWhenTheOptionsSuspend(Frame frame)
    {
        Probe(frame, "var p = import(spec(), (SUSPEND {}));")
            .Should().Be("42|pre,spec,post|m");
    }

    /// <summary>
    /// The issue's repro verbatim, in an async function: <c>f()</c> resolves to 1 in Node.
    /// </summary>
    [Test]
    public void TheIssueReproCallsTheSpecifierOnce()
    {
        var engine = new Engine(options => options.UseModules(new RecordingModuleLoader()));
        engine.Evaluate("""
            let calls = 0;
            const spec = () => { calls++; return 'm'; };
            async function f() {
              try { await import(spec(), await Promise.resolve({})); } catch {}
              return calls;
            }
            f();
            """).UnwrapIfPromise().AsInteger().Should().Be(1);
    }

    /// <summary>
    /// EvaluateImportCall's order: the specifier, then the options, and only once both are in hand the specifier's
    /// <c>ToString</c>. The resume must convert the specifier it kept, once, after the options.
    /// </summary>
    [TestCase(Frame.Generator)]
    [TestCase(Frame.Async)]
    [TestCase(Frame.AsyncGeneratorAwait)]
    [TestCase(Frame.AsyncGeneratorYield)]
    public void TheKeptSpecifierIsConvertedToAStringOnceAfterTheOptions(Frame frame)
    {
        Probe(frame, "var p = import((log.push('spec'), sp), (log.push('opts'), SUSPEND undefined));")
            .Should().Be("42|pre,spec,opts,toString,post|m");
    }

    /// <summary>
    /// A <c>ToString</c> that throws rejects the promise, and it must do so once, on the resume; the unfixed tree
    /// also ran it at the suspension in an async frame.
    /// </summary>
    [TestCase(Frame.Generator)]
    [TestCase(Frame.Async)]
    [TestCase(Frame.AsyncGeneratorAwait)]
    [TestCase(Frame.AsyncGeneratorYield)]
    public void ASpecifierWhoseToStringThrowsRejectsOnce(Frame frame)
    {
        Probe(frame,
                "var p = import(bad, (log.push('opts'), SUSPEND undefined));",
                "p.then(function (ns) { return ns.x; }, function (e) { return e.message; })")
            .Should().Be("bad specifier|pre,opts,toString,post|");
    }

    /// <summary>
    /// Two suspensions in the options: the specifier must survive the first resume and still be there for the
    /// second.
    /// </summary>
    [TestCase(Frame.Generator)]
    [TestCase(Frame.Async)]
    [TestCase(Frame.AsyncGeneratorAwait)]
    [TestCase(Frame.AsyncGeneratorYield)]
    public void TheSpecifierIsEvaluatedOnceAcrossTwoSuspensionsInTheOptions(Frame frame)
    {
        Probe(frame, "var p = import(spec(), (SUSPEND 0, SUSPEND {}));")
            .Should().Be("42|pre,spec,post|m");
    }

    // ------------------------------------------------------------------ the options object

    /// <summary>
    /// A suspension nested in the <c>with</c> object, which is where an import attribute's value is written.
    /// </summary>
    [TestCase(Frame.Generator)]
    [TestCase(Frame.Async)]
    [TestCase(Frame.AsyncGeneratorAwait)]
    [TestCase(Frame.AsyncGeneratorYield)]
    public void ASuspensionInsideTheWithObjectEvaluatesTheSpecifierOnce(Frame frame)
    {
        Probe(frame,
                "var p = import(spec('j'), { with: { type: SUSPEND 'json' } });",
                "p.then(function (ns) { return ns.default.x; })")
            .Should().Be("42|pre,spec,post|j{type=json}");
    }

    [TestCase(Frame.Generator)]
    [TestCase(Frame.Async)]
    [TestCase(Frame.AsyncGeneratorAwait)]
    [TestCase(Frame.AsyncGeneratorYield)]
    public void ASuspendedWithObjectEvaluatesTheSpecifierOnce(Frame frame)
    {
        Probe(frame,
                "var p = import(spec('j'), { with: (SUSPEND { type: 'json' }) });",
                "p.then(function (ns) { return ns.default.x; })")
            .Should().Be("42|pre,spec,post|j{type=json}");
    }

    /// <summary>
    /// Options that resume as a non-object reject the promise with a <c>TypeError</c>, and ask the host for
    /// nothing.
    /// </summary>
    [TestCase(Frame.Generator)]
    [TestCase(Frame.Async)]
    [TestCase(Frame.AsyncGeneratorAwait)]
    [TestCase(Frame.AsyncGeneratorYield)]
    public void OptionsThatResumeAsANonObjectRejectWithoutALoad(Frame frame)
    {
        Probe(frame,
                "var p = import(spec(), (SUSPEND 1));",
                "p.then(function (ns) { return ns.x; }, function (e) { return e.constructor.name; })")
            .Should().Be("TypeError|pre,spec,post|");
    }

    // ------------------------------------------------------------------ a suspension in the specifier

    /// <summary>
    /// Nothing is resolved before the specifier, so a resume into it needs nothing kept; but the handler must
    /// stop there. The unfixed tree stopped only in a generator, and in an async frame went on to ask the host
    /// for a module named <c>undefined</c>, the placeholder an <c>await</c> suspends with.
    /// </summary>
    [TestCase(Frame.Generator)]
    [TestCase(Frame.Async)]
    [TestCase(Frame.AsyncGeneratorAwait)]
    [TestCase(Frame.AsyncGeneratorYield)]
    public void ASuspensionInTheSpecifierStartsNoLoadAtTheSuspension(Frame frame)
    {
        Probe(frame, "var p = import((SUSPEND 0, spec()), opts({}));")
            .Should().Be("42|pre,spec,opts,post|m");
    }

    [TestCase(Frame.Generator)]
    [TestCase(Frame.Async)]
    [TestCase(Frame.AsyncGeneratorAwait)]
    [TestCase(Frame.AsyncGeneratorYield)]
    public void ASuspensionInTheSpecifierAndThenInTheOptionsEvaluatesEachOnce(Frame frame)
    {
        Probe(frame, "var p = import((spec(), SUSPEND M), (opts(), SUSPEND {}));")
            .Should().Be("42|pre,spec,opts,post|m");
    }

    /// <summary>
    /// An async generator that awaits in the specifier and then yields in the options: the yield clears the
    /// completed-await memo, so a replay of the specifier would await again.
    /// </summary>
    [Test]
    public void AnAsyncGeneratorThatAwaitsInTheSpecifierAndYieldsInTheOptionsFinishes()
    {
        var loader = new RecordingModuleLoader();
        var engine = new Engine(options => options.UseModules(loader));
        engine.Evaluate($$"""
            {{Setup}}
            async function* ag() { log.push('pre'); var p = import((spec(), await M), yield {}); log.push('post'); return p.then(function (ns) { return ns.x; }); }
            async function drive() {
              var it = ag();
              var res = await it.next();
              for (var i = 0; !res.done && i < 8; i++) res = await it.next(res.value);
              return String(res.value) + (res.done ? '' : ' (not done after 8 steps)') + '|' + log.join(',');
            }
            drive();
            """).UnwrapIfPromise().AsString().Should().Be("42|pre,spec,post");
        string.Join(",", loader.Requests).Should().Be("m");
    }

    // ------------------------------------------------------------------ enclosing expressions

    [TestCase(Frame.Generator)]
    [TestCase(Frame.Async)]
    [TestCase(Frame.AsyncGeneratorAwait)]
    [TestCase(Frame.AsyncGeneratorYield)]
    public void AnImportInACallArgumentEvaluatesItsSpecifierOnce(Frame frame)
    {
        Probe(frame, "var p = f(import(spec(), (SUSPEND {})));")
            .Should().Be("42|pre,spec,f,post|m");
    }

    [TestCase(Frame.Generator)]
    [TestCase(Frame.Async)]
    [TestCase(Frame.AsyncGeneratorAwait)]
    [TestCase(Frame.AsyncGeneratorYield)]
    public void AnImportThatIsAMemberCallsReceiverEvaluatesItsSpecifierOnce(Frame frame)
    {
        Probe(frame, "var p = import(spec(), (SUSPEND {})).then(function (ns) { log.push('then'); return ns; });")
            .Should().Be("42|pre,spec,post,then|m");
    }

    /// <summary>
    /// An import with no suspension at all, in a frame that suspends elsewhere: the bound for the claim.
    /// </summary>
    [TestCase(Frame.Generator)]
    [TestCase(Frame.Async)]
    [TestCase(Frame.AsyncGeneratorAwait)]
    [TestCase(Frame.AsyncGeneratorYield)]
    public void AnImportThatCannotSuspendNextToOneThatDoesControl(Frame frame)
    {
        Probe(frame, "SUSPEND 0; var p = import(spec(), opts({})); SUSPEND 1;")
            .Should().Be("42|pre,spec,opts,post|m");
    }

    // ------------------------------------------------------------------ abrupt resumptions

    /// <summary>
    /// A <c>throw()</c> at the yield in the options leaves the import without a load, and the next evaluation of
    /// the same site evaluates its own specifier rather than the one the abandoned evaluation kept.
    /// </summary>
    [Test]
    public void AThrowResumptionInTheOptionsLeavesNothingForTheNextEvaluation()
    {
        var loader = new RecordingModuleLoader();
        var engine = new Engine(options => options.UseModules(loader));
        engine.Evaluate($$"""
            {{Setup}}
            function* g() {
              for (var i = 0; i < 2; i++) {
                try { var p = import(spec(), yield i); log.push('ok' + i); } catch (e) { log.push('caught' + i); }
              }
              return p;
            }
            var it = g();
            it.next();
            it.throw(new Error('x'));
            var r = it.next({});
            r.value.then(function (ns) { return ns.x + '|' + log.join(',') + '|' + r.done; });
            """).UnwrapIfPromise().AsString().Should().Be("42|spec,caught0,spec,ok1|true");
        string.Join(",", loader.Requests).Should().Be("m");
    }

    [Test]
    public void AThrowResumptionInAnAsyncGeneratorsOptionsLeavesNothingForTheNextEvaluation()
    {
        var loader = new RecordingModuleLoader();
        var engine = new Engine(options => options.UseModules(loader));
        engine.Evaluate($$"""
            {{Setup}}
            async function* g() {
              for (var i = 0; i < 2; i++) {
                try { var p = import(spec(), yield i); log.push('ok' + i); } catch (e) { log.push('caught' + i); }
              }
              return p;
            }
            async function drive() {
              var it = g();
              await it.next();
              await it.throw(new Error('x'));
              var r = await it.next({});
              return r.value.x + '|' + log.join(',') + '|' + r.done;
            }
            drive();
            """).UnwrapIfPromise().AsString().Should().Be("42|spec,caught0,spec,ok1|true");
        string.Join(",", loader.Requests).Should().Be("m");
    }

    [Test]
    public void ARejectedAwaitInTheOptionsLeavesNothingForTheNextEvaluation()
    {
        var loader = new RecordingModuleLoader();
        var engine = new Engine(options => options.UseModules(loader));
        engine.Evaluate($$"""
            {{Setup}}
            async function g() {
              for (var i = 0; i < 2; i++) {
                try {
                  var ns = await import(spec(), i === 0 ? await Promise.reject(new Error('r')) : await {});
                  log.push('ok' + i);
                } catch (e) { log.push('caught' + i); }
              }
              return ns.x + '|' + log.join(',');
            }
            g();
            """).UnwrapIfPromise().AsString().Should().Be("42|spec,caught0,spec,ok1");
        string.Join(",", loader.Requests).Should().Be("m");
    }

    [TestCase(false)]
    [TestCase(true)]
    public void AReturnResumptionInTheOptionsLeavesWithoutALoad(bool async)
    {
        var loader = new RecordingModuleLoader();
        var engine = new Engine(options => options.UseModules(loader));
        var star = async ? "async function*" : "function*";
        var wait = async ? "await " : "";
        engine.Evaluate($$"""
            {{Setup}}
            {{star}} g() { try { var p = import(spec(), yield 0); log.push('after'); } finally { log.push('finally'); } }
            async function drive() {
              var it = g();
              {{wait}}it.next();
              var r = {{wait}}it.return(5);
              return r.value + '|' + log.join(',') + '|' + r.done;
            }
            drive();
            """).UnwrapIfPromise().AsString().Should().Be("5|spec,finally|true");
        loader.Requests.Should().BeEmpty();
    }

    /// <summary>
    /// A <c>return()</c> at a <c>yield</c> statement runs the <c>finally</c> blocks, and an <c>import()</c> in one
    /// that cannot suspend is an ordinary call there: it returns a promise and loads the module. The handler that
    /// cannot suspend no longer tests the generator's state at all, so this bounds what dropping that test changed.
    /// </summary>
    [TestCase(false)]
    [TestCase(true)]
    public void AnImportInAFinallyBlockThatAReturnRunsLoadsControl(bool async)
    {
        var loader = new RecordingModuleLoader();
        var engine = new Engine(options => options.UseModules(loader));
        var star = async ? "async function*" : "function*";
        var wait = async ? "await " : "";
        engine.Evaluate($$"""
            {{Setup}}
            var p;
            {{star}} g() { try { yield 1; } finally { log.push('finally'); p = import(spec()); log.push(typeof p); } }
            async function drive() {
              var it = g();
              {{wait}}it.next();
              var r = {{wait}}it.return(5);
              return r.value + '|' + log.join(',') + '|' + (await p).x;
            }
            drive();
            """).UnwrapIfPromise().AsString().Should().Be("5|finally,spec,object|42");
        string.Join(",", loader.Requests).Should().Be("m");
    }

    // ------------------------------------------------------------------ modules and phases

    /// <summary>
    /// A module's top-level <c>await</c> suspends the module body, which resumes the same way.
    /// </summary>
    [Test]
    public void ATopLevelAwaitInTheOptionsEvaluatesTheSpecifierOnce()
    {
        var loader = new RecordingModuleLoader();
        var engine = new Engine(options => options.UseModules(loader));
        engine.Modules.Add("main", """
            globalThis.log = [];
            const spec = () => { log.push('spec'); return 'm'; };
            log.push('pre');
            const ns = await import(spec(), await {});
            log.push('post');
            globalThis.result = ns.x + '|' + log.join(',');
            """);

        engine.Modules.Import("main");

        engine.Evaluate("globalThis.result").AsString().Should().Be("42|pre,spec,post");
        string.Join(",", loader.Requests).Should().Be("main,m");
    }

    /// <summary>
    /// A phase import takes a specifier and no options, so a suspension can only be in the specifier, and the
    /// handler must stop there rather than ask the host for a module named <c>undefined</c>. Node 26 has no
    /// <c>import.defer</c> and rejects every source phase import of a source text module, so the expected answers
    /// come from the proposals: a deferred namespace
    /// (https://tc39.es/proposal-defer-import-eval/#sec-ContinueDynamicImport), and a <c>SyntaxError</c> for a module
    /// with no source object (https://tc39.es/proposal-source-phase-imports/#sec-ContinueDynamicImport).
    /// </summary>
    [TestCase("defer", "Deferred Module")]
    [TestCase("source", "SyntaxError")]
    public void APhaseImportWhoseSpecifierSuspendsStartsNoLoadAtTheSuspension(string phase, string expected)
    {
        var loader = new RecordingModuleLoader();
        var engine = new Engine(options => options.UseModules(loader));
        engine.Evaluate($$"""
            {{Setup}}
            async function g() {
              var p = import.{{phase}}((await 0, spec()));
              return p.then(function (ns) { return ns[Symbol.toStringTag]; }, function (e) { return e.constructor.name; });
            }
            g().then(function (r) { return r + '|' + log.join(','); });
            """).UnwrapIfPromise().AsString().Should().Be(expected + "|spec");
        string.Join(",", loader.Requests).Should().Be("m");
    }
}
