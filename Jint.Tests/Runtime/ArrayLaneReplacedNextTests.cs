#nullable enable

using Jint.Native;
using Jint.Native.Object;

namespace Jint.Tests.Runtime;

/// <summary>
/// Pins step 3 of <a href="https://tc39.es/ecma262/#sec-getiteratorfrommethod">GetIteratorFromMethod</a> for an
/// array: <c>Array.prototype.values</c> answers a fresh array iterator
/// (<a href="https://tc39.es/ecma262/#sec-createarrayiterator">CreateArrayIterator</a>), <c>next</c> is read off it
/// once, and <a href="https://tc39.es/ecma262/#sec-iteratorstep">IteratorStep</a> calls what was read. So a script
/// that replaces <a href="https://tc39.es/ecma262/#sec-%arrayiteratorprototype%.next">%ArrayIteratorPrototype%.next</a>
/// sees spread in an array literal
/// (<a href="https://tc39.es/ecma262/#sec-runtime-semantics-arrayaccumulation">ArrayAccumulation</a>) and in
/// arguments (<a href="https://tc39.es/ecma262/#sec-runtime-semantics-argumentlistevaluation">ArgumentListEvaluation</a>),
/// and array destructuring in every position
/// (<a href="https://tc39.es/ecma262/#sec-runtime-semantics-iteratorbindinginitialization">IteratorBindingInitialization</a>,
/// <a href="https://tc39.es/ecma262/#sec-runtime-semantics-iteratordestructuringassignmentevaluation">IteratorDestructuringAssignmentEvaluation</a>),
/// call the replacement.
/// <para>
/// Those lanes walked the array by index whenever its <c>Symbol.iterator</c> was <c>Array.prototype.values</c>, and
/// never asked about <c>next</c> (sebastienros/jint#4418). <c>for…of</c>, <c>Array.from</c>, <c>new Set</c>,
/// <c>yield*</c> and <c>Promise.all</c> already took the iterator and are the controls, as are the destructuring
/// patterns that may suspend, which always take it. Every expected value is Node 26's; the realm, host-shape and guard
/// tests have no Node counterpart and say where theirs come from.
/// </para>
/// </summary>
public class ArrayLaneReplacedNextTests
{
    private const string Harness = """
        // A next() answering R1, R2 and then done, counting its calls, so a lane that walked the array by index instead
        // shows the array's own values and a count of zero.
        function replacement(calls) {
            let n = 0;
            return function () { calls.count++; n++; return n <= 2 ? { value: 'R' + n, done: false } : { value: undefined, done: true }; };
        }

        function* one(v) { yield v; }

        // Every construct that takes an iterator from an array. Each answers what it produced, joined.
        const lanes = {
            arraySpread: arr => [...arr].join(','),
            arraySpreadBetween: arr => [0, ...arr, 3].join(','),
            callSpread: arr => ((...a) => a)(...arr).join(','),
            callSpreadBetween: arr => ((...a) => a)(0, ...arr, 3).join(','),
            methodCallSpread: arr => ({ m(...a) { return a; } }).m(...arr).join(','),
            optionalCallSpread: arr => { const f = (...a) => a; return f?.(...arr).join(','); },
            newSpread: arr => new (function (...a) { this.a = a; })(...arr).a.join(','),
            superSpread: arr => { class B { constructor(...a) { this.a = a; } } class D extends B { constructor(v) { super(...v); } } return new D(arr).a.join(','); },
            varPattern: arr => { var [a, b] = arr; return a + ',' + b; },
            letPattern: arr => { let [a, b] = arr; return a + ',' + b; },
            constRest: arr => { const [a, ...r] = arr; return a + ',' + r.join(','); },
            elision: arr => { const [, b] = arr; return String(b); },
            defaults: arr => { const [a = 'd', b = 'd', c = 'd'] = arr; return a + ',' + b + ',' + c; },
            nestedInObjectPattern: arr => { const { p: [a, b] } = { p: arr }; return a + ',' + b; },
            assignment: arr => { let a, b; [a, b] = arr; return a + ',' + b; },
            assignmentRest: arr => { let a, r; [a, ...r] = arr; return a + ',' + r.join(','); },
            assignmentMembers: arr => { const o = {}; [o.a, o.b] = arr; return o.a + ',' + o.b; },
            parameter: arr => (([a, b]) => a + ',' + b)(arr),
            parameterDefault: arr => (function ([a, b] = arr) { return a + ',' + b; })(),
            restParameterPattern: arr => (function (...[a, b]) { return a + ',' + b; })(arr[0], arr[1]),
            forOfHead: arr => { let out; for (const [a, b] of one(arr)) { out = a + ',' + b; } return out; },
            forOfHeadAssignment: arr => { let a, b; for ([a, b] of one(arr)) { } return a + ',' + b; },
            catchParameter: arr => { try { throw arr; } catch ([a, b]) { return a + ',' + b; } },
            generatorSpread: arr => (function* () { yield [...arr].join(','); })().next().value,
            generatorPattern: arr => (function* () { const [a, b] = arr; yield a + ',' + b; })().next().value,
            generatorParameter: arr => (function* ([a, b]) { yield a + ',' + b; })(arr).next().value,
            generatorPatternThatMaySuspend: arr => (function* () { const [a = yield 'paused', b] = arr; return a + ',' + b; })().next().value,
            asyncSpread: arr => (async function () { await 0; return [...arr].join(','); })(),
            asyncSpreadArgumentsThatSuspend: arr => (async function () { return ((...a) => a)(await 0, ...arr).join(','); })(),
            asyncPattern: arr => (async function () { await 0; const [a, b] = arr; return a + ',' + b; })(),
            asyncArrowParameter: arr => (async ([a, b]) => a + ',' + b)(arr),
            asyncPatternThatMaySuspend: arr => (async function () { const [a = await 'd', b] = arr; return a + ',' + b; })(),
            // Already took the iterator through GetIterator.
            forOf: arr => { const out = []; for (const x of arr) { out.push(x); } return out.join(','); },
            arrayFrom: arr => Array.from(arr).join(','),
            setConstructor: arr => Array.from(new Set(arr).values()).join(','),
            yieldStar: arr => { const out = []; const it = (function* () { yield* arr; })(); for (let s = it.next(); !s.done; s = it.next()) { out.push(s.value); } return out.join(','); },
            promiseAll: arr => Promise.all(arr).then(v => v.join(',')),
        };

        // The ways a script makes `next`, read off a fresh array iterator, resolve to `next`, each answering the function
        // that puts everything back. 'reparentOwnIntact' changes only what the prototype's own `next` shadows, and 'none'
        // nothing: the built-in stays in force under both.
        function applyRoute(route, next) {
            const P = Object.getPrototypeOf([][Symbol.iterator]());
            const IP = Object.getPrototypeOf(P);
            const saved = [];
            function save(o, k) { saved.push([o, k, Object.getOwnPropertyDescriptor(o, k)]); }
            switch (route) {
                case 'assign': save(P, 'next'); P.next = next; break;
                case 'defineData': save(P, 'next'); Object.defineProperty(P, 'next', { value: next }); break;
                case 'defineAccessor': save(P, 'next'); Object.defineProperty(P, 'next', { get() { return next; } }); break;
                case 'deleteAndInherit': save(P, 'next'); save(IP, 'next'); delete P.next; IP.next = next; break;
                case 'deleteAndReparent': save(P, 'next'); delete P.next; Object.setPrototypeOf(P, { __proto__: IP, next: next }); break;
                case 'reparentOwnIntact': Object.setPrototypeOf(P, { __proto__: IP, next: next }); break;
                case 'none': break;
            }
            return function () {
                Object.setPrototypeOf(P, IP);
                for (let i = saved.length - 1; i >= 0; i--) { const s = saved[i]; if (s[2]) { Object.defineProperty(s[0], s[1], s[2]); } else { delete s[0][s[1]]; } }
            };
        }

        // When `warm`, spreads an array, which creates %ArrayIteratorPrototype% and checks its `next`, and runs the lane
        // once untouched, so that whatever the engine remembers about `next` is cached. Then takes the route, runs the
        // lane over a fresh make() - [1, 2] unless the caller says otherwise - and puts everything back; then runs the
        // lane once more. Answers "<values> <calls> | <values>", the second half showing the built-in iteration back.
        async function run(lane, route, warm, make) {
            make = make || function () { return [1, 2]; };
            if (warm) { [...[0]]; await lanes[lane](make()); }
            const calls = { count: 0 };
            const restore = applyRoute(route, replacement(calls));
            let first;
            try { first = (await lanes[lane](make())) + ' ' + calls.count; }
            finally { restore(); }
            return first + ' | ' + (await lanes[lane](make()));
        }

        // Makes `next` an accessor that counts its reads and answers the built-in, runs the lane and answers
        // "<values> <reads>".
        async function reads(lane) {
            const P = Object.getPrototypeOf([][Symbol.iterator]());
            const saved = Object.getOwnPropertyDescriptor(P, 'next');
            let count = 0;
            Object.defineProperty(P, 'next', { get() { count++; return saved.value; }, configurable: true });
            try { return (await lanes[lane]([1, 2])) + ' ' + count; }
            finally { Object.defineProperty(P, 'next', saved); }
        }
        """;

