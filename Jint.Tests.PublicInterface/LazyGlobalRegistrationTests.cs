#nullable enable

using Jint.Native;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;

namespace Jint.Tests.PublicInterface;

/// <summary>
/// <c>Options.AddLazyGlobal</c> installs a global whose value is produced on first read. These tests pin the
/// three properties an embedder relies on: nothing is built until script asks for it, it is built at most
/// once per engine, and a single <see cref="Options"/> instance can drive many engines.
/// </summary>
public class LazyGlobalRegistrationTests
{
    [Fact]
    public void LazyGlobalIsNotMaterializedUntilRead()
    {
        var calls = 0;
        var engine = new Engine(options => options.AddLazyGlobal("hostApi", e =>
        {
            calls++;
            return new ClrFunction(e, "hostApi", (_, _) => "from-host");
        }));

        // constructing the engine and running unrelated script must not build it
        engine.Evaluate("var x = 1 + 1;");
        calls.Should().Be(0);

        // ... but the property is already visible
        engine.Evaluate("'hostApi' in globalThis").AsBoolean().Should().BeTrue();
        engine.Evaluate("Object.keys(globalThis).indexOf('hostApi') >= 0").AsBoolean().Should().BeTrue();
        calls.Should().Be(0);
    }

    [Fact]
    public void FactoryRunsOnFirstReadOnly()
    {
        var calls = 0;
        var engine = new Engine(options => options.AddLazyGlobal("hostApi", e =>
        {
            calls++;
            return new ClrFunction(e, "hostApi", (_, _) => "from-host");
        }));

        calls.Should().Be(0);

        engine.Evaluate("hostApi()").AsString().Should().Be("from-host");
        calls.Should().Be(1);

        engine.Evaluate("hostApi(); hostApi(); globalThis.hostApi;");
        calls.Should().Be(1);

        // identity is stable across reads
        engine.Evaluate("hostApi === globalThis.hostApi").AsBoolean().Should().BeTrue();
        calls.Should().Be(1);
    }

    [Fact]
    public void TypeofDoesMaterializeButExistenceChecksDoNot()
    {
        var calls = 0;
        var engine = new Engine(options => options.AddLazyGlobal("value", _ =>
        {
            calls++;
            return 42;
        }));

        engine.Evaluate("'value' in globalThis").AsBoolean().Should().BeTrue();
        engine.Evaluate("globalThis.hasOwnProperty('value')").AsBoolean().Should().BeTrue();
        engine.Evaluate("Object.getOwnPropertyNames(globalThis).indexOf('value') >= 0").AsBoolean().Should().BeTrue();
        calls.Should().Be(0);

        engine.Evaluate("typeof value").AsString().Should().Be("number");
        calls.Should().Be(1);
    }

    [Fact]
    public void SharedOptionsBuildOnePerEngine()
    {
        var calls = 0;
        var options = new Options();
        options.AddLazyGlobal("counter", _ =>
        {
            calls++;
            return calls;
        });

        var first = new Engine(options);
        var second = new Engine(options);
        var third = new Engine(options);

        calls.Should().Be(0);

        first.Evaluate("counter").AsNumber().Should().Be(1);
        second.Evaluate("counter").AsNumber().Should().Be(2);
        // the third never reads it
        calls.Should().Be(2);

        // and each engine caches its own value
        first.Evaluate("counter").AsNumber().Should().Be(1);
        second.Evaluate("counter").AsNumber().Should().Be(2);
        calls.Should().Be(2);

        third.Evaluate("counter").AsNumber().Should().Be(3);
        calls.Should().Be(3);
    }

    [Fact]
    public void OverwritingBeforeFirstReadStillWinsButDoesMaterializeOnce()
    {
        var calls = 0;
        var engine = new Engine(options => options.AddLazyGlobal("value", _ =>
        {
            calls++;
            return "lazy";
        }));

        engine.Evaluate("value = 'replaced';");
        engine.Evaluate("value").AsString().Should().Be("replaced");

        // [[Set]] on the global object routes through [[DefineOwnProperty]], whose validation step reads
        // the current value before replacing it - so the factory runs once and its result is discarded.
        // The observable end state is still exactly the script's value.
        calls.Should().Be(1);

        // and it is not re-run afterwards
        engine.Evaluate("value = 'again'; value").AsString().Should().Be("again");
        calls.Should().Be(1);
    }

