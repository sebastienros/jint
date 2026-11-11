#nullable enable

namespace Jint.Tests.Runtime;

/// <summary>
/// An elision in an array pattern steps the iterator and discards the step, and never reads the step result's
/// <c>value</c>. <c>Elision : ,</c> performs <c>IteratorStep</c>, which calls <c>next</c> and reads <c>done</c>
/// (https://tc39.es/ecma262/#sec-iteratorstep), where an element that binds performs <c>IteratorStepValue</c>
/// (https://tc39.es/ecma262/#sec-iteratorstepvalue) and reads <c>value</c> after it. The rule is the same in
/// IteratorDestructuringAssignmentEvaluation
/// (https://tc39.es/ecma262/#sec-runtime-semantics-iteratordestructuringassignmentevaluation), which an assignment
/// pattern runs and which IteratorBindingInitialization hands every elision of a binding pattern to
/// (https://tc39.es/ecma262/#sec-runtime-semantics-iteratorbindinginitialization). Jint stepped an elision with the
/// value read, so <c>(function ([,]) {})(it)</c> logged <c>next,done,value,return</c> for an iterator whose result
/// has a logging <c>value</c> getter, where Node logs <c>next,done,return</c>.
/// <para>
/// Every expected string is Node's answer to the same script, the iterator's own log: each <c>next()</c> call, each
/// <c>done</c> and <c>value</c> read on its result object, each <c>return()</c> close, then what the script itself
/// logged. The rows cover every place an array pattern is evaluated (a parameter, an assignment, a declaration, a
/// <c>for</c>-<c>of</c> head, a <c>catch</c> parameter), the lane a pattern that may suspend takes in an async
/// function, a generator and an async generator, and the built-in iterators, whose step results have no observable
/// <c>value</c>. None of them uses a real array, whose pattern reads by index and never creates the iterator, except
/// the two that pin what that lane still reads.
/// </para>
/// </summary>
public class DestructuringElisionStepTests
{
    /// <summary>
    /// The logging iterables. <c>iterableOf</c> hands out a result whose <c>done</c> and <c>value</c> are getters that
    /// log; <c>throwingDone</c> throws from <c>done</c> and <c>throwingValue</c> from <c>value</c>.
    /// </summary>
    private const string Prelude = """
        var log = [];
        function iterableOf(values) {
            var i = 0;
            return {
                [Symbol.iterator]() { return this; },
                next() {
                    log.push('next');
                    var has = i < values.length;
                    var v = has ? values[i++] : undefined;
                    return {
                        get done() { log.push('done'); return !has; },
                        get value() { log.push('value'); return v; }
                    };
                },
                return() { log.push('return'); return {}; }
            };
        }
        function throwingDone() {
            return {
                [Symbol.iterator]() { return this; },
                next() {
                    log.push('next');
                    return {
                        get done() { log.push('done'); throw new Error('boom'); },
                        get value() { log.push('value'); return 1; }
                    };
                },
                return() { log.push('return'); return {}; }
            };
        }
        function throwingValue(values) {
            var i = 0;
            return {
                [Symbol.iterator]() { return this; },
                next() {
                    log.push('next');
                    var has = i < values.length;
                    var v = has ? values[i++] : undefined;
                    return {
                        get done() { log.push('done'); return !has; },
                        get value() { log.push('value'); throw new Error('value read'); }
                    };
                },
                return() { log.push('return'); return {}; }
            };
        }
        """;

    private static string Run(string script)
    {
        var engine = new Engine();
        engine.Execute(Prelude);
        engine.Execute(script);
        engine.Advanced.ProcessTasks();
        return engine.Evaluate("log.join(',')").AsString();
    }

    // ------------------------------------------------------------------ the rows

