using System.Text.Json;

namespace Jint.Tests.DevTools.Session;

/// <summary>
/// <c>Runtime.getProperties</c>: what a client sees when it expands a value, and what expanding one must
/// never do.
/// </summary>
/// <remarks>
/// The load-bearing tests here are the negative ones. A client expands an object while the engine is stopped
/// or between turns, and an implementation that read through <c>Get</c> instead of through a descriptor
/// would run the page's own code at the moment somebody clicked a triangle.
/// </remarks>
public class RuntimePropertiesTests
{
    [Test]
    public async Task OwnPropertiesCarryTheirDescriptorFlags()
    {
        await using var session = await AttachedSession.CreateAsync();

        var handle = await session.HandleAsync(
            """
            (function () {
                const value = { open: 1 };
                Object.defineProperty(value, 'sealed', { value: 2, writable: false, enumerable: false, configurable: false });
                return value;
            })()
            """);

        var properties = await session.PropertiesAsync(handle, ownProperties: true);

        var open = properties.Property("open");
        open.GetProperty("value").GetProperty("value").GetInt32().Should().Be(1);
        open.GetProperty("writable").GetBoolean().Should().BeTrue();
        open.GetProperty("enumerable").GetBoolean().Should().BeTrue();
        open.GetProperty("configurable").GetBoolean().Should().BeTrue();
        open.GetProperty("isOwn").GetBoolean().Should().BeTrue();

        var sealedProperty = properties.Property("sealed");
        sealedProperty.GetProperty("writable").GetBoolean().Should().BeFalse();
        sealedProperty.GetProperty("enumerable").GetBoolean().Should().BeFalse();
        sealedProperty.GetProperty("configurable").GetBoolean().Should().BeFalse();
    }

    /// <summary>
    /// A getter is reported as its two functions and never called, which is the whole promise of this
    /// command.
    /// </summary>
    [Test]
    public async Task AnAccessorIsReportedAndNeverInvoked()
    {
        await using var session = await AttachedSession.CreateAsync();

        var handle = await session.HandleAsync(
            """
            (function () {
                globalThis.reads = 0;
                globalThis.writes = 0;
                const value = {};
                Object.defineProperty(value, 'trap', {
                    enumerable: true,
                    configurable: true,
                    get: function reader() { globalThis.reads++; throw new Error('a getter ran'); },
                    set: function writer(v) { globalThis.writes++; },
                });
                return value;
            })()
            """);

        var properties = await session.PropertiesAsync(handle, ownProperties: true);
        var trap = properties.Property("trap");

        trap.Optional("value").Should().BeNull("an accessor has no value that could be reported without calling it");
        trap.GetProperty("get").GetProperty("type").GetString().Should().Be("function");
        trap.GetProperty("get").GetProperty("description").GetString().Should().StartWith("function reader() {");
        trap.GetProperty("set").GetProperty("type").GetString().Should().Be("function");

        var counters = await session.EvaluateAsync("[reads, writes]", returnByValue: true);
        counters.GetProperty("value").EnumerateArray().Select(count => count.GetInt32()).Should().Equal(0, 0);
    }

    [Test]
    public async Task WithoutOwnPropertiesThePrototypeChainIsWalkedAndMarked()
    {
        await using var session = await AttachedSession.CreateAsync();

        var handle = await session.HandleAsync(
            """
            (function () {
                function Base() {}
                Base.prototype.inherited = 'from the prototype';
                const value = new Base();
                value.own = 1;
                return value;
            })()
            """);

        var walked = await session.PropertiesAsync(handle);
        walked.Property("own").GetProperty("isOwn").GetBoolean().Should().BeTrue();
        walked.Property("inherited").GetProperty("isOwn").GetBoolean().Should().BeFalse();
        walked.Names().Should().Contain("hasOwnProperty", "the walk reaches Object.prototype");

        var ownOnly = await session.PropertiesAsync(handle, ownProperties: true);
        ownOnly.Names().Should().Equal("own");
    }

    [Test]
    public async Task APropertyTheChainShadowsIsReportedOnce()
    {
        await using var session = await AttachedSession.CreateAsync();

        var handle = await session.HandleAsync(
            """
            (function () {
                function Base() {}
                Base.prototype.shadowed = 'prototype';
                const value = new Base();
                value.shadowed = 'own';
                return value;
            })()
            """);

        var properties = await session.PropertiesAsync(handle);

        var shadowed = properties.Property("shadowed");
        shadowed.GetProperty("isOwn").GetBoolean().Should().BeTrue();
        shadowed.GetProperty("value").GetProperty("value").GetString().Should().Be(
            "own",
            "the object nearest the receiver is the one a script would read");
    }

