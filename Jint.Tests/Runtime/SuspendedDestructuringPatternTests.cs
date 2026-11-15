#nullable enable

using System.Text.RegularExpressions;

namespace Jint.Tests.Runtime;

/// <summary>
/// A destructuring pattern that suspends part-way through, at an <c>await</c> or a <c>yield</c> in a default, in a
/// computed key, in a member target or in a nested pattern, must resume at the element or property that suspended:
/// the iterator is not stepped again and the properties already read are not read again.
/// <para>
/// Jint resumes a suspended body by replaying the statement it suspended in, and the completed-await and
/// resumed-yield memos hand back only the suspensions themselves. Before sebastienros/jint#4313 the pattern replayed
/// from its first element. An array pattern over an iterator kept the iterator but not its position, so the replay
/// stepped it again and bound every element after the suspension to the wrong value (<c>[a = yield, b]</c> over
/// <c>undefined, 2</c> came out <c>a = 2, b = 3</c>, and a <c>throw()</c> never reached the yield at all). An
/// object pattern read every property before the suspended one a second time, getters and all. The right-hand side
/// of the assignment or the declaration was evaluated again too.
/// </para>
/// <para>
/// Each row runs the same body in an async function (<c>S(v)</c> is <c>await v</c>), a generator and an async
/// generator (<c>S(v)</c> is <c>yield v</c>, resumed with the value it yielded), and every expected string is
/// Node 24's answer to that script: the report, then everything the iterators, getters and helpers logged.
/// The semantics are those of DestructuringAssignmentEvaluation
/// (https://tc39.es/ecma262/#sec-runtime-semantics-destructuringassignmentevaluation),
/// IteratorDestructuringAssignmentEvaluation
/// (https://tc39.es/ecma262/#sec-runtime-semantics-iteratordestructuringassignmentevaluation),
/// KeyedDestructuringAssignmentEvaluation
/// (https://tc39.es/ecma262/#sec-runtime-semantics-keyeddestructuringassignmentevaluation), BindingInitialization
/// (https://tc39.es/ecma262/#sec-runtime-semantics-bindinginitialization), IteratorBindingInitialization
/// (https://tc39.es/ecma262/#sec-runtime-semantics-iteratorbindinginitialization) and KeyedBindingInitialization
/// (https://tc39.es/ecma262/#sec-runtime-semantics-keyedbindinginitialization): every element steps the iterator
/// once and every property is read once, left to right.
/// </para>
/// </summary>
public class SuspendedDestructuringPatternTests
{
    private const string Setup = """
        var log = [];
        function iter(name, values) {
          return { [Symbol.iterator]() { log.push(name + ':iter'); var i = 0; return {
            next() { var k = i++; log.push(name + ':next' + k); return { value: values[k], done: k >= values.length }; },
            return() { log.push(name + ':return'); return {}; } }; } };
        }
        function src(name, obj) {
          var o = {};
          Object.keys(obj).forEach(function (k) {
            Object.defineProperty(o, k, { get: function () { log.push(name + '.' + k); return obj[k]; }, enumerable: true, configurable: true });
          });
          return o;
        }
        function rhs(v) { log.push('rhs'); return v; }
        function key(k) { log.push('key ' + k); return k; }
        function side(v) { log.push('side'); return v; }
        var o = { set x(v) { log.push('set x=' + v); this._x = v; }, get x() { return this._x; } };
        function target() { log.push('target'); return o; }

        """;

    private static readonly Regex Suspension = new(@"S\(([^()]*)\)", RegexOptions.CultureInvariant);

    private static string AsyncProbe(string body, string report) => new Engine().Evaluate(Setup + $$"""
        async function m() { log.push('pre'); {{Suspension.Replace(body, "(await $1)")}} log.push('post'); return {{report}}; }
        m().then(function (r) { return String(r) + '|' + log.join(','); }, function (e) { return 'rejected ' + e + '|' + log.join(','); });
        """).UnwrapIfPromise().AsString();

