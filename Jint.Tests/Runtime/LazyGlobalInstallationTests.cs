#nullable enable

using Jint.Native;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Descriptors.Specialized;

namespace Jint.Tests.Runtime;

/// <summary>
/// <c>engine.AddLazyGlobal</c> installs the same descriptor the options-time registration does, but
/// onto an engine whose interpreter caches may already hold a resolved binding for that name. These are the
/// parts of that the public surface cannot see: which descriptor is stored, that it is stored unmaterialized,
/// and — the load-bearing one — that installing it bumps the own-property version every global-binding and
/// member-read inline cache validates against.
/// </summary>
public class LazyGlobalInstallationTests
{
    [Fact]
    public void InstallsAnUnmaterializedLazyDescriptor()
    {
        var engine = new Engine();
        engine.AddLazyGlobal("value", _ => "built");

        var descriptor = engine.Realm.GlobalObject.GetOwnProperty("value");

        descriptor.Should().BeOfType<LazyPropertyDescriptor<Engine>>();
        // CustomJsValue is what routes the read through the resolver; it is cleared the moment the value
        // materializes, which is also what admits the descriptor to the global-binding inline cache.
        (descriptor._flags & PropertyFlag.CustomJsValue).Should().NotBe(PropertyFlag.None);
        descriptor._value.Should().BeNull();
    }

    /// <summary>
    /// The design risk of a post-construction install, pinned directly. At construction time no inline cache
    /// exists, so an install that skipped the version bump would still be correct; after construction a
    /// warmed identifier site holds the previous descriptor by reference and revalidates it against
    /// <c>_propertiesVersion</c> alone. Every storage path the global object can take must bump it.
    /// </summary>
    [Fact]
    public void InstallingBumpsTheOwnPropertyVersionOnEveryStoragePath()
    {
        var engine = new Engine();
        var global = engine.Realm.GlobalObject;

        // (1) a brand new name — the hybrid side dictionary of the still-shaped global
        var before = global._propertiesVersion;
        engine.AddLazyGlobal("fresh", _ => 1);
        global._propertiesVersion.Should().NotBe(before);

        // (2) replacing a host global that already sits in that dictionary
        engine.SetValue("eager", "value");
        before = global._propertiesVersion;
        engine.AddLazyGlobal("eager", _ => 2);
        global._propertiesVersion.Should().NotBe(before);

        // (3) replacing a built-in, which lives in the shared layout rather than the dictionary
        before = global._propertiesVersion;
        engine.AddLazyGlobal("parseInt", _ => 3);
        global._propertiesVersion.Should().NotBe(before);

        // (4) and again once the global has been forced out of its shared layout altogether
        engine.Evaluate("globalThis[0] = 'deopt';");
        before = global._propertiesVersion;
        engine.AddLazyGlobal("afterDeopt", _ => 4);
        global._propertiesVersion.Should().NotBe(before);

        engine.Evaluate("[fresh, eager, parseInt, afterDeopt].join(',')").AsString().Should().Be("1,2,3,4");
    }

    /// <summary>
    /// Installing a global object property creates no lexical binding and injects nothing into an existing
    /// environment, so the two counters that describe those must NOT move — a lexical declaration shadows a
    /// global property, and bumping either would be invalidating caches for a change that did not happen.
    /// </summary>
    [Fact]
    public void InstallingDoesNotDisturbTheLexicalCounters()
    {
        var engine = new Engine();
        var globalEnv = engine.Realm.GlobalEnv;

        var lexicalMutations = globalEnv._lexicalMutations;
        var injectionEpoch = engine._envBindingInjectionEpoch;

        engine.AddLazyGlobal("value", _ => JsValue.Undefined);

        globalEnv._lexicalMutations.Should().Be(lexicalMutations);
        engine._envBindingInjectionEpoch.Should().Be(injectionEpoch);
    }

    [Fact]
    public void AnInstalledLazyGlobalIsFieldBackedSoARestoreCanRevertIt()
    {
        var engine = new Engine();
        engine.AddLazyGlobal("value", _ => "built");

        // The marker asserts the value lives in the inherited _value field, which is what lets a snapshot
        // restore put the descriptor back into its unmaterialized state.
        engine.Realm.GlobalObject.GetOwnProperty("value").Should().BeAssignableTo<IFieldBackedLazyDescriptor>();
    }
}
