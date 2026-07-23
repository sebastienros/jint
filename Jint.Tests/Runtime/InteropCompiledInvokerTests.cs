#nullable enable
using Jint.Native;

namespace Jint.Tests.Runtime;

/// <summary>
/// Exercises the exact-type compiled-invoker fast lane for single-candidate interop method calls
/// (see <c>CompiledMethodInvoker</c>) together with the fallback path it declines to for anything
/// that is not an exact-type hit. Every fallback assertion is written against the behavior that
/// predates the fast lane so a regression is caught.
/// </summary>
public class InteropCompiledInvokerTests
{
    public sealed class Host
    {
        public bool VoidCalled { get; private set; }

        public int AddInt(int a, int b) => a + b;
        public long AddLong(long a, long b) => a + b;
        public double AddDouble(double a, double b) => a + b;
        public bool And(bool a, bool b) => a && b;
        public string Concat(string a, string b) => a + b;
        public JsValue Echo(JsValue value) => value;
        public int TimesTwo(int x) => x * 2;

        public void DoVoid() => VoidCalled = true;

        public int Throws() => throw new InvalidOperationException("boom from host");

        public static int StaticAdd(int a, int b) => a + b;

        // overloaded -> not a single candidate, never uses the fast lane
        public int Over(int x) => x + 1;
        public string Over(string x) => x + "!";

        // params -> ineligible for the fast lane
        public int SumParams(params int[] values)
        {
            var sum = 0;
            foreach (var v in values)
            {
                sum += v;
            }
            return sum;
        }

        // optional argument -> ineligible for the fast lane
        public int WithOptional(int a, int b = 10) => a + b;
    }

    private static Engine CreateEngine()
    {
        var engine = new Engine();
        engine.SetValue("host", new Host());
        return engine;
    }

    [Fact]
    public void FastLane_IntArgsAndReturn()
    {
        var engine = CreateEngine();
        engine.Evaluate("host.AddInt(2, 3)").AsNumber().Should().Be(5);
        // negative and zero still exact-type integers
        engine.Evaluate("host.AddInt(-4, 3)").AsNumber().Should().Be(-1);
    }

    [Fact]
    public void FastLane_LongArgsAndReturn()
    {
        var engine = CreateEngine();
        engine.Evaluate("host.AddLong(3, 4)").AsNumber().Should().Be(7);
    }

    [Fact]
    public void FastLane_DoubleArgsAndReturn()
    {
        var engine = CreateEngine();
        engine.Evaluate("host.AddDouble(1.5, 2.25)").AsNumber().Should().Be(3.75);
        // integral JS numbers bind to a double parameter as well
        engine.Evaluate("host.AddDouble(2, 3)").AsNumber().Should().Be(5.0);
    }

    [Fact]
    public void FastLane_BoolArgsAndReturn()
    {
        var engine = CreateEngine();
        engine.Evaluate("host.And(true, true)").AsBoolean().Should().BeTrue();
        engine.Evaluate("host.And(true, false)").AsBoolean().Should().BeFalse();
    }

    [Fact]
    public void FastLane_StringArgsAndReturn()
    {
        var engine = CreateEngine();
        engine.Evaluate("host.Concat('a', 'b')").AsString().Should().Be("ab");
    }

    [Fact]
    public void FastLane_JsValuePassthrough()
    {
        var engine = CreateEngine();
        engine.Evaluate("host.Echo(42)").AsNumber().Should().Be(42);
        engine.Evaluate("host.Echo('x')").AsString().Should().Be("x");
        engine.Evaluate("host.Echo(true)").AsBoolean().Should().BeTrue();
    }

    [Fact]
    public void FastLane_StaticMethod()
    {
        var engine = CreateEngine();
        engine.Evaluate("host.StaticAdd(4, 5)").AsNumber().Should().Be(9);
    }

    [Fact]
    public void FastLane_VoidReturnsNull()
    {
        var engine = CreateEngine();
        var host = new Host();
        engine.SetValue("host", host);

        var result = engine.Evaluate("host.DoVoid()");
        // CLR void is exposed to JS as null (not undefined) - preserved by the fast lane
        result.IsNull().Should().BeTrue();
        host.VoidCalled.Should().BeTrue();
    }

    [Fact]
    public void HostExceptionSurfacesAsSameClrException()
    {
        var engine = CreateEngine();
        var ex = Invoking(() => engine.Evaluate("host.Throws()")).Should().ThrowExactly<InvalidOperationException>().Which;
        ex.Message.Should().Be("boom from host");
    }