    /// <summary>
    /// Every <c>yield</c> is resumed with its own yielded value. The driver stops after a fixed number of steps, so a
    /// generator that never finishes fails the assertion instead of spinning the test host.
    /// </summary>
    private static string GeneratorProbe(string body, string report) => new Engine().Evaluate(Setup + $$"""
        function* g() { log.push('pre'); {{Suspension.Replace(body, "(yield $1)")}} log.push('post'); return {{report}}; }
        var it = g();
        var res = it.next();
        for (var i = 0; !res.done && i < 8; i++) res = it.next(res.value);
        String(res.value) + (res.done ? '' : ' (not done after 8 steps)') + '|' + log.join(',');
        """).AsString();

    private static string AsyncGeneratorProbe(string body, string report) => new Engine().Evaluate(Setup + $$"""
        async function* ag() { log.push('pre'); {{Suspension.Replace(body, "(yield $1)")}} log.push('post'); return {{report}}; }
        async function drive() {
          var it = ag();
          var res = await it.next();
          for (var i = 0; !res.done && i < 8; i++) res = await it.next(res.value);
          return String(res.value) + (res.done ? '' : ' (not done after 8 steps)') + '|' + log.join(',');
        }
        drive();
        """).UnwrapIfPromise().AsString();

    private readonly record struct Row(string Body, string Report, string Expected);

