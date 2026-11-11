#nullable enable

using System.Collections.Generic;
using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Interop;

namespace Jint.Tests.Runtime;

/// <summary>
/// A declaration's or an assignment's array pattern over an array reads exactly the elements its iterator would step to,
/// at the moment it steps to them. IteratorBindingInitialization
/// (https://tc39.es/ecma262/#sec-runtime-semantics-iteratorbindinginitialization) and
/// IteratorDestructuringAssignmentEvaluation
/// (https://tc39.es/ecma262/#sec-runtime-semantics-iteratordestructuringassignmentevaluation) make one step per element,
/// and only while the iterator has not reported done; a step of the built-in array iterator reads the length and then
/// gets the element at the next index, or reports done without reading anything
/// (https://tc39.es/ecma262/#sec-%arrayiteratorprototype%.next). Jint read the element at the pattern's own index for
/// every element, whatever the length: <c>var [a, b] = [1]</c> ran a getter at <c>Array.prototype[1]</c> and bound
/// <c>b</c> to what it returned, an element after the iterator was done bound whatever an initializer had pushed since,
/// and a rest element collected up to the length from before its first read. A parameter's pattern was fixed the same
/// way in #4559, and <see cref="ParameterArrayPatternReadTests"/> holds the same scenarios for it.
/// <para>
/// Every expected string is Node 26's answer to the same script, run in a fresh realm per row, and Node gives the same
/// answer for every construct, which is why the rows are the cross product of the scenarios and the constructs. Each
/// construct reaches the pattern lane that cannot suspend: none of the patterns has an <c>await</c> or a <c>yield</c>.
/// </para>
/// </summary>
/// <remarks>
/// Every case builds its own <see cref="Engine"/>, since several scenarios put an index on <c>Array.prototype</c>, and
/// the fixture holds no mutable state, so its cases run in parallel.
/// </remarks>
[Parallelizable(ParallelScope.All)]
public class DestructuringArrayPatternReadTests
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

    /// <summary>The scenarios: a name, the setup, the pattern, what runs after it, the source, and Node's log.</summary>
    private static readonly (string Name, string Setup, string Pattern, string Body, string Source, string Expected)[] Scenarios =
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

        // A hole is read through the prototype chain, but only below the length: at or past it the step is done and
        // reads nothing.
        ("InheritedHoleInsideThePattern",
            "Object.defineProperty(Array.prototype, 1, { get() { note('proto 1'); return 'p'; }, configurable: true }); var arr = [1, , 3];",
            "[a, b]", "note('a=' + a + ',b=' + b);", "arr",
            "proto 1,a=1,b=p"),
        ("InheritedHolePastThePattern",
            "Object.defineProperty(Array.prototype, 2, { get() { note('proto 2'); return 'p'; }, configurable: true }); var arr = [1, , ,];",
            "[a]", "note('a=' + a);", "arr",
            "a=1"),
        ("InheritedIndexJustPastTheLength",
            "Object.defineProperty(Array.prototype, 1, { get() { note('leak'); return 7; }, configurable: true }); var arr = [1];",
            "[a, b]", "note('a=' + a + ',b=' + b);", "arr",
            "a=1,b=undefined"),
        ("InheritedIndexPastTheLength",
            "Object.defineProperty(Array.prototype, 3, { get() { note('proto 3'); return 'p'; }, configurable: true }); var arr = [1, 2, 3];",
            "[a, b, c, d]", "note('d=' + d);", "arr",
            "d=undefined"),
        ("InheritedIndexPastAFrozenArray",
            "Object.defineProperty(Array.prototype, 3, { get() { note('proto 3'); return 'p'; }, configurable: true }); var arr = Object.freeze([1, 2, 3]);",
            "[a, b, c, d]", "note('c=' + c + ',d=' + d);", "arr",
            "c=3,d=undefined"),

        // Each step reads the length again, until a step finds the iterator done; after that nothing is read, whatever
        // grows the array.
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
            "[a, b = arr.push(8, 9), c]", "note('a=' + a + ',b=' + b + ',c=' + c);", "arr",
            "a=1,b=3,c=undefined"),
        ("GetterPushesBeforeALaterElement",
            "var arr = [1]; Object.defineProperty(arr, 0, { get() { note('get 0'); arr.push('late'); return 1; }, configurable: true });",
            "[a, b]", "note('a=' + a + ',b=' + b);", "arr",
            "get 0,a=1,b=late"),
        ("GetterTruncatesBeforeAnInheritedIndex",
            "Object.defineProperty(Array.prototype, 1, { get() { note('proto 1'); return 'p'; }, configurable: true }); var arr = [1, 2]; Object.defineProperty(arr, 0, { get() { note('get 0'); arr.length = 1; return 1; }, configurable: true });",
            "[a, b]", "note('a=' + a + ',b=' + b);", "arr",
            "get 0,a=1,b=undefined"),

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
            "[a, b = arr.push(8, 9), ...r]", "note('b=' + b + ',r=' + JSON.stringify(r));", "arr",
            "b=3,r=[]"),
        ("RestOverInheritedHoles",
            "Object.defineProperty(Array.prototype, 1, { get() { note('proto 1'); return 'p'; }, configurable: true }); var arr = [1, , 3];",
            "[a, ...r]", "note('a=' + a + ',r=' + JSON.stringify(r));", "arr",
            """proto 1,a=1,r=["p",3]"""),
        ("RestOverAFrozenArray",
            "var arr = Object.freeze([1, 2, 3]);",
            "[a, ...r]", "note('a=' + a + ',r=' + JSON.stringify(r));", "arr",
            "a=1,r=[2,3]"),
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
    ];

    /// <summary>The names every assignment row declares before it assigns to them.</summary>
    private const string Names = "var a, b, c, d, r, x, k;";

    /// <summary>
    /// Every construct that destructures through the lane, as a run of one taking the pattern: a declaration of each
    /// kind, an assignment, a pattern nested in an object pattern, a <c>for</c>-<c>of</c> head of both kinds, a
    /// <c>catch</c> parameter, and a declaration inside a generator, an async function and an async generator.
    /// </summary>
    private static readonly (string Name, Func<string, string, string, string> Run)[] Constructs =
    [
        ("Var", (p, b, s) => $"var {p} = {s}; {b}"),
        ("Let", (p, b, s) => $"{{ let {p} = {s}; {b} }}"),
        ("Const", (p, b, s) => $"{{ const {p} = {s}; {b} }}"),
        ("Assignment", (p, b, s) => $"{Names} ({p} = {s}); {b}"),
        ("NestedInObjectPattern", (p, b, s) => $"var {{ p: {p} }} = {{ p: {s} }}; {b}"),
        ("ForOfConst", (p, b, s) => $"for (const {p} of [{s}]) {{ {b} }}"),
        ("ForOfAssignment", (p, b, s) => $"{Names} for ({p} of [{s}]) {{ {b} }}"),
        ("CatchParameter", (p, b, s) => $"try {{ throw {s}; }} catch ({p}) {{ {b} }}"),
        ("Generator", (p, b, s) => $"(function* () {{ const {p} = {s}; {b} }})().next();"),
        ("AsyncFunction", (p, b, s) => $"(async function () {{ const {p} = {s}; {b} }})();"),
        ("AsyncGenerator", (p, b, s) => $"(async function* () {{ const {p} = {s}; {b} }})().next();"),
    ];

    public static TestCases<string, string, string> Rows()
    {
        var rows = new TestCases<string, string, string>();
        foreach (var scenario in Scenarios)
        {
            foreach (var construct in Constructs)
            {
                var run = construct.Run(scenario.Pattern, scenario.Body, scenario.Source);
                var script = $"{scenario.Setup} try {{ {run} }} catch (e) {{ note('threw ' + e.message); }}";
                rows.Add(construct.Name + "." + scenario.Name, script, scenario.Expected);
            }
        }

        return rows;
    }

    [TestCaseSource(nameof(Rows))]
    public void AnArrayPatternReadsWhatTheArrayIteratorWould(string name, string script, string expected)
    {
        var engine = new Engine();
        engine.Execute(Prelude);
        engine.Execute(script);
        engine.Tasks.ProcessTasks();
        engine.Evaluate("out").AsString().Should().Be(expected, name);
    }

    /// <summary>
    /// An assignment's member targets are evaluated before their step, and a setter they reach runs between two steps,
    /// so it can grow the array the same way an initializer can. Node 26's answers.
    /// </summary>
    [TestCase("Object.defineProperty(Array.prototype, 1, { get() { note('leak'); return 7; }, configurable: true }); var arr = [1]; var o = {};",
        "[o.a, o.b] = arr; note('a=' + o.a + ',b=' + o.b);",
        "a=1,b=undefined")]
    [TestCase("var arr = [1]; var o = { set b(v) { note('set b=' + v); arr.push(8, 9); } };",
        "[o.a, o.b, o.c] = arr; note('c=' + o.c);",
        "set b=undefined,c=undefined")]
    [TestCase("var arr = [1, 2]; var o = { set a(v) { note('set a=' + v); arr.push(3); } };",
        "[o.a, o.b, o.c] = arr; note('b=' + o.b + ',c=' + o.c);",
        "set a=1,b=2,c=3")]
    [TestCase("var arr = [1, 2]; var o = { set a(v) { note('set a=' + v); arr.push(3); } };",
        "[o.a, ...o.r] = arr; note('r=' + JSON.stringify(o.r));",
        "set a=1,r=[2,3]")]
    [TestCase("var arr = [1]; var o = { set b(v) { note('set b=' + v); arr.push(8, 9); } };",
        "[o.a, o.b, ...o.r] = arr; note('r=' + JSON.stringify(o.r));",
        "set b=undefined,r=[]")]
    public void AMemberTargetStepsWhatTheArrayIteratorWould(string setup, string script, string expected)
    {
        var engine = new Engine();
        engine.Execute(Prelude);
        engine.Execute(setup);
        engine.Execute(script);
        engine.Evaluate("out").AsString().Should().Be(expected);
    }

    /// <summary>
    /// A host <see cref="ArrayLikeObject"/> whose prototype is <c>Array.prototype</c> takes the lane too, and its iterator
    /// is the array iterator over an array-like, which reads its <c>length</c> at every step just the same. An index the
    /// host does not answer is read through the prototype chain, but only below the length. Each expectation is Node 26's
    /// for a plain object with own read-only indices, an own <c>length</c> and <c>Array.prototype</c> as its prototype.
    /// </summary>
    [TestCase("Object.defineProperty(Array.prototype, 1, { get() { note('leak'); return 7; }, configurable: true });",
        "var [a, b] = host; note('a=' + a + ',b=' + b);",
        "a=1,b=undefined")]
    [TestCase("Object.defineProperty(Array.prototype, 1, { get() { note('leak'); return 7; }, configurable: true }); var a, b;",
        "[a, b] = host; note('a=' + a + ',b=' + b);",
        "a=1,b=undefined")]
    [TestCase("",
        "var [a, b = grow(8, 9), c] = host; note('a=' + a + ',b=' + b + ',c=' + c);",
        "a=1,b=grew,c=undefined")]
    [TestCase("",
        "var [a, b = grow(8, 9), ...r] = host; note('b=' + b + ',r=' + JSON.stringify(r));",
        "b=grew,r=[]")]
    public void AHostArrayLikeObjectStepsWhatTheArrayIteratorWould(string setup, string script, string expected)
    {
        var engine = new Engine();
        var host = new HostList(engine, 1);
        engine.SetValue("host", host);
        engine.SetValue("grow", host.Grow);
        engine.Execute(Prelude);
        engine.Execute(setup);
        engine.Execute(script);
        engine.Evaluate("out").AsString().Should().Be(expected);
    }

    /// <summary>
    /// A rest element over a host <see cref="ArrayLikeObject"/> reads the length at every step: an inherited getter at an
    /// index the host does not answer grows the host, and the rest collects what it grew by. Node 26's answer for the
    /// plain-object model the test above describes.
    /// </summary>
    [Test]
    public void ARestOverAHostArrayLikeObjectFollowsItsLength()
    {
        var engine = new Engine();
        var host = new HostList(engine, 1, null, 3);
        engine.SetValue("host", host);
        engine.SetValue("grow", host.Grow);
        engine.Execute(Prelude);
        engine.Execute("Object.defineProperty(Array.prototype, 1, { get() { note('proto 1'); grow(4); return 'p'; }, configurable: true });");
        engine.Execute("var [...r] = host; note('r=' + JSON.stringify(r));");
        engine.Evaluate("out").AsString().Should().Be("""proto 1,r=[1,"p",3,4]""");
    }

    /// <summary>
    /// A wrapped CLR list takes the lane as well, and its iterator enumerates the list, so nothing past its <c>Count</c>
    /// is read: an index <c>Array.prototype</c> holds there is never reached.
    /// </summary>
    [Test]
    public void AWrappedClrListReadsNothingPastItsCount()
    {
        var engine = new Engine();
        engine.SetValue("list", new List<int> { 1, 2 });
        engine.Execute(Prelude);
        engine.Execute("Object.defineProperty(Array.prototype, 2, { get() { note('leak'); return 7; }, configurable: true });");
        engine.Execute("var [a, b, c, ...r] = list; note('a=' + a + ',b=' + b + ',c=' + c + ',r=' + JSON.stringify(r));");
        engine.Execute("var x, y, z; [x, ...z] = list; note('x=' + x + ',z=' + JSON.stringify(z));");
        engine.Evaluate("out").AsString().Should().Be("a=1,b=2,c=undefined,r=[],x=1,z=[2]");
    }

    /// <summary>
    /// The rest array a pattern builds over an array is an array the engine creates, so <c>MaxArraySize</c> bounds it the
    /// way it bounds every other one, in a declaration, an assignment and a parameter alike. The source is a dense host
    /// array no script could have built under the limit.
    /// </summary>
    [TestCase("var [...r] = big;")]
    [TestCase("var r; [...r] = big;")]
    [TestCase("var [a, ...r] = big;")]
    [TestCase("(function ([...r]) { })(big);")]
    [TestCase("(function ([a, ...r]) { })(big);")]
    public void ARestOverAnArrayIsBoundedByMaxArraySize(string script)
    {
        var engine = new Engine(options => options.Constraints.MaxArraySize = 50);
        var items = new JsValue[100];
        for (var i = 0; i < items.Length; i++)
        {
            items[i] = i;
        }

        engine.SetValue("big", new JsArray(engine, items));

        var thrown = Caught.Exception(() => engine.Execute(script));

        thrown.Should().BeOfType<MemoryLimitExceededException>();
    }

    /// <summary>
    /// A host collection with read-only indices whose <c>null</c> entries it does not answer, so they read through the
    /// prototype chain the way a hole does, and which <see cref="Grow"/> appends to from script.
    /// </summary>
    private sealed class HostList : ArrayLikeObject
    {
        private readonly List<JsValue?> _items;

        public HostList(Engine engine, params JsValue?[] items) : base(engine)
        {
            _items = new List<JsValue?>(items);
            Prototype = engine.Intrinsics.Array.PrototypeObject;
            Grow = new ClrFunction(engine, "grow", (_, arguments) =>
            {
                _items.AddRange(arguments);
                return "grew";
            });
        }

        public ClrFunction Grow { get; }

        public override uint Length => (uint) _items.Count;

        public override bool TryGetIndex(uint index, out JsValue value)
        {
            if (index < (uint) _items.Count && _items[(int) index] is { } item)
            {
                value = item;
                return true;
            }

            value = JsValue.Undefined;
            return false;
        }
    }
}