    [Fact]
    public void DeletingBeforeFirstReadSkipsTheFactory()
    {
        var calls = 0;
        var engine = new Engine(options => options.AddLazyGlobal("value", _ =>
        {
            calls++;
            return "lazy";
        }));

        engine.Evaluate("delete globalThis.value").AsBoolean().Should().BeTrue();
        engine.Evaluate("'value' in globalThis").AsBoolean().Should().BeFalse();
        engine.Evaluate("typeof value").AsString().Should().Be("undefined");
        calls.Should().Be(0);
    }

    [Fact]
    public void RedefiningBeforeFirstReadWins()
    {
        var calls = 0;
        var engine = new Engine(options => options.AddLazyGlobal("value", _ =>
        {
            calls++;
            return "lazy";
        }));

        engine.Evaluate("Object.defineProperty(globalThis, 'value', { value: 'defined', configurable: true });");
        engine.Evaluate("value").AsString().Should().Be("defined");

        // same reason as OverwritingBeforeFirstReadStillWinsButDoesMaterializeOnce: the redefinition has to
        // compare against the current value
        calls.Should().Be(1);
    }

    [Fact]
    public void NonEnumerableNonConfigurableFlagsAreHonoured()
    {
        var engine = new Engine(options => options.AddLazyGlobal(
            "locked",
            _ => "value",
            PropertyFlag.None));

        engine.Evaluate("Object.keys(globalThis).indexOf('locked') >= 0").AsBoolean().Should().BeFalse();
        engine.Evaluate("'locked' in globalThis").AsBoolean().Should().BeTrue();
        engine.Evaluate("locked").AsString().Should().Be("value");

        // non-writable, non-configurable
        engine.Evaluate("delete globalThis.locked").AsBoolean().Should().BeFalse();
        engine.Evaluate("locked = 'other'; locked").AsString().Should().Be("value");
    }

    [Fact]
    public void HostObjectFactoriesSeeAFullyBuiltEngine()
    {
        var engine = new Engine(options => options.AddLazyGlobal("api", e =>
        {
            // an intrinsic-touching factory would be unsafe at construction time; lazily it is fine
            var value = e.Evaluate("({ ok: true })");
            return value;
        }));

        engine.Evaluate("api.ok").AsBoolean().Should().BeTrue();
    }

    [Fact]
    public void ManyLazyGlobalsCostNothingUntilUsed()
    {
        var built = 0;
        var options = new Options();
        for (var i = 0; i < 40; i++)
        {
            var name = "api" + i.ToString(System.Globalization.CultureInfo.InvariantCulture);
            options.AddLazyGlobal(name, e =>
            {
                built++;
                return new ClrFunction(e, name, (_, _) => name);
            });
        }

        var engine = new Engine(options);
        built.Should().Be(0);

        engine.Evaluate("api7()").AsString().Should().Be("api7");
        built.Should().Be(1);
    }

    [Fact]
    public void NullArgumentsAreRejected()
    {
        var options = new Options();
        Invoking(() => options.AddLazyGlobal(null!, _ => JsValue.Undefined))
            .Should().Throw<System.ArgumentNullException>();
        Invoking(() => options.AddLazyGlobal("name", null!))
            .Should().Throw<System.ArgumentNullException>();
    }

    // ---------------------------------------------------------------------------------------------
    // engine.Advanced.AddLazyGlobal — the same mechanism, installable after construction
    // ---------------------------------------------------------------------------------------------
    //
    // Options.AddLazyGlobal is options-time only, so a host whose globals are computed from per-request
    // data — which it only knows AFTER `new Engine(...)` — cannot use it. The post-construction install
    // carries one design risk the options-time one does not have: at construction time no interpreter
    // cache exists yet, while a post-construction install lands on an engine whose handler trees may
    // already hold a resolved binding for that very name. Everything below the first two tests is the
    // same contract as above; those two are about the caches.

    [Fact]
    public void PostConstructionInstallIsSeenByASiteThatAlreadyFailedToResolveTheName()
    {
        var engine = new Engine();

        // Same handler tree both times: a Prepared<Script> is what a reusing host actually runs, and it
        // is the only way to guarantee the second evaluation reaches the node the first one warmed.
        var script = Engine.PrepareScript("x");

        Invoking(() => engine.Evaluate(script))
            .Should().Throw<JavaScriptException>()
            .WithMessage("*x is not defined*");

        engine.Advanced.AddLazyGlobal("x", _ => 42);

        engine.Evaluate(script).AsNumber().Should().Be(42);
    }