    private static readonly Dictionary<string, Row> Rows = new(StringComparer.Ordinal)
    {
        // ------------------------------------------------------------------ array assignment patterns

        ["ArrayFirstElement"] = new(
            "var a, b; [a = S(5), b] = rhs(iter('i', [undefined, 2]));", "a + ',' + b",
            "5,2|pre,rhs,i:iter,i:next0,i:next1,i:return,post"),
        ["ArrayMiddleElement"] = new(
            "var a, b, c; [a, b = S(5), c] = rhs(iter('i', [1, undefined, 3]));", "a + ',' + b + ',' + c",
            "1,5,3|pre,rhs,i:iter,i:next0,i:next1,i:next2,i:return,post"),
        ["ArrayAfterElision"] = new(
            "var a; [, a = S(5)] = iter('i', [1, undefined]);", "a",
            "5|pre,i:iter,i:next0,i:next1,i:return,post"),
        ["ArrayTwoSuspensions"] = new(
            "var a, b; [a = S(5), b = S(6)] = rhs(iter('i', [undefined, undefined]));", "a + ',' + b",
            "5,6|pre,rhs,i:iter,i:next0,i:next1,i:return,post"),
        ["ArrayMemberTarget"] = new(
            "var b; [target().x = S(5), b] = iter('i', [undefined, 2]);", "o.x + ',' + b",
            "5,2|pre,i:iter,target,i:next0,set x=5,i:next1,i:return,post"),
        ["ArrayComputedMemberTarget"] = new(
            "var p = {}; [p[key('k')] = S(5)] = iter('i', [undefined]);", "p.k",
            "5|pre,i:iter,key k,i:next0,i:return,post"),
        ["ArraySuspensionInMemberKey"] = new(
            "var b; [target()[S('x')], b] = iter('i', [1, 2]);", "o.x + ',' + b",
            "1,2|pre,i:iter,target,i:next0,set x=1,i:next1,i:return,post"),
        ["ArrayRestAfterSuspension"] = new(
            "var a, r; [a = S(5), ...r] = iter('i', [undefined, 2, 3]);", "a + ',' + JSON.stringify(r)",
            "5,[2,3]|pre,i:iter,i:next0,i:next1,i:next2,i:next3,post"),
        ["ArraySuspensionInRestPattern"] = new(
            "var a; [...[a = S(5)]] = iter('i', [undefined]);", "a",
            "5|pre,i:iter,i:next0,i:next1,post"),
        ["ArrayNested"] = new(
            "var a, b; [[a = S(5)], b] = iter('o', [iter('n', [undefined]), 2]);", "a + ',' + b",
            "5,2|pre,o:iter,o:next0,n:iter,n:next0,n:return,o:next1,o:return,post"),
        ["ArrayFastLane"] = new(
            "var a, b; [a = S(5), b] = rhs([undefined, 2]);", "a + ',' + b",
            "5,2|pre,rhs,post"),
        ["ArrayDefaultOperandBeforeSuspension"] = new(
            "var a; [a = side(1) + S(4)] = [undefined];", "a",
            "5|pre,side,post"),
        ["ArrayDefaultSequence"] = new(
            "var a; [a = (side(), S(5))] = [undefined];", "a",
            "5|pre,side,post"),
        ["ArrayAssignmentValue"] = new(
            "var a, b; var v = ([a = S(5), b] = rhs(iter('i', [undefined, 2]))); log.push(typeof v[Symbol.iterator]);", "a + ',' + b",
            "5,2|pre,rhs,i:iter,i:next0,i:next1,i:return,function,post"),
        ["ArrayNestedPatternDefault"] = new(
            "var a; [[a] = S([5])] = iter('i', [undefined]);", "a",
            "5|pre,i:iter,i:next0,i:return,post"),
        ["ArrayDefaultThenNestedSuspension"] = new(
            "var a; [[a = S(5)] = rhs([])] = iter('i', [undefined]);", "a",
            "5|pre,i:iter,i:next0,rhs,i:return,post"),
        ["ArrayRestMemberTargetAfterSuspension"] = new(
            "var a, p = {}; [a = S(5), ...p[key('r')]] = iter('i', [undefined, 2]);", "a + ',' + JSON.stringify(p.r)",
            "5,[2]|pre,i:iter,i:next0,key r,i:next1,i:next2,post"),
        ["ArrayFastLaneRest"] = new(
            "var a, r; [a = S(5), ...r] = rhs([undefined, 2, 3]);", "a + ',' + JSON.stringify(r)",
            "5,[2,3]|pre,rhs,post"),
        ["ArrayOverAString"] = new(
            "var a, b; [a, b = S(5)] = rhs('x');", "a + ',' + b",
            "x,5|pre,rhs,post"),
        ["ArrayInALoop"] = new(
            "var out = []; for (var k = 0; k < 2; k++) { var a, b; [a = S(k), b] = iter('i' + k, [undefined, k]); out.push(a + ',' + b); }", "out.join(';')",
            "0,0;1,1|pre,i0:iter,i0:next0,i0:next1,i0:return,i1:iter,i1:next0,i1:next1,i1:return,post"),

        // ------------------------------------------------------------------ object assignment patterns

        ["ObjectFirstProperty"] = new(
            "var a, b; ({ x: a = S(5), y: b } = rhs(src('s', { x: undefined, y: 2 })));", "a + ',' + b",
            "5,2|pre,rhs,s.x,s.y,post"),
        ["ObjectMiddleProperty"] = new(
            "var a, b, c; ({ x: a, y: b = S(5), z: c } = src('s', { x: 1, y: undefined, z: 3 }));", "a + ',' + b + ',' + c",
            "1,5,3|pre,s.x,s.y,s.z,post"),
        ["ObjectComputedKey"] = new(
            "var a, b; ({ [key('x')]: a = S(5), y: b } = src('s', { x: undefined, y: 2 }));", "a + ',' + b",
            "5,2|pre,key x,s.x,s.y,post"),
        ["ObjectRestAfterSuspension"] = new(
            "var a, r; ({ x: a = S(5), ...r } = src('s', { x: undefined, y: 2 }));", "a + ',' + JSON.stringify(r)",
            "5,{\"y\":2}|pre,s.x,s.y,post"),
        ["ObjectNested"] = new(
            "var a, b; ({ x: { y: a = S(5) }, z: b } = src('s', { x: src('t', { y: undefined }), z: 2 }));", "a + ',' + b",
            "5,2|pre,s.x,t.y,s.z,post"),
        ["ObjectMemberTarget"] = new(
            "var b; ({ x: target().x = S(5), y: b } = src('s', { x: undefined, y: 2 }));", "o.x + ',' + b",
            "5,2|pre,target,s.x,set x=5,s.y,post"),
        ["ObjectShorthand"] = new(
            "var x, y; ({ x = S(5), y } = src('s', { x: undefined, y: 2 }));", "x + ',' + y",
            "5,2|pre,s.x,s.y,post"),
        ["ObjectSuspensionInComputedKey"] = new(
            "var a, b; ({ [S('x')]: a, y: b } = rhs(src('s', { x: 1, y: 2 })));", "a + ',' + b",
            "1,2|pre,rhs,s.x,s.y,post"),
        ["ObjectArrayInObject"] = new(
            "var a, b; ({ x: [a = S(5)], y: b } = src('s', { x: iter('i', [undefined]), y: 2 }));", "a + ',' + b",
            "5,2|pre,s.x,i:iter,i:next0,i:return,s.y,post"),
        ["ObjectTwoSuspensions"] = new(
            "var a, b; ({ x: a = S(5), y: b = S(6) } = rhs(src('s', { x: undefined, y: undefined })));", "a + ',' + b",
            "5,6|pre,rhs,s.x,s.y,post"),
        ["ObjectDefaultThenNestedSuspension"] = new(
            "var a; ({ x: [a = S(5)] = rhs([]) } = src('s', { x: undefined }));", "a",
            "5|pre,s.x,rhs,post"),
        ["ObjectInALoop"] = new(
            "var out = []; for (var k = 0; k < 2; k++) { var a, b; ({ x: a = S(k), y: b } = src('s' + k, { x: undefined, y: k })); out.push(a + ',' + b); }", "out.join(';')",
            "0,0;1,1|pre,s0.x,s0.y,s1.x,s1.y,post"),

        // The target is resolved before the property is read (KeyedDestructuringAssignmentEvaluation step 1.a),
        // and a resume must not resolve it again: a `with` object's `has` trap sees every resolution. V8 resolves the
        // target only when it writes it, so Node 24 logs `s.x` before `has a`; the order here is the specification's.
        ["ObjectTargetResolvedOnce"] = new(
            "var a; var p = new Proxy({}, { has(t, k) { if (k === 'a') log.push('has a'); return false; } }); with (p) { ({ x: a = S(5) } = src('s', { x: undefined })); }", "a",
            "5|pre,has a,s.x,post"),
        ["ObjectTargetResolvedOnceControl"] = new(
            "var a; var p = new Proxy({}, { has(t, k) { if (k === 'a') log.push('has a'); return false; } }); with (p) { ({ x: a = 5 } = src('s', { x: undefined })); }", "a",
            "5|pre,has a,s.x,post"),

        // ------------------------------------------------------------------ catch parameters

        ["CatchParameterArray"] = new(
            "try { throw rhs(iter('i', [undefined, 2])); } catch ([a = S(5), b]) { log.push(a + ',' + b); }", "'done'",
            "done|pre,rhs,i:iter,i:next0,i:next1,i:return,5,2,post"),
        ["CatchParameterObject"] = new(
            "try { throw src('s', { x: undefined, y: 2 }); } catch ({ x: a = S(5), y: b }) { log.push(a + ',' + b); }", "'done'",
            "done|pre,s.x,s.y,5,2,post"),
        ["CatchParameterThenFinally"] = new(
            "try { try { throw rhs(iter('i', [1, undefined])); } catch ([x, a = S(5)]) { log.push(x + ',' + a); } finally { log.push('finally'); } } catch (e) { log.push('outer ' + e); }", "'done'",
            "done|pre,rhs,i:iter,i:next0,i:next1,i:return,1,5,finally,post"),

        // ------------------------------------------------------------------ binding patterns

        ["LetArray"] = new(
            "let [a = S(5), b] = rhs(iter('i', [undefined, 2]));", "a + ',' + b",
            "5,2|pre,rhs,i:iter,i:next0,i:next1,i:return,post"),
        ["ConstObject"] = new(
            "const { x: a = S(5), y: b } = rhs(src('s', { x: undefined, y: 2 }));", "a + ',' + b",
            "5,2|pre,rhs,s.x,s.y,post"),
        ["VarArray"] = new(
            "var [a = S(5), b] = rhs(iter('i', [undefined, 2]));", "a + ',' + b",
            "5,2|pre,rhs,i:iter,i:next0,i:next1,i:return,post"),
        ["NestedBinding"] = new(
            "let [{ x: a = S(5) }, b] = iter('i', [src('s', { x: undefined }), 2]);", "a + ',' + b",
            "5,2|pre,i:iter,i:next0,s.x,i:next1,i:return,post"),
        ["PatternThenDeclarator"] = new(
            "let [a = S(5)] = rhs(iter('i', [undefined])), b = side(2);", "a + ',' + b",
            "5,2|pre,rhs,i:iter,i:next0,i:return,side,post"),
        ["DeclaratorThenPattern"] = new(
            "let b = side(2), [a = S(5)] = rhs(iter('i', [undefined]));", "a + ',' + b",
            "5,2|pre,side,rhs,i:iter,i:next0,i:return,post"),
        ["LetRest"] = new(
            "let [a = S(5), ...r] = iter('i', [undefined, 2, 3]);", "a + ',' + JSON.stringify(r)",
            "5,[2,3]|pre,i:iter,i:next0,i:next1,i:next2,i:next3,post"),
        ["ConstObjectRest"] = new(
            "const { x: a = S(5), ...r } = src('s', { x: undefined, y: 2 });", "a + ',' + JSON.stringify(r)",
            "5,{\"y\":2}|pre,s.x,s.y,post"),

        // The bindings initialized before the suspension have to be found initialized in the environment the
        // resume continues in.
        ["LetInABlockAfterABinding"] = new(
            "var out; { let [x, a = S(5)] = rhs(iter('i', [1, undefined])); out = x + ',' + a; }", "out",
            "1,5|pre,rhs,i:iter,i:next0,i:next1,i:return,post"),
        ["ConstObjectInABlockAfterABinding"] = new(
            "var out; { const { x, y: a = S(5) } = rhs(src('s', { x: 1, y: undefined })); out = x + ',' + a; }", "out",
            "1,5|pre,rhs,s.x,s.y,post"),
        ["ForStatementInit"] = new(
            "var out; for (let [a = S(5), b] = rhs(iter('i', [undefined, 2])); !out; ) { out = a + ',' + b; }", "out",
            "5,2|pre,rhs,i:iter,i:next0,i:next1,i:return,post"),
        ["ForStatementInitAfterABinding"] = new(
            "var out; for (let [x, a = S(5)] = rhs(iter('i', [1, undefined])); !out; ) { out = x + ',' + a; }", "out",
            "1,5|pre,rhs,i:iter,i:next0,i:next1,i:return,post"),

        // ------------------------------------------------------------------ for-of heads

        ["ForOfConstArrayHead"] = new(
            "var out = []; for (const [a = S(5), b] of iter('outer', [iter('i', [undefined, 2])])) out.push(a + ',' + b);", "out.join(';')",
            "5,2|pre,outer:iter,outer:next0,i:iter,i:next0,i:next1,i:return,outer:next1,post"),
        ["ForOfAssignmentArrayHead"] = new(
            "var a, b, out = []; for ([a = S(5), b] of iter('outer', [iter('i', [undefined, 2])])) out.push(a + ',' + b);", "out.join(';')",
            "5,2|pre,outer:iter,outer:next0,i:iter,i:next0,i:next1,i:return,outer:next1,post"),
        ["ForOfConstObjectHead"] = new(
            "var out = []; for (const { x: a = S(5), y: b } of iter('outer', [src('s', { x: undefined, y: 2 })])) out.push(a + ',' + b);", "out.join(';')",
            "5,2|pre,outer:iter,outer:next0,s.x,s.y,outer:next1,post"),
        ["ForOfSecondIteration"] = new(
            "var out = []; for (const [a = S(5)] of iter('outer', [iter('i', [1]), iter('j', [undefined])])) out.push(a);", "out.join(';')",
            "1;5|pre,outer:iter,outer:next0,i:iter,i:next0,i:return,outer:next1,j:iter,j:next0,j:return,outer:next2,post"),
        ["ForOfVarHead"] = new(
            "var out = []; for (var [a = S(5), b] of iter('outer', [iter('i', [undefined, 2])])) out.push(a + ',' + b);", "out.join(';')",
            "5,2|pre,outer:iter,outer:next0,i:iter,i:next0,i:next1,i:return,outer:next1,post"),
        ["ForOfHeadAndBody"] = new(
            "var out = []; for (const [a = S(5)] of iter('outer', [iter('i', [undefined])])) { out.push(a); out.push(S(6)); }", "out.join(';')",
            "5;6|pre,outer:iter,outer:next0,i:iter,i:next0,i:return,outer:next1,post"),
        ["ForInConstHead"] = new(
            "var out = []; for (const [a, b = S(5)] in { a: 1 }) out.push(a + ',' + b);", "out.join(';')",
            "a,5|pre,post"),
        ["ForInAssignmentHead"] = new(
            "var a, b, out = []; for ([a, b = S(5)] in { a: 1, c: 2 }) out.push(a + ',' + b);", "out.join(';')",
            "a,5;c,5|pre,post"),
    };

