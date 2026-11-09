#nullable enable

using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Interop;

namespace Jint.Tests.Runtime;

/// <summary>
/// An argument list whose length the script chose at run time is bounded by <c>Options.Constraints.MaxArraySize</c>
/// like an array built from a count (issue #4505): a spread
/// (https://tc39.es/ecma262/#sec-runtime-semantics-argumentlistevaluation, and an array literal's
/// https://tc39.es/ecma262/#sec-runtime-semantics-arrayaccumulation, which shares its builder), an array-like handed to
/// <c>apply</c>, <c>Reflect.apply</c> or <c>Reflect.construct</c> (https://tc39.es/ecma262/#sec-createlistfromarraylike),
/// and the list a bound function makes of its own arguments and the call's
/// (https://tc39.es/ecma262/#sec-bound-function-exotic-objects-call-thisargument-argumentslist). The rest parameter and
/// the <c>arguments</c> object are made from that list, so bounding it bounds them. Each shape is pinned past the
/// limit, at exactly the limit, and on an engine with no limit.
/// </summary>
public class ArgumentListMaxArraySizeTests
{
    private const uint Limit = 4;

    private const string Setup = """
        function rest(...r) { return r.length; }
        function args() { return arguments.length; }
        function Ctor(...r) { this.n = r.length; }
        class Base { constructor(...r) { this.n = r.length; } }
        class Derived extends Base { constructor(x) { super(...x, ...x); } }
        class Implicit extends Base {}
        var yielded = 0;
        function* g(n) { for (let i = 0; i < n; i++) { yielded++; yield i; } }
        var a2 = [0, 1], a3 = [0, 1, 2], a4 = [0, 1, 2, 3];
        function chain(links, perLink) { let f = rest; for (let i = 0; i < links; i++) f = f.bind(null, ...g(perLink)); return f; }
        """;

    private static Engine EngineWith(uint maxArraySize)
    {
        var engine = new Engine(options => options.Constraints.MaxArraySize = maxArraySize);
        engine.Execute(Setup);
        return engine;
    }

    /// <summary>
    /// The size each refusal reports says where the check fired: the spread of a long generator stops at the first
    /// element past the limit rather than at its end, and an array's spread is refused before it is copied.
    /// </summary>
    [TestCase("rest(...g(1000))", 5)]
    [TestCase("args(...g(1000))", 5)]
    [TestCase("[...g(1000)]", 5)]
    [TestCase("rest(...a4, ...a4)", 8)]
    [TestCase("rest(...a3, ...a2)", 5)]
    [TestCase("args(...a3, ...a2)", 5)]
    [TestCase("rest(0, ...a4)", 5)]
    [TestCase("rest(...a4, 0)", 5)]
    [TestCase("new Ctor(...a3, ...a2)", 5)]
    [TestCase("new Derived(a3)", 6)]
    [TestCase("new Implicit(...a3, ...a2)", 5)]
    [TestCase("[...a3, ...a2]", 5)]
    [TestCase("[0, ...a4]", 5)]
    [TestCase("rest.apply(null, { length: 5 })", 5)]
    [TestCase("Reflect.apply(rest, null, { length: 5 })", 5)]
    [TestCase("Reflect.construct(Ctor, { length: 5 })", 5)]
    [TestCase("(() => { let n = 0; for (const k in new Proxy({}, { ownKeys: () => ({ length: 5, 0: 'a', 1: 'b', 2: 'c', 3: 'd', 4: 'e' }) })) n++; return n; })()", 5)]
    [TestCase("rest.bind(null, ...a3)(...a2)", 5)]
    [TestCase("new (Ctor.bind(null, ...a3))(...a2)", 5)]
    [TestCase("chain(2, 3)()", 6)]
    public void AListPastTheLimitIsRefused(string expression, int reportedSize)
    {
        var engine = EngineWith(Limit);

        var thrown = Caught.Exception(() => engine.Evaluate(expression));

        thrown.Should().BeOfType<MemoryLimitExceededException>();
        thrown!.Message.Should().Be($"The array size {reportedSize} is larger than maximum allowed ({Limit})");
    }

