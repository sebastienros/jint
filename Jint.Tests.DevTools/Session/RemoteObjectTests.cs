using System.Text.Json;
using Jint.DevTools;
using Jint.DevTools.Domains;
using Jint.Native;

namespace Jint.Tests.DevTools.Session;

/// <summary>
/// The handles a client holds values by: what one says about the value, how long it lives, and what ends it.
/// </summary>
/// <remarks>
/// A handle is a promise to keep a value alive until the client releases it, so the tests that matter most
/// here are the ones about the promise ending — released, group released, session detached — rather than the
/// ones about it being made.
/// </remarks>
public class RemoteObjectTests
{
    [Test]
    public async Task AHandleResolvesBackToTheValueItWasMintedFor()
    {
        await using var session = await AttachedSession.CreateAsync();

        var objectId = await session.HandleAsync("({ a: 1 })");
        var properties = await session.PropertiesAsync(objectId, ownProperties: true);

        properties.Property("a").GetProperty("value").GetProperty("value").GetInt32().Should().Be(1);
    }

    [Test]
    public async Task EveryHandleIsDistinctEvenForTheSameValue()
    {
        await using var session = await AttachedSession.CreateAsync();

        await session.EvaluateAsync("globalThis.shared = { a: 1 }");

        var first = await session.HandleAsync("shared");
        var second = await session.HandleAsync("shared");

        second.Should().NotBe(first, "V8 mints a fresh identifier per wrap, so releasing one never invalidates another");

        await session.ResultAsync("Runtime.releaseObject", $$"""{"objectId":"{{first}}"}""");
        var properties = await session.PropertiesAsync(second);
        properties.GetProperty("result").GetArrayLength().Should().BeGreaterThan(0);
    }

    [Test]
    public async Task AnUnknownHandleIsRefusedInChromesWording()
    {
        await using var session = await AttachedSession.CreateAsync();

        var error = await session.ErrorAsync("Runtime.getProperties", """{"objectId":"nope"}""");

        error.GetProperty("code").GetInt32().Should().Be(-32000);
        error.GetProperty("message").GetString().Should().Be("Could not find object with given id");
    }

    [Test]
    public async Task AHandleFromOneTargetIsNotResolvableByAnother()
    {
        await using var first = await AttachedSession.CreateAsync();
        await using var second = await AttachedSession.CreateAsync();

        var objectId = await first.HandleAsync("({ a: 1 })");
        var error = await second.ErrorAsync("Runtime.getProperties", $$"""{"objectId":"{{objectId}}"}""");

        error.GetProperty("message").GetString().Should().Be(
            "Could not find object with given id",
            "an identifier carries the target it came from, so a client that mixed two up is told rather than answered about the wrong engine");
    }

    [Test]
    public async Task ReleaseObjectEndsTheHandleAndReleasingItTwiceIsStillASuccess()
    {
        await using var session = await AttachedSession.CreateAsync();

        var objectId = await session.HandleAsync("({ a: 1 })");
        session.Target.RemoteObjects.Count.Should().Be(1);

        await session.ResultAsync("Runtime.releaseObject", $$"""{"objectId":"{{objectId}}"}""");
        session.Target.RemoteObjects.Count.Should().Be(0);

        // A client tidying up releases what a detach may already have taken, and Chrome answers that with a
        // success rather than an error.
        await session.ResultAsync("Runtime.releaseObject", $$"""{"objectId":"{{objectId}}"}""");

        var error = await session.ErrorAsync("Runtime.getProperties", $$"""{"objectId":"{{objectId}}"}""");
        error.GetProperty("message").GetString().Should().Be("Could not find object with given id");
    }

