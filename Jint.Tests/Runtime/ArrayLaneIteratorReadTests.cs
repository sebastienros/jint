#nullable enable

using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;

namespace Jint.Tests.Runtime;

/// <summary>
/// Pins step 1 of <a href="https://tc39.es/ecma262/#sec-getiterator">GetIterator</a> for an array: <c>@@iterator</c> is
/// read once, through <a href="https://tc39.es/ecma262/#sec-getmethod">GetMethod</a>, and what that one read found is
/// the method <a href="https://tc39.es/ecma262/#sec-getiteratorfrommethod">GetIteratorFromMethod</a> calls. That holds
/// for spread in an array literal
/// (<a href="https://tc39.es/ecma262/#sec-runtime-semantics-arrayaccumulation">ArrayAccumulation</a>), in a call, a
/// <c>new</c> and a <c>super(...)</c>
/// (<a href="https://tc39.es/ecma262/#sec-runtime-semantics-argumentlistevaluation">ArgumentListEvaluation</a>), and
/// for array destructuring in every position
/// (<a href="https://tc39.es/ecma262/#sec-runtime-semantics-iteratorbindinginitialization">IteratorBindingInitialization</a>,
/// <a href="https://tc39.es/ecma262/#sec-runtime-semantics-iteratordestructuringassignmentevaluation">IteratorDestructuringAssignmentEvaluation</a>).
/// <para>
/// Those lanes read the array by index while its <c>@@iterator</c> is <c>Array.prototype.values</c>, and their check made
/// a read of its own (sebastienros/jint#4426). A spread read <c>@@iterator</c> in GetIterator and again in the check,
/// and a destructuring whose check found something else read it again in the GetIterator it fell back to. So an accessor
/// that answers differently on each read made the check and the iteration disagree, and an accessor that replaced
/// <c>next</c> or added a <c>return</c> to the array iterator prototype did so after the check had already passed.
/// <c>for…of</c>, <c>Array.from</c>, <c>new Set</c>, <c>yield*</c> and <c>Promise.all</c> always took GetIterator and are
/// the controls, as are the destructuring patterns that may suspend. Every expected value is Node 26's.
/// </para>
/// </summary>
public class ArrayLaneIteratorReadTests
{
    private const string Harness = """
        function* one(v) { yield v; }

        // Every construct that takes an iterator from an array, each answering what it produced, joined.
        const lanes = {
            arraySpread: arr => [...arr].join(','),
            arraySpreadBetween: arr => [0, ...arr, 3].join(','),
            callSpread: arr => ((...a) => a)(...arr).join(','),
            callSpreadBetween: arr => ((...a) => a)(0, ...arr, 3).join(','),
            methodCallSpread: arr => ({ m(...a) { return a; } }).m(...arr).join(','),
            optionalCallSpread: arr => { const f = (...a) => a; return f?.(...arr).join(','); },
            newSpread: arr => new (function (...a) { this.a = a; })(...arr).a.join(','),
            superSpread: arr => { class B { constructor(...a) { this.a = a; } } class D extends B { constructor(v) { super(...v); } } return new D(arr).a.join(','); },
            defaultDerivedConstructor: arr => { class B { constructor(...a) { this.a = a; } } class D extends B { } return new D(...arr).a.join(','); },
            generatorSpread: arr => (function* () { yield [...arr].join(','); })().next().value,
            generatorCallSpread: arr => (function* () { yield ((...a) => a)(...arr).join(','); })().next().value,
            generatorSpreadAfterYield: arr => { const g = (function* () { yield 0; yield [...arr].join(','); })(); g.next(); return g.next().value; },
            generatorSpreadElementsThatSuspend: arr => { const g = (function* () { return [yield 0, ...arr].join(','); })(); g.next(); return g.next(0).value; },
            asyncSpread: arr => (async function () { await 0; return [...arr].join(','); })(),
            asyncCallSpread: arr => (async function () { await 0; return ((...a) => a)(...arr).join(','); })(),
            asyncSpreadArgumentsThatSuspend: arr => (async function () { return ((...a) => a)(await 0, ...arr).join(','); })(),
            asyncSpreadElementsThatSuspend: arr => (async function () { return [await 0, ...arr].join(','); })(),
            asyncNewSpread: arr => (async function () { await 0; return new (function (...a) { this.a = a; })(...arr).a.join(','); })(),
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
            forOfHead: arr => { let out; for (const [a, b] of one(arr)) { out = a + ',' + b; } return out; },
            forOfHeadAssignment: arr => { let a, b; for ([a, b] of one(arr)) { } return a + ',' + b; },
            catchParameter: arr => { try { throw arr; } catch ([a, b]) { return a + ',' + b; } },
            generatorPattern: arr => (function* () { const [a, b] = arr; yield a + ',' + b; })().next().value,
            generatorParameter: arr => (function* ([a, b]) { yield a + ',' + b; })(arr).next().value,
            generatorPatternThatMaySuspend: arr => (function* () { const [a = yield 'paused', b] = arr; return a + ',' + b; })().next().value,
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

        // Makes Symbol.iterator an accessor - on the array itself ('own') or on Array.prototype ('proto') - that counts its
        // reads and answers per `answers`, runs the lane over [1, 2] and answers "<values> <reads>/<calls>/<closes>": the
        // reads of Symbol.iterator, the calls of the script's Symbol.iterator, of what it answered or of a replaced `next`,
        // and the calls of a `return` a getter added.
        //   values              Array.prototype.values, every read.
        //   replaced            an iterator method of the script's, whose iterator answers R1, R2 and then done.
        //   valuesThenReplaced  values on the first read, the script's method on any later one.
        //   replacedThenValues  the script's method on the first read, values on any later one.
        //   replacingNext       values, after making %ArrayIteratorPrototype%.next answer R1, R2 and then done.
        //   addingReturn        values, after giving %ArrayIteratorPrototype% a `return`.
        //   undefinedMethod, nullMethod, nonCallable
        //                       undefined, null and 42, for which GetIterator throws a TypeError; the lane answers its name.
        // When `warm`, first runs the lane once over a plain array.
        async function reads(lane, where, answers, warm, make) {
            make = make || function () { return [1, 2]; };
            const values = Array.prototype[Symbol.iterator];
            const P = Object.getPrototypeOf([][Symbol.iterator]());
            const savedNext = Object.getOwnPropertyDescriptor(P, 'next');
            const savedIterator = Object.getOwnPropertyDescriptor(Array.prototype, Symbol.iterator);
            const counts = { reads: 0, calls: 0, closes: 0 };
            function results() {
                let n = 0;
                return function () { counts.calls++; n++; return n <= 2 ? { value: 'R' + n, done: false } : { value: undefined, done: true }; };
            }
            function replacement() { counts.calls++; return { next: results() }; }
            const answer = {
                values: () => values,
                replaced: () => replacement,
                valuesThenReplaced: () => counts.reads === 1 ? values : replacement,
                replacedThenValues: () => counts.reads === 1 ? replacement : values,
                replacingNext: () => { P.next = results(); return values; },
                addingReturn: () => { P.return = function () { counts.closes++; return {}; }; return values; },
                undefinedMethod: () => undefined,
                nullMethod: () => null,
                nonCallable: () => 42,
            }[answers];
            if (warm) { await lanes[lane](make()); }
            const subject = make();
            Object.defineProperty(where === 'own' ? subject : Array.prototype, Symbol.iterator, { get() { counts.reads++; return answer(); }, configurable: true });
            try {
                let result;
                try { result = await lanes[lane](subject); }
                catch (e) { result = e.name; }
                return result + ' ' + counts.reads + '/' + counts.calls + '/' + counts.closes;
            } finally {
                Object.defineProperty(Array.prototype, Symbol.iterator, savedIterator);
                Object.defineProperty(P, 'next', savedNext);
                delete P.return;
            }
        }
        """;