    /// <summary>
    /// A proxy anywhere in the chain ends the walk, not only at its start: <c>ownKeys</c> is a trap wherever
    /// the object holding it sits.
    /// </summary>
    [Test]
    public async Task AProxyInThePrototypeChainEndsTheWalkWithoutRunningATrap()
    {
        await using var session = await AttachedSession.CreateAsync();

        var handle = await session.HandleAsync(
            """
            (function () {
                globalThis.traps = 0;
                const proxy = new Proxy({ hidden: 1 }, {
                    ownKeys: function () { globalThis.traps++; throw new Error('ownKeys'); },
                    getOwnPropertyDescriptor: function () { globalThis.traps++; throw new Error('descriptor'); },
                });
                const value = { own: 1 };
                Object.setPrototypeOf(value, proxy);
                return value;
            })()
            """);

        var properties = await session.PropertiesAsync(handle);
        properties.Names().Should().Equal("own");

        var traps = await session.EvaluateAsync("traps", returnByValue: true);
        traps.GetProperty("value").GetInt32().Should().Be(0);
    }

    [Test]
    public async Task AccessorPropertiesOnlyLeavesOutTheDataOnesAndTheInternalOnes()
    {
        await using var session = await AttachedSession.CreateAsync();

        var handle = await session.HandleAsync(
            """
            (function () {
                const value = { data: 1 };
                Object.defineProperty(value, 'computed', { enumerable: true, get: function () { return 2; } });
                return value;
            })()
            """);

        var properties = await session.PropertiesAsync(handle, ownProperties: true, accessorPropertiesOnly: true);

        properties.Names().Should().Equal("computed");
        properties.TryGetProperty("internalProperties", out _).Should().BeFalse(
            "the protocol says an accessor-only listing carries none");
    }

    /// <summary>
    /// A function whose definition the engine knows carries <c>[[FunctionLocation]]</c>, which is what makes
    /// the row clickable in a front end: it opens the script at the declaration.
    /// </summary>
    [Test]
    public async Task AFunctionCarriesWhereItWasDeclared()
    {
        await using var session = await AttachedSession.CreateAsync();

        await session.Target.PostAsync(engine => engine.Execute(
            """
            function first() {}
            function second(a, b) { return a + b; }
            """,
            "main.js"));
        await session.EnableDebuggerAsync();
        var scriptId = (await session.EventAsync("Debugger.scriptParsed")).GetProperty("scriptId").GetString();

        var handle = await session.HandleAsync("second");
        var properties = await session.PropertiesAsync(handle, ownProperties: true);

        var location = properties.Internal("[[FunctionLocation]]").GetProperty("value");
        location.GetProperty("type").GetString().Should().Be("object");
        location.GetProperty("subtype").GetString().Should().Be("internal#location");

        var position = location.GetProperty("value");
        position.GetProperty("scriptId").GetString().Should().Be(scriptId);
        position.GetProperty("lineNumber").GetInt32().Should().Be(1, "the protocol counts lines from zero");
        position.GetProperty("columnNumber").GetInt32().Should().Be(0);
    }

    /// <summary>
    /// A function the engine has no declaration for — a built-in — carries none, rather than a location
    /// against the sentinel script a front end cannot open.
    /// </summary>
    [Test]
    public async Task ANativeFunctionCarriesNoLocation()
    {
        await using var session = await AttachedSession.CreateAsync();

        var handle = await session.HandleAsync("Math.max");
        var properties = await session.PropertiesAsync(handle, ownProperties: true);

        properties.GetProperty("internalProperties").EnumerateArray()
            .Select(property => property.GetProperty("name").GetString())
            .Should().NotContain("[[FunctionLocation]]");
    }

    /// <summary>
    /// A bound function carries what it is bound to, which is the only way a client sees through one: the
    /// wrapper has no source of its own and its properties say nothing about the target.
    /// </summary>
    [Test]
    public async Task ABoundFunctionCarriesItsTargetItsThisAndItsArguments()
    {
        await using var session = await AttachedSession.CreateAsync();

        var handle = await session.HandleAsync(
            "(function () { const receiver = { tag: 'r' }; return (function named(a, b) {}).bind(receiver, 1, 'two'); })()");
        var properties = await session.PropertiesAsync(handle, ownProperties: true);

        var target = properties.Internal("[[TargetFunction]]").GetProperty("value");
        target.GetProperty("type").GetString().Should().Be("function");
        target.GetProperty("objectId").GetString().Should().NotBeNullOrEmpty();

        var boundThis = properties.Internal("[[BoundThis]]").GetProperty("value");
        boundThis.GetProperty("type").GetString().Should().Be("object");

        var boundArgs = properties.Internal("[[BoundArgs]]").GetProperty("value");
        boundArgs.GetProperty("subtype").GetString().Should().Be("array");
        boundArgs.GetProperty("description").GetString().Should().Be("Array(2)");

        // The array is a copy, so expanding it never hands a client the storage the bound call reads from.
        var arguments = await session.PropertiesAsync(boundArgs.GetProperty("objectId").GetString()!, ownProperties: true);
        arguments.Property("0").GetProperty("value").GetProperty("value").GetInt32().Should().Be(1);
        arguments.Property("1").GetProperty("value").GetProperty("value").GetString().Should().Be("two");
    }