    // What each lane answers over [1, 2] with the built-in `next`, and with the replacement (followed by its call count).
    private static readonly Dictionary<string, (string Native, string Replaced)> Lanes = new()
    {
        ["arraySpread"] = ("1,2", "R1,R2 3"),
        ["arraySpreadBetween"] = ("0,1,2,3", "0,R1,R2,3 3"),
        ["callSpread"] = ("1,2", "R1,R2 3"),
        ["callSpreadBetween"] = ("0,1,2,3", "0,R1,R2,3 3"),
        ["methodCallSpread"] = ("1,2", "R1,R2 3"),
        ["optionalCallSpread"] = ("1,2", "R1,R2 3"),
        ["newSpread"] = ("1,2", "R1,R2 3"),
        ["superSpread"] = ("1,2", "R1,R2 3"),
        ["varPattern"] = ("1,2", "R1,R2 2"),
        ["letPattern"] = ("1,2", "R1,R2 2"),
        ["constRest"] = ("1,2", "R1,R2 3"),
        ["elision"] = ("2", "R2 2"),
        ["defaults"] = ("1,2,d", "R1,R2,d 3"),
        ["nestedInObjectPattern"] = ("1,2", "R1,R2 2"),
        ["assignment"] = ("1,2", "R1,R2 2"),
        ["assignmentRest"] = ("1,2", "R1,R2 3"),
        ["assignmentMembers"] = ("1,2", "R1,R2 2"),
        ["parameter"] = ("1,2", "R1,R2 2"),
        ["parameterDefault"] = ("1,2", "R1,R2 2"),
        ["restParameterPattern"] = ("1,2", "R1,R2 2"),
        ["forOfHead"] = ("1,2", "R1,R2 2"),
        ["forOfHeadAssignment"] = ("1,2", "R1,R2 2"),
        ["catchParameter"] = ("1,2", "R1,R2 2"),
        ["generatorSpread"] = ("1,2", "R1,R2 3"),
        ["generatorPattern"] = ("1,2", "R1,R2 2"),
        ["generatorParameter"] = ("1,2", "R1,R2 2"),
        ["generatorPatternThatMaySuspend"] = ("1,2", "R1,R2 2"),
        ["asyncSpread"] = ("1,2", "R1,R2 3"),
        ["asyncSpreadArgumentsThatSuspend"] = ("0,1,2", "0,R1,R2 3"),
        ["asyncPattern"] = ("1,2", "R1,R2 2"),
        ["asyncArrowParameter"] = ("1,2", "R1,R2 2"),
        ["asyncPatternThatMaySuspend"] = ("1,2", "R1,R2 2"),
        ["forOf"] = ("1,2", "R1,R2 3"),
        ["arrayFrom"] = ("1,2", "R1,R2 3"),
        ["setConstructor"] = ("1,2", "R1,R2 3"),
        ["yieldStar"] = ("1,2", "R1,R2 3"),
        ["promiseAll"] = ("1,2", "R1,R2 3"),
    };