    public static TheoryData<string, string, string> ParameterRows() => new()
    {
        { "ParamLeadingHole",
          "(function ([,]) {})(iterableOf([1, 2]));",
          "next,done,return" },
        { "ParamHoleThenElement",
          "(function ([, a]) { log.push('a=' + a); })(iterableOf([1, 2]));",
          "next,done,next,done,value,return,a=2" },
        { "ParamTrailingHole",
          "(function ([a, ,]) { log.push('a=' + a); })(iterableOf([1, 2, 3]));",
          "next,done,value,next,done,return,a=1" },
        { "ParamConsecutiveHoles",
          "(function ([, , a]) { log.push('a=' + a); })(iterableOf([1, 2, 3, 4]));",
          "next,done,next,done,next,done,value,return,a=3" },
        { "ParamHoleBetweenElements",
          "(function ([a, , b]) { log.push('a=' + a + ',b=' + b); })(iterableOf([1, 2, 3]));",
          "next,done,value,next,done,next,done,value,return,a=1,b=3" },
        { "ParamHoleThenRest",
          "(function ([, ...r]) { log.push('r=' + r); })(iterableOf([1, 2, 3]));",
          "next,done,next,done,value,next,done,value,next,done,r=2,3" },
        { "ParamHoleOnDoneIterator",
          "(function ([,]) {})(iterableOf([]));",
          "next,done" },
        { "ParamHoleAfterDoneStep",
          "(function ([a, , b = 7]) { log.push('a=' + a + ',b=' + b); })(iterableOf([]));",
          "next,done,a=undefined,b=7" },
        { "ParamHoleThenDefault",
          "(function ([, a = 7]) { log.push('a=' + a); })(iterableOf([1]));",
          "next,done,next,done,a=7" },
        { "ParamNestedHole",
          "(function ([[,]]) {})(iterableOf([iterableOf([1, 2])]));",
          "next,done,value,next,done,return,return" },
        { "ParamPatternDefault",
          "(function ([,] = iterableOf([1, 2])) {})();",
          "next,done,return" },
        { "ParamArrowFunction",
          "(([, a]) => { log.push('a=' + a); })(iterableOf([1, 2]));",
          "next,done,next,done,value,return,a=2" },
        { "ParamMethod",
          "({ m([, a]) { log.push('a=' + a); } }).m(iterableOf([1, 2]));",
          "next,done,next,done,value,return,a=2" },
        { "ParamAsyncFunction",
          "(async function ([, a]) { log.push('body a=' + a); })(iterableOf([1, 2]));",
          "next,done,next,done,value,return,body a=2" },
        { "ParamGenerator",
          "(function* ([, a]) { log.push('body a=' + a); })(iterableOf([1, 2]));",
          "next,done,next,done,value,return" },
        { "ParamAsyncGenerator",
          "(async function* ([, a]) { log.push('body a=' + a); })(iterableOf([1, 2]));",
          "next,done,next,done,value,return" },
        { "ParamHoleStepThrows",
          "try { (function ([,]) {})(throwingDone()); } catch (e) { log.push('caught ' + e.message); }",
          "next,done,caught boom" },
        { "ParamHoleValueGetterThrows",
          "(function ([,]) {})(throwingValue([1, 2]));",
          "next,done,return" },
    };

    public static TheoryData<string, string, string> AssignmentRows() => new()
    {
        { "AssignLeadingHole",
          "[,] = iterableOf([1, 2]);",
          "next,done,return" },
        { "AssignHoleThenElement",
          "var a; [, a] = iterableOf([1, 2]); log.push('a=' + a);",
          "next,done,next,done,value,return,a=2" },
        { "AssignTrailingHole",
          "var a; [a, ,] = iterableOf([1, 2, 3]); log.push('a=' + a);",
          "next,done,value,next,done,return,a=1" },
        { "AssignConsecutiveHoles",
          "var a; [, , a] = iterableOf([1, 2, 3, 4]); log.push('a=' + a);",
          "next,done,next,done,next,done,value,return,a=3" },
        { "AssignHoleBetweenElements",
          "var a, b; [a, , b] = iterableOf([1, 2, 3]); log.push('a=' + a + ',b=' + b);",
          "next,done,value,next,done,next,done,value,return,a=1,b=3" },
        { "AssignHoleThenRest",
          "var r; [, ...r] = iterableOf([1, 2, 3]); log.push('r=' + r);",
          "next,done,next,done,value,next,done,value,next,done,r=2,3" },
        { "AssignHoleOnDoneIterator",
          "[,] = iterableOf([]);",
          "next,done" },
        { "AssignHoleAfterDoneStep",
          "var a, b; [a, , b = 7] = iterableOf([]); log.push('a=' + a + ',b=' + b);",
          "next,done,a=undefined,b=7" },
        { "AssignHoleThenDefault",
          "var a; [, a = 7] = iterableOf([1]); log.push('a=' + a);",
          "next,done,next,done,a=7" },
        { "AssignNestedHole",
          "[[,]] = iterableOf([iterableOf([1, 2])]);",
          "next,done,value,next,done,return,return" },
        { "AssignMemberTargetAfterHole",
          "var t = {}; [, t.a] = iterableOf([1, 2]); log.push('t.a=' + t.a);",
          "next,done,next,done,value,return,t.a=2" },
        { "AssignHoleStepThrows",
          "try { [,] = throwingDone(); } catch (e) { log.push('caught ' + e.message); }",
          "next,done,caught boom" },
        { "AssignHoleValueGetterThrows",
          "[,] = throwingValue([1, 2]);",
          "next,done,return" },
    };