    [Fact]
    public void Fallback_FractionalNumberToIntParam()
    {
        var engine = CreateEngine();
        // fractional numbers are not exact-type integers: the fast lane declines and the fallback
        // converter rounds (banker's rounding, 2.5 -> 2), so 2 * 2 == 4. This locks in the behavior
        // that predates the fast lane.
        engine.Evaluate("host.TimesTwo(2.5)").AsNumber().Should().Be(4);
    }

    [Fact]
    public void Fallback_StringToIntParam()
    {
        var engine = CreateEngine();
        // a non-number argument to an int parameter is not an exact-type hit; the fallback coerces
        // the string "5" to 5, so 5 * 2 == 10.
        engine.Evaluate("host.TimesTwo('5')").AsNumber().Should().Be(10);
    }

    [Fact]
    public void Fallback_OverloadedMethod()
    {
        var engine = CreateEngine();
        engine.Evaluate("host.Over(5)").AsNumber().Should().Be(6);
        engine.Evaluate("host.Over('hi')").AsString().Should().Be("hi!");
    }

    [Fact]
    public void Fallback_ParamsMethod()
    {
        var engine = CreateEngine();
        engine.Evaluate("host.SumParams(1, 2, 3)").AsNumber().Should().Be(6);
    }

    [Fact]
    public void Fallback_OptionalArgMethod()
    {
        var engine = CreateEngine();
        engine.Evaluate("host.WithOptional(5)").AsNumber().Should().Be(15);
        engine.Evaluate("host.WithOptional(5, 4)").AsNumber().Should().Be(9);
    }

    [Fact]
    public void CustomObjectConverterStillSeesReturnValue()
    {
        // when a custom object converter is registered the fast lane is bypassed so the converter
        // observes primitive return values exactly as before
        var engine = new Engine(options => options.Interop.ObjectConverters.Add(new PlusOneIntConverter()));
        engine.SetValue("host", new Host());

        // AddInt returns 5, the converter turns any int into int+1 => 6
        engine.Evaluate("host.AddInt(2, 3)").AsNumber().Should().Be(6);
    }

    private sealed class PlusOneIntConverter : Jint.Runtime.Interop.IObjectConverter
    {
        public bool TryConvert(Engine engine, object value, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out JsValue? result)
        {
            if (value is int i)
            {
                result = JsNumber.Create(i + 1);
                return true;
            }

            result = JsValue.Undefined;
            return false;
        }
    }

    [Fact]
    public void CustomTypeConverterStillIntercepts()
    {
        // a user-installed ITypeConverter participates in some exact-type argument conversions on
        // the slow path (e.g. bool); the fast lane must decline so it keeps being consulted
        var engine = new Engine(options => options.SetTypeConverter(e => new BoolVetoingTypeConverter(e)));
        engine.SetValue("host", new Host());

        var ex = Invoking(() => engine.Evaluate("host.And(true, true)")).Should().ThrowExactly<Jint.Runtime.JavaScriptException>().Which;
        ex.Message.Should().Contain("No public methods");

        // conversions the veto does not touch keep working
        engine.Evaluate("host.AddInt(2, 3)").AsNumber().Should().Be(5);
    }

    private sealed class BoolVetoingTypeConverter : Jint.Runtime.Interop.DefaultTypeConverter
    {
        public BoolVetoingTypeConverter(Engine engine) : base(engine)
        {
        }

        public override bool TryConvert(object? value, Type type, IFormatProvider formatProvider, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out object? converted)
        {
            if (type == typeof(bool))
            {
                converted = null;
                return false;
            }

            return base.TryConvert(value, type, formatProvider, out converted);
        }
    }

    [Fact]
    public void WrongTypedThisSurfacesReflectionPathException()
    {
        // an extracted method invoked with a foreign receiver must decline the fast lane so the
        // host observes the same TargetException the reflection path always produced (not the
        // compiled cast's InvalidCastException) — ExceptionHandler predicates key on the type
        var engine = new Engine(options => options.Interop.ExceptionHandler = _ => false);
        engine.SetValue("host", new Host());
        engine.SetValue("other", new OtherHost());

        var ex = Invoking(() => engine.Evaluate("var f = host.TimesTwo; f.call(other, 21)")).Should().Throw<Exception>().Which;
        ex.Should().BeAssignableTo<System.Reflection.TargetException>();

        // a correctly-typed extracted call still uses the fast lane and works
        engine.Evaluate("var g = host.TimesTwo; g.call(host, 21)").AsNumber().Should().Be(42);
    }

