#nullable enable

using System;
using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime.Descriptors;

namespace Jint.Tests.PublicInterface;

/// <summary>
/// <c>PropertyDescriptor.CreateLazy</c> is the sanctioned way for a host to build a property whose value is
/// produced on the first read. These live in the public-interface suite because reachability is the point:
/// the project has no internals access, so a lazy property built here is one a third-party embedder can
/// actually build. The engine-internal half of the contract — that the descriptor drops
/// <c>PropertyFlag.CustomJsValue</c> once it holds a value, and so rejoins the write and global-binding
/// inline caches — is pinned in <c>Jint.Tests/Runtime/LazyDescriptorInternalsTests.cs</c>.
/// </summary>
public class LazyPropertyDescriptorTests
{
    /// <summary>
    /// The shape the friction report describes: a host object that stores descriptors and serves them from
    /// the ordinary own-property machinery. Deliberately overrides nothing, so it is exactly what an embedder
    /// gets from <c>protected ObjectInstance(Engine)</c> plus a descriptor store.
    /// </summary>
    private sealed class DescriptorBackedHost : ObjectInstance
    {
        public DescriptorBackedHost(Engine engine) : base(engine)
        {
        }

        public DescriptorBackedHost Add(string name, PropertyDescriptor descriptor)
        {
            SetOwnProperty(new JsString(name), descriptor);
            return this;
        }
    }

    private static (Engine Engine, Func<int> Calls) HostWith(
        string name,
        JsValue value,
        PropertyFlag flags = PropertyFlag.ConfigurableEnumerableWritable)
    {
        var calls = 0;
        var engine = new Engine();
        var host = new DescriptorBackedHost(engine);
        host.Add(name, PropertyDescriptor.CreateLazy(
            () =>
            {
                calls++;
                return value;
            },
            flags));
        engine.SetValue("host", host);
        return (engine, () => calls);
    }

    [Test]
    public void FactoryRunsOnTheFirstReadOnly()
    {
        var (engine, calls) = HostWith("report", "built");

        engine.Evaluate("var unrelated = 1 + 1;");
        calls().Should().Be(0);

        engine.Evaluate("host.report").AsString().Should().Be("built");
        calls().Should().Be(1);

        engine.Evaluate("host.report; host.report; host['report'];");
        engine.Evaluate("host.report").AsString().Should().Be("built");
        calls().Should().Be(1);
    }

    [Test]
    public void ExistenceAndEnumerationDoNotMaterialize()
    {
        var (engine, calls) = HostWith("report", "built");

        engine.Evaluate("'report' in host").AsBoolean().Should().BeTrue();
        engine.Evaluate("host.hasOwnProperty('report')").AsBoolean().Should().BeTrue();
        engine.Evaluate("host.propertyIsEnumerable('report')").AsBoolean().Should().BeTrue();
        engine.Evaluate("Object.keys(host).join(',')").AsString().Should().Be("report");
        engine.Evaluate("Object.getOwnPropertyNames(host).join(',')").AsString().Should().Be("report");

        var seen = engine.Evaluate("var names = []; for (var k in host) { names.push(k); } names.join(',');");
        seen.AsString().Should().Be("report");

        calls().Should().Be(0, "attributes are decided up front; only the value is deferred");

        engine.Evaluate("host.report").AsString().Should().Be("built");
        calls().Should().Be(1);
    }

    /// <summary>
    /// <c>JSON.stringify</c> reads values, so it does materialize an enumerable lazy property — but the
    /// <em>shape</em> decision that precedes it is made from the attributes alone, so a non-enumerable one is
    /// skipped without the factory running.
    /// </summary>
    [Test]
    public void JsonStringifySkipsANonEnumerableLazyPropertyWithoutMaterializingIt()
    {
        var calls = 0;
        var engine = new Engine();
        var host = new DescriptorBackedHost(engine);
        host.Add("hidden", PropertyDescriptor.CreateLazy(
            () =>
            {
                calls++;
                return "never-needed";
            },
            PropertyFlag.NonEnumerable));
        host.Add("shown", new PropertyDescriptor("plain", PropertyFlag.ConfigurableEnumerableWritable));
        engine.SetValue("host", host);

        engine.Evaluate("JSON.stringify(host)").AsString().Should().Be("""{"shown":"plain"}""");
        engine.Evaluate("JSON.stringify({ ...host })").AsString().Should().Be("""{"shown":"plain"}""");
        engine.Evaluate("JSON.stringify(Object.assign({}, host))").AsString().Should().Be("""{"shown":"plain"}""");
        calls.Should().Be(0);

        engine.Evaluate("host.hidden").AsString().Should().Be("never-needed");
        calls.Should().Be(1);
    }