    // What each lane answers over [1, 2] with the built-in iterator and with the script's (R1, R2), how many times it
    // steps an iterator, and whether its pattern closes one it has not run out (https://tc39.es/ecma262/#sec-iteratorclose).
    private static readonly Dictionary<string, (string Native, string Replaced, int Steps, int Closes)> Lanes = new()
    {
        ["arraySpread"] = ("1,2", "R1,R2", 3, 0),
        ["arraySpreadBetween"] = ("0,1,2,3", "0,R1,R2,3", 3, 0),
        ["callSpread"] = ("1,2", "R1,R2", 3, 0),
        ["callSpreadBetween"] = ("0,1,2,3", "0,R1,R2,3", 3, 0),
        ["methodCallSpread"] = ("1,2", "R1,R2", 3, 0),
        ["optionalCallSpread"] = ("1,2", "R1,R2", 3, 0),
        ["newSpread"] = ("1,2", "R1,R2", 3, 0),
        ["superSpread"] = ("1,2", "R1,R2", 3, 0),
        ["defaultDerivedConstructor"] = ("1,2", "R1,R2", 3, 0),
        ["generatorSpread"] = ("1,2", "R1,R2", 3, 0),
        ["generatorCallSpread"] = ("1,2", "R1,R2", 3, 0),
        ["generatorSpreadAfterYield"] = ("1,2", "R1,R2", 3, 0),
        ["generatorSpreadElementsThatSuspend"] = ("0,1,2", "0,R1,R2", 3, 0),
        ["asyncSpread"] = ("1,2", "R1,R2", 3, 0),
        ["asyncCallSpread"] = ("1,2", "R1,R2", 3, 0),
        ["asyncSpreadArgumentsThatSuspend"] = ("0,1,2", "0,R1,R2", 3, 0),
        ["asyncSpreadElementsThatSuspend"] = ("0,1,2", "0,R1,R2", 3, 0),
        ["asyncNewSpread"] = ("1,2", "R1,R2", 3, 0),
        ["varPattern"] = ("1,2", "R1,R2", 2, 1),
        ["letPattern"] = ("1,2", "R1,R2", 2, 1),
        ["constRest"] = ("1,2", "R1,R2", 3, 0),
        ["elision"] = ("2", "R2", 2, 1),
        ["defaults"] = ("1,2,d", "R1,R2,d", 3, 0),
        ["nestedInObjectPattern"] = ("1,2", "R1,R2", 2, 1),
        ["assignment"] = ("1,2", "R1,R2", 2, 1),
        ["assignmentRest"] = ("1,2", "R1,R2", 3, 0),
        ["assignmentMembers"] = ("1,2", "R1,R2", 2, 1),
        ["parameter"] = ("1,2", "R1,R2", 2, 1),
        ["parameterDefault"] = ("1,2", "R1,R2", 2, 1),
        ["forOfHead"] = ("1,2", "R1,R2", 2, 1),
        ["forOfHeadAssignment"] = ("1,2", "R1,R2", 2, 1),
        ["catchParameter"] = ("1,2", "R1,R2", 2, 1),
        ["generatorPattern"] = ("1,2", "R1,R2", 2, 1),
        ["generatorParameter"] = ("1,2", "R1,R2", 2, 1),
        ["generatorPatternThatMaySuspend"] = ("1,2", "R1,R2", 2, 1),
        ["asyncPattern"] = ("1,2", "R1,R2", 2, 1),
        ["asyncArrowParameter"] = ("1,2", "R1,R2", 2, 1),
        ["asyncPatternThatMaySuspend"] = ("1,2", "R1,R2", 2, 1),
        ["forOf"] = ("1,2", "R1,R2", 3, 0),
        ["arrayFrom"] = ("1,2", "R1,R2", 3, 0),
        ["setConstructor"] = ("1,2", "R1,R2", 3, 0),
        ["yieldStar"] = ("1,2", "R1,R2", 3, 0),
        ["promiseAll"] = ("1,2", "R1,R2", 3, 0),
    };