    [Test]
    public async Task NonIndexedPropertiesOnlyLeavesOutTheElements()
    {
        await using var session = await AttachedSession.CreateAsync();

        var handle = await session.HandleAsync("(function () { const value = ['a', 'b']; value.tag = 'x'; return value; })()");

        var everything = await session.PropertiesAsync(handle, ownProperties: true);
        everything.Names().Should().Contain(["0", "1", "length", "tag"]);

        var named = await session.PropertiesAsync(handle, ownProperties: true, nonIndexedPropertiesOnly: true);
        named.Names().Should().Equal("length", "tag");
    }

    [Test]
    public async Task ASymbolKeyedPropertyCarriesItsSymbol()
    {
        await using var session = await AttachedSession.CreateAsync();

        var handle = await session.HandleAsync(
            "(function () { const value = {}; value[Symbol.for('marker')] = 1; return value; })()");

        var properties = await session.PropertiesAsync(handle, ownProperties: true);
        var marker = properties.Property("Symbol(marker)");

        marker.GetProperty("symbol").GetProperty("type").GetString().Should().Be("symbol");
        marker.GetProperty("symbol").GetProperty("description").GetString().Should().Be("Symbol(marker)");
        marker.GetProperty("value").GetProperty("value").GetInt32().Should().Be(1);
    }

    [Test]
    public async Task ThePrototypeIsAnInternalProperty()
    {
        await using var session = await AttachedSession.CreateAsync();

        var handle = await session.HandleAsync("({ a: 1 })");
        var properties = await session.PropertiesAsync(handle, ownProperties: true);

        var prototype = properties.Internal("[[Prototype]]");
        prototype.GetProperty("value").GetProperty("type").GetString().Should().Be("object");
        prototype.GetProperty("value").GetProperty("objectId").GetString().Should().NotBeNullOrEmpty();

        var bare = await session.HandleAsync("Object.create(null)");
        var bareProperties = await session.PropertiesAsync(bare, ownProperties: true);
        bareProperties.Internal("[[Prototype]]").GetProperty("value").GetProperty("subtype").GetString().Should().Be("null");
    }

    [TestCase("Promise.resolve(42)", "fulfilled", "42")]
    [TestCase("new Promise(function () {})", "pending", null)]
    public async Task APromiseReportsItsStateAsAnInternalProperty(string expression, string state, string? result)
    {
        await using var session = await AttachedSession.CreateAsync();

        // Settling happens on a later turn, so the state is read after one round trip through the loop.
        await session.EvaluateAsync("globalThis.promise = " + expression);
        await session.EvaluateAsync("Promise.resolve()", awaitPromise: true);

        var handle = await session.HandleAsync("promise");
        var properties = await session.PropertiesAsync(handle, ownProperties: true);

        properties.Internal("[[PromiseState]]").GetProperty("value").GetProperty("value").GetString().Should().Be(state);

        if (result is null)
        {
            properties.GetProperty("internalProperties").EnumerateArray()
                .Select(property => property.GetProperty("name").GetString())
                .Should().NotContain("[[PromiseResult]]");
            return;
        }

        var settled = properties.Internal("[[PromiseResult]]").GetProperty("value");
        settled.GetProperty("description").GetString().Should().Be(result);
        settled.Optional("objectId").Should().BeNull(
            "a settled promise's value is answered as a description, because the engine publishes the value itself to nothing outside its own assembly; Runtime.awaitPromise is what hands it over");
    }

    /// <summary>
    /// A CLR value's members are listed and not read, which is why a member that throws does not take the
    /// listing with it.
    /// </summary>
    /// <remarks>
    /// The engine projects a CLR property as an accessor descriptor, so it arrives here as one and is
    /// reported the way every other accessor is: named, with its functions, uninvoked. That is a real
    /// limitation for a host inspecting its own objects — it sees the members and not their values — and it
    /// is the same promise the rest of this command makes rather than an exception carved out for interop.
    /// </remarks>
    [Test]
    public async Task AHostValuesMembersAreListedAndNeverRead()
    {
        await using var session = await AttachedSession.CreateAsync(engine => engine.SetValue("host", new PartlyBroken()));

        var handle = await session.HandleAsync("host");
        var properties = await session.PropertiesAsync(handle, ownProperties: true);

        properties.Names().Should().Contain(["Fine", "Broken"]);

        var broken = properties.Property("Broken");
        broken.Optional("value").Should().BeNull("reading it is what would have thrown");
        broken.Optional("wasThrown").Should().BeNull("nothing was read, so nothing threw");
        broken.GetProperty("get").GetProperty("type").GetString().Should().Be("function");
    }

    [Test]
    public async Task GeneratePreviewReachesThePropertyValues()
    {
        await using var session = await AttachedSession.CreateAsync();

        var handle = await session.HandleAsync("({ nested: { a: 1 } })");
        var properties = await session.PropertiesAsync(handle, ownProperties: true, generatePreview: true);

        var nested = properties.Property("nested").GetProperty("value");
        nested.GetProperty("preview").GetProperty("properties").EnumerateArray().Single()
            .GetProperty("name").GetString().Should().Be("a");
    }

    /// <summary>A CLR value one of whose members throws when read.</summary>
    private sealed class PartlyBroken
    {
        public int Fine => 1;

        public int Broken => throw new InvalidOperationException("this host member throws");
    }
}
