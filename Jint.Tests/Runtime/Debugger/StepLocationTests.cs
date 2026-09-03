#nullable enable

using System.Globalization;
using Jint.Runtime.Debugger;

namespace Jint.Tests.Runtime.Debugger;

/// <summary>
/// <see cref="DebugHandler.GetStepLocations(Program)"/> held to the runtime it describes: for each corpus
/// script the set of positions the engine actually pauses at, stepping with
/// <see cref="StepMode.Into"/> through a run that takes every branch, must equal the set the static walk
/// reports.
/// </summary>
/// <remarks>
/// <para>
/// The runtime is the oracle, and the corpus is written so that it can be: every function is called, every
/// conditional arm is taken, every loop iterates, every <c>try</c> throws. A location the walk reports for
/// code no run reaches would be a false positive nothing here could distinguish from a defect, so there is
/// no unreachable code in the corpus.
/// </para>
/// <para>
/// One known divergence is deliberately kept out of it: code reached through <c>eval</c>, which is a
/// program of its own. A derived class without an explicit constructor is in the corpus instead - the
/// engine runs its implicit <c>super(...args)</c> from a separately parsed AST and steps over the whole
/// of it, which <see cref="AnImplicitDerivedConstructorIsSteppedOverAsOneUnit"/> states directly.
/// </para>
/// </remarks>
public class StepLocationTests
{
    private const string SourceName = "corpus.js";

    /// <summary>
    /// One pause, as the two sides describe it: what <see cref="DebugInformation"/> reports at run time and
    /// what <see cref="StepLocation"/> reports statically are compared through this projection.
    /// </summary>
    private static StepLocation Describe(DebugInformation info)
    {
        var location = info.Location;
        var kind = info.CurrentNode switch
        {
            // OnReturnPoint passes no node; the location is the end of the function body.
            null => StepLocationKind.Return,
            DebuggerStatement => StepLocationKind.DebuggerStatement,
            _ => StepLocationKind.Statement,
        };

        return new StepLocation(location.SourceFile, location.Start.Line, location.Start.Column, kind);
    }

    private sealed class Recorder
    {
        private readonly List<Program> _programs = new();
        private readonly HashSet<StepLocation> _visited = new();

        public Engine Engine { get; }

        public Recorder()
        {
            Engine = new Engine(options =>
            {
                options.Debugger.Enabled = true;
                options.Debugger.InitialStepMode = StepMode.Into;
                options.Debugger.StatementHandling = DebuggerStatementHandling.Script;
            });

            Engine.Debugger.BeforeEvaluate += (_, ast) => _programs.Add(ast);
            Engine.Debugger.Step += OnPause;
            Engine.Debugger.Break += OnPause;
        }

        private StepMode OnPause(object sender, DebugInformation info)
        {
            _visited.Add(Describe(info));
            return StepMode.Into;
        }

        public IReadOnlyList<Program> Programs => _programs;

        public IReadOnlyList<StepLocation> Visited => Ordered(_visited);

        public IReadOnlyList<StepLocation> Expected
        {
            get
            {
                var all = new HashSet<StepLocation>();
                foreach (var program in _programs)
                {
                    foreach (var location in DebugHandler.GetStepLocations(program))
                    {
                        all.Add(location);
                    }
                }

                return Ordered(all);
            }
        }

        private static IReadOnlyList<StepLocation> Ordered(IEnumerable<StepLocation> locations)
        {
            return locations
                .OrderBy(static l => l.Line)
                .ThenBy(static l => l.Column)
                .ThenBy(static l => (int) l.Kind)
                .ToList();
        }
    }

    private static void AssertWalkMatchesRun(string name, string script)
    {
        var recorder = new Recorder();
        recorder.Engine.Execute(script, SourceName);

        AssertSetsMatch(recorder, script, name);
    }

    private static void AssertSetsMatch(Recorder recorder, string script, string name)
    {
        var visited = recorder.Visited;
        var expected = recorder.Expected;

        expected.Should().NotBeEmpty("a corpus entry that reports nothing proves nothing");
        visited.Should().NotBeEmpty("a corpus entry that pauses nowhere proves nothing");

        var missing = expected.Except(visited).ToList();
        var extra = visited.Except(expected).ToList();

        if (missing.Count == 0 && extra.Count == 0)
        {
            return;
        }

        Assert.Fail(
            $"""
            The static walk and the stepped run disagree for '{name}'.

            Reported by GetStepLocations but never paused at ({missing.Count}):
            {Render(missing, script)}

            Paused at but not reported by GetStepLocations ({extra.Count}):
            {Render(extra, script)}
            """);
    }