    [Fact]
    public void PostConstructionInstallReplacesAWarmedEagerGlobal()
    {
        var engine = new Engine();
        engine.SetValue("y", "eager");

        // A plain writable data property of the global object is exactly what the identifier cache
        // remembers by descriptor reference, and `globalThis.y` warms the member-read cache as well.
        var identifierRead = Engine.PrepareScript("y");
        var memberRead = Engine.PrepareScript("globalThis.y");
        for (var i = 0; i < 5; i++)
        {
            engine.Evaluate(identifierRead).AsString().Should().Be("eager");
            engine.Evaluate(memberRead).AsString().Should().Be("eager");
        }

        engine.Advanced.AddLazyGlobal("y", _ => "lazy");

        engine.Evaluate(identifierRead).AsString().Should().Be("lazy");
        engine.Evaluate(memberRead).AsString().Should().Be("lazy");
    }

    [Fact]
    public void PostConstructionInstallReplacesAWarmedBuiltinGlobal()
    {
        var engine = new Engine();

        // A built-in global lives in the global object's shared layout rather than its property
        // dictionary, so installing over one takes a different storage path than the two above.
        var identifierRead = Engine.PrepareScript("parseInt");
        var memberRead = Engine.PrepareScript("globalThis.parseInt");
        for (var i = 0; i < 5; i++)
        {
            engine.Evaluate(identifierRead).IsCallable().Should().BeTrue();
            engine.Evaluate(memberRead).IsCallable().Should().BeTrue();
        }

        engine.Advanced.AddLazyGlobal("parseInt", _ => "shadowed");

        engine.Evaluate(identifierRead).AsString().Should().Be("shadowed");
        engine.Evaluate(memberRead).AsString().Should().Be("shadowed");
    }

    [Fact]
    public void PostConstructionFactoryRunsLazilyAndAtMostOnce()
    {
        var calls = 0;
        var engine = new Engine();
        engine.Advanced.AddLazyGlobal("value", _ =>
        {
            calls++;
            return "built";
        });

        engine.Evaluate("var unrelated = 1 + 1;");
        calls.Should().Be(0);

        engine.Evaluate("value").AsString().Should().Be("built");
        calls.Should().Be(1);

        engine.Evaluate("value; value; globalThis.value; value === globalThis.value;")
            .AsBoolean().Should().BeTrue();
        calls.Should().Be(1);
    }

    [Fact]
    public void PostConstructionExistenceChecksDoNotMaterialize()
    {
        var calls = 0;
        var engine = new Engine();
        engine.Advanced.AddLazyGlobal("value", _ =>
        {
            calls++;
            return 42;
        });

        engine.Evaluate("'value' in globalThis").AsBoolean().Should().BeTrue();
        engine.Evaluate("globalThis.hasOwnProperty('value')").AsBoolean().Should().BeTrue();
        engine.Evaluate("Object.keys(globalThis).indexOf('value') >= 0").AsBoolean().Should().BeTrue();
        engine.Evaluate("Object.getOwnPropertyNames(globalThis).indexOf('value') >= 0").AsBoolean().Should().BeTrue();
        calls.Should().Be(0);

        engine.Evaluate("value").AsNumber().Should().Be(42);
        calls.Should().Be(1);
    }

    [Fact]
    public void PostConstructionFlagsAreHonoured()
    {
        var engine = new Engine();
        engine.Advanced.AddLazyGlobal("hidden", _ => "value", PropertyFlag.NonEnumerable);

        engine.Evaluate("Object.keys(globalThis).indexOf('hidden') >= 0").AsBoolean().Should().BeFalse();
        engine.Evaluate("'hidden' in globalThis").AsBoolean().Should().BeTrue();
        engine.Evaluate("hidden").AsString().Should().Be("value");
    }

    [Fact]
    public void PostConstructionFactoryMayCaptureEngineAffineState()
    {
        var engine = new Engine();
        var perRequest = engine.Evaluate("({ requestId: 7 })");

        // The whole point of the per-engine overload: this closure holds a JsValue of THIS engine, which
        // an Options-registered factory must never do because an Options instance is shared.
        engine.Advanced.AddLazyGlobal("context", _ => perRequest);

        engine.Evaluate("context.requestId").AsNumber().Should().Be(7);
        engine.Evaluate("context === globalThis.context").AsBoolean().Should().BeTrue();
    }

    [Fact]
    public void PostConstructionNullFactoryResultBecomesUndefinedRatherThanReRunning()
    {
        var calls = 0;
        var engine = new Engine();
        engine.Advanced.AddLazyGlobal("nothing", _ =>
        {
            calls++;
            return null!;
        });

        engine.Evaluate("typeof nothing").AsString().Should().Be("undefined");
        engine.Evaluate("typeof nothing").AsString().Should().Be("undefined");
        calls.Should().Be(1);
    }