    // Whether the route leaves `next` resolving to the replacement.
    private static readonly Dictionary<string, bool> Routes = new()
    {
        ["assign"] = true,
        ["defineData"] = true,
        ["defineAccessor"] = true,
        ["deleteAndInherit"] = true,
        ["deleteAndReparent"] = true,
        ["reparentOwnIntact"] = false,
        ["none"] = false,
    };

    // Every lane under every route after a warm-up, and under an assignment with nothing warmed.
    public static TestCases<string, string, bool, string> EveryLaneUnderEveryRoute
    {
        get
        {
            var rows = new TestCases<string, string, bool, string>();
            foreach (var lane in Lanes)
            {
                foreach (var route in Routes)
                {
                    rows.Add(lane.Key, route.Key, true, (route.Value ? lane.Value.Replaced : lane.Value.Native + " 0") + " | " + lane.Value.Native);
                }

                rows.Add(lane.Key, "assign", false, lane.Value.Replaced + " | " + lane.Value.Native);
            }

            return rows;
        }
    }

    public static TestCases<string, string> EveryLaneReadsNextOnce
    {
        get
        {
            var rows = new TestCases<string, string>();
            foreach (var lane in Lanes)
            {
                // yield* calls the replacement (the matrix above), but reads `next` a second time for a record it steps
                // natively, whose NextMethod is null: a defect of its own, outside these lanes. The fix for
                // sebastienros/jint#4424 puts this row back.
                if (lane.Key != "yieldStar")
                {
                    rows.Add(lane.Key, lane.Value.Native + " 1");
                }
            }

            return rows;
        }
    }

