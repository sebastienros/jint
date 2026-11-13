#nullable enable

namespace Jint.Tests.Runtime;

/// <summary>
/// A parameter's array pattern over a real array reads exactly the elements its iterator would step to, at the moment
/// it steps to them. IteratorBindingInitialization
/// (https://tc39.es/ecma262/#sec-runtime-semantics-iteratorbindinginitialization) makes one step per element and binds
/// it before the next, and a step of the built-in array iterator reads the length and then gets the element at the next
/// index, or finds the iterator done (https://tc39.es/ecma262/#sec-createarrayiterator). An array iterator has no
/// <c>return</c>, so the close at the end calls nothing and no other index is read. Jint copied the whole array before
/// binding: <c>(function ([a]) {})(arr)</c> ran a getter at <c>arr[2]</c> that Node never runs, and an initializer that
/// pushed or popped was invisible to the elements after it. An assignment or a declaration pattern was already right.
/// <para>
/// Every expected string is Node 26's answer to the same script, run in a fresh realm per row, and Node gives the same
/// answer for every kind of function, which is why the rows are the cross product of the scenarios and the kinds. A
/// generator binds its parameters when it is called and an async generator when it is called too; both run their body
/// on the first <c>next()</c>, which each row makes at once.
/// </para>
/// </summary>
/// <remarks>
/// Every case builds its own <see cref="Engine"/>, since several scenarios put an index on <c>Array.prototype</c>, and
/// the fixture holds no mutable state, so its cases run in parallel.
/// </remarks>
[Parallelizable(ParallelScope.All)]
public class ParameterArrayPatternReadTests
{
    /// <summary>
    /// <c>note</c> logs to a string, not to an array: a scenario that defines a getter-only index on
    /// <c>Array.prototype</c> makes a <c>push</c> onto an array that lacks that index throw.
    /// </summary>
    private const string Prelude = """
        var out = '';
        function note(s) { out += (out ? ',' : '') + s; }
        function trap(arr, index, value) {
            Object.defineProperty(arr, index, { get() { note('get ' + index); return value; }, configurable: true, enumerable: true });
            return arr;
        }
        """;

