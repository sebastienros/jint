#nullable enable

using System.Collections;
using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime.Interop;

namespace Jint.Tests.Runtime.Interop;

/// <summary>
/// Pins step 1 of <a href="https://tc39.es/ecma262/#sec-getiterator">GetIterator</a> for a wrapped CLR collection:
/// <c>@@iterator</c> is read through <a href="https://tc39.es/ecma262/#sec-getmethod">GetMethod</a>, so an own
/// <c>Symbol.iterator</c> a script gave the wrapper is the method
/// <a href="https://tc39.es/ecma262/#sec-getiteratorfrommethod">GetIteratorFromMethod</a> calls. That holds for spread
/// (<a href="https://tc39.es/ecma262/#sec-runtime-semantics-arrayaccumulation">ArrayAccumulation</a>,
/// <a href="https://tc39.es/ecma262/#sec-runtime-semantics-argumentlistevaluation">ArgumentListEvaluation</a>) and for
/// array destructuring in every position
/// (<a href="https://tc39.es/ecma262/#sec-runtime-semantics-iteratorbindinginitialization">IteratorBindingInitialization</a>,
/// <a href="https://tc39.es/ecma262/#sec-runtime-semantics-iteratordestructuringassignmentevaluation">IteratorDestructuringAssignmentEvaluation</a>).
/// <para>
/// The destructuring lane reads a wrapped indexed collection by index whenever <c>ObjectWrapper.HasOriginalIterator</c>
/// says that is what its iterator would yield, and that check never looked at the wrapper's own <c>Symbol.iterator</c>,
/// so a script's method there was ignored (sebastienros/jint#4427). Spread and the parameter lane only read by index for
/// a <see cref="JsArray"/>, and are controls here, as are <c>for…of</c> and <c>Array.from</c>.
/// </para>
/// <para>
/// The expected values are Node 26's over the array <c>[1, 2]</c>, which is a subject here too. Node has no CLR
/// collection, and the wrapper answers what an array answers: its own <c>Symbol.iterator</c> is a property it
/// materializes on the first read, so deleting what a script put there brings the CLR one back where an array falls
/// back to <c>Array.prototype</c>'s, and both iterate <c>1, 2</c>.
/// </para>
/// </summary>
/// <remarks>
/// Every case builds its own <see cref="Engine"/> and the fixture holds no mutable state, so its cases run in parallel
/// over one prepared harness.
/// </remarks>
[Parallelizable(ParallelScope.All)]
public class WrappedCollectionOwnIteratorTests
{
    private const string Harness = """
        // Counts the calls of the script's own iterator method, and the reads of an accessor holding it.
        const counts = { calls: 0, reads: 0 };
        function* own() { counts.calls++; yield 9; yield 8; }
        function* one(v) { yield v; }

        // Every construct that takes an iterator from its subject, each answering the two values it produced, joined.
        const lanes = {
            arraySpread: x => [...x].join(','),
            callSpread: x => ((...a) => a)(...x).join(','),
            newSpread: x => new (function (...a) { this.a = a; })(...x).a.join(','),
            generatorSpread: x => (function* () { yield [...x].join(','); })().next().value,
            asyncSpread: x => (async function () { await 0; return [...x].join(','); })(),
            varPattern: x => { var [a, b] = x; return a + ',' + b; },
            letPattern: x => { let [a, b] = x; return a + ',' + b; },
            constRest: x => { const [...r] = x; return r.join(','); },
            nestedInObjectPattern: x => { const { p: [a, b] } = { p: x }; return a + ',' + b; },
            forOfHead: x => { let out; for (const [a, b] of one(x)) { out = a + ',' + b; } return out; },
            catchParameter: x => { try { throw x; } catch ([a, b]) { return a + ',' + b; } },
            generatorPattern: x => (function* () { const [a, b] = x; yield a + ',' + b; })().next().value,
            asyncPattern: x => (async function () { await 0; const [a, b] = x; return a + ',' + b; })(),
            assignment: x => { let a, b; [a, b] = x; return a + ',' + b; },
            assignmentRest: x => { let r; [...r] = x; return r.join(','); },
            forOfHeadAssignment: x => { let a, b; for ([a, b] of one(x)) { } return a + ',' + b; },
            parameter: x => (([a, b]) => a + ',' + b)(x),
            parameterDefault: x => (function ([a, b] = x) { return a + ',' + b; })(),
            generatorParameter: x => (function* ([a, b]) { yield a + ',' + b; })(x).next().value,
            asyncArrowParameter: x => (async ([a, b]) => a + ',' + b)(x),
            // Already took the iterator through GetIterator.
            forOf: x => { const out = []; for (const v of x) { out.push(v); } return out.join(','); },
            arrayFrom: x => Array.from(x).join(','),
        };

        // Gives `x` the own Symbol.iterator `state` names:
        //   none      nothing.
        //   assigned  the script's method, by assignment.
        //   defined   the script's method, by Object.defineProperty.
        //   getter    an accessor answering the script's method.
        //   deleted   the script's method, defined and then deleted.
        //   restored  the script's method, defined, and then what `x[Symbol.iterator]` read before that defined back.
        function install(state, x) {
            const define = value => Object.defineProperty(x, Symbol.iterator, { value, writable: true, configurable: true });
            switch (state) {
                case 'none': return;
                case 'assigned': x[Symbol.iterator] = own; return;
                case 'defined': define(own); return;
                case 'getter': Object.defineProperty(x, Symbol.iterator, { get() { counts.reads++; return own; }, configurable: true }); return;
                case 'deleted': define(own); delete x[Symbol.iterator]; return;
                case 'restored': { const original = x[Symbol.iterator]; define(own); define(original); return; }
            }
            throw new Error(state);
        }

        // Runs the lane over `x` after `install` and answers "<values> <calls>/<reads>". `setting` says what happened to
        // `x` first: nothing ('cold'), the lane once ('warm'), or a for-of over it ('iterated').
        async function run(lane, state, setting, x) {
            if (setting === 'warm') { await lanes[lane](x); }
            if (setting === 'iterated') { for (const v of x) { } }
            install(state, x);
            let result;
            try { result = await lanes[lane](x); }
            catch (e) { result = e.name + ': ' + e.message; }
            return result + ' ' + counts.calls + '/' + counts.reads;
        }
        """;