    /// <summary>
    /// Growing an array's <c>length</c> writes no element, so a sparse array can be longer than the limit; the list a
    /// spread or <c>apply</c> makes of it is as long as that <c>length</c>, and is refused.
    /// </summary>
    [TestCase("rest(...sparse)")]
    [TestCase("rest.apply(null, sparse)")]
    [TestCase("Reflect.apply(rest, null, sparse)")]
    public void ASparseArrayLongerThanTheLimitIsRefused(string expression)
    {
        var engine = EngineWith(Limit);
        engine.Execute("var sparse = []; sparse.length = 5;");
        engine.Evaluate("sparse.length").AsNumber().Should().Be(5);

        var thrown = Caught.Exception(() => engine.Evaluate(expression));

        thrown.Should().BeOfType<MemoryLimitExceededException>();
        thrown!.Message.Should().Be($"The array size 5 is larger than maximum allowed ({Limit})");
    }

    /// <summary>
    /// A host <see cref="ProxyHandler"/>'s keys are a list the host already built, so holding them to the limit would
    /// bound no memory and only break enumeration: <c>for…in</c>, <c>Object.assign</c> and object spread read past the
    /// limit, and so does the host. A script <c>ownKeys</c> trap over the same target, whose array-like's
    /// <c>length</c> the script chose, is still refused.
    /// </summary>
    [Test]
    public void AHostProxyHandlersKeysAreNotCountedButAScriptTrapsAre()
    {
        var engine = EngineWith(Limit);
        var target = engine.Evaluate("({ a: 1, b: 2, c: 3, d: 4, e: 5 })").AsObject();
        var proxy = engine.Advanced.CreateProxy(target, new HostKeys(["a", "b", "c", "d", "e"]));
        engine.SetValue("hostProxy", proxy);
        engine.SetValue("target", target);

        engine.Evaluate("(() => { let n = 0; for (const k in hostProxy) n++; return n; })()").AsNumber().Should().Be(5);
        engine.Evaluate("Object.assign({}, hostProxy).e").AsNumber().Should().Be(5);
        engine.Evaluate("({ ...hostProxy }).e").AsNumber().Should().Be(5);
        proxy.GetOwnPropertyKeys().Should().HaveCount(5);

        var thrown = Caught.Exception(() => engine.Evaluate("""
            (() => { let n = 0; for (const k in new Proxy(target, { ownKeys: () => ['a', 'b', 'c', 'd', 'e'] })) n++; return n; })()
            """));
        thrown.Should().BeOfType<MemoryLimitExceededException>();
        thrown!.Message.Should().Be($"The array size 5 is larger than maximum allowed ({Limit})");
    }

    private sealed class HostKeys(string[] keys) : ProxyHandler
    {
        public override IReadOnlyList<JsValue>? OwnKeys(ObjectInstance target)
            => keys.Select(static key => (JsValue) JsString.Create(key)).ToList();
    }

    [Test]
    public void ASpreadOfALongIteratorStopsSteppingItAtTheLimit()
    {
        var engine = EngineWith(Limit);

        Caught.Exception(() => engine.Evaluate("rest(...g(1000))")).Should().BeOfType<MemoryLimitExceededException>();

        engine.Evaluate("yielded").AsNumber().Should().Be(Limit + 1);
    }

    [TestCase("rest(...g(4))", 4)]
    [TestCase("args(...g(4))", 4)]
    [TestCase("[...g(4)].length", 4)]
    [TestCase("rest(...a2, ...a2)", 4)]
    [TestCase("args(...a2, ...a2)", 4)]
    [TestCase("rest(0, ...a3)", 4)]
    [TestCase("rest(...a3, 0)", 4)]
    [TestCase("new Ctor(...a2, ...a2).n", 4)]
    [TestCase("new Derived(a2).n", 4)]
    [TestCase("new Implicit(...a2, ...a2).n", 4)]
    [TestCase("[...a2, ...a2].length", 4)]
    [TestCase("[0, ...a3].length", 4)]
    [TestCase("rest.apply(null, { length: 4 })", 4)]
    [TestCase("Reflect.apply(rest, null, { length: 4 })", 4)]
    [TestCase("Reflect.construct(Ctor, { length: 4 }).n", 4)]
    [TestCase("Reflect.ownKeys(new Proxy({}, { ownKeys: () => ({ length: 4, 0: 'a', 1: 'b', 2: 'c', 3: 'd' }) })).length", 4)]
    [TestCase("rest.bind(null, ...a2)(...a2)", 4)]
    [TestCase("new (Ctor.bind(null, ...a2))(...a2).n", 4)]
    [TestCase("chain(2, 2)()", 4)]
    public void AListOfExactlyTheLimitIsAllowed(string expression, double expected)
    {
        var engine = EngineWith(Limit);

        engine.Evaluate(expression).AsNumber().Should().Be(expected);
    }