    /// <summary>The scenarios: a name, the setup, the parameter pattern, the body, the argument, and Node's log.</summary>
    private static readonly (string Name, string Setup, string Pattern, string Body, string Argument, string Expected)[] Scenarios =
    [
        // Getters past the pattern are never read.
        ("GetterPastThePattern",
            "var arr = trap([1, 2], 2, 3);",
            "[a]", "note('a=' + a);", "arr",
            "a=1"),
        ("GettersPastATwoElementPattern",
            "var arr = trap(trap([1, 2, 3, 4], 2, 3), 3, 4);",
            "[a, b]", "note('a=' + a + ',b=' + b);", "arr",
            "a=1,b=2"),
        ("ThrowingGetterPastThePattern",
            "var arr = [1, 2]; Object.defineProperty(arr, 1, { get() { throw new Error('read 1'); } });",
            "[a]", "note('a=' + a);", "arr",
            "a=1"),
        ("GetterPastATrailingElision",
            "var arr = trap([1, 2, 3], 2, 3);",
            "[a, ,]", "note('a=' + a);", "arr",
            "a=1"),
        ("HugeSparseLength",
            "var arr = [1, 2]; arr.length = 4294967295;",
            "[a, b]", "note('a=' + a + ',b=' + b);", "arr",
            "a=1,b=2"),

        // A getter inside the pattern runs once, when its step comes, after everything before it has bound.
        ("GetterInsideThePattern",
            "var arr = trap([1, 2, 3], 1, 'g');",
            "[a, b]", "note('a=' + a + ',b=' + b);", "arr",
            "get 1,a=1,b=g"),
        ("GetterAtAnElidedIndex",
            "var arr = trap([1, 2, 3], 1, 2);",
            "[a, , c]", "note('a=' + a + ',c=' + c);", "arr",
            "get 1,a=1,c=3"),
        ("GetterAfterAnEarlierDefault",
            "var arr = trap([undefined, 2], 1, 'g');",
            "[a = note('default a'), b]", "note('b=' + b);", "arr",
            "default a,get 1,b=g"),

        // A hole is read through the prototype chain, but only below the length.
        ("InheritedHoleInsideThePattern",
            "Object.defineProperty(Array.prototype, 1, { get() { note('proto 1'); return 'p'; }, configurable: true }); var arr = [1, , 3];",
            "[a, b]", "note('a=' + a + ',b=' + b);", "arr",
            "proto 1,a=1,b=p"),
        ("InheritedHolePastThePattern",
            "Object.defineProperty(Array.prototype, 2, { get() { note('proto 2'); return 'p'; }, configurable: true }); var arr = [1, , ,];",
            "[a]", "note('a=' + a);", "arr",
            "a=1"),
        ("InheritedIndexPastTheLength",
            "Object.defineProperty(Array.prototype, 3, { get() { note('proto 3'); return 'p'; }, configurable: true }); var arr = [1, 2, 3];",
            "[a, b, c, d]", "note('d=' + d);", "arr",
            "d=undefined"),

        // Each step reads the length again, until a step finds the iterator done; after that nothing is read.
        ("DefaultPushesBeforeALaterElement",
            "var arr = [undefined];",
            "[a = arr.push('pushed'), b]", "note('a=' + a + ',b=' + b);", "arr",
            "a=2,b=pushed"),
        ("DefaultPopsBeforeALaterElement",
            "var arr = [undefined, 'second'];",
            "[a = arr.pop(), b]", "note('a=' + a + ',b=' + b);", "arr",
            "a=second,b=undefined"),
        ("PushAfterTheIteratorIsDone",
            "var arr = [1];",
            "[a, b = arr.push(9), c]", "note('a=' + a + ',b=' + b + ',c=' + c);", "arr",
            "a=1,b=2,c=undefined"),
        ("GetterPushesBeforeALaterElement",
            "var arr = [1]; Object.defineProperty(arr, 0, { get() { note('get 0'); arr.push('late'); return 1; }, configurable: true });",
            "[a, b]", "note('a=' + a + ',b=' + b);", "arr",
            "get 0,a=1,b=late"),

        // A rest element takes every step left, the length read at each.
        ("RestReadsEveryRemainingIndex",
            "var arr = trap([1, 2, 3], 2, 3);",
            "[a, ...r]", "note('a=' + a + ',r=' + JSON.stringify(r));", "arr",
            "get 2,a=1,r=[2,3]"),
        ("RestFollowsAGrowingLength",
            "var arr = [1, 2]; Object.defineProperty(arr, 1, { get() { note('get 1'); arr.push(3); return 2; }, configurable: true });",
            "[a, ...r]", "note('a=' + a + ',r=' + JSON.stringify(r));", "arr",
            "get 1,a=1,r=[2,3]"),
        ("RestStopsAtAShrinkingLength",
            "var arr = [1, 2, 3, 4]; Object.defineProperty(arr, 1, { get() { note('get 1'); arr.length = 2; return 2; }, configurable: true });",
            "[a, ...r]", "note('a=' + a + ',r=' + JSON.stringify(r));", "arr",
            "get 1,a=1,r=[2]"),
        ("RestAfterADefaultThatPushes",
            "var arr = [undefined];",
            "[a = arr.push(7, 8), ...r]", "note('a=' + a + ',r=' + JSON.stringify(r));", "arr",
            "a=3,r=[7,8]"),
        ("RestAfterTheIteratorIsDone",
            "var arr = [1];",
            "[a, b, ...r]", "note('b=' + b + ',r=' + JSON.stringify(r));", "arr",
            "b=undefined,r=[]"),
        ("RestAfterAPushOnceDone",
            "var arr = [1];",
            "[a, b = arr.push(9), ...r]", "note('b=' + b + ',r=' + JSON.stringify(r));", "arr",
            "b=2,r=[]"),
        ("RestOverInheritedHoles",
            "Object.defineProperty(Array.prototype, 1, { get() { note('proto 1'); return 'p'; }, configurable: true }); var arr = [1, , 3];",
            "[a, ...r]", "note('a=' + a + ',r=' + JSON.stringify(r));", "arr",
            """proto 1,a=1,r=["p",3]"""),
        ("RestAsANestedPattern",
            "var arr = trap([1, 2, 3, 4], 3, 4);",
            "[a, ...[b, c]]", "note('a=' + a + ',b=' + b + ',c=' + c);", "arr",
            "get 3,a=1,b=2,c=3"),

        // A nested pattern over an array steps it the same way, and so does a pattern a default supplied.
        ("NestedArrayPatternOverAnInnerGetter",
            "var inner = trap([1, 2], 1, 2);",
            "[[x]]", "note('x=' + x);", "[inner]",
            "x=1"),
        ("OuterGetterPastANestedPattern",
            "var arr = trap([[1], 'o'], 1, 'o');",
            "[[x]]", "note('x=' + x);", "arr",
            "x=1"),
        ("NestedObjectPatternBeforeAGetter",
            "var arr = trap([{ k: 1 }, 2], 1, 2);",
            "[{ k }]", "note('k=' + k);", "arr",
            "k=1"),
        ("NestedPatternDefault",
            "var arr = [undefined]; var inner = trap(['i', 2], 1, 2);",
            "[[x] = inner]", "note('x=' + x);", "arr",
            "x=i"),
        ("ParameterDefaultArray",
            "var arr = trap([1, 2], 1, 2);",
            "[a] = arr", "note('a=' + a);", "",
            "a=1"),
    ];