    public static TheoryData<string, string, string> DeclarationRows() => new()
    {
        { "VarHole",
          "var [,] = iterableOf([1, 2]);",
          "next,done,return" },
        { "VarHoleThenElement",
          "var [, a] = iterableOf([1, 2]); log.push('a=' + a);",
          "next,done,next,done,value,return,a=2" },
        { "VarTrailingHole",
          "var [a, ,] = iterableOf([1, 2, 3]); log.push('a=' + a);",
          "next,done,value,next,done,return,a=1" },
        { "VarConsecutiveHoles",
          "var [, , a] = iterableOf([1, 2, 3, 4]); log.push('a=' + a);",
          "next,done,next,done,next,done,value,return,a=3" },
        { "LetHole",
          "let [,] = iterableOf([1, 2]);",
          "next,done,return" },
        { "LetHoleOnDoneIterator",
          "let [,] = iterableOf([]);",
          "next,done" },
        { "ConstHoleThenRest",
          "const [, ...r] = iterableOf([1, 2, 3]); log.push('r=' + r);",
          "next,done,next,done,value,next,done,value,next,done,r=2,3" },
        { "LetInsideFunction",
          "(function () { let [, a] = iterableOf([1, 2]); log.push('a=' + a); })();",
          "next,done,next,done,value,return,a=2" },
        { "VarNestedHole",
          "var [[,]] = iterableOf([iterableOf([1, 2])]);",
          "next,done,value,next,done,return,return" },
        { "VarHoleStepThrows",
          "try { var [,] = throwingDone(); } catch (e) { log.push('caught ' + e.message); }",
          "next,done,caught boom" },
        { "LetHoleValueGetterThrows",
          "let [,] = throwingValue([1, 2]);",
          "next,done,return" },
    };

    public static TheoryData<string, string, string> HeadRows() => new()
    {
        { "ForOfAssignHole",
          "for ([,] of [iterableOf([1, 2])]) ;",
          "next,done,return" },
        { "ForOfVarHole",
          "for (var [,] of [iterableOf([1, 2])]) ;",
          "next,done,return" },
        { "ForOfLetHoleThenElement",
          "for (let [, a] of [iterableOf([1, 2])]) log.push('a=' + a);",
          "next,done,next,done,value,return,a=2" },
        { "ForOfConstHole",
          "for (const [,] of [iterableOf([1, 2])]) ;",
          "next,done,return" },
        { "ForAwaitLetHole",
          "(async function () { for await (let [, a] of [iterableOf([1, 2])]) log.push('a=' + a); })();",
          "next,done,next,done,value,return,a=2" },
        { "CatchHole",
          "try { throw iterableOf([1, 2]); } catch ([,]) {}",
          "next,done,return" },
        { "CatchHoleThenElement",
          "try { throw iterableOf([1, 2]); } catch ([, a]) { log.push('a=' + a); }",
          "next,done,next,done,value,return,a=2" },
    };