    // What Node 26 answers for a lane under each of the harness's answers: one read every time, and the method that read
    // found deciding. The script's iterator is called once and then once per step. Array.from, alone, reads an array-like
    // by index when GetMethod finds undefined or null (https://tc39.es/ecma262/#sec-array.from).
    private static string Expected(string lane, string answers)
    {
        var (native, replaced, steps, closes) = Lanes[lane];
        var notIterable = lane == "arrayFrom" ? native : "TypeError";
        return answers switch
        {
            "values" => native + " 1/0/0",
            "replaced" => replaced + " 1/" + (steps + 1) + "/0",
            "valuesThenReplaced" => native + " 1/0/0",
            "replacedThenValues" => replaced + " 1/" + (steps + 1) + "/0",
            "replacingNext" => replaced + " 1/" + steps + "/0",
            "addingReturn" => native + " 1/0/" + closes,
            "undefinedMethod" or "nullMethod" => notIterable + " 1/0/0",
            "nonCallable" => "TypeError 1/0/0",
            _ => throw new ArgumentOutOfRangeException(nameof(answers), answers, null),
        };
    }

    private static readonly string[] Answers =
    [
        "values",
        "replaced",
        "valuesThenReplaced",
        "replacedThenValues",
        "replacingNext",
        "addingReturn",
        "undefinedMethod",
        "nullMethod",
        "nonCallable",
    ];

