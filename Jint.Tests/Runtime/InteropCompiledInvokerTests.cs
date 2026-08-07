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

        public int IdentityInt(int value) => value;
        public long IdentityLong(long value) => value;
        public double IdentityDouble(double value) => value;

        public static int StaticIdentityInt(int value) => value;

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

        // optional argument -> eligible; the declared default is baked into the thunk
        public int WithOptional(int a, int b = 10) => a + b;

        // a default the thunk cannot bake in (decimal is outside the lane's parameter set)
        public decimal WithDecimalOptional(decimal a, decimal b = 1.5m) => a + b;
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
    public void OptionalArgMethod()
    {
        // the values here predate the lane covering optional parameters: whichever lane runs, an elided
        // argument has to arrive as the declared default the reflection binder would have substituted
        var engine = CreateEngine();
        engine.Evaluate("host.WithOptional(5)").AsNumber().Should().Be(15);
        engine.Evaluate("host.WithOptional(5, 4)").AsNumber().Should().Be(9);

        // declining on a non-exact argument still reaches the same answer through the binding path
        engine.Evaluate("host.WithOptional(5.5)").AsNumber().Should().Be(16);
    }

    [Fact]
    public void Fallback_OptionalArgOfANonLaneType()
    {
        var engine = CreateEngine();
        engine.Evaluate("host.WithDecimalOptional(1)").AsNumber().Should().Be(2.5);
        engine.Evaluate("host.WithDecimalOptional(1, 2)").AsNumber().Should().Be(3);
    }

#if NET8_0_OR_GREATER

    [Fact]
    public void CompiledInvokerIsBuiltForAMethodWithAnOptionalParameter()
    {
        // the engagement probe: the behavioral assertions above pass either way, only this says the
        // reflection binder was actually replaced
        var descriptor = new Jint.Runtime.Interop.MethodDescriptor(typeof(Host).GetMethod(nameof(Host.WithOptional))!);

        descriptor.GetCompiledInvoker().Should().NotBeNull();

        var host = new Host();
        descriptor.GetCompiledInvoker()!(host, [JsNumber.Create(5)], out var elided).Should().BeTrue();
        elided.AsNumber().Should().Be(15);

        descriptor.GetCompiledInvoker()!(host, [JsNumber.Create(5), JsNumber.Create(4)], out var supplied).Should().BeTrue();
        supplied.AsNumber().Should().Be(9);
    }

    [Fact]
    public void CompiledInvokerDeclinesWhenTheDefaultIsOutsideTheLane()
    {
        var descriptor = new Jint.Runtime.Interop.MethodDescriptor(typeof(Host).GetMethod(nameof(Host.WithDecimalOptional))!);

        descriptor.GetCompiledInvoker().Should().BeNull();
    }

