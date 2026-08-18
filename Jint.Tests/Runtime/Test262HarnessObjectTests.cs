#nullable enable

using Jint.Native.Function;
using Jint.Native.Object;

namespace Jint.Tests.Runtime;

/// <summary>
/// test262's INTERPRETING.md specifies that <c>$262.createRealm()</c> "creates a new ECMAScript Realm,
/// defines this API on the new realm's global object, and returns the <c>$262</c> property of the new
/// realm's global object". So the value handed back is a full <c>$262</c> — not the bare global — and the
/// new realm's global carries it, which is how <c>otherGlobal.$262.detachArrayBuffer</c> resolves.
/// </summary>
public class Test262HarnessObjectTests
{
    [Fact]
    public void CreateRealmReturnsTheNewRealmsOwn262Object()
    {
        var engine = new Engine();
        Test262Object.Install(engine);

        var created = engine.Evaluate("$262.createRealm()");
        created.Should().BeAssignableTo<ObjectInstance>();
        engine.SetValue("other", created);

        foreach (var member in new[] { "createRealm", "detachArrayBuffer", "evalScript", "gc" })
        {
            engine.Evaluate("typeof other." + member).AsString().Should().Be("function", "$262." + member + " must exist on the created realm's $262");
        }

        engine.Evaluate("typeof other.global").AsString().Should().Be("object");

        // The returned object is the new realm's global's own $262, per INTERPRETING.md.
        engine.Evaluate("other.global.$262 === other").AsBoolean().Should().BeTrue();

        // ... and the top-level $262 knows its own global too.
        engine.Evaluate("$262.global === globalThis").AsBoolean().Should().BeTrue();
    }

    [Fact]
    public void CreatedRealmHasItsOwnGlobalAndIntrinsics()
    {
        var engine = new Engine();
        Test262Object.Install(engine);

        engine.SetValue("other", engine.Evaluate("$262.createRealm()"));

        engine.Evaluate("other.global !== globalThis").AsBoolean().Should().BeTrue();
        engine.Evaluate("other.global.Array !== Array").AsBoolean().Should().BeTrue();
        engine.Evaluate("other.global.TypeError !== TypeError").AsBoolean().Should().BeTrue();

        // Nested realms: the created $262 can create one of its own.
        engine.Evaluate("other.createRealm().global !== other.global").AsBoolean().Should().BeTrue();
    }

    [Fact]
    public void CreatedRealmsFunctionsBelongToThatRealm()
    {
        var engine = new Engine();
        Test262Object.Install(engine);

        var created = (ObjectInstance) engine.Evaluate("$262.createRealm()");
        var newGlobal = (ObjectInstance) created.Get("global");

        // The $262 object and every function on it must be built from the new realm's intrinsics,
        // never from whichever realm happened to be active when createRealm ran.
        created.Prototype.Should().BeSameAs(newGlobal.Get("Object").AsObject().Get("prototype"));

        foreach (var member in new[] { "createRealm", "detachArrayBuffer", "evalScript", "gc" })
        {
            var function = created.Get(member).Should().BeAssignableTo<Function>().Subject;
            function._realm.Should().NotBeSameAs(engine.Realm, member + " must not be pinned to the principal realm");
            function._realm.GlobalObject.Should().BeSameAs(newGlobal);
        }
    }

    [Fact]
    public void CreatedRealmDetachesABufferMadeInThatRealm()
    {
        var engine = new Engine();
        Test262Object.Install(engine);

        engine.SetValue("other", engine.Evaluate("$262.createRealm()"));

        engine.Evaluate("var buffer = other.evalScript('new ArrayBuffer(8)');");
        engine.Evaluate("buffer instanceof other.global.ArrayBuffer").AsBoolean().Should().BeTrue();
        engine.Evaluate("buffer.byteLength").AsNumber().Should().Be(8);

        engine.Evaluate("other.detachArrayBuffer(buffer);");
        engine.Evaluate("buffer.byteLength").AsNumber().Should().Be(0);
    }
}