    // Every lane under every answer, with the accessor on the array and on Array.prototype ("own", "proto"), on the
    // lane's first run and after one over a plain array ("cold", "warm").
    public static TestCases<string, string, string, string> EveryLaneUnderEveryAnswer
    {
        get
        {
            var rows = new TestCases<string, string, string, string>();
            foreach (var lane in Lanes.Keys)
            {
                foreach (var answers in Answers)
                {
                    foreach (var setting in new[] { "own cold", "own warm", "proto cold", "proto warm" })
                    {
                        rows.Add(lane, answers, setting, Expected(lane, answers));
                    }
                }
            }

            return rows;
        }
    }

    // The lanes a host ArrayLikeObject inheriting Array.prototype's Symbol.iterator also takes, under the answers that
    // leave it iterable.
    public static TestCases<string, string, string> EveryHostLaneUnderEveryAnswer
    {
        get
        {
            var rows = new TestCases<string, string, string>();
            foreach (var lane in new[] { "varPattern", "assignment", "constRest", "elision", "catchParameter", "parameter", "arraySpread", "callSpread", "forOf" })
            {
                foreach (var answers in new[] { "values", "replaced", "valuesThenReplaced", "replacedThenValues", "replacingNext", "addingReturn" })
                {
                    rows.Add(lane, answers, Expected(lane, answers));
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
        new Engine().Evaluate("""
            let reads = 0;
            const arr = [1, 2];
            const values = arr[Symbol.iterator];
            Object.defineProperty(arr, Symbol.iterator, { get() { reads++; return values; } });
            [...arr];
            reads
            """).AsNumber().Should().Be(1);
    }

    /// <summary>
    /// Each lane reads <c>@@iterator</c> once and iterates with what that read found: the built-in, the script's own
    /// method, one that answers differently on a second read, one that replaces <c>next</c> or adds a <c>return</c> as
    /// it is read, and the three a GetMethod refuses.
    /// </summary>
    [TestCaseSource(nameof(EveryLaneUnderEveryAnswer))]
    public void EveryArrayLaneReadsIteratorOnce(string lane, string answers, string setting, string expected)
    {
        var where = setting.StartsWith("own", StringComparison.Ordinal) ? "own" : "proto";
        var warm = setting.EndsWith("warm", StringComparison.Ordinal) ? "true" : "false";
        Run(new Engine(), $"reads('{lane}', '{where}', '{answers}', {warm})").Should().Be(expected);
    }

    /// <summary>
    /// A host <see cref="ArrayLikeObject"/> whose <c>Symbol.iterator</c> is Array.prototype's takes the destructuring
    /// lane's check too. Node has no counterpart: the expected values are the ones an array gives.
    /// </summary>
    [TestCaseSource(nameof(EveryHostLaneUnderEveryAnswer))]
    public void AHostArrayLikeLaneReadsIteratorOnce(string lane, string answers, string expected)
    {
        var engine = new Engine();
        engine.SetValue("host", new HostList(engine));

        Run(engine, $"reads('{lane}', 'proto', '{answers}', true, function () {{ return host; }})").Should().Be(expected);
    }

    /// <summary>
    /// The read GetMethod refuses still throws the TypeError it did: GetIterator's for a value that is not iterable,
    /// and GetMethod's for one that is not callable, naming the spread argument as before.
    /// </summary>
    [TestCase("undefined", "arr is not iterable")]
    [TestCase("null", "arr is not iterable")]
    [TestCase("42", "Value returned for property 'Symbol(Symbol.iterator)' of object is not a function")]
    public void ASpreadOfAnArrayThatIsNotIterableThrowsWhatItDid(string method, string message)
    {
        var engine = new Engine();
        var exception = Caught.Exception(() => engine.Execute($$"""
            const arr = [1, 2];
            Object.defineProperty(arr, Symbol.iterator, { value: {{method}} });
            [...arr];
            """));

        exception.Should().BeOfType<JavaScriptException>().Which.Message.Should().Be(message);
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
}