    /// <summary>
    /// <c>for await</c> heads, which only an async function or an async generator can hold.
    /// </summary>
    private static readonly Dictionary<string, Row> AsyncRows = new(StringComparer.Ordinal)
    {
        ["ForAwaitArrayHead"] = new(
            "var out = []; for await (const [a = S(5), b] of iter('outer', [iter('i', [undefined, 2])])) out.push(a + ',' + b);", "out.join(';')",
            "5,2|pre,outer:iter,outer:next0,i:iter,i:next0,i:next1,i:return,outer:next1,post"),
        ["ForAwaitSecondIteration"] = new(
            "var out = []; for await (const { x: a = S(5) } of iter('outer', [src('s', { x: 1 }), src('t', { x: undefined })])) out.push(a);", "out.join(';')",
            "1;5|pre,outer:iter,outer:next0,s.x,outer:next1,t.x,outer:next2,post"),
        ["ForAwaitHeadAndBody"] = new(
            "var out = []; for await (const [a = S(5), b] of iter('outer', [iter('i', [undefined, 2]), iter('j', [undefined, 3])])) { out.push(a + ',' + b); out.push(S(6)); }", "out.join(';')",
            "5,2;6;5,3;6|pre,outer:iter,outer:next0,i:iter,i:next0,i:next1,i:return,outer:next1,j:iter,j:next0,j:next1,j:return,outer:next2,post"),
    };