    [Test]
    public async Task ReleaseObjectGroupEndsOnlyThatGroup()
    {
        await using var session = await AttachedSession.CreateAsync();

        var grouped = await session.HandleAsync("({ a: 1 })", objectGroup: "one");
        var other = await session.HandleAsync("({ b: 2 })", objectGroup: "two");

        await session.ResultAsync("Runtime.releaseObjectGroup", """{"objectGroup":"one"}""");

        (await session.ErrorAsync("Runtime.getProperties", $$"""{"objectId":"{{grouped}}"}"""))
            .GetProperty("message").GetString().Should().Be("Could not find object with given id");

        var survivor = await session.PropertiesAsync(other, ownProperties: true);
        survivor.Property("b").GetProperty("value").GetProperty("value").GetInt32().Should().Be(2);
    }

    /// <summary>
    /// A property a client expanded is billed to the group the object it came from belongs to, so releasing
    /// that group frees the tree rather than only its root.
    /// </summary>
    [Test]
    public async Task AGroupReleasesTheHandlesMintedWhileWalkingIntoIt()
    {
        await using var session = await AttachedSession.CreateAsync();

        var root = await session.HandleAsync("({ nested: { a: 1 } })", objectGroup: "walk");
        var properties = await session.PropertiesAsync(root, ownProperties: true);
        var nested = properties.Property("nested").GetProperty("value").GetProperty("objectId").GetString()!;

        await session.ResultAsync("Runtime.releaseObjectGroup", """{"objectGroup":"walk"}""");

        (await session.ErrorAsync("Runtime.getProperties", $$"""{"objectId":"{{nested}}"}"""))
            .GetProperty("message").GetString().Should().Be("Could not find object with given id");
    }

    /// <summary>
    /// Detaching releases everything the attachment registered, because the client it promised to keep them
    /// for has gone.
    /// </summary>
    [Test]
    public async Task DetachingReleasesEveryHandleThatSessionRegistered()
    {
        await using var session = await AttachedSession.CreateAsync();

        await session.HandleAsync("({ a: 1 })");
        await session.HandleAsync("({ b: 2 })");
        session.Target.RemoteObjects.Count.Should().Be(2);

        await session.Protocol.SendAsync("Target.detachFromTarget", $$"""{"sessionId":"{{session.SessionId}}"}""");

        session.Target.RemoteObjects.Count.Should().Be(0);
    }

    [Test]
    public async Task DisposingTheTargetReleasesTheTable()
    {
        var session = await AttachedSession.CreateAsync();
        try
        {
            await session.HandleAsync("({ a: 1 })");
            session.Target.RemoteObjects.Count.Should().Be(1);

            await session.Target.DisposeAsync();
            session.Target.RemoteObjects.Count.Should().Be(0);
        }
        finally
        {
            await session.DisposeAsync();
        }
    }

    [TestCase("Symbol('tag')", "symbol", TestName = "a symbol")]
    [TestCase("10n", "bigint", TestName = "a BigInt")]
    public async Task APrimitiveWithNoJsonFormIsStillAddressable(string expression, string type)
    {
        await using var session = await AttachedSession.CreateAsync();

        var result = await session.EvaluateAsync(expression);

        result.GetProperty("type").GetString().Should().Be(type);
        result.GetProperty("objectId").GetString().Should().NotBeNullOrEmpty();

        // And has nothing inside it, which is a listing rather than a refusal.
        var properties = await session.PropertiesAsync(result.GetProperty("objectId").GetString()!);
        properties.GetProperty("result").GetArrayLength().Should().Be(0);
    }

    [TestCase("Symbol('tag')", TestName = "a symbol")]
    [TestCase("10n", TestName = "a BigInt")]
    public async Task APrimitiveWithNoJsonFormIsRefusedByValue(string expression)
    {
        await using var session = await AttachedSession.CreateAsync();

        var error = await session.ErrorAsync("Runtime.evaluate", $$"""{"expression":"{{expression}}","returnByValue":true}""");

        error.GetProperty("code").GetInt32().Should().Be(-32000);
        error.GetProperty("message").GetString().Should().Be("Object couldn't be returned by value");
    }