#endif

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

    /// <summary>
    /// Records every CLR value handed to it and never converts, so a test can assert exactly which
    /// return values reached the converter chain.
    /// </summary>
    private sealed class RecordingConverter : Jint.Runtime.Interop.IObjectConverter
    {
        public List<object> Seen { get; } = [];

        public bool TryConvert(Engine engine, object value, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out JsValue? result)
        {
            Seen.Add(value);
            result = null;
            return false;
        }
    }

    public sealed class ConverterInvisibleReturnsHost
    {
        public int Calls { get; private set; }

        public void DoVoid() => Calls++;
        public JsValue ReturnsJsValue() => JsNumber.Create(7);
        public JsString ReturnsJsValueSubtype() => new JsString("sub");
        public JsValue? ReturnsNullJsValue() => null;
        public string ReturnsString() => "raw";
    }

    [Fact]
    public void ObjectConverterNeverSeesVoidOrJsValueReturns()
    {
        // A converter can only ever observe a return value that FromObjectWithType actually hands
        // it, and that excludes void (no CLR value at all) and JsValue (short-circuited before the
        // converter chain). Those return types therefore keep the fast lane even with a converter
        // registered - and the observable results are identical either way.
        var recorder = new RecordingConverter();
        var engine = new Engine(options => options.Interop.ObjectConverters.Add(recorder));
        var host = new ConverterInvisibleReturnsHost();
        engine.SetValue("host", host);
        // exposing the host itself is a conversion too - only the return values matter here
        recorder.Seen.Clear();

        engine.Evaluate("host.DoVoid()").IsNull().Should().BeTrue();
        host.Calls.Should().Be(1);
        engine.Evaluate("host.ReturnsJsValue()").AsNumber().Should().Be(7);
        engine.Evaluate("host.ReturnsJsValueSubtype()").AsString().Should().Be("sub");
        engine.Evaluate("host.ReturnsNullJsValue()").IsNull().Should().BeTrue();

        // none of the above is a CLR value the converter chain is entitled to see
        recorder.Seen.Should().BeEmpty();

        // a string return is a plain CLR value, so it must still reach the converter
        engine.Evaluate("host.ReturnsString()").AsString().Should().Be("raw");
        recorder.Seen.Should().ContainSingle().Which.Should().Be("raw");
    }

    [Fact]
    public void ObjectConverterStillSeesEveryPrimitiveReturnType()
    {
        // the narrowing must not let int/long/double/bool/string slip past the converter chain
        var recorder = new RecordingConverter();
        var engine = new Engine(options => options.Interop.ObjectConverters.Add(recorder));
        engine.SetValue("host", new Host());
        recorder.Seen.Clear();

        engine.Evaluate("host.AddInt(2, 3)").AsNumber().Should().Be(5);
        engine.Evaluate("host.AddLong(3, 4)").AsNumber().Should().Be(7);
        engine.Evaluate("host.AddDouble(1.5, 2.25)").AsNumber().Should().Be(3.75);
        engine.Evaluate("host.And(true, true)").AsBoolean().Should().BeTrue();
        engine.Evaluate("host.Concat('a', 'b')").AsString().Should().Be("ab");

        recorder.Seen.Should().Equal(5, 7L, 3.75d, true, "ab");
    }

    [Fact]
    public void VoidAndJsValueLaneStaysConsistentAcrossConverterPolicies()
    {
        // the compiled invoker is cached process-wide; registering a converter in one engine must
        // not change what another engine observes for these return types, in either order
        var host = new ConverterInvisibleReturnsHost();

        var converting = new Engine(options => options.Interop.ObjectConverters.Add(new RecordingConverter()));
        converting.SetValue("host", host);
        var plain = new Engine();
        plain.SetValue("host", host);

        converting.Evaluate("host.ReturnsJsValue()").AsNumber().Should().Be(7);
        plain.Evaluate("host.ReturnsJsValue()").AsNumber().Should().Be(7);
        converting.Evaluate("host.DoVoid()").IsNull().Should().BeTrue();
        plain.Evaluate("host.DoVoid()").IsNull().Should().BeTrue();
        host.Calls.Should().Be(2);
    }

    public sealed class ByRefHost
    {
        public bool TryGet(int input, out int doubled)
        {
            doubled = input * 2;
            return true;
        }

        public void Bump(ref int value) => value++;
    }

    private static string DescribeEvaluation(Engine engine, string script)
    {
        try
        {
            return "ok:" + engine.Evaluate(script);
        }
        catch (Exception e)
        {
            return e.GetType().Name + ":" + e.Message;
        }
    }

    [Fact]
    public void ByRefParameterMethodsBehaveIdenticallyUnderEveryConverterPolicy()
    {
        // out/ref parameter types are ByRef types (Int32&) which never equal any supported
        // parameter type, so such methods are ineligible for the compiled lane. Whatever the
        // reflection path does with them, registering an object converter must not change it.
        var host = new ByRefHost();

        var plain = new Engine();
        plain.SetValue("host", host);
        var converting = new Engine(o => o.Interop.ObjectConverters.Add(new RecordingConverter()));
        converting.SetValue("host", host);

        foreach (var script in new[] { "host.TryGet(21)", "host.TryGet(21, 0)", "host.Bump(1)" })
        {
            DescribeEvaluation(converting, script).Should().Be(DescribeEvaluation(plain, script), script);
        }
    }