    public static TheoryData<string, string, string> SuspendingRows() => new()
    {
        { "AsyncBodyHole",
          "(async function () { var [,] = iterableOf([1, 2]); var b; [, b] = iterableOf([3, 4]); log.push('b=' + b); })();",
          "next,done,return,next,done,next,done,value,return,b=4" },
        { "AsyncBodyHoleAfterAwaitedSource",
          "(async function () { var [, a] = await iterableOf([1, 2]); log.push('a=' + a); })();",
          "next,done,next,done,value,return,a=2" },
        { "AsyncBodyResumableStepThrows",
          "(async function () { try { var a; [, a = await 0] = throwingDone(); } catch (e) { log.push('caught ' + e.message); } })();",
          "next,done,caught boom" },
        { "AsyncBodyResumableValueGetterThrows",
          "(async function () { var a; [, a = await 0] = throwingValue([1, 2]); })().catch(function (e) { log.push('rejected ' + e.message); });",
          "next,done,next,done,value,rejected value read" },
        { "GeneratorBodyHole",
          "function* g() { var [,] = iterableOf([1, 2]); yield 1; } g().next();",
          "next,done,return" },
        { "AsyncGeneratorBodyHole",
          "async function* g() { var [,] = iterableOf([1, 2]); yield 1; } g().next();",
          "next,done,return" },
    };

    public static TheoryData<string, string, string> BuiltinRows() => new()
    {
        { "ArrayFastLaneHoleReadsTheElement",
          "var arr = [1, 2]; Object.defineProperty(arr, 0, { get() { log.push('get0'); return 1; } }); var b; [, b] = arr; log.push('b=' + b);",
          "get0,b=2" },
        { "ArrayFastLaneParameterHoleReadsTheElement",
          "var arr = [1, 2]; Object.defineProperty(arr, 0, { get() { log.push('get0'); return 1; } }); (function ([, b]) { log.push('b=' + b); })(arr);",
          "get0,b=2" },
        { "BuiltInIteratorsWithAHole",
          "var a, b, c, d; [, a] = new Set([1, 2]); [, b] = 'xy'; [, c] = new Map([[1, 2], [3, 4]]).values(); [, d] = [10, 20].values(); log.push([a, b, c, d].join());",
          "2,y,4,20" },
        { "BuiltInIteratorsWithAParameterHole",
          "(function ([, a], [, b], [, c], [, d]) { log.push([a, b, c, d].join()); })(new Set([1, 2]), 'xy', new Map([[1, 2], [3, 4]]).values(), [10, 20].values());",
          "2,y,4,20" },
        { "GeneratorObjectWithAHole",
          "function* g() { log.push('start'); yield 1; log.push('y2'); yield 2; log.push('end'); } var a; [, a] = g(); log.push('a=' + a);",
          "start,y2,a=2" },
    };

    // ------------------------------------------------------------------ the tests

    [Theory]
    [MemberData(nameof(ParameterRows))]
    public void AParameterPatternElisionDoesNotReadValue(string name, string script, string expected)
        => Run(script).Should().Be(expected, name);

    [Theory]
    [MemberData(nameof(AssignmentRows))]
    public void AnAssignmentPatternElisionDoesNotReadValue(string name, string script, string expected)
        => Run(script).Should().Be(expected, name);

    [Theory]
    [MemberData(nameof(DeclarationRows))]
    public void ADeclarationPatternElisionDoesNotReadValue(string name, string script, string expected)
        => Run(script).Should().Be(expected, name);

    [Theory]
    [MemberData(nameof(HeadRows))]
    public void AForOfHeadOrCatchParameterElisionDoesNotReadValue(string name, string script, string expected)
        => Run(script).Should().Be(expected, name);

    /// <summary>
    /// Patterns in async functions, generators and async generators. 4.x has no resumable-pattern lane, so only the
    /// rows whose elision steps once are kept here.
    /// </summary>
    [Theory]
    [MemberData(nameof(SuspendingRows))]
    public void AnElisionInAPatternThatMaySuspendDoesNotReadValue(string name, string script, string expected)
        => Run(script).Should().Be(expected, name);

    /// <summary>
    /// What an elision still reads, because the iteration it stands for has an observable step: an array pattern over
    /// a real array reads the element the hole skips, and a built-in iterator or a replaced <c>next</c> is stepped
    /// whole. Nothing here changes with the fix.
    /// </summary>
    [Theory]
    [MemberData(nameof(BuiltinRows))]
    public void AnElisionOverABuiltInIteratorStillSteps(string name, string script, string expected)
        => Run(script).Should().Be(expected, name);
}