    private static string Render(IEnumerable<StepLocation> locations, string script)
    {
        var lines = script.Replace("\r\n", "\n").Split('\n');
        return string.Join(
            Environment.NewLine,
            locations.Select(l => string.Format(
                CultureInfo.InvariantCulture,
                "  {0} {1}:{2}:{3} | {4}",
                l.Kind,
                l.Source ?? "<none>",
                l.Line,
                l.Column,
                l.Line - 1 < lines.Length && l.Line >= 1 ? lines[l.Line - 1].Trim() : "<past end>")));
    }

    public static TestCases<string, string> Corpus() => new()
    {
        {
            "statements",
            """
            var a = 1;
            let b = a + 1;
            const c = b;
            a;
            ;
            {
                let inner = c;
                inner;
            }
            outer: {
                break outer;
            }
            debugger;
            """
        },
        {
            "conditionals",
            """
            for (const flag of [true, false]) {
                if (flag) {
                    'then';
                } else {
                    'else';
                }
                const ternary = flag ? 1 : 2;
                ternary;
            }
            """
        },
        {
            "loops",
            """
            let total = 0;
            for (let i = 0; i < 2; i++) {
                total += i;
            }
            let j = 0;
            for (j = 0; j < 2; j++) total += j;
            for (;;) {
                break;
            }
            while (total < 10) {
                total++;
            }
            do {
                total--;
            } while (total > 5);
            const obj = { x: 1, y: 2 };
            for (const key in obj) {
                total += obj[key];
            }
            for (const value of [1, 2]) {
                total += value;
            }
            let target;
            for (target of [3]) {
                total += target;
            }
            const pair = [0, 0];
            for ([pair[0], pair[1]] of [[1, 2]]) {
                total += pair[0];
            }
            outer:
            for (let a = 0; a < 2; a++) {
                inner:
                for (let b = 0; b < 2; b++) {
                    if (b === 0) continue inner;
                    if (a === 1) break outer;
                }
            }
            total;
            """
        },
        {
            "switch",
            """
            for (const value of [1, 2, 3]) {
                switch (value) {
                    case 1:
                    case 2:
                        'one or two';
                        break;
                    default:
                        'other';
                        break;
                }
            }
            """
        },
        {
            "trycatchfinally",
            """
            for (const shouldThrow of [true, false]) {
                try {
                    if (shouldThrow) {
                        throw new Error('boom');
                    }
                    'no throw';
                } catch (err) {
                    err.message;
                } finally {
                    'finally';
                }
            }
            """
        },
        {
            "functions",
            """
            function declared(x) {
                if (x) {
                    return 1;
                }
                'falls off the end';
            }
            declared(true);
            declared(false);
            const expression = function named() {
                return 2;
            };
            expression();
            const block = (x) => {
                return x;
            };
            block(3);
            const concise = (x) => x * 2;
            concise(4);
            const noArgs = () => 5;
            noArgs();
            (function immediate() {
                'iife';
            })();
            function outerFn() {
                function innerFn() {
                    'inner';
                }
                innerFn();
            }
            outerFn();
            const literal = {
                method() {
                    return 6;
                },
                get value() {
                    return 7;
                },
                set value(v) {
                    'setter';
                }
            };
            literal.method();
            literal.value;
            literal.value = 8;
            function withDefault(a = concise(1)) {
                return a;
            }
            withDefault();
            """
        },
        {
            "generators",
            """
            function* counter() {
                yield 1;
                yield 2;
                return 3;
            }
            for (const value of counter()) {
                value;
            }
            const manual = counter();
            manual.next();
            manual.next();
            manual.next();
            """
        },
        {
            "classes",
            """
            class Shape {
                field = 1;
                static staticField = 2;
                #secret = 3;
                static {
                    'static block';
                }
                constructor(size) {
                    this.size = size;
                }
                area() {
                    return this.size * this.field;
                }
                get doubled() {
                    return this.size * 2;
                }
                set doubled(value) {
                    this.size = value / 2;
                }
                static create(size) {
                    return new Shape(size);
                }
                reveal() {
                    return this.#secret;
                }
            }
            class Square extends Shape {
                constructor(size) {
                    super(size);
                }
                area() {
                    return super.area();
                }
            }
            class Implicit extends Shape {
                describe() {
                    return this.size;
                }
            }
            const shape = Shape.create(2);
            shape.area();
            shape.doubled;
            shape.doubled = 8;
            shape.reveal();
            const square = new Square(3);
            square.area();
            const implicit = new Implicit(4);
            implicit.describe();
            const anonymous = class {
                run() {
                    return 9;
                }
            };
            new anonymous().run();
            """
        },
        {
            "sloppy",
            """
            var scope = { value: 1 };
            with (scope) {
                value;
            }
            {
                function blockScoped() {
                    'annex b';
                }
                blockScoped();
            }
            """
        },
        {
            "emptyandsingle",
            """
            let n = 0;
            for (let i = 0; i < 2; i++);
            while (n < 1) n++;
            do n--; while (n > 0);
            for (const cond of [true, false]) if (cond) ; else ;
            for (var key in { a: 1 }) key;
            var first = 1, second = 2, third;
            third = first + second;
            label: third++;
            """
        },
        {
            "trystatements",
            """
            for (const shouldThrow of [true, false]) {
                try {
                    try {
                        if (shouldThrow) {
                            throw 1;
                        }
                    } finally {
                        'inner finally';
                    }
                } catch {
                    'optional binding';
                }
            }
            function thrower() {
                try {
                    throw new Error('x');
                } catch (e) {
                    return e.message;
                } finally {
                    'outer finally';
                }
            }
            thrower();
            """
        },
        {
            "nestedfunctions",
            """
            class Holder {
                arrow = () => 1;
                factory = function () {
                    return 2;
                };
                static staticArrow = () => 3;
                static {
                    Holder.fromStatic = () => 4;
                }
            }
            const holder = new Holder();
            holder.arrow();
            holder.factory();
            Holder.staticArrow();
            Holder.fromStatic();
            const keys = {
                ['computed' + (() => 'Key')()]: () => 5,
            };
            keys.computedKey();
            function withDefaults(cb = () => 6, value = cb()) {
                return value;
            }
            withDefaults();
            const nestedArrows = () => () => 7;
            nestedArrows()();
            """
        },
        {
            "strictanddestructuring",
            """
            'use strict';
            function strictFn() {
                'use strict';
                return 1;
            }
            strictFn();
            const { p, q = 2 } = { p: 1 };
            const [r, ...rest] = [1, 2, 3];
            p + q + r + rest.length;
            for (const [k, v] of [[1, 2]]) {
                k + v;
            }
            for (const { z } of [{ z: 1 }]) {
                z;
            }
            class Private {
                #value = 1;
                static #shared = 2;
                #method() {
                    return this.#value;
                }
                get #wrapped() {
                    return this.#method();
                }
                read() {
                    return this.#wrapped + Private.#shared;
                }
            }
            new Private().read();
            const tag = (strings, ...values) => strings.length + values.length;
            tag`a${1}b`;
            """
        },
    };