    public static TestCases<string> RowNames() => new(Rows.Keys);

    public static TestCases<string> AsyncRowNames() => new(AsyncRows.Keys);

    [TestCaseSource(nameof(AsyncRowNames))]
    public void ForAwaitInAnAsyncFunction(string name)
    {
        var row = AsyncRows[name];
        AsyncProbe(row.Body, row.Report).Should().Be(row.Expected);
    }

    [TestCaseSource(nameof(AsyncRowNames))]
    public void ForAwaitInAnAsyncGenerator(string name)
    {
        var row = AsyncRows[name];
        AsyncGeneratorProbe(row.Body, row.Report).Should().Be(row.Expected);
    }

    [TestCaseSource(nameof(RowNames))]
    public void InAnAsyncFunction(string name)
    {
        var row = Rows[name];
        AsyncProbe(row.Body, row.Report).Should().Be(row.Expected);
    }

    [TestCaseSource(nameof(RowNames))]
    public void InAGenerator(string name)
    {
        var row = Rows[name];
        GeneratorProbe(row.Body, row.Report).Should().Be(row.Expected);
    }

    [TestCaseSource(nameof(RowNames))]
    public void InAnAsyncGenerator(string name)
    {
        var row = Rows[name];
        AsyncGeneratorProbe(row.Body, row.Report).Should().Be(row.Expected);
    }