    private static readonly Prepared<Script> PreparedHarness = Engine.PrepareScript(Harness);

    private static readonly string[] LaneNames =
    [
        "arraySpread", "callSpread", "newSpread", "generatorSpread", "asyncSpread",
        "varPattern", "letPattern", "constRest", "nestedInObjectPattern", "forOfHead", "catchParameter", "generatorPattern", "asyncPattern",
        "assignment", "assignmentRest", "forOfHeadAssignment",
        "parameter", "parameterDefault", "generatorParameter", "asyncArrowParameter",
        "forOf", "arrayFrom",
    ];

    // Node 26 over [1, 2], for every lane alike: the built-in iterator, or the script's method called once.
    private static readonly Dictionary<string, string> States = new()
    {
        ["none"] = "1,2 0/0",
        ["assigned"] = "9,8 1/0",
        ["defined"] = "9,8 1/0",
        ["getter"] = "9,8 1/1",
        ["deleted"] = "1,2 0/0",
        ["restored"] = "1,2 0/0",
    };

    // The script expression each subject is reached by. Every one iterates 1, 2.
    private static readonly string[] Subjects =
    [
        "[1, 2]",
        "list",
        "arrayList",
        "liveArray",
        "plainWrapper",
        "disposableList",
    ];

    // Every lane over every subject, under every state, on the lane's first run, after one ("warm"), and after a for-of
    // ("iterated"), which is what materializes a wrapper's own Symbol.iterator.
    public static TestCases<string, string, string, string> EveryLaneOverEverySubject
    {
        get
        {
            var rows = new TestCases<string, string, string, string>();
            foreach (var subject in Subjects)
            {
                foreach (var lane in LaneNames)
                {
                    foreach (var state in States)
                    {
                        foreach (var setting in new[] { "cold", "warm", "iterated" })
                        {
                            rows.Add(subject, lane, state.Key + " " + setting, state.Value);
                        }
                    }
                }
            }

            return rows;
        }
    }