    public sealed class OtherHost
    {
        public int Unrelated => 1;
    }

    // -----------------------------------------------------------------------------------------
    // Process-wide invoker cache.
    //
    // The compiled invoker and the BCL MethodInvoker/ConstructorInvoker are cached by MethodBase in
    // static dictionaries, shared by every Engine, because MethodDescriptor instances themselves are
    // per-Engine (Engine._reflectionAccessors). These tests prove that sharing carries no
    // Engine-specific state - above all that one Engine's interop policy (a custom ITypeConverter or
    // registered object converters, both of which must decline the compiled lane) never leaks into
    // another Engine through the shared cache, in either order.
    // -----------------------------------------------------------------------------------------

    public enum Season
    {
        Spring = 0,
        Summer = 1,
    }

    public sealed class Box
    {
        public Box(int value) => Value = value;

        public int Value { get; }
    }

    public struct StructHost
    {
        public int Value { get; set; }

        // instance method on a value-type receiver -> ineligible for the compiled lane
        public int Doubled() => Value * 2;
    }

    public sealed class IneligibleHost
    {
        // generic -> ineligible for the compiled lane
        public T Identity<T>(T value) => value;

        // enum parameter -> unsupported parameter type
        public string Name(Season season) => season.ToString();

        // custom class parameter -> unsupported parameter type
        public int Unwrap(Box box) => box.Value;
    }

    [Fact]
    public void SharedInvokerCache_SameMethodsFromTwoIndependentEngines()
    {
        var first = CreateEngine();
        var second = CreateEngine();

        // interleave so each engine both populates and consumes the shared cache entries
        first.Evaluate("host.AddInt(2, 3)").AsNumber().Should().Be(5);
        second.Evaluate("host.AddInt(20, 30)").AsNumber().Should().Be(50);
        second.Evaluate("host.Concat('a', 'b')").AsString().Should().Be("ab");
        first.Evaluate("host.Concat('c', 'd')").AsString().Should().Be("cd");
        first.Evaluate("host.AddLong(3, 4)").AsNumber().Should().Be(7);
        second.Evaluate("host.AddDouble(1.5, 2.25)").AsNumber().Should().Be(3.75);
        second.Evaluate("host.And(true, false)").AsBoolean().Should().BeFalse();
        first.Evaluate("host.And(true, true)").AsBoolean().Should().BeTrue();
        first.Evaluate("host.Echo('x')").AsString().Should().Be("x");
        second.Evaluate("host.StaticAdd(4, 5)").AsNumber().Should().Be(9);
    }

    [Fact]
    public void SharedInvokerCache_CustomTypeConverterEngineCreatedAfterDefaultEngine()
    {
        // default engine first: it populates the shared compiled-invoker entry for And
        var plain = CreateEngine();
        plain.Evaluate("host.And(true, true)").AsBoolean().Should().BeTrue();

        var custom = new Engine(options => options.SetTypeConverter(e => new BoolVetoingTypeConverter(e)));
        custom.SetValue("host", new Host());

        // the vetoing converter must still be consulted - the cached invoker is available but the
        // call site declines the lane for this engine
        Invoking(() => custom.Evaluate("host.And(true, true)"))
            .Should().ThrowExactly<Jint.Runtime.JavaScriptException>()
            .Which.Message.Should().Contain("No public methods");

        // and the default engine is unaffected by the other engine's policy
        plain.Evaluate("host.And(true, true)").AsBoolean().Should().BeTrue();
    }

    [Fact]
    public void SharedInvokerCache_DefaultEngineCreatedAfterCustomTypeConverterEngine()
    {
        // reverse order: the custom-converter engine runs first and never builds a compiled invoker
        var custom = new Engine(options => options.SetTypeConverter(e => new BoolVetoingTypeConverter(e)));
        custom.SetValue("host", new Host());

        Invoking(() => custom.Evaluate("host.And(true, true)"))
            .Should().ThrowExactly<Jint.Runtime.JavaScriptException>()
            .Which.Message.Should().Contain("No public methods");

        // a plain engine created afterwards must still get the fast lane and the correct result
        var plain = CreateEngine();
        plain.Evaluate("host.And(true, true)").AsBoolean().Should().BeTrue();
        plain.Evaluate("host.And(true, false)").AsBoolean().Should().BeFalse();

        // and the custom engine keeps vetoing after the plain engine populated the shared cache
        Invoking(() => custom.Evaluate("host.And(false, true)"))
            .Should().ThrowExactly<Jint.Runtime.JavaScriptException>()
            .Which.Message.Should().Contain("No public methods");
    }