    [TestCaseSource(nameof(Corpus))]
    public void TheWalkReportsExactlyWhatAStepIntoRunPausesAt(string name, string script)
    {
        AssertWalkMatchesRun(name, script);
    }

    [Test]
    public void AnAsyncFunctionDrivenByTheEventLoopPausesWhereTheWalkSaysItWill()
    {
        const string Script = """
            let resolved = 0;
            async function work(value) {
                const first = await value;
                resolved += first;
                return resolved;
            }
            async function arrowHost() {
                const later = await Promise.resolve(2);
                return later;
            }
            work(1);
            arrowHost();
            const conciseAsync = async (x) => await x;
            conciseAsync(3);
            async function* asyncCounter() {
                yield 4;
                yield 5;
            }
            async function consume() {
                for await (const value of asyncCounter()) {
                    resolved += value;
                }
                return resolved;
            }
            consume();
            """;

        var recorder = new Recorder();
        recorder.Engine.Execute(Script, SourceName);
        recorder.Engine.Tasks.ProcessTasks();

        AssertSetsMatch(recorder, Script, nameof(Script));
    }

    [Test]
    public void AModuleWithTopLevelAwaitPausesWhereTheWalkSaysItWill()
    {
        const string Imported = """
            export const one = 1;
            export function twice(x) {
                return x * 2;
            }
            """;

        const string Main = """
            import { one, twice } from 'imported';
            export const value = twice(one);
            const awaited = await Promise.resolve(value);
            export default awaited;
            export { value as alias };
            """;

        var recorder = new Recorder();
        recorder.Engine.Modules.Add("imported", Imported);
        recorder.Engine.Modules.Add("main", Main);
        recorder.Engine.Modules.Import("main");

        AssertSetsMatch(recorder, Main, nameof(Main));
    }