    /// <summary>
    /// An engine that lets script write through a wrapper, so that an assignment can install an own
    /// <c>Symbol.iterator</c>, with the five wrapped subjects: a <see cref="List{T}"/>, an <see cref="ArrayList"/> (an
    /// <see cref="IList"/>), a live <c>int[]</c>, a disposable list, whose wrapper holds <c>Symbol.dispose</c> from the
    /// start, and a <see cref="List{T}"/> in a plain <see cref="ObjectWrapper"/> rather than an array-like view. Under a
    /// JIT no exposure of an indexed collection builds that last shape any more, so the internal constructor builds it,
    /// which pins the base class's own check beside the views'.
    /// </summary>
    private static Engine CreateEngine(Action<Options>? configure = null)
    {
        var engine = new Engine(options =>
        {
            options.Interop.AllowWrite = true;
            options.Interop.ArrayConversion = ArrayConversionMode.LiveView;
            configure?.Invoke(options);
        });
        engine.SetValue("list", new List<int> { 1, 2 });
        engine.SetValue("arrayList", new ArrayList { 1, 2 });
        engine.SetValue("liveArray", new[] { 1, 2 });
        engine.SetValue("plainWrapper", new ObjectWrapper(engine, new List<int> { 1, 2 }));
        engine.SetValue("disposableList", new DisposableList { 1, 2 });
        return engine;
    }

    private static string Run(Engine engine, string script)
    {
        engine.Execute(PreparedHarness);
        return engine.Evaluate(script).UnwrapIfPromise().AsString();
    }

    /// <summary>
    /// The issue's probe, with a destructuring beside its spread: the spread already called the script's method, and
    /// the destructuring read the list by index.
    /// </summary>
    [Test]
    public void TheIssueProbeCallsTheOwnIterator()
    {
        var engine = CreateEngine();
        engine.Evaluate("""
            list[Symbol.iterator] = function* () { yield 9; };
            const [a] = list;
            [a, ...list].join(',')
            """).AsString().Should().Be("9,9");
    }

    /// <summary>
    /// The migration guide's example, under the default options, where only Object.defineProperty can install one.
    /// </summary>
    [Test]
    public void TheMigrationGuideExampleHolds()
    {
        var engine = new Engine();
        engine.SetValue("list", new List<int> { 1, 2 });
        engine.Evaluate("""
            Object.defineProperty(list, Symbol.iterator, { value: function* () { yield 9; }, configurable: true });
            const [a] = list;
            [a, ...list].join(',')
            """).AsString().Should().Be("9,9");
    }

    /// <summary>
    /// Each lane calls the own <c>Symbol.iterator</c> a script gave its subject, however it was installed, and iterates
    /// the collection once it is gone again, before and after the lane has run once and after a <c>for…of</c> has
    /// materialized the wrapper's own iterator.
    /// </summary>
    [TestCaseSource(nameof(EveryLaneOverEverySubject))]
    public void EveryLaneCallsTheOwnIterator(string subject, string lane, string stateAndSetting, string expected)
    {
        var parts = stateAndSetting.Split(' ');
        Run(CreateEngine(), $"run('{lane}', '{parts[0]}', '{parts[1]}', {subject})").Should().Be(expected);
    }

    /// <summary>
    /// The subjects are the wrapper shapes they are named for: every wrapped one takes the destructuring lane's check,
    /// which a plain <see cref="ObjectWrapper"/> and an array-like view answer alike.
    /// </summary>
    [Test]
    public void TheSubjectsAreTheWrapperShapesTheyAreNamedFor()
    {
        var engine = CreateEngine();
        engine.GetValue("plainWrapper").Should().BeOfType<ObjectWrapper>();
        foreach (var name in new[] { "list", "arrayList", "liveArray", "disposableList" })
        {
            engine.GetValue(name).Should().BeAssignableTo<ObjectWrapper>(name);
            engine.GetValue(name).GetType().Should().NotBe(typeof(ObjectWrapper), name);
        }

        ((ObjectInstance) engine.GetValue("disposableList"))._symbols.Should().NotBeNull();
        ((ObjectInstance) engine.GetValue("list"))._symbols.Should().BeNull();
    }