    /// <summary>
    /// A list written out in the source is as long as the source, which the host chose to run, so it costs no check:
    /// a call without a spread pays nothing for the bound. Only a list a run-time count can lengthen is counted.
    /// </summary>
    [TestCase("rest(0, 1, 2, 3, 4)", 5)]
    [TestCase("args(0, 1, 2, 3, 4)", 5)]
    [TestCase("new Ctor(0, 1, 2, 3, 4).n", 5)]
    [TestCase("[0, 1, 2, 3, 4].length", 5)]
    public void AListWrittenOutInTheSourceIsNotCounted(string expression, double expected)
    {
        var engine = EngineWith(Limit);

        engine.Evaluate(expression).AsNumber().Should().Be(expected);
    }

    [TestCase("rest(...a4, ...a4, ...a4)", 12)]
    [TestCase("[...a4, ...g(10)].length", 14)]
    [TestCase("rest.apply(null, { length: 50 })", 50)]
    [TestCase("Reflect.construct(Ctor, { length: 50 }).n", 50)]
    [TestCase("chain(3, 3)(...a4)", 13)]
    public void AnEngineWithoutALimitBuildsAnyList(string expression, double expected)
    {
        var engine = new Engine();
        engine.Execute(Setup);

        engine.Evaluate(expression).AsNumber().Should().Be(expected);
    }

    /// <summary>
    /// An array-like's list is refused when its <c>length</c> is read, before it is allocated and before any element
    /// is read: a <c>{ length: n }</c> object costs a script nothing to make, and the list it asks for costs the host
    /// eight bytes per element.
    /// </summary>
    [TestCase("rest.apply(null, o)")]
    [TestCase("Reflect.apply(rest, null, o)")]
    [TestCase("Reflect.construct(Ctor, o)")]
    public void AnArrayLikePastTheLimitIsRefusedBeforeItsElementsAreRead(string expression)
    {
        var engine = EngineWith(Limit);
        engine.Execute("var reads = 0; var o = { length: 1000000, get 0() { reads++; return 0; } };");

#if NET
        var before = GC.GetAllocatedBytesForCurrentThread();
#endif
        var thrown = Caught.Exception(() => engine.Evaluate(expression));
#if NET
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
#endif

        thrown.Should().BeOfType<MemoryLimitExceededException>();
        engine.Evaluate("reads").AsNumber().Should().Be(0);
#if NET
        // The list would be 8 MB; the refusal allocates its message and stack trace.
        allocated.Should().BeLessThan(1_000_000);
#endif
    }

    /// <summary>
    /// A spread list suspended at an <c>await</c> or a <c>yield</c> keeps what it built so far, and the resumed walk
    /// counts from there, so the part built before the suspension is not forgotten.
    /// </summary>
    [TestCase("(async () => rest(...a3, ...(await Promise.resolve(a2))))()")]
    [TestCase("(async () => [...a3, ...(await Promise.resolve(a2))])()")]
    [TestCase("(() => { function* gen() { return rest(...a3, ...(yield)); } const it = gen(); it.next(); return it.next(a2).value; })()")]
    [TestCase("(() => { function* gen() { return [...a3, ...(yield)]; } const it = gen(); it.next(); return it.next(a2).value; })()")]
    public void ASpreadListResumedPastTheLimitIsRefused(string expression)
    {
        var engine = EngineWith(Limit);

        var thrown = Caught.Exception(() => engine.Evaluate(expression).UnwrapIfPromise());

        thrown.Should().BeOfType<MemoryLimitExceededException>();
        thrown!.Message.Should().Be($"The array size 5 is larger than maximum allowed ({Limit})");
    }

    [TestCase("(async () => rest(...a2, ...(await Promise.resolve(a2))))()")]
    [TestCase("(async () => [...a2, ...(await Promise.resolve(a2))].length)()")]
    [TestCase("(() => { function* gen() { return rest(...a2, ...(yield)); } const it = gen(); it.next(); return it.next(a2).value; })()")]
    [TestCase("(() => { function* gen() { return [...a2, ...(yield)].length; } const it = gen(); it.next(); return it.next(a2).value; })()")]
    public void ASpreadListResumedAtExactlyTheLimitIsAllowed(string expression)
    {
        var engine = EngineWith(Limit);

        engine.Evaluate(expression).UnwrapIfPromise().AsNumber().Should().Be(Limit);
    }
}
