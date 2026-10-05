#nullable enable

using Jint.Native;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;

namespace Jint.Tests.PublicInterface;

/// <summary>
/// A lazy global holding a host delegate has to materialize to exactly what
/// <see cref="Engine.SetValue(string, Delegate)"/> installs eagerly. <see cref="JsValue.FromObject"/> cannot
/// promise that — it consults the registered object converters, and unwraps a delegate that came from a
/// script function — so <see cref="JsValue.FromDelegate"/> is the converter-free conversion a factory uses.
/// </summary>
public class LazyDelegateGlobalTests
{
    // Typed as Delegate on purpose: a Func<...> expression binds to SetValue<T>, which converts like any
    // object, and only a Delegate-typed argument reaches SetValue(string, Delegate).
    private static readonly Delegate Add = new Func<int, int, int>((a, b) => a + b);

    /// <summary>
    /// Claims every delegate, which is what makes the two conversions observably different.
    /// </summary>
    private sealed class DelegateClaimingConverter : ObjectConverter
    {
        public int Calls;

        public override bool TryConvert(Engine engine, object value, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out JsValue? result)
        {
            if (value is Delegate)
            {
                Calls++;
                result = "converted";
                return true;
            }

            result = null;
            return false;
        }
    }

    public enum Registration
    {
        Options,
        Engine,
        EngineWithState,
    }

    /// <summary>
    /// Everything a script can observe about a global function without calling a method of the host's:
    /// its attributes, its type, its <c>name</c> and <c>length</c>, whether it enumerates, and a call.
    /// </summary>
    private static string Describe(Engine engine, string name)
    {
        return engine.Evaluate($$"""
            (() => {
                const d = Object.getOwnPropertyDescriptor(globalThis, '{{name}}');
                return [
                    typeof d.value, d.writable, d.enumerable, d.configurable,
                    d.value.name, d.value.length,
                    Object.keys(globalThis).includes('{{name}}'),
                    Object.getPrototypeOf(d.value) === Function.prototype,
                    d.value(2, 3),
                ].join('|');
            })()
            """).AsString();
    }

    private static Engine Build(Registration registration, DelegateClaimingConverter converter)
    {
        Engine engine;
        switch (registration)
        {
            case Registration.Options:
                engine = new Engine(options => options
                    .AddObjectConverter(converter)
                    .AddLazyGlobal("lazy", static e => JsValue.FromDelegate(e, Add), PropertyFlag.NonEnumerable));
                break;
            case Registration.Engine:
                engine = new Engine(options => options.AddObjectConverter(converter));
                engine.AddLazyGlobal("lazy", static e => JsValue.FromDelegate(e, Add), PropertyFlag.NonEnumerable);
                break;
            default:
                engine = new Engine(options => options.AddObjectConverter(converter));
                engine.AddLazyGlobal("lazy", Add, static (e, d) => JsValue.FromDelegate(e, d), PropertyFlag.NonEnumerable);
                break;
        }

        engine.SetValue("eager", Add);
        return engine;
    }

    [Test]
    public void AFromObjectFactoryIsSteeredByAConverterThatSetValueBypasses()
    {
        // The divergence FromDelegate exists to close, pinned so the reason stays visible.
        var converter = new DelegateClaimingConverter();
        var engine = new Engine(options => options
            .AddObjectConverter(converter)
            .AddLazyGlobal("lazy", static e => JsValue.FromObject(e, Add), PropertyFlag.NonEnumerable));
        engine.SetValue("eager", Add);

        engine.Evaluate("typeof eager").AsString().Should().Be("function");
        engine.Evaluate("lazy").AsString().Should().Be("converted");
        converter.Calls.Should().Be(1);
    }

    [TestCase(Registration.Options)]
    [TestCase(Registration.Engine)]
    [TestCase(Registration.EngineWithState)]
    public void ALazyDelegateGlobalMaterializesToWhatSetValueInstalls(Registration registration)
    {
        var converter = new DelegateClaimingConverter();
        var engine = Build(registration, converter);

        var eager = Describe(engine, "eager");
        eager.Should().Be("function|true|false|true|delegate|0|false|true|5");
        Describe(engine, "lazy").Should().Be(eager);
        converter.Calls.Should().Be(0);
    }

    [Test]
    public void ADelegateBackedByAScriptFunctionIsWrappedTheWaySetValueWrapsIt()
    {
        // No converter involved: FromObject hands a script function's own delegate back as that function,
        // while SetValue wraps it in a fresh host function. FromDelegate follows SetValue.
        var engine = new Engine();
        engine.Execute("function twice(a) { return a * 2; }");
        var scriptDelegate = (Delegate) engine.GetValue("twice").ToObject()!;

        engine.SetValue("eager", scriptDelegate);
        engine.AddLazyGlobal("lazy", scriptDelegate, static (e, d) => JsValue.FromDelegate(e, d), PropertyFlag.NonEnumerable);
        engine.AddLazyGlobal("viaFromObject", scriptDelegate, static (e, d) => JsValue.FromObject(e, d), PropertyFlag.NonEnumerable);

        engine.Evaluate("eager === twice").AsBoolean().Should().BeFalse();
        engine.Evaluate("lazy === twice").AsBoolean().Should().BeFalse();
        engine.Evaluate("viaFromObject === twice").AsBoolean().Should().BeTrue();

        // The wrapper binds the delegate's own (thisObject, arguments) signature, so that is how it is called.
        engine.Evaluate("[eager(undefined, [4]), lazy(undefined, [4]), eager.name, lazy.name].join('|')").AsString()
            .Should().Be("8|8|delegate|delegate");
        engine.Evaluate("Object.getOwnPropertyDescriptor(globalThis, 'lazy').enumerable").AsBoolean().Should().BeFalse();
    }

    [Test]
    public void TheWrapperIsBuiltOnFirstReadAndOnlyOnce()
    {
        var calls = 0;
        var engine = new Engine();
        engine.AddLazyGlobal("lazy", e =>
        {
            calls++;
            return JsValue.FromDelegate(e, Add);
        }, PropertyFlag.NonEnumerable);

        engine.Evaluate("'lazy' in globalThis").AsBoolean().Should().BeTrue();
        calls.Should().Be(0);

        engine.Evaluate("lazy(1, 2) + lazy(3, 4)").AsNumber().Should().Be(10);
        engine.Evaluate("lazy === globalThis.lazy").AsBoolean().Should().BeTrue();
        calls.Should().Be(1);
    }

    [Test]
    public void EachCallBuildsADistinctFunction()
    {
        // The same as calling SetValue twice: a wrapper per conversion, never a shared one.
        var engine = new Engine();
        var first = JsValue.FromDelegate(engine, Add);
        var second = JsValue.FromDelegate(engine, Add);

        ReferenceEquals(first, second).Should().BeFalse();
        engine.Invoke(first, 20, 22).AsNumber().Should().Be(42);
    }

    [Test]
    public void NullArgumentsAreRefused()
    {
        var engine = new Engine();

        var noEngine = () => JsValue.FromDelegate(null!, Add);
        noEngine.Should().Throw<ArgumentNullException>().WithParameterName("engine");

        var noDelegate = () => JsValue.FromDelegate(engine, null!);
        noDelegate.Should().Throw<ArgumentNullException>().WithParameterName("value");
    }
}