#if NET8_0_OR_GREATER
    [Fact]
    public void ReturnTypeVisibilityPredicateMatchesFromObjectWithTypeShortCircuits()
    {
        static bool Invisible(Type? t) => Jint.Runtime.Interop.CompiledMethodInvoker.ReturnValueIsInvisibleToObjectConverters(t);

        // FromObjectWithType returns before the converter chain for null and for JsValue values
        Invisible(typeof(void)).Should().BeTrue();
        Invisible(typeof(JsValue)).Should().BeTrue();
        Invisible(typeof(JsString)).Should().BeTrue();
        Invisible(typeof(JsNumber)).Should().BeTrue();

        // plain CLR values the converter chain is entitled to intercept
        Invisible(typeof(int)).Should().BeFalse();
        Invisible(typeof(long)).Should().BeFalse();
        Invisible(typeof(double)).Should().BeFalse();
        Invisible(typeof(bool)).Should().BeFalse();
        Invisible(typeof(string)).Should().BeFalse();

        // types the lane does not support at all are conservatively converter-visible
        Invisible(typeof(System.Threading.Tasks.Task)).Should().BeFalse();
        Invisible(typeof(DateTime)).Should().BeFalse();
        Invisible(typeof(object)).Should().BeFalse();

        // a constructor descriptor has no return type
        Invisible(null).Should().BeFalse();
    }