    [Test]
    public async Task ByValueRunsToJsonBecauseThatIsWhatTheClientAskedFor()
    {
        await using var session = await AttachedSession.CreateAsync();

        var result = await session.EvaluateAsync(
            "({ hidden: 1, toJSON: function () { return { shown: 2 }; } })",
            returnByValue: true);

        result.GetProperty("value").GetProperty("shown").GetInt32().Should().Be(2);
        result.GetProperty("value").TryGetProperty("hidden", out _).Should().BeFalse(
            "returnByValue is JSON.stringify's contract, hooks and all, and V8 answers the same");
    }

    [TestCase("new Uint8Array([1, 2, 3])", "object", "typedarray", "Uint8Array(3)")]
    [TestCase("new ArrayBuffer(8)", "object", "arraybuffer", "ArrayBuffer(8)")]
    [TestCase("new DataView(new ArrayBuffer(8))", "object", "dataview", "DataView(8)")]
    [TestCase("new WeakMap()", "object", "weakmap", "WeakMap")]
    [TestCase("new WeakSet()", "object", "weakset", "WeakSet")]
    [TestCase("new Map([[1, 2], [3, 4]])", "object", "map", "Map(2)")]
    [TestCase("new Set([1, 2, 3])", "object", "set", "Set(3)")]
    [TestCase("[1, 2, 3]", "object", "array", "Array(3)")]
    [TestCase("(function* g() {})()", "object", "generator", "Generator")]
    [TestCase("[][Symbol.iterator]()", "object", "iterator", "Object")]
    [TestCase("new Proxy({}, {})", "object", "proxy", "Proxy")]
    public async Task ASubtypeAndDescriptionComeFromTheEnginesOwnDescriber(string expression, string type, string subtype, string description)
    {
        await using var session = await AttachedSession.CreateAsync();

        var result = await session.EvaluateAsync(expression);

        result.GetProperty("type").GetString().Should().Be(type);
        result.GetProperty("subtype").GetString().Should().Be(subtype);
        result.GetProperty("description").GetString().Should().Be(description);
    }

    /// <summary>
    /// A function's <c>description</c> is its source text, because the front end reads that field as
    /// <c>Function.prototype.toString</c> output and parses the name back out of it — a short label makes
    /// every function in a Scope pane render as <c>ƒ undefined()</c>. Recorded from a real Chrome.
    /// </summary>
    [TestCase("(function named() {})", "function named() {}")]
    [TestCase("(async function a() {})", "async function a() {}")]
    [TestCase("(function* g() {})", "function* g() {}")]
    [TestCase("(class C { m() {} })", "class C { m() {} }")]
    [TestCase("(x => x * 2)", "x => x * 2")]
    public async Task AFunctionIsDescribedByItsSourceTheWayAFrontEndParsesIt(string expression, string description)
    {
        await using var session = await AttachedSession.CreateAsync();

        var result = await session.EvaluateAsync(expression);

        result.GetProperty("type").GetString().Should().Be("function");
        result.GetProperty("description").GetString().Should().Be(description);
        result.TryGetProperty("preview", out _).Should().BeFalse("a function's declaration is already its description");
    }

    /// <summary>
    /// A bound function is a function to the front end — Chrome sends <c>type: "function"</c> and the
    /// nameless native placeholder for one, never <c>type: "object"</c>.
    /// </summary>
    [Test]
    public async Task ABoundFunctionIsAFunctionAndNotAnObject()
    {
        await using var session = await AttachedSession.CreateAsync();

        var result = await session.EvaluateAsync("(function named(a) { return a; }).bind(null)");

        result.GetProperty("type").GetString().Should().Be("function");
        result.GetProperty("className").GetString().Should().Be("Function");
        result.GetProperty("description").GetString().Should().Be("function () { [native code] }");
    }

    [Test]
    public async Task AFunctionWithNoSourceIsThePlaceholderChromeSends()
    {
        await using var session = await AttachedSession.CreateAsync();

        var result = await session.EvaluateAsync("Math.max");

        result.GetProperty("type").GetString().Should().Be("function");
        result.GetProperty("description").GetString().Should().Be("function max() { [native code] }");
    }

