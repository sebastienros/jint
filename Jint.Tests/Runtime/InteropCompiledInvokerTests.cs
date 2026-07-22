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
        Assert.Equal(5, engine.Evaluate("host.AddInt(2, 3)").AsNumber());
        // negative and zero still exact-type integers
        Assert.Equal(-1, engine.Evaluate("host.AddInt(-4, 3)").AsNumber());
    }

    [Fact]
    public void FastLane_LongArgsAndReturn()
    {
        var engine = CreateEngine();
        Assert.Equal(7, engine.Evaluate("host.AddLong(3, 4)").AsNumber());
    }

    [Fact]
    public void FastLane_DoubleArgsAndReturn()
    {
        var engine = CreateEngine();
        Assert.Equal(3.75, engine.Evaluate("host.AddDouble(1.5, 2.25)").AsNumber());
        // integral JS numbers bind to a double parameter as well
        Assert.Equal(5.0, engine.Evaluate("host.AddDouble(2, 3)").AsNumber());
    }

    [Fact]
    public void FastLane_BoolArgsAndReturn()
    {
        var engine = CreateEngine();
        Assert.True(engine.Evaluate("host.And(true, true)").AsBoolean());
        Assert.False(engine.Evaluate("host.And(true, false)").AsBoolean());
    }

    [Fact]
    public void FastLane_StringArgsAndReturn()
    {
        var engine = CreateEngine();
        Assert.Equal("ab", engine.Evaluate("host.Concat('a', 'b')").AsString());
    }

    [Fact]
    public void FastLane_JsValuePassthrough()
    {
        var engine = CreateEngine();
        Assert.Equal(42, engine.Evaluate("host.Echo(42)").AsNumber());
        Assert.Equal("x", engine.Evaluate("host.Echo('x')").AsString());
        Assert.True(engine.Evaluate("host.Echo(true)").AsBoolean());
    }

    [Fact]
    public void FastLane_StaticMethod()
    {
        var engine = CreateEngine();
        Assert.Equal(9, engine.Evaluate("host.StaticAdd(4, 5)").AsNumber());
    }

    [Fact]
    public void FastLane_VoidReturnsNull()
    {
        var engine = CreateEngine();
        var host = new Host();
        engine.SetValue("host", host);

        var result = engine.Evaluate("host.DoVoid()");
        // CLR void is exposed to JS as null (not undefined) - preserved by the fast lane
        Assert.True(result.IsNull());
        Assert.True(host.VoidCalled);
    }

    [Fact]
    public void HostExceptionSurfacesAsSameClrException()
    {
        var engine = CreateEngine();
        var ex = Assert.Throws<InvalidOperationException>(() => engine.Evaluate("host.Throws()"));
        Assert.Equal("boom from host", ex.Message);
    }

    [Fact]
    public void Fallback_FractionalNumberToIntParam()
    {
        var engine = CreateEngine();
        // fractional numbers are not exact-type integers: the fast lane declines and the fallback
        // converter rounds (banker's rounding, 2.5 -> 2), so 2 * 2 == 4. This locks in the behavior
        // that predates the fast lane.
        Assert.Equal(4, engine.Evaluate("host.TimesTwo(2.5)").AsNumber());
    }

    [Fact]
    public void Fallback_StringToIntParam()
    {
        var engine = CreateEngine();
        // a non-number argument to an int parameter is not an exact-type hit; the fallback coerces
        // the string "5" to 5, so 5 * 2 == 10.
        Assert.Equal(10, engine.Evaluate("host.TimesTwo('5')").AsNumber());
    }

    [Fact]
    public void Fallback_OverloadedMethod()
    {
        var engine = CreateEngine();
        Assert.Equal(6, engine.Evaluate("host.Over(5)").AsNumber());
        Assert.Equal("hi!", engine.Evaluate("host.Over('hi')").AsString());
    }

    [Fact]
    public void Fallback_ParamsMethod()
    {
        var engine = CreateEngine();
        Assert.Equal(6, engine.Evaluate("host.SumParams(1, 2, 3)").AsNumber());
    }

    [Fact]
    public void Fallback_OptionalArgMethod()
    {
        var engine = CreateEngine();
        Assert.Equal(15, engine.Evaluate("host.WithOptional(5)").AsNumber());
        Assert.Equal(9, engine.Evaluate("host.WithOptional(5, 4)").AsNumber());
    }

    [Fact]
    public void CustomObjectConverterStillSeesReturnValue()
    {
        // when a custom object converter is registered the fast lane is bypassed so the converter
        // observes primitive return values exactly as before
        var engine = new Engine(options => options.Interop.ObjectConverters.Add(new PlusOneIntConverter()));
        engine.SetValue("host", new Host());

        // AddInt returns 5, the converter turns any int into int+1 => 6
        Assert.Equal(6, engine.Evaluate("host.AddInt(2, 3)").AsNumber());
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

        var ex = Assert.Throws<Jint.Runtime.JavaScriptException>(() => engine.Evaluate("host.And(true, true)"));
        Assert.Contains("No public methods", ex.Message);

        // conversions the veto does not touch keep working
        Assert.Equal(5, engine.Evaluate("host.AddInt(2, 3)").AsNumber());
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

        var ex = Assert.ThrowsAny<Exception>(() => engine.Evaluate("var f = host.TimesTwo; f.call(other, 21)"));
        Assert.IsAssignableFrom<System.Reflection.TargetException>(ex);

        // a correctly-typed extracted call still uses the fast lane and works
        Assert.Equal(42, engine.Evaluate("var g = host.TimesTwo; g.call(host, 21)").AsNumber());
    }

    public sealed class OtherHost
    {
        public int Unrelated => 1;
    }
}