    [Test]
    public void AnExportedDefaultDeclarationIsNotSteppedOntoTwice()
    {
        // JintExportDefaultDeclaration builds an exported class itself and finds an exported function's
        // binding already initialized, so only the export statement is stepped onto - never the
        // declaration it wraps.
        const string DefaultFunction = """
            export default function twice(x) {
                return x * 2;
            }
            """;

        const string AnonymousFunction = """
            export default function (x) {
                return x + 1;
            }
            """;

        const string DefaultClass = """
            export default class Doubler {
                run(x) {
                    return x * 2;
                }
            }
            """;

        const string Main = """
            import twice from 'fn';
            import increment from 'anon';
            import Doubler from 'cls';
            twice(1);
            increment(1);
            new Doubler().run(2);
            """;

        var recorder = new Recorder();
        recorder.Engine.Modules.Add("fn", DefaultFunction);
        recorder.Engine.Modules.Add("anon", AnonymousFunction);
        recorder.Engine.Modules.Add("cls", DefaultClass);
        recorder.Engine.Modules.Add("main", Main);
        recorder.Engine.Modules.Import("main");

        AssertSetsMatch(recorder, Main, nameof(Main));
    }

    [Test]
    public void LocationsAreOrderedByLineThenColumn()
    {
        var program = Engine.PrepareScript(
            """
            var a = 1;
            function f() { return a; }
            f();
            """,
            SourceName).Program!;

        var locations = DebugHandler.GetStepLocations(program);

        locations.Should().HaveCountGreaterThan(1);
        for (var i = 1; i < locations.Count; i++)
        {
            var previous = locations[i - 1];
            var current = locations[i];
            var ordered = previous.Line < current.Line
                || (previous.Line == current.Line && previous.Column < current.Column)
                || (previous.Line == current.Line && previous.Column == current.Column && previous.Kind < current.Kind);
            ordered.Should().BeTrue($"{previous} must sort before {current}");
        }
    }

    [Test]
    public void ABreakPointBuiltFromAStepLocationIsHit()
    {
        const string Script = """
            function f() {
                    return 42;
            }
            f();
            """;

        var prepared = Engine.PrepareScript(Script, SourceName);
        var snapped = DebugHandler.FindStepLocation(prepared.Program!, line: 2, column: 0);

        snapped.Should().NotBeNull();
        snapped!.Value.Line.Should().Be(2);
        snapped.Value.Column.Should().Be(8, "the statement is indented, and a front end asks for column 0");

        var engine = new Engine(options => options.Debugger.Enabled = true);
        var hits = 0;
        engine.Debugger.Break += (_, info) =>
        {
            hits++;
            info.Location.Start.Line.Should().Be(2);
            return StepMode.None;
        };
        var breakLocation = snapped.Value.ToBreakLocation();
        engine.Debugger.BreakPoints.Set(new BreakPoint(breakLocation.Source, breakLocation.Line, breakLocation.Column));
        engine.Execute(prepared);

        hits.Should().Be(1);
    }

    [Test]
    public void TheRangeOverloadIsTheFullListFiltered()
    {
        const string Script = """
            var a = 1;
            var b = 2;
            var c = 3;
            var d = 4;
            """;

        var program = Engine.PrepareScript(Script, SourceName).Program!;

        var all = DebugHandler.GetStepLocations(program);
        var range = DebugHandler.GetStepLocations(program, startLine: 2, startColumn: 0, endLine: 4, endColumn: 0);

        range.Should().Equal(all.Where(l => l.Line is 2 or 3));
    }

    [Test]
    public void AnImplicitDerivedConstructorIsSteppedOverAsOneUnit()
    {
        // The engine runs `constructor(...args) { super(...args); }` from an AST parsed once, statically,
        // when a derived class declares no constructor - a program no host was ever handed, whose positions
        // GetStepLocations therefore cannot report. Stepping is transparent to it: `new Derived()` steps
        // straight into the base constructor's body, so every pause names the running program.
        const string Script = """
            class Base {
                constructor() {
                    this.x = 1;
                }
            }
            class Derived extends Base {
            }
            new Derived();
            """;

        var recorder = new Recorder();
        recorder.Engine.Execute(Script, SourceName);

        var program = recorder.Programs.Should().ContainSingle().Subject;
        foreach (var location in recorder.Visited)
        {
            location.Source.Should().Be(SourceName, "a pause outside the program names a source no editor can open");
            location.Line.Should().BeInRange(
                program.Location.Start.Line,
                program.Location.End.Line,
                "a pause outside the program's range names a line the host was never handed");
        }

        recorder.Visited.Should().Contain(
            l => l.Line == 3,
            "stepping into the implicit constructor lands in the base constructor's body");

        AssertSetsMatch(recorder, Script, nameof(Script));
    }
}