    [Test]
    public async Task AFunctionInsideAPreviewCarriesTheEmptyValueChromeSends()
    {
        await using var session = await AttachedSession.CreateAsync();

        var result = await session.EvaluateAsync("({ run: function run() { return 1; } })", generatePreview: true);

        var property = result.GetProperty("preview").GetProperty("properties").EnumerateArray().Single();
        property.GetProperty("name").GetString().Should().Be("run");
        property.GetProperty("type").GetString().Should().Be("function");

        // Chrome sends the empty string here and lets the front end draw the ƒ from the type; a whole
        // declaration inside an inline preview is not what that field is for.
        property.GetProperty("value").GetString().Should().Be("");
    }

    [Test]
    public async Task AClrValueIsNamedRatherThanRead()
    {
        await using var session = await AttachedSession.CreateAsync(engine => engine.SetValue("host", new HostThing()));

        var result = await session.EvaluateAsync("host", generatePreview: true);

        result.GetProperty("type").GetString().Should().Be("object");
        result.TryGetProperty("subtype", out _).Should().BeFalse("the protocol has no subtype for a CLR value");
        result.GetProperty("className").GetString().Should().Be(nameof(HostThing));
        result.GetProperty("preview").GetProperty("properties").GetArrayLength().Should().Be(
            0,
            "reading a host object's members is host code, and a preview never runs any");
    }

    [Test]
    public async Task APreviewCarriesTheMembersAndNeverCallsAnAccessor()
    {
        await using var session = await AttachedSession.CreateAsync();

        var result = await session.EvaluateAsync(
            """
            (function () {
                globalThis.reads = 0;
                const value = { a: 1, b: 'two' };
                Object.defineProperty(value, 'lazy', { enumerable: true, get: function () { globalThis.reads++; return 3; } });
                return value;
            })()
            """,
            generatePreview: true);

        var preview = result.GetProperty("preview");
        preview.GetProperty("type").GetString().Should().Be("object");
        preview.GetProperty("overflow").GetBoolean().Should().BeFalse();

        var properties = preview.GetProperty("properties").EnumerateArray().ToArray();
        properties.Select(property => property.GetProperty("name").GetString()).Should().Equal("a", "b", "lazy");

        properties[0].GetProperty("type").GetString().Should().Be("number");
        properties[0].GetProperty("value").GetString().Should().Be("1");
        properties[1].GetProperty("type").GetString().Should().Be("string");
        properties[1].GetProperty("value").GetString().Should().Be("two");
        properties[2].GetProperty("type").GetString().Should().Be("accessor");
        properties[2].Optional("value").Should().BeNull("an accessor has no value that could be read without calling it");

        var reads = await session.EvaluateAsync("reads", returnByValue: true);
        reads.GetProperty("value").GetInt32().Should().Be(0, "generating a preview must never invoke a getter");
    }

    [Test]
    public async Task APreviewOfACollectionCarriesItsEntries()
    {
        await using var session = await AttachedSession.CreateAsync();

        var map = await session.EvaluateAsync("new Map([['k', 1]])", generatePreview: true);
        var entry = map.GetProperty("preview").GetProperty("entries").EnumerateArray().Single();
        entry.GetProperty("key").GetProperty("description").GetString().Should().Be("k");
        entry.GetProperty("value").GetProperty("description").GetString().Should().Be("1");

        var set = await session.EvaluateAsync("new Set(['only'])", generatePreview: true);
        var member = set.GetProperty("preview").GetProperty("entries").EnumerateArray().Single();
        member.Optional("key").Should().BeNull("a set entry has no key");
        member.GetProperty("value").GetProperty("description").GetString().Should().Be("only");
    }