    /// <summary>Every kind of function a parameter list belongs to, as a call of one taking the pattern.</summary>
    private static readonly (string Name, Func<string, string, string, string> Call)[] Kinds =
    [
        ("Function", (p, b, a) => $"(function ({p}) {{ {b} }})({a});"),
        ("Arrow", (p, b, a) => $"(({p}) => {{ {b} }})({a});"),
        ("Method", (p, b, a) => $"({{ m({p}) {{ {b} }} }}).m({a});"),
        ("ClassConstructor", (p, b, a) => $"new (class {{ constructor({p}) {{ {b} }} }})({a});"),
        ("AsyncFunction", (p, b, a) => $"(async function ({p}) {{ {b} }})({a});"),
        ("AsyncArrow", (p, b, a) => $"(async ({p}) => {{ {b} }})({a});"),
        ("Generator", (p, b, a) => $"(function* ({p}) {{ {b} }})({a}).next();"),
        ("AsyncGenerator", (p, b, a) => $"(async function* ({p}) {{ {b} }})({a}).next();"),
    ];

    public static TestCases<string, string, string> Rows()
    {
        var rows = new TestCases<string, string, string>();
        foreach (var scenario in Scenarios)
        {
            foreach (var kind in Kinds)
            {
                var call = kind.Call(scenario.Pattern, scenario.Body, scenario.Argument);
                var script = $"{scenario.Setup} try {{ {call} }} catch (e) {{ note('threw ' + e.message); }}";
                rows.Add(kind.Name + "." + scenario.Name, script, scenario.Expected);
            }
        }

        return rows;
    }

    [TestCaseSource(nameof(Rows))]
    public void AParameterArrayPatternReadsWhatTheArrayIteratorWould(string name, string script, string expected)
    {
        var engine = new Engine();
        engine.Execute(Prelude);
        engine.Execute(script);
        engine.Tasks.ProcessTasks();
        engine.Evaluate("out").AsString().Should().Be(expected, name);
    }
}
