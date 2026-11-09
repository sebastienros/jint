#nullable enable

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

    [TestCase("Object.getOwnPropertyNames(o)")]
    [TestCase("Object.getOwnPropertySymbols(s)")]
    [TestCase("g().chunks(3).next()")]
    [TestCase("g().chunks(5).next()")]
    [TestCase("g().windows(3).next()")]
    [TestCase("g().windows(5, 'allow-partial').next()")]
    public void AnArrayPastTheLimitIsRefused(string expression)
    {
        var engine = EngineWith(2);
        engine.Execute("""
            var o = { a: 1, b: 2, c: 3 };
            var s = { [Symbol('a')]: 1, [Symbol('b')]: 2, [Symbol('c')]: 3 };
            function* g() { yield 0; yield 1; yield 2; }
            """);

        var thrown = Caught.Exception(() => engine.Evaluate(expression));

        thrown.Should().BeOfType<MemoryLimitExceededException>();
        thrown!.Message.Should().Contain("3").And.Contain("2");
    }

    [TestCase("Object.getOwnPropertyNames(o).length", 3)]
    [TestCase("Object.getOwnPropertySymbols(s).length", 3)]
    [TestCase("g().chunks(3).next().value.length", 3)]
    [TestCase("g().chunks(5).next().value.length", 3)]
    [TestCase("g().windows(3).next().value.length", 3)]
    [TestCase("g().windows(5, 'allow-partial').next().value.length", 3)]
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
    [TestCase("Reflect.apply(applied, null, { length: 3 })")]
    [TestCase("applied(...g())")]
    [TestCase("Reflect.construct(constructed, { length: 3 })")]
    [TestCase("new constructed(...g())")]
    public void AProxyTrapIsRefusedAnArgumentsArrayPastTheLimit(string expression)
    {
        var engine = EngineWith(2);
        engine.Execute("""
            function* g() { yield 0; yield 1; yield 2; }
            var applied = new Proxy(function () {}, { apply(t, thisArg, args) { return args.length; } });
            var constructed = new Proxy(function () {}, { construct(t, args) { return { n: args.length }; } });
            """);

        Caught.Exception(() => engine.Evaluate(expression)).Should().BeOfType<MemoryLimitExceededException>();
    }

    [TestCase("Reflect.apply(applied, null, { length: 3 })", 3)]
    [TestCase("applied(...g())", 3)]
    [TestCase("Reflect.construct(constructed, { length: 3 }).n", 3)]
    [TestCase("new constructed(...g()).n", 3)]
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
    [Test]
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

#if NET8_0_OR_GREATER
    [Test]
    public void FormDataGetAllRefusesAnArrayPastTheLimit()
    {
        var engine = new Engine(options =>
        {
            options.UseWebApis(WebApiFeatures.Files);
            options.Constraints.MaxArraySize = 2;
        });
        engine.Execute("var fd = new FormData(); fd.append('a', '1'); fd.append('a', '2'); fd.append('a', '3'); fd.append('b', '4');");

        Caught.Exception(() => engine.Evaluate("fd.getAll('a')")).Should().BeOfType<MemoryLimitExceededException>();

        // The entries are in the form data, not in the array: one name fits.
        engine.Evaluate("fd.getAll('b').length").AsNumber().Should().Be(1);
    }

    [Test]
    public void FormDataGetAllAllowsAnArrayOfExactlyTheLimit()
    {
        var engine = new Engine(options =>
        {
            options.UseWebApis(WebApiFeatures.Files);
            options.Constraints.MaxArraySize = 3;
        });
        engine.Execute("var fd = new FormData(); fd.append('a', '1'); fd.append('a', '2'); fd.append('a', '3');");

        engine.Evaluate("fd.getAll('a').join()").AsString().Should().Be("1,2,3");
    }

    [Test]
    public void PerformanceGetEntriesRefusesAnArrayPastTheLimit()
    {
        var engine = new Engine(options =>
        {
            options.UseWebApis(WebApiFeatures.Performance);
            options.Constraints.MaxArraySize = 2;
        });
        engine.Execute("performance.mark('a'); performance.mark('b'); performance.mark('c');");

        Caught.Exception(() => engine.Evaluate("performance.getEntries()")).Should().BeOfType<MemoryLimitExceededException>();
        Caught.Exception(() => engine.Evaluate("performance.getEntriesByType('mark')")).Should().BeOfType<MemoryLimitExceededException>();

        // A filter that keeps two is within the limit.
        engine.Evaluate("performance.getEntriesByName('a').length").AsNumber().Should().Be(1);
    }

    [Test]
    public void PerformanceGetEntriesAllowsAnArrayOfExactlyTheLimit()
    {
        var engine = new Engine(options =>
        {
            options.UseWebApis(WebApiFeatures.Performance);
            options.Constraints.MaxArraySize = 3;
        });
        engine.Execute("performance.mark('a'); performance.mark('b'); performance.mark('c');");

        engine.Evaluate("performance.getEntries().length").AsNumber().Should().Be(3);
    }

    /// <summary>
    /// The same port three times, from a generator so that no array larger than the limit is built by the script.
    /// </summary>
    [Test]
    public void MessageEventPortsRefuseAnArrayPastTheLimit()
    {
        var engine = new Engine(options =>
        {
            options.UseWebApis(WebApiFeatures.Messaging);
            options.Constraints.MaxArraySize = 2;
        });
        engine.Execute("var port = new MessageChannel().port1; function* ports(n) { for (let i = 0; i < n; i++) yield port; }");

        Caught.Exception(() => engine.Evaluate("new MessageEvent('m', { ports: ports(3) })")).Should().BeOfType<MemoryLimitExceededException>();
        engine.Evaluate("new MessageEvent('m', { ports: ports(2) }).ports.length").AsNumber().Should().Be(2);
    }
#endif
}