    // ------------------------------------------------------------------ the reported shape and abrupt resumptions

    /// <summary>
    /// The script from the issue: the replay stepped the iterator a third time and bound <c>a = 2, b = 3</c>.
    /// </summary>
    [Test]
    public void TheReportedShape()
    {
        new Engine().Evaluate(Setup + """
            var a, b; var it = (function* () { [a = yield 0, b] = iter('i', [undefined, 2]); })(); it.next(); it.next(5);
            a + ',' + b + '|' + log.join(',');
            """).AsString().Should().Be("5,2|i:iter,i:next0,i:next1,i:return");
    }

    /// <summary>
    /// A <c>throw()</c> at a default must reach that default's <c>yield</c>: the replay used to step the iterator to
    /// a value that skipped the default, so the throw was lost and the pattern completed normally.
    /// </summary>
    [Test]
    public void AThrowAtAnArrayDefaultReachesTheYield()
    {
        GeneratorThrow("try { [a = yield 0, b] = iter('i', [undefined, 2]); log.push('after'); } catch (e) { log.push('caught ' + e); } return a + ',' + b;")
            .Should().Be("undefined,undefined,true|i:iter,i:next0,i:return,caught boom");
    }

    [Test]
    public void AThrowAtANestedArrayDefaultClosesBothIterators()
    {
        GeneratorThrow("try { [[a = yield 0], b] = iter('o', [iter('n', [undefined]), 2]); log.push('after'); } catch (e) { log.push('caught ' + e); } return a + ',' + b;")
            .Should().Be("undefined,undefined,true|o:iter,o:next0,n:iter,n:next0,n:return,o:return,caught boom");
    }