    [Test]
    public void NonEnumerableFlagsAreHonouredAndTheValueIsStillReadable()
    {
        var (engine, calls) = HostWith("hidden", "value", PropertyFlag.NonEnumerable);

        engine.Evaluate("Object.keys(host).length").AsNumber().Should().Be(0);
        engine.Evaluate("host.propertyIsEnumerable('hidden')").AsBoolean().Should().BeFalse();
        engine.Evaluate("'hidden' in host").AsBoolean().Should().BeTrue();
        calls().Should().Be(0);

        engine.Evaluate("host.hidden").AsString().Should().Be("value");
        calls().Should().Be(1);
    }

    [Test]
    public void NonWritableLazyPropertyRefusesAssignmentAndStillReads()
    {
        var (engine, calls) = HostWith("readonly", "value", PropertyFlag.NonWritable);

        engine.Evaluate("host.readonly = 'other';");
        engine.Evaluate("host.readonly").AsString().Should().Be("value");
        calls().Should().Be(1);

        engine.Evaluate("Object.keys(host).join(',')").AsString().Should().Be("readonly");
    }

    /// <summary>
    /// A write is materialization: nothing can ever run the factory afterwards. Spelled with attribute flags
    /// that leave no attribute undecided (<see cref="PropertyFlag.NonEnumerable"/> here) so the redefinition
    /// underneath <c>[[Set]]</c> takes its attribute-only path; with a partially decided combination such as
    /// the default, <c>ValidateAndApplyPropertyDescriptor</c> compares against the current value first, which
    /// reads it — the same documented behaviour <c>AddLazyGlobal</c> has.
    /// </summary>
    [Test]
    public void WriteBeforeFirstReadNeverRunsTheFactory()
    {
        var (engine, calls) = HostWith("report", "built", PropertyFlag.NonEnumerable);

        engine.Evaluate("host.report = 'written';");
        calls().Should().Be(0);

        engine.Evaluate("host.report").AsString().Should().Be("written");
        engine.Evaluate("host.report").AsString().Should().Be("written");
        calls().Should().Be(0);
    }

    [Test]
    public void StateOverloadPassesItsStateThroughWithoutCapturing()
    {
        var engine = new Engine();
        var host = new DescriptorBackedHost(engine);

        host.Add("greeting", PropertyDescriptor.CreateLazy(
            "world",
            static state => "hello " + state));
        host.Add("count", PropertyDescriptor.CreateLazy(
            41,
            static state => JsNumber.Create(state + 1)));

        engine.SetValue("host", host);

        engine.Evaluate("host.greeting").AsString().Should().Be("hello world");
        engine.Evaluate("host.count").AsNumber().Should().Be(42);
    }

    [Test]
    public void NullStateIsAllowed()
    {
        var engine = new Engine();
        var host = new DescriptorBackedHost(engine);
        host.Add("value", PropertyDescriptor.CreateLazy<string?>(null, static state => state ?? "fallback"));
        engine.SetValue("host", host);

        engine.Evaluate("host.value").AsString().Should().Be("fallback");
    }

    [Test]
    public void NullFactoryResultBecomesUndefinedRatherThanReRunning()
    {
        var calls = 0;
        var engine = new Engine();
        var host = new DescriptorBackedHost(engine);
        host.Add("nothing", PropertyDescriptor.CreateLazy(() =>
        {
            calls++;
            return null!;
        }));
        engine.SetValue("host", host);

        engine.Evaluate("typeof host.nothing").AsString().Should().Be("undefined");
        engine.Evaluate("typeof host.nothing").AsString().Should().Be("undefined");
        calls.Should().Be(1);
    }

    /// <summary>
    /// The same guarantee for the state overload, which is the one the factory reaches unwrapped: nothing
    /// stands between a host's delegate and the descriptor, so it is the descriptor that has to read a
    /// <see langword="null"/> return as <c>undefined</c> rather than as "not resolved yet".
    /// </summary>
    [Test]
    public void NullFactoryResultFromTheStateOverloadBecomesUndefinedRatherThanReRunning()
    {
        var calls = 0;
        var engine = new Engine();
        var host = new DescriptorBackedHost(engine);
        host.Add("nothing", PropertyDescriptor.CreateLazy<string?>("ignored", _ =>
        {
            calls++;
            return null!;
        }));
        engine.SetValue("host", host);

        engine.Evaluate("typeof host.nothing").AsString().Should().Be("undefined");
        engine.Evaluate("typeof host.nothing").AsString().Should().Be("undefined");
        calls.Should().Be(1);

        // and it is a real own property holding undefined, not an absent one
        engine.Evaluate("'nothing' in host").AsBoolean().Should().BeTrue();
        calls.Should().Be(1);
    }