    [Fact]
    public void SharedInvokerCache_ObjectConverterPolicyDoesNotLeakBetweenEngines()
    {
        var converting = new Engine(options => options.Interop.ObjectConverters.Add(new PlusOneIntConverter()));
        converting.SetValue("host", new Host());
        converting.Evaluate("host.AddInt(2, 3)").AsNumber().Should().Be(6);

        var plain = CreateEngine();
        plain.Evaluate("host.AddInt(2, 3)").AsNumber().Should().Be(5);

        // both policies survive repeated interleaving over the shared cache entry
        converting.Evaluate("host.AddInt(2, 3)").AsNumber().Should().Be(6);
        plain.Evaluate("host.AddInt(2, 3)").AsNumber().Should().Be(5);
    }

    [Fact]
    public void SharedInvokerCache_ThrowingHostMethodFromTwoEngines()
    {
        var first = CreateEngine();
        var second = CreateEngine();

        Invoking(() => first.Evaluate("host.Throws()"))
            .Should().ThrowExactly<InvalidOperationException>()
            .Which.Message.Should().Be("boom from host");

        Invoking(() => second.Evaluate("host.Throws()"))
            .Should().ThrowExactly<InvalidOperationException>()
            .Which.Message.Should().Be("boom from host");

        // the first engine still surfaces the same shape after the second engine used the same entry
        Invoking(() => first.Evaluate("host.Throws()"))
            .Should().ThrowExactly<InvalidOperationException>()
            .Which.Message.Should().Be("boom from host");
    }

    [Fact]
    public void SharedInvokerCache_IneligibleMethodsFromTwoEngines()
    {
        // exercises the null "known ineligible" sentinel in the shared cache: params, optional
        // arguments, generic methods, a value-type receiver, an enum parameter and a custom class
        // parameter all decline the compiled lane and must keep working from every engine.
        static Engine Create()
        {
            var engine = new Engine();
            engine.SetValue("host", new Host());
            engine.SetValue("ineligible", new IneligibleHost());
            engine.SetValue("point", new StructHost { Value = 21 });
            engine.SetValue("box", new Box(7));
            return engine;
        }

        var first = Create();
        var second = Create();

        foreach (var engine in new[] { first, second, first, second })
        {
            engine.Evaluate("host.SumParams(1, 2, 3)").AsNumber().Should().Be(6);
            engine.Evaluate("host.WithOptional(5)").AsNumber().Should().Be(15);
            engine.Evaluate("host.WithOptional(5, 4)").AsNumber().Should().Be(9);
            engine.Evaluate("ineligible.Identity('abc')").AsString().Should().Be("abc");
            engine.Evaluate("ineligible.Name(1)").AsString().Should().Be("Summer");
            engine.Evaluate("ineligible.Unwrap(box)").AsNumber().Should().Be(7);
            engine.Evaluate("point.Doubled()").AsNumber().Should().Be(42);
        }
    }

    [Fact]
    public void SharedInvokerCache_OverloadResolutionFromTwoEngines()
    {
        var first = CreateEngine();
        var second = CreateEngine();

        first.Evaluate("host.Over(5)").AsNumber().Should().Be(6);
        second.Evaluate("host.Over('hi')").AsString().Should().Be("hi!");
        first.Evaluate("host.Over('hi')").AsString().Should().Be("hi!");
        second.Evaluate("host.Over(5)").AsNumber().Should().Be(6);
    }

    [Fact]
    public void SharedInvokerCache_ConstructorInvokerFromTwoEngines()
    {
        static Engine Create()
        {
            var engine = new Engine();
            engine.SetValue("Box", typeof(Box));
            return engine;
        }

        var first = Create();
        var second = Create();

        first.Evaluate("new Box(3).Value").AsNumber().Should().Be(3);
        second.Evaluate("new Box(4).Value").AsNumber().Should().Be(4);
        first.Evaluate("new Box(5).Value").AsNumber().Should().Be(5);
    }
}