    [Test]
    public void AThrowAtAnObjectDefaultReadsNothingAgain()
    {
        GeneratorThrow("try { ({ x: a = yield 0, y: b } = src('s', { x: undefined, y: 2 })); log.push('after'); } catch (e) { log.push('caught ' + e); } return a + ',' + b;")
            .Should().Be("undefined,undefined,true|s.x,caught boom");
    }

    [Test]
    public void AReturnAtAnArrayDefaultClosesTheIterator()
    {
        GeneratorReturn("try { [a = yield 0, b] = iter('i', [undefined, 2]); log.push('after'); } finally { log.push('finally'); }")
            .Should().Be("9,true|i:iter,i:next0,i:return,finally");
    }

    /// <summary>
    /// Both iterators are closed, innermost first, as each pattern's own IteratorClose propagates the return
    /// completion outwards.
    /// </summary>
    [Test]
    public void AReturnAtANestedArrayDefaultClosesBothIterators()
    {
        GeneratorReturn("[[a = yield 0], b] = iter('o', [iter('n', [undefined]), 2]); log.push('after');")
            .Should().Be("9,true|o:iter,o:next0,n:iter,n:next0,n:return,o:return");
    }

    [Test]
    public void AReturnAtANestedArrayDefaultInAnAsyncGeneratorClosesBothIterators()
    {
        new Engine().Evaluate(Setup + """
            var a, b; async function* g() { [[a = yield 0], b] = iter('o', [iter('n', [undefined]), 2]); log.push('after'); }
            var it = g(); it.next().then(function () { return it.return(9); }).then(function (r) { return String(r.value) + ',' + r.done + '|' + log.join(','); });
            """).UnwrapIfPromise().AsString().Should().Be("9,true|o:iter,o:next0,n:iter,n:next0,n:return,o:return");
    }

    /// <summary>
    /// A <c>return()</c> that throws replaces the return completion it was closing with (IteratorClose step 7), so
    /// the <c>catch</c> sees the throw; a <c>throw()</c> keeps its own completion and the failing <c>return()</c> is
    /// swallowed (step 5).
    /// </summary>
    [Test]
    public void AFailingIteratorReturnReplacesAReturnButNotAThrow()
    {
        const string Body = """
            var a, b; function bad() { return { [Symbol.iterator]() { return { next() { log.push('next'); return { value: undefined, done: false }; }, return() { log.push('return'); throw 'close error'; } }; } }; }
            function* g() { try { [a = yield 0, b] = bad(); } catch (e) { log.push('caught ' + e); } return 'end'; }
            var it = g(); it.next();
            """;

        new Engine().Evaluate(Setup + Body + "var r = it.return(9); String(r.value) + ',' + r.done + '|' + log.join(',');")
            .AsString().Should().Be("end,true|next,return,caught close error");
        new Engine().Evaluate(Setup + Body + "var r = it.throw('boom'); String(r.value) + ',' + r.done + '|' + log.join(',');")
            .AsString().Should().Be("end,true|next,return,caught boom");
    }

    /// <summary>
    /// Two live generators of one function suspended in the same pattern: what each parks belongs to it, not to the
    /// handler they share.
    /// </summary>
    [Test]
    public void TwoGeneratorsSuspendedInOnePatternKeepTheirOwnPlace()
    {
        new Engine().Evaluate(Setup + """
            function* g(tag) { var a, b; [a = yield tag, b] = iter(tag, [undefined, tag + '2']); return a + ',' + b; }
            var g1 = g('x'), g2 = g('y'); var r = [g1.next().value, g2.next().value, g1.next('X').value, g2.next('Y').value]; r.join(';') + '|' + log.join(',');
            """).AsString().Should().Be("x;y;X,x2;Y,y2|x:iter,x:next0,y:iter,y:next0,x:next1,x:return,y:next1,y:return");
    }