    [Test]
    public void AThrowingFactoryPropagatesAndLeavesThePropertyUnmaterialized()
    {
        var calls = 0;
        var engine = new Engine();
        var host = new DescriptorBackedHost(engine);
        host.Add("boom", PropertyDescriptor.CreateLazy(() =>
        {
            calls++;
            throw new InvalidOperationException("host failure");
        }));
        engine.SetValue("host", host);

        Invoking(() => engine.Evaluate("host.boom"))
            .Should().Throw<InvalidOperationException>().WithMessage("host failure");
        calls.Should().Be(1);

        // still unmaterialized, so the next read tries again
        Invoking(() => engine.Evaluate("host.boom"))
            .Should().Throw<InvalidOperationException>();
        calls.Should().Be(2);
    }

    [Test]
    public void NullFactoriesAreRejected()
    {
        Invoking(() => PropertyDescriptor.CreateLazy(null!))
            .Should().Throw<ArgumentNullException>();
        Invoking(() => PropertyDescriptor.CreateLazy<object?>(null, null!))
            .Should().Throw<ArgumentNullException>();
    }

    [TestCase(PropertyFlag.CustomJsValue)]
    [TestCase(PropertyFlag.NonData)]
    [TestCase(PropertyFlag.MutableBinding)]
    [TestCase(PropertyFlag.ConfigurableEnumerableWritable | PropertyFlag.CustomJsValue)]
    public void FlagsReservedForTheEngineAreRejected(PropertyFlag flags)
    {
        Invoking(() => PropertyDescriptor.CreateLazy(static () => JsValue.Undefined, flags))
            .Should().Throw<ArgumentException>();
        Invoking(() => PropertyDescriptor.CreateLazy<object?>(null, static _ => JsValue.Undefined, flags))
            .Should().Throw<ArgumentException>();
    }

    // ---------------------------------------------------------------------------------------------
    // A global the host installs itself, rather than through AddLazyGlobal
    // ---------------------------------------------------------------------------------------------

    [Test]
    public void AHostInstalledLazyGlobalIsReadCorrectlyByAWarmedSite()
    {
        var calls = 0;
        var engine = new Engine();
        engine.Global.DefineOwnPropertyUnchecked("hostValue", PropertyDescriptor.CreateLazy(() =>
        {
            calls++;
            return "built";
        }));

        engine.Evaluate("'hostValue' in globalThis").AsBoolean().Should().BeTrue();
        engine.Evaluate("Object.keys(globalThis).indexOf('hostValue') >= 0").AsBoolean().Should().BeTrue();
        calls.Should().Be(0);

        // Same handler tree every time, which is what warms the global-identifier cache: a re-running host
        // is exactly the shape that must keep seeing the memoized value after the descriptor rejoins it.
        var script = Engine.PrepareScript("hostValue");
        for (var i = 0; i < 10; i++)
        {
            engine.Evaluate(script).AsString().Should().Be("built");
        }

        engine.Evaluate("globalThis.hostValue").AsString().Should().Be("built");
        calls.Should().Be(1);
    }

    [Test]
    public void AHostInstalledLazyGlobalCanBeOverwrittenByScript()
    {
        var engine = new Engine();
        engine.Global.DefineOwnPropertyUnchecked("hostValue", PropertyDescriptor.CreateLazy(
            static () => "built",
            PropertyFlag.NonEnumerable));

        var read = Engine.PrepareScript("hostValue");

        engine.Evaluate("hostValue = 'replaced';");
        for (var i = 0; i < 5; i++)
        {
            engine.Evaluate(read).AsString().Should().Be("replaced");
        }

        engine.Evaluate("hostValue = 'again';");
        engine.Evaluate(read).AsString().Should().Be("again");
    }

    /// <summary>
    /// A lazy property on a host <em>prototype</em>: the receiver is an ordinary script object, so the read
    /// resolves through the prototype chain and the value is shared by every instance, materialized once.
    /// </summary>
    [Test]
    public void ALazyPropertyOnAPrototypeMaterializesOnceForEveryInstance()
    {
        var calls = 0;
        var engine = new Engine();
        var proto = new DescriptorBackedHost(engine);
        proto.Add("shared", PropertyDescriptor.CreateLazy(() =>
        {
            calls++;
            return "one-instance";
        }));
        engine.SetValue("proto", proto);

        engine.Evaluate("var a = Object.create(proto); var b = Object.create(proto);");
        calls.Should().Be(0);

        engine.Evaluate("a.shared").AsString().Should().Be("one-instance");
        engine.Evaluate("b.shared").AsString().Should().Be("one-instance");
        engine.Evaluate("a.shared === b.shared").AsBoolean().Should().BeTrue();
        calls.Should().Be(1);
    }
}
