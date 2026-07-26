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
}