    private static string Run(Engine engine, string script) =>
        engine.Evaluate(Harness + "\n" + script).UnwrapIfPromise().AsString();

    /// <summary>
    /// The issue's own probe.
    /// </summary>
    [Test]
    public void TheIssueProbeMatchesNode()
    {
        var engine = new Engine();
        engine.Evaluate("""
            const proto = Object.getPrototypeOf([][Symbol.iterator]());
            proto.next = () => ({ done: true, value: undefined });
            const arr = [1, 2];
            const f = (...a) => a;
            var [x] = arr;
            JSON.stringify([[...arr], f(), f(...arr), x ?? null])
            """).AsString().Should().Be("[[],[],[],null]");
    }

    /// <summary>
    /// Each lane, with <c>next</c> replaced on %ArrayIteratorPrototype% by every route a script has, after the lane
    /// has run once untouched; and once with nothing warmed. The two routes that leave the built-in in force are the
    /// controls, as are the lanes that already took the iterator.
    /// </summary>
    [TestCaseSource(nameof(EveryLaneUnderEveryRoute))]
    public void EveryArrayLaneCallsAReplacedNext(string lane, string route, bool warm, string expected)
    {
        Run(new Engine(), $"run('{lane}', '{route}', {(warm ? "true" : "false")})").Should().Be(expected);
    }

    /// <summary>
    /// GetIteratorFromMethod reads <c>next</c> once per iteration, which an accessor on %ArrayIteratorPrototype%
    /// answering the built-in counts. The lanes that walked by index read it zero times.
    /// </summary>
    [TestCaseSource(nameof(EveryLaneReadsNextOnce))]
    public void EveryArrayLaneReadsNextOnce(string lane, string expected)
    {
        Run(new Engine(), $"reads('{lane}')").Should().Be(expected);
    }

    /// <summary>
    /// Array.prototype.values creates its iterator in its own realm, so what decides is the array's realm's
    /// %ArrayIteratorPrototype%, never the running one's. The second realm comes from <c>$262.createRealm()</c>; the
    /// expected values are Node 26's for the same probe over a <c>vm</c> context.
    /// </summary>
    [TestCase("other", "other.eval('[1, 2]')", "[[],[],null,null,null]")]
    [TestCase("other", "new other.Array(1, 2)", "[[],[],null,null,null]")]
    [TestCase("other", "[1, 2]", "[[1,2],[1,2],1,1,1]")]
    [TestCase("main", "other.eval('[1, 2]')", "[[1,2],[1,2],1,1,1]")]
    [TestCase("main", "new other.Array(1, 2)", "[[1,2],[1,2],1,1,1]")]
    [TestCase("main", "[1, 2]", "[[],[],null,null,null]")]
    public void TheArraysOwnRealmsNextDecides(string replacedRealm, string array, string expected)
    {
        var engine = new Engine();
        Test262Object.Install(engine);
        engine.Evaluate($$"""
            (function () {
                var other = $262.createRealm().global;
                var realms = { main: globalThis, other: other };
                var P = Object.getPrototypeOf(realms['{{replacedRealm}}'].eval('[][Symbol.iterator]()'));
                var original = P.next;
                P.next = function () { return { done: true, value: undefined }; };
                try {
                    var arr = {{array}};
                    var f = (...a) => a;
                    var [x] = arr;
                    var y; [y] = arr;
                    var g = ([z]) => z;
                    return JSON.stringify([[...arr], f(...arr), x ?? null, y ?? null, g(arr) ?? null]);
                } finally {
                    P.next = original;
                }
            })()
            """).AsString().Should().Be(expected);
    }