    [Test]
    public async Task APreviewSaysSoWhenItLeftSomethingOut()
    {
        await using var session = await AttachedSession.CreateAsync();

        var result = await session.EvaluateAsync("Array.from({ length: 500 }, function (_, i) { return i; })", generatePreview: true);

        var preview = result.GetProperty("preview");
        preview.GetProperty("overflow").GetBoolean().Should().BeTrue();
        preview.GetProperty("properties").GetArrayLength().Should().BeLessThan(500, "a preview is bounded and says when it was");
    }

    /// <summary>
    /// A proxy is described by its kind alone and never opened: every way into one is a trap, and a trap is
    /// script.
    /// </summary>
    [Test]
    public async Task AProxyWithThrowingTrapsIsDescribedWithoutRunningOne()
    {
        await using var session = await AttachedSession.CreateAsync();

        var result = await session.EvaluateAsync(
            """
            (function () {
                globalThis.traps = 0;
                const handler = {
                    ownKeys: function () { globalThis.traps++; throw new Error('ownKeys'); },
                    get: function () { globalThis.traps++; throw new Error('get'); },
                    getOwnPropertyDescriptor: function () { globalThis.traps++; throw new Error('descriptor'); },
                    getPrototypeOf: function () { globalThis.traps++; throw new Error('prototype'); },
                };
                return new Proxy({ a: 1 }, handler);
            })()
            """,
            generatePreview: true);

        result.GetProperty("subtype").GetString().Should().Be("proxy");
        result.GetProperty("description").GetString().Should().Be("Proxy");

        var properties = await session.PropertiesAsync(result.GetProperty("objectId").GetString()!);
        properties.GetProperty("result").GetArrayLength().Should().Be(0);

        var traps = await session.EvaluateAsync("traps", returnByValue: true);
        traps.GetProperty("value").GetInt32().Should().Be(0, "not one trap may fire while a client is looking at a value");
    }

    /// <summary>
    /// The seam <c>Jint.Browser</c> answers <c>subtype: "node"</c> through, exercised by a describer this
    /// suite declares itself: an extension point nothing has tried is a design nobody has tried.
    /// </summary>
    [Test]
    public async Task ADescriberNamesWhatThisPackageDoesNotRecognize()
    {
        await using var session = await AttachedSession.CreateAsync(
            engine => engine.SetValue("element", engine.Evaluate("({ tagName: 'DIV' })")),
            new EngineTargetOptions { RemoteObjectDescriber = new TagNameDescriber() });

        var element = await session.EvaluateAsync("element");
        element.GetProperty("subtype").GetString().Should().Be("node");
        element.GetProperty("className").GetString().Should().Be("HTMLDivElement");
        element.GetProperty("description").GetString().Should().Be("div");

        var ordinary = await session.EvaluateAsync("({ a: 1 })");
        ordinary.Optional("subtype").Should().BeNull("a describer that declines leaves the ordinary description alone");
        ordinary.GetProperty("description").GetString().Should().Be("Object");
    }

    /// <summary>A describer that recognizes anything carrying a <c>tagName</c>, and reads no accessor to do it.</summary>
    private sealed class TagNameDescriber : RemoteObjectDescriber
    {
        internal override bool TryDescribe(JsValue value, out RemoteObjectHint hint)
        {
            if (value is Jint.Native.Object.ObjectInstance instance &&
                instance.GetOwnProperty("tagName") is { } descriptor &&
                !ReferenceEquals(descriptor, Jint.Runtime.Descriptors.PropertyDescriptor.Undefined) &&
                descriptor.Value.IsString())
            {
                var tag = descriptor.Value.AsString();
                hint = new RemoteObjectHint
                {
                    Subtype = "node",
                    ClassName = "HTML" + tag[..1] + tag[1..].ToLowerInvariant() + "Element",
                    Description = tag.ToLowerInvariant(),
                };

                return true;
            }

            hint = default;
            return false;
        }
    }

    /// <summary>A CLR value whose members a description must not read.</summary>
    private sealed class HostThing
    {
        public string Name => throw new InvalidOperationException("a description must never read a host member");
    }
}