    [Fact]
    public void PostConstructionInstallAfterACaptureIsRemovedByTheRestore()
    {
        var calls = 0;
        var engine = new Engine();
        var snapshot = engine.Advanced.CaptureGlobalSnapshot();

        engine.Advanced.AddLazyGlobal("perRequest", _ =>
        {
            calls++;
            return "value";
        });
        engine.Evaluate("'perRequest' in globalThis").AsBoolean().Should().BeTrue();

        engine.Advanced.RestoreGlobalSnapshot(snapshot);

        engine.Evaluate("'perRequest' in globalThis").AsBoolean().Should().BeFalse();
        engine.Evaluate("typeof perRequest").AsString().Should().Be("undefined");
        calls.Should().Be(0);
    }

    [Fact]
    public void RestoreReArmsAFactoryThatWasUnmaterializedAtCapture()
    {
        var calls = 0;
        var engine = new Engine();
        engine.Advanced.AddLazyGlobal("value", _ =>
        {
            calls++;
            return "built";
        });

        // captured while still unmaterialized, so "this exact surface" IS the unresolved state
        var snapshot = engine.Advanced.CaptureGlobalSnapshot();

        engine.Evaluate("value").AsString().Should().Be("built");
        calls.Should().Be(1);

        engine.Advanced.RestoreGlobalSnapshot(snapshot);

        engine.Evaluate("value").AsString().Should().Be("built");
        calls.Should().Be(2, "a restore returns the binding to its state at capture, where the factory had not run");
    }

    [Fact]
    public void PostConstructionNullArgumentsAreRejected()
    {
        var engine = new Engine();
        Invoking(() => engine.Advanced.AddLazyGlobal(null!, _ => JsValue.Undefined))
            .Should().Throw<System.ArgumentNullException>();
        Invoking(() => engine.Advanced.AddLazyGlobal("name", null!))
            .Should().Throw<System.ArgumentNullException>();
    }

    [Fact]
    public void StatefulFactoryReceivesTheStateAndTheEngine()
    {
        var engine = new Engine();

        engine.Advanced.AddLazyGlobal(
            "greeting",
            "world",
            static (e, s) => JsValue.FromObject(e, "hello " + s));

        engine.Evaluate("greeting").AsString().Should().Be("hello world");
    }

    [Fact]
    public void StatefulFactoryIsStillLazyAndRunsAtMostOnce()
    {
        var box = new Box();
        var engine = new Engine();

        engine.Advanced.AddLazyGlobal(
            "value",
            box,
            static (_, b) =>
            {
                b.Calls++;
                return "built";
            });

        // The property exists from the moment it is declared; only its value is deferred.
        engine.Evaluate("'value' in globalThis").AsBoolean().Should().BeTrue();
        box.Calls.Should().Be(0, "an existence check does not need the value");

        engine.Evaluate("value").AsString().Should().Be("built");
        engine.Evaluate("value").AsString().Should().Be("built");
        box.Calls.Should().Be(1);
    }

    [Fact]
    public void StatefulFactoryNullResultBecomesUndefinedRatherThanReRunning()
    {
        var box = new Box();
        var engine = new Engine();

        engine.Advanced.AddLazyGlobal(
            "nothing",
            box,
            static (_, b) =>
            {
                b.Calls++;
                return null!;
            });

        engine.Evaluate("typeof nothing").AsString().Should().Be("undefined");
        engine.Evaluate("typeof nothing").AsString().Should().Be("undefined");
        box.Calls.Should().Be(1);
    }

    [Fact]
    public void StatefulRegistrationReplacesAnExistingGlobalLikeTheNonGenericOverload()
    {
        var engine = new Engine();
        engine.SetValue("name", "eager");

        engine.Advanced.AddLazyGlobal("name", "lazy", static (e, s) => JsValue.FromObject(e, s));

        engine.Evaluate("name").AsString().Should().Be("lazy");
    }

    [Fact]
    public void StatefulNullArgumentsAreRejected()
    {
        var engine = new Engine();
        Invoking(() => engine.Advanced.AddLazyGlobal(null!, 1, static (_, _) => JsValue.Undefined))
            .Should().Throw<System.ArgumentNullException>();
        Invoking(() => engine.Advanced.AddLazyGlobal<int>("name", 1, null!))
            .Should().Throw<System.ArgumentNullException>();
    }

    private sealed class Box
    {
        public int Calls;
    }
}