    /// <summary>
    /// The check every destructuring lane over a wrapper asks, asked directly: true while the wrapper's
    /// <c>Symbol.iterator</c> is absent or its own CLR one, false while a script's method or an accessor is there, and
    /// true again once that is deleted or the original is put back. It reads nothing, so it hands the caller's
    /// GetIterator nothing.
    /// </summary>
    [TestCase("list")]
    [TestCase("arrayList")]
    [TestCase("liveArray")]
    [TestCase("plainWrapper")]
    [TestCase("disposableList")]
    public void TheCheckFollowsTheOwnIterator(string name)
    {
        var engine = CreateEngine();
        engine.Execute(PreparedHarness);
        var subject = (ObjectInstance) engine.GetValue(name);

        void Check(bool expected, string because)
        {
            subject.HasOriginalIterator(out var iteratorMethod).Should().Be(expected, because);
            iteratorMethod.Should().BeNull(because);
        }

        Check(true, "fresh");
        engine.Execute($"for (const v of {name}) {{ }}");
        Check(true, "after a for-of materialized the wrapper's own iterator");
        engine.Execute($"const original = {name}[Symbol.iterator]; {name}[Symbol.iterator] = own;");
        Check(false, "assigned");
        engine.Execute($"{name}[Symbol.iterator] = original;");
        Check(true, "assigned back");
        engine.Execute($"Object.defineProperty({name}, Symbol.iterator, {{ value: own }});");
        Check(false, "defined");
        engine.Execute($"delete {name}[Symbol.iterator];");
        Check(true, "deleted");
        engine.Execute($"Object.defineProperty({name}, Symbol.iterator, {{ get() {{ return original; }}, configurable: true }});");
        Check(false, "an accessor, whose getter GetIterator has to call even when it answers the original");
        engine.Execute($"delete {name}[Symbol.iterator]; {name}[Symbol.iterator] = 42;");
        Check(false, "not callable");
        engine.SetValue("otherList", new List<int> { 7 });
        engine.Execute($"{name}[Symbol.iterator] = otherList[Symbol.iterator];");
        Check(true, "another wrapper's own iterator, which enumerates whatever wrapper it is called on");
        engine.Evaluate($"{{ const [a, b] = {name}; a + ',' + b }}").AsString().Should().Be("1,2");
    }

    /// <summary>
    /// The accessor's getter runs once, as GetIterator's one read, and the iterator it answers is the one stepped, even
    /// when it answers the wrapper's own CLR iterator.
    /// </summary>
    [TestCase("list")]
    [TestCase("plainWrapper")]
    public void AnAccessorIsReadOnce(string name)
    {
        var engine = CreateEngine();
        engine.Evaluate($$"""
            const original = {{name}}[Symbol.iterator];
            let reads = 0;
            Object.defineProperty({{name}}, Symbol.iterator, { get() { reads++; return original; }, configurable: true });
            const [a, b] = {{name}};
            a + ',' + b + ' ' + reads
            """).AsString().Should().Be("1,2 1");
    }

    /// <summary>
    /// Under the default options a script cannot write through a wrapper, so an assignment to its
    /// <c>Symbol.iterator</c> is refused - a no-op in sloppy code, a TypeError in strict - and Object.defineProperty,
    /// which redefines the wrapper's own configurable property, still installs one, which every lane then calls.
    /// </summary>
    [TestCase("varPattern", "defined", "9,8 1/0")]
    [TestCase("assignment", "defined", "9,8 1/0")]
    [TestCase("parameter", "defined", "9,8 1/0")]
    [TestCase("arraySpread", "defined", "9,8 1/0")]
    [TestCase("varPattern", "getter", "9,8 1/1")]
    [TestCase("assignment", "getter", "9,8 1/1")]
    [TestCase("varPattern", "assigned", "1,2 0/0")]
    [TestCase("arraySpread", "assigned", "1,2 0/0")]
    public void WithoutWritesDefinePropertyStillInstallsOne(string lane, string state, string expected)
    {
        var engine = CreateEngine(options => options.Interop.AllowWrite = false);
        Run(engine, $"run('{lane}', '{state}', 'cold', list)").Should().Be(expected);
    }

    private sealed class DisposableList : List<int>, IDisposable
    {
        public void Dispose()
        {
        }
    }
}
