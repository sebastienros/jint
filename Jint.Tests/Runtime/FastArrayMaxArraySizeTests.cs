#nullable enable

using Jint.Native;
using Jint.Runtime;

namespace Jint.Tests.Runtime;

/// <summary>
/// An array a built-in hands to script from a list whose length the script chose is subject to
/// <c>Options.Constraints.MaxArraySize</c> like every other array built from a count. These are the callers of
/// <c>ArrayConstructor.ConstructFast</c>, which copies a list into an array without going through
/// <c>ArrayCreate</c> and so had no check of its own (issue #4493); each one is pinned past the limit and, as the
/// control, at exactly the limit.
/// </summary>
public class FastArrayMaxArraySizeTests
{
    private static Engine EngineWith(uint maxArraySize)
        => new(options => options.Constraints.MaxArraySize = maxArraySize);

    [Theory]
    [InlineData("Object.getOwnPropertyNames(o)")]
    [InlineData("Object.getOwnPropertySymbols(s)")]
    [InlineData("g().chunks(3).next()")]
    [InlineData("g().chunks(5).next()")]
    [InlineData("g().windows(3).next()")]
    [InlineData("g().windows(5, 'allow-partial').next()")]
    public void AnArrayPastTheLimitIsRefused(string expression)
    {
        var engine = EngineWith(2);
        engine.Execute("""
            var o = { a: 1, b: 2, c: 3 };
            var s = { [Symbol('a')]: 1, [Symbol('b')]: 2, [Symbol('c')]: 3 };
            function* g() { yield 0; yield 1; yield 2; }
            """);

        var thrown = Assert.Throws<MemoryLimitExceededException>(() => engine.Evaluate(expression));

        thrown.Message.Should().Contain("3").And.Contain("2");
    }

    [Theory]
    [InlineData("Object.getOwnPropertyNames(o).length", 3)]
    [InlineData("Object.getOwnPropertySymbols(s).length", 3)]
    [InlineData("g().chunks(3).next().value.length", 3)]
    [InlineData("g().chunks(5).next().value.length", 3)]
    [InlineData("g().windows(3).next().value.length", 3)]
    [InlineData("g().windows(5, 'allow-partial').next().value.length", 3)]
    public void AnArrayOfExactlyTheLimitIsAllowed(string expression, double expected)
    {
        var engine = EngineWith(3);
        engine.Execute("""
            var o = { a: 1, b: 2, c: 3 };
            var s = { [Symbol('a')]: 1, [Symbol('b')]: 2, [Symbol('c')]: 3 };
            function* g() { yield 0; yield 1; yield 2; }
            """);

        engine.Evaluate(expression).AsNumber().Should().Be(expected);
    }

    /// <summary>
    /// A trap receives the call's arguments as an array (https://tc39.es/ecma262/#sec-proxy-object-internal-methods-and-internal-slots-call-thisargument-argumentslist),
    /// and a script chooses how many: a spread or an array-like <c>length</c> is not bounded by <c>MaxArraySize</c>.
    /// </summary>
    [Theory]
    [InlineData("Reflect.apply(applied, null, { length: 3 })")]
    [InlineData("applied(...g())")]
    [InlineData("Reflect.construct(constructed, { length: 3 })")]
    [InlineData("new constructed(...g())")]
    public void AProxyTrapIsRefusedAnArgumentsArrayPastTheLimit(string expression)
    {
        var engine = EngineWith(2);
        engine.Execute("""
            function* g() { yield 0; yield 1; yield 2; }
            var applied = new Proxy(function () {}, { apply(t, thisArg, args) { return args.length; } });
            var constructed = new Proxy(function () {}, { construct(t, args) { return { n: args.length }; } });
            """);

        Assert.Throws<MemoryLimitExceededException>(() => engine.Evaluate(expression));
    }

    [Theory]
    [InlineData("Reflect.apply(applied, null, { length: 3 })", 3)]
    [InlineData("applied(...g())", 3)]
    [InlineData("Reflect.construct(constructed, { length: 3 }).n", 3)]
    [InlineData("new constructed(...g()).n", 3)]
    public void AProxyTrapIsGivenAnArgumentsArrayOfExactlyTheLimit(string expression, double expected)
    {
        var engine = EngineWith(3);
        engine.Execute("""
            function* g() { yield 0; yield 1; yield 2; }
            var applied = new Proxy(function () {}, { apply(t, thisArg, args) { return args.length; } });
            var constructed = new Proxy(function () {}, { construct(t, args) { return { n: args.length }; } });
            """);

        engine.Evaluate(expression).AsNumber().Should().Be(expected);
    }

    /// <summary>
    /// A window slides: it is the same buffer, copied into a fresh array each step, so a window that fits once keeps
    /// fitting and the limit is about the window, not about how many windows there are.
    /// </summary>
    [Fact]
    public void ManyWindowsThatEachFitAreAllowed()
    {
        var engine = EngineWith(2);

        engine.Evaluate("""
            function* g() { for (let i = 0; i < 50; i++) yield i; }
            let count = 0;
            for (const w of g().windows(2)) count += w.length;
            count
            """).AsNumber().Should().Be(98);
    }

    /// <summary>
    /// The public <c>ConstructFast(JsValue[])</c> is a published method, so on this line its behaviour is unchanged:
    /// it builds the array past <c>MaxArraySize</c>. Only the built-ins that choose the count go through the check.
    /// </summary>
    [Fact]
    public void PublicConstructFastOfAnArrayIsUnchecked()
    {
        var engine = EngineWith(2);

        var array = engine.Realm.Intrinsics.Array.ConstructFast(new JsValue[] { 1, 2, 3 });

        array.Length.Should().Be(3);
    }
}