#endif

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
    public void WrongTypedThisSurfacesCatchableTypeError()
    {
        // an extracted method invoked with a foreign CLR receiver must decline the fast lane so the
        // reflection path can classify the receiver mismatch — surfaced as a JavaScript TypeError,
        // catchable by script, never a raw TargetException (or the compiled cast's
        // InvalidCastException); Interop.ExceptionHandler is not consulted for this binding failure
        var engine = new Engine(options => options.Interop.ExceptionHandler = _ => false);
        engine.SetValue("host", new Host());
        engine.SetValue("other", new OtherHost());

        var ex = Invoking(() => engine.Evaluate("var f = host.TimesTwo; f.call(other, 21)")).Should().ThrowExactly<Jint.Runtime.JavaScriptException>().Which;
        ex.Message.Should().Be("Method 'TimesTwo' called on incompatible receiver");

        engine.Evaluate("var f2 = host.TimesTwo; try { f2.call(other, 21); 'no error' } catch (e) { e instanceof TypeError }").AsBoolean().Should().BeTrue();

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
    // static dictionaries, shared by every Engine, because MethodDescriptor instances themselves live
    // on the accessors cached by a TypeResolver and so are only shared as far as the resolver is.
    // These tests prove that sharing carries no
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
        // exercises the null "known ineligible" sentinel in the shared cache: params, generic methods, a
        // value-type receiver, an enum parameter and a custom class parameter all decline the compiled lane
        // and must keep working from every engine. The optional-argument rows are here for the converse -
        // they do take the lane now, and must agree across engines sharing one cached invoker.
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

    [Theory]
    // exact integers, including the int boundaries and negative zero, are exact-type hits
    [InlineData("host.IdentityInt(0)", 0)]
    [InlineData("host.IdentityInt(-0)", 0)]
    [InlineData("host.IdentityInt(1)", 1)]
    [InlineData("host.IdentityInt(-1)", -1)]
    [InlineData("host.IdentityInt(2147483647)", int.MaxValue)]
    [InlineData("host.IdentityInt(-2147483648)", int.MinValue)]
    public void IntBoundaryValuesBindExactly(string script, int expected)
    {
        var engine = CreateEngine();
        engine.Evaluate(script).AsNumber().Should().Be(expected);
    }

    [Theory]
    // non-integral values are declined by the fast lane and rounded by the fallback converter
    // (banker's rounding) exactly as before the fast lane existed
    [InlineData("host.IdentityInt(1.5)", 2)]
    [InlineData("host.IdentityInt(2.5)", 2)]
    [InlineData("host.IdentityInt(-1.5)", -2)]
    [InlineData("host.IdentityInt(-0.5)", 0)]
    [InlineData("host.IdentityInt(0.5)", 0)]
    public void NonIntegralIntArgumentsFallBackToRoundingConversion(string script, int expected)
    {
        var engine = CreateEngine();
        engine.Evaluate(script).AsNumber().Should().Be(expected);
    }

    [Theory]
    // out-of-range and non-finite numbers are declined by the fast lane AND rejected by the
    // fallback conversion, which surfaces the resolution error
    [InlineData("host.IdentityInt(2147483648)")]
    [InlineData("host.IdentityInt(-2147483649)")]
    [InlineData("host.IdentityInt(NaN)")]
    [InlineData("host.IdentityInt(Infinity)")]
    [InlineData("host.IdentityInt(-Infinity)")]
    [InlineData("host.IdentityLong(NaN)")]
    [InlineData("host.IdentityLong(Infinity)")]
    [InlineData("host.IdentityLong(-Infinity)")]
    // (double) long.MaxValue rounds up to 2^63, which overflows long - the upper bound is exclusive
    [InlineData("host.IdentityLong(9223372036854775808)")]
    public void OutOfRangeOrNonFiniteIntegerArgumentsAreRejected(string script)
    {
        var engine = CreateEngine();
        var ex = Invoking(() => engine.Evaluate(script)).Should().ThrowExactly<Jint.Runtime.JavaScriptException>().Which;
        ex.Message.Should().Contain("No public methods");
    }

    [Theory]
    [InlineData("host.IdentityLong(0)", 0d)]
    [InlineData("host.IdentityLong(-0)", 0d)]
    [InlineData("host.IdentityLong(1)", 1d)]
    // a very large integral double still binds exactly
    [InlineData("host.IdentityLong(1e18)", 1e18)]
    [InlineData("host.IdentityLong(-1e18)", -1e18)]
    // long.MinValue is exactly representable as a double, so it is still in range
    [InlineData("host.IdentityLong(-9223372036854775808)", -9223372036854775808d)]
    public void LongBoundaryValuesBindExactly(string script, double expected)
    {
        var engine = CreateEngine();
        engine.Evaluate(script).AsNumber().Should().Be(expected);
    }

    [Fact]
    public void DoubleParameterAcceptsEveryNumberIncludingNonFinite()
    {
        var engine = CreateEngine();
        // a double parameter has no integrality/range test at all, every JsNumber flows through
        engine.Evaluate("host.IdentityDouble(1.5)").AsNumber().Should().Be(1.5);
        engine.Evaluate("host.IdentityDouble(-0.5)").AsNumber().Should().Be(-0.5);
        engine.Evaluate("host.IdentityDouble(1e300)").AsNumber().Should().Be(1e300);
        double.IsNaN(engine.Evaluate("host.IdentityDouble(NaN)").AsNumber()).Should().BeTrue();
        engine.Evaluate("host.IdentityDouble(Infinity)").AsNumber().Should().Be(double.PositiveInfinity);
        engine.Evaluate("host.IdentityDouble(-Infinity)").AsNumber().Should().Be(double.NegativeInfinity);
        // negative zero survives the round trip
        engine.Evaluate("1 / host.IdentityDouble(-0)").AsNumber().Should().Be(double.NegativeInfinity);
    }

    /// <summary>
    /// Values that straddle every interesting integrality/range boundary of the interop numeric
    /// binding: integral and non-integral, the int/long limits, the 2^52 and 2^53 precision steps,
    /// negative zero, subnormals and the non-finite values.
    /// </summary>
    private static double[] IntegralityProbeValues() =>
    [
        0d, -0d, 1d, -1d, 0.5, -0.5, 1.5, -1.5, 2.5, -2.5,
        int.MaxValue, int.MinValue, 2147483648d, -2147483649d, 2147483647.5, -2147483648.5,
        4503599627370495.5, // largest representable non-integral magnitude (just below 2^52)
        4503599627370496d,  // 2^52 - every double from here up is integral
        9007199254740992d,  // 2^53
        9223372036854775808d, -9223372036854775808d, // +-2^63
        1e18, -1e18, 1e300, -1e300,
        double.Epsilon, -double.Epsilon,
        double.MaxValue, double.MinValue,
        double.NaN, double.PositiveInfinity, double.NegativeInfinity,
    ];

    [Fact]
    public void IsIntegralNumberMatchesRemainderFormulation()
    {
        // the Math.Floor(v) == v formulation must agree with the v % 1 == 0 one it replaced on every
        // value, so no interop or spec boundary can shift
        foreach (var value in IntegralityProbeValues())
        {
            var viaRemainder = !double.IsNaN(value) && !double.IsInfinity(value) && value % 1 == 0;
            Jint.Runtime.TypeConverter.IsIntegralNumber(value).Should().Be(viaRemainder, "for {0}", value);
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

    [Fact]
    public void IsIntegralNumberEquivalenceIsObservableThroughBigIntConversion()
    {
        // BigInt(number) is a JS-visible consumer of IsIntegralNumber: it throws a RangeError for
        // every non-integral (or non-finite) number and succeeds for every integral one
        var engine = new Engine();
        var isIntegral = engine.Evaluate("(function (v) { try { BigInt(v); return true; } catch (e) { return false; } })");

        foreach (var value in IntegralityProbeValues())
        {
            var viaRemainder = !double.IsNaN(value) && !double.IsInfinity(value) && value % 1 == 0;
            engine.Invoke(isIntegral, value).AsBoolean().Should().Be(viaRemainder, "for {0}", value);
        }
    }

    [Fact]
    public void CustomTypeConverterInstalledAfterConstructionDisablesFastLane()
    {
        // the fast lane gates on a cached "is the stock converter" flag; swapping the converter after
        // construction has to keep that flag in sync, otherwise the custom converter is skipped
        var engine = new Engine();
        engine.SetValue("host", new Host());

        engine._typeConverterIsDefault.Should().BeTrue();
        engine.Evaluate("host.And(true, true)").AsBoolean().Should().BeTrue();

        engine.TypeConverter = new BoolVetoingTypeConverter(engine);
        engine._typeConverterIsDefault.Should().BeFalse();

        var ex = Invoking(() => engine.Evaluate("host.And(true, true)")).Should().ThrowExactly<Jint.Runtime.JavaScriptException>().Which;
        ex.Message.Should().Contain("No public methods");
        // conversions the veto does not touch keep working through the slow path
        engine.Evaluate("host.AddInt(2, 3)").AsNumber().Should().Be(5);

        // swapping the stock converter back re-enables the fast lane
        engine.TypeConverter = new Jint.Runtime.Interop.DefaultTypeConverter(engine);
        engine._typeConverterIsDefault.Should().BeTrue();
        engine.Evaluate("host.And(true, true)").AsBoolean().Should().BeTrue();
    }

    [Fact]
    public void DefaultEngineUsesTheStockTypeConverterFastLane()
    {
        var engine = CreateEngine();
        engine._typeConverterIsDefault.Should().BeTrue();
        engine.Evaluate("host.AddInt(2, 3)").AsNumber().Should().Be(5);
        engine.Evaluate("host.And(true, false)").AsBoolean().Should().BeFalse();
    }

    [Fact]
    public void StaticAndInstanceMethodsDispatchThroughCachedReflectionFacts()
    {
        var engine = CreateEngine();
        engine.SetValue("other", new OtherHost());

        // instance method: the receiver type check must still pass
        engine.Evaluate("host.IdentityInt(7)").AsNumber().Should().Be(7);
        engine.Evaluate("var f = host.IdentityInt; f.call(host, 8)").AsNumber().Should().Be(8);

        // static method: no receiver type check at all, including through an extracted reference
        engine.Evaluate("host.StaticIdentityInt(9)").AsNumber().Should().Be(9);
        engine.Evaluate("var g = host.StaticIdentityInt; g.call(null, 10)").AsNumber().Should().Be(10);
        engine.Evaluate("var h = host.StaticIdentityInt; h.call(other, 11)").AsNumber().Should().Be(11);
    }

    [Fact]
    public void ExtractedInstanceMethodWithWrongReceiverSurfacesCatchableTypeError()
    {
        // same guarantee as WrongTypedThisSurfacesCatchableTypeError, but for a method whose
        // receiver check reads the cached DeclaringType instead of the reflection property
        var engine = new Engine(options => options.Interop.ExceptionHandler = _ => false);
        engine.SetValue("host", new Host());
        engine.SetValue("other", new OtherHost());

        var ex = Invoking(() => engine.Evaluate("var f = host.IdentityInt; f.call(other, 3)")).Should().ThrowExactly<Jint.Runtime.JavaScriptException>().Which;
        ex.Message.Should().Be("Method 'IdentityInt' called on incompatible receiver");
    }
}