    /// <summary>
    /// A <c>yield*</c> in a default resumes once per delegated step; the pattern steps its own iterator once.
    /// </summary>
    [Test]
    public void ADelegatingYieldInADefaultStepsThePatternsIteratorOnce()
    {
        new Engine().Evaluate(Setup + """
            var a, b; function* inner() { log.push('inner'); yield 1; yield 2; return 7; } function* g() { [a = yield* inner(), b] = iter('i', [undefined, 2]); return a + ',' + b; }
            var it = g(); var res = it.next(); var seen = [res.value]; for (var k = 0; !res.done && k < 8; k++) { res = it.next(res.value); seen.push(res.value); } seen.join(',') + '|' + log.join(',');
            """).AsString().Should().Be("1,2,7,2|i:iter,i:next0,inner,i:next1,i:return");
    }

    [Test]
    public void AReturnAtAnArrayDefaultInAnAsyncGeneratorClosesTheIterator()
    {
        new Engine().Evaluate(Setup + """
            var a, b; async function* g() { try { [a = yield 0, b] = iter('i', [undefined, 2]); log.push('after'); } finally { log.push('finally'); } }
            var it = g(); it.next().then(function () { return it.return(9); }).then(function (r) { return String(r.value) + ',' + r.done + '|' + log.join(','); });
            """).UnwrapIfPromise().AsString().Should().Be("9,true|i:iter,i:next0,i:return,finally");
    }

    [Test]
    public void AThrowAtAnArrayDefaultInAnAsyncGeneratorReachesTheYield()
    {
        new Engine().Evaluate(Setup + """
            var a, b; async function* g() { try { [a = yield 0, b] = iter('i', [undefined, 2]); log.push('after'); } catch (e) { log.push('caught ' + e); } return a + ',' + b; }
            var it = g(); it.next().then(function () { return it.throw('boom'); }).then(function (r) { return String(r.value) + ',' + r.done + '|' + log.join(','); });
            """).UnwrapIfPromise().AsString().Should().Be("undefined,undefined,true|i:iter,i:next0,i:return,caught boom");
    }

    /// <summary>
    /// The async twin of <see cref="AThrowAtAnArrayDefaultReachesTheYield"/>: the rejection is thrown at the
    /// <c>await</c> once, and the one iterator the pattern opened is the one closed.
    /// </summary>
    [Test]
    public void ARejectedAwaitAtAnArrayDefaultClosesTheIteratorItOpened()
    {
        new Engine().Evaluate(Setup + """
            var a, b; async function m() { try { [a = await Promise.reject('boom'), b] = iter('i', [undefined, 2]); log.push('after'); } catch (e) { log.push('caught ' + e); } return a + ',' + b; }
            m().then(function (r) { return String(r) + '|' + log.join(','); });
            """).UnwrapIfPromise().AsString().Should().Be("undefined,undefined|i:iter,i:next0,i:return,caught boom");
    }

    /// <summary>
    /// What a pattern parks for its resume belongs to that one suspension. A <c>throw()</c> that ends the pattern
    /// must leave nothing behind for the next evaluation of the same pattern, which here suspends in its right-hand
    /// side first and would otherwise take the stale state for a resume inside the pattern.
    /// </summary>
    [Test]
    public void AThrowAtADefaultLeavesNothingForTheNextEvaluation()
    {
        new Engine().Evaluate(Setup + """
            var a; function* g() { for (var k = 0; k < 2; k++) { try { ({ x: a = yield 'x' + k } = (yield 'rhs' + k, src('s', { x: undefined }))); log.push('a=' + a); } catch (e) { log.push('caught ' + e); } } }
            var it = g(); var seen = [it.next().value, it.next().value]; it.throw('boom'); seen.push(it.next().value); seen.push(it.next('A').done); seen.join(',') + '|' + log.join(',');
            """).AsString().Should().Be("rhs0,x0,x1,true|s.x,caught boom,s.x,a=A");
    }

    private static string GeneratorThrow(string body) => new Engine().Evaluate(Setup + $$"""
        var a, b; function* g() { {{body}} }
        var it = g(); it.next(); var r = it.throw('boom'); String(r.value) + ',' + r.done + '|' + log.join(',');
        """).AsString();

    private static string GeneratorReturn(string body) => new Engine().Evaluate(Setup + $$"""
        var a, b; function* g() { {{body}} }
        var it = g(); it.next(); var r = it.return(9); String(r.value) + ',' + r.done + '|' + log.join(',');
        """).AsString();
}