    private sealed class HostList : ArrayLikeObject
    {
        public HostList(Engine engine) : base(engine)
        {
            Prototype = engine.Intrinsics.Array.PrototypeObject;
        }

        public override uint Length => 2;

        public override bool TryGetIndex(uint index, out JsValue value)
        {
            value = index < 2 ? index + 1 : JsValue.Undefined;
            return index < 2;
        }
    }

    /// <summary>
    /// The two host shapes the destructuring lane also walked by index: an <see cref="ArrayLikeObject"/> whose
    /// <c>Symbol.iterator</c> is <c>Array.prototype.values</c> (an array iterator, as for an array), and a wrapped
    /// CLR list, whose iterator inherits %ArrayIteratorPrototype%. Node has no counterpart: the expected values are
    /// the ones the lanes that already took the iterator give, which are the controls here.
    /// </summary>
    [TestCase("host", "varPattern", "R1,R2 2 | 1,2")]
    [TestCase("host", "assignment", "R1,R2 2 | 1,2")]
    [TestCase("host", "constRest", "R1,R2 3 | 1,2")]
    [TestCase("host", "catchParameter", "R1,R2 2 | 1,2")]
    [TestCase("host", "parameter", "R1,R2 2 | 1,2")]
    [TestCase("host", "arraySpread", "R1,R2 3 | 1,2")]
    [TestCase("host", "callSpread", "R1,R2 3 | 1,2")]
    [TestCase("host", "forOf", "R1,R2 3 | 1,2")]
    [TestCase("list", "varPattern", "R1,R2 2 | 1,2")]
    [TestCase("list", "assignment", "R1,R2 2 | 1,2")]
    [TestCase("list", "constRest", "R1,R2 3 | 1,2")]
    [TestCase("list", "catchParameter", "R1,R2 2 | 1,2")]
    [TestCase("list", "parameter", "R1,R2 2 | 1,2")]
    [TestCase("list", "arraySpread", "R1,R2 3 | 1,2")]
    [TestCase("list", "callSpread", "R1,R2 3 | 1,2")]
    [TestCase("list", "forOf", "R1,R2 3 | 1,2")]
    public void AHostArrayLikeLaneCallsAReplacedNext(string name, string lane, string expected)
    {
        var engine = new Engine();
        engine.SetValue("host", new HostList(engine));
        engine.SetValue("list", new List<int> { 1, 2 });

        Run(engine, $"run('{lane}', 'assign', true, function () {{ return {name}; }})").Should().Be(expected);
    }

    /// <summary>
    /// The gate every lane above shares, asked directly: it answers false under each route that leaves the
    /// replacement in force, true under the two that do not, and true again once the built-in is back, for an array,
    /// a host array-like and a wrapped CLR list alike.
    /// </summary>
    [TestCase("assign")]
    [TestCase("defineData")]
    [TestCase("defineAccessor")]
    [TestCase("deleteAndInherit")]
    [TestCase("deleteAndReparent")]
    [TestCase("reparentOwnIntact")]
    [TestCase("none")]
    public void TheGateFollowsNextAndComesBack(string route)
    {
        var engine = new Engine();
        engine.SetValue("host", new HostList(engine));
        engine.SetValue("list", new List<int> { 1, 2 });
        engine.Execute(Harness);

        var subjects = new[]
        {
            (ObjectInstance) engine.Evaluate("[1, 2]"),
            (ObjectInstance) engine.GetValue("host"),
            (ObjectInstance) engine.GetValue("list"),
        };

        // Before anything has created %ArrayIteratorPrototype%, and again once a spread has created it and checked its
        // `next`.
        foreach (var subject in subjects)
        {
            subject.HasOriginalIterator.Should().BeTrue(subject.GetType().Name);
        }

        engine.Execute("[...[0]];");
        foreach (var subject in subjects)
        {
            subject.HasOriginalIterator.Should().BeTrue(subject.GetType().Name);
        }

        engine.Execute($"var restore = applyRoute('{route}', function () {{ return {{ done: true, value: undefined }}; }});");
        foreach (var subject in subjects)
        {
            subject.HasOriginalIterator.Should().Be(!Routes[route], subject.GetType().Name);
        }

        engine.Execute("restore();");
        foreach (var subject in subjects)
        {
            subject.HasOriginalIterator.Should().BeTrue(subject.GetType().Name);
        }
    }
}
