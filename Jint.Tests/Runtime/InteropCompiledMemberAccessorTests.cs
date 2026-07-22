#nullable enable
using System.Diagnostics.CodeAnalysis;
using Jint.Native;
using Jint.Runtime;
using Jint.Runtime.Interop;

namespace Jint.Tests.Runtime;

/// <summary>
/// Exercises the compiled property/field accessor fast lanes (see <c>CompiledMemberAccessor</c> and
/// <c>CompilableMemberAccessor</c>) together with the reflection fallback they decline to. Every
/// assertion is written against the behavior that predates the fast lanes — the value of this file
/// is entirely in catching a divergence between the two, so nothing here may be "adjusted" to what
/// the fast lane happens to produce.
/// </summary>
public class InteropCompiledMemberAccessorTests
{
    #region hosts

    public enum Level
    {
        Zero = 0,
        One = 1,
        Two = 2,
    }

    public sealed class Nested
    {
        public int N { get; set; }
    }

    /// <summary>Members whose types are eligible for the JsValue lane, as both properties and fields.</summary>
    public sealed class Host
    {
        public int IntProp { get; set; }
        public long LongProp { get; set; }
        public double DoubleProp { get; set; }
        public bool BoolProp { get; set; }
        public string? StringProp { get; set; }
        public JsValue? JsValueProp { get; set; }

        public int IntField;
        public long LongField;
        public double DoubleField;
        public bool BoolField;
        public string? StringField;
        public JsValue? JsValueField;
    }

    /// <summary>Members whose types are not eligible for the JsValue lane (raw lane / reflection).</summary>
    public sealed class RawHost
    {
        public int[]? IntArrayProp { get; set; }
        public Level EnumProp { get; set; }
        public int? NullableIntProp { get; set; }
        public Nested? NestedProp { get; set; }
        public object? ObjectProp { get; set; }
        public DateTime DateTimeProp { get; set; }
        public decimal DecimalProp { get; set; }

        public int[]? IntArrayField;
        public Level EnumField;
        public int? NullableIntField;
        public Nested? NestedField;
        public object? ObjectField;
        public DateTime DateTimeField;
        public decimal DecimalField;
    }

    public sealed class ThrowingHost
    {
        // JsValue lane member types
        public int ThrowingIntGetter => throw new InvalidOperationException("int getter boom");

        public int ThrowingIntSetter
        {
            get => 0;
            set => throw new InvalidOperationException("int setter boom");
        }

        // raw lane member types
        public decimal ThrowingDecimalGetter => throw new InvalidOperationException("decimal getter boom");

        public decimal ThrowingDecimalSetter
        {
            get => 0m;
            set => throw new InvalidOperationException("decimal setter boom");
        }
    }

    public sealed class ReadOnlyHost
    {
        public int ReadOnlyIntProp { get; } = 11;
        public readonly int ReadOnlyIntField = 22;
        public readonly string ReadOnlyStringField = "fixed";
    }

    public sealed class NonPublicGetterHost
    {
        private int _value = 5;

        // CanRead is true but there is no public get accessor
        public int HalfHidden
        {
            private get => _value;
            set => _value = value;
        }

        public int Observe() => _value;
    }

    public struct StructHost
    {
        public int IntProp { get; set; }
        public string? StringProp { get; set; }
        public int IntField;

        public StructHost(int value)
        {
            IntProp = value;
            StringProp = null;
            IntField = value;
        }
    }

    public sealed class IndexerHost
    {
        public int Value { get; set; } = 7;

        public int this[string key] => 99;
    }

    public interface IValueHolder
    {
        int Value { get; set; }
        string? Name { get; set; }
    }

    public sealed class ValueHolder : IValueHolder
    {
        public int Value { get; set; }
        public string? Name { get; set; }
    }

    public sealed class HolderOwner
    {
        public IValueHolder Holder { get; } = new ValueHolder();
    }

    public sealed class StaticHost
    {
        public static int StaticIntProp { get; set; }
        public static string? StaticStringField;
    }

    public sealed class JsSubtypeHost
    {
        public JsString? JsStringProp { get; set; }
        public JsNumber? JsNumberField;
    }

    public sealed class IsolationHost
    {
        public int IntProp { get; set; }
        public long LongProp { get; set; }
    }

    #endregion

    private static Engine CreateEngine(object host, Action<Options>? configure = null)
    {
        var engine = configure is null ? new Engine() : new Engine(configure);
        engine.SetValue("host", host);
        return engine;
    }

    // the CLR's own runtime double->integer semantics (saturating on .NET Core, wrapping on the
    // .NET Framework JIT); computed here rather than spelled out so the expectation always matches
    // whatever the coercion path itself performs on this runtime
    private static int ToInt(double value) => unchecked((int) value);

    private static long ToLong(double value) => unchecked((long) value);

    private static bool IsNegativeZero(double value)
        => BitConverter.DoubleToInt64Bits(value) == BitConverter.DoubleToInt64Bits(-0d);

    private static Host Assign(string script, ValueCoercionType? coercion = null)
    {
        var host = new Host();
        var engine = CreateEngine(host, coercion is null ? null : options => options.Interop.ValueCoercion = coercion.Value);
        engine.Execute(script);
        return host;
    }

    private static Exception AssignThrows(string script, ValueCoercionType? coercion = null)
    {
        var host = new Host();
        var engine = CreateEngine(host, coercion is null ? null : options => options.Interop.ValueCoercion = coercion.Value);
        return Invoking(() => engine.Execute(script)).Should().Throw<Exception>().Which;
    }

    #region 1. round-trip per supported member type

    [Fact]
    public void RoundTrip_Int()
    {
        var host = new Host { IntProp = 1, IntField = 2 };
        var engine = CreateEngine(host);

        engine.Evaluate("host.IntProp").Should().Be(1);
        engine.Evaluate("host.IntField").Should().Be(2);

        engine.Execute("host.IntProp = 42; host.IntField = 43;");
        host.IntProp.Should().Be(42);
        host.IntField.Should().Be(43);

        // the descriptor stays live: a host-side change is visible to a later read
        host.IntProp = 100;
        host.IntField = 101;
        engine.Evaluate("host.IntProp").Should().Be(100);
        engine.Evaluate("host.IntField").Should().Be(101);
    }

    [Fact]
    public void RoundTrip_Long()
    {
        var host = new Host { LongProp = 1L, LongField = 2L };
        var engine = CreateEngine(host);

        engine.Evaluate("host.LongProp").Should().Be(1);
        engine.Evaluate("host.LongField").Should().Be(2);

        engine.Execute("host.LongProp = 42; host.LongField = 43;");
        host.LongProp.Should().Be(42L);
        host.LongField.Should().Be(43L);

        host.LongProp = 8589934592L; // 2^33, outside int range
        host.LongField = -8589934592L;
        engine.Evaluate("host.LongProp").Should().Be(8589934592d);
        engine.Evaluate("host.LongField").Should().Be(-8589934592d);
    }

    [Fact]
    public void RoundTrip_Double()
    {
        var host = new Host { DoubleProp = 1.25, DoubleField = 2.5 };
        var engine = CreateEngine(host);

        engine.Evaluate("host.DoubleProp").Should().Be(1.25);
        engine.Evaluate("host.DoubleField").Should().Be(2.5);

        engine.Execute("host.DoubleProp = 3.75; host.DoubleField = -0.5;");
        host.DoubleProp.Should().Be(3.75);
        host.DoubleField.Should().Be(-0.5);

        host.DoubleProp = 9.5;
        engine.Evaluate("host.DoubleProp").Should().Be(9.5);
    }

    [Fact]
    public void RoundTrip_Bool()
    {
        var host = new Host { BoolProp = true, BoolField = false };
        var engine = CreateEngine(host);

        engine.Evaluate("host.BoolProp").Should().BeTrue();
        engine.Evaluate("host.BoolField").Should().BeFalse();

        engine.Execute("host.BoolProp = false; host.BoolField = true;");
        host.BoolProp.Should().BeFalse();
        host.BoolField.Should().BeTrue();

        host.BoolProp = true;
        engine.Evaluate("host.BoolProp").Should().BeTrue();
    }

    [Fact]
    public void RoundTrip_String()
    {
        var host = new Host { StringProp = "a", StringField = "b" };
        var engine = CreateEngine(host);

        engine.Evaluate("host.StringProp").Should().Be("a");
        engine.Evaluate("host.StringField").Should().Be("b");

        engine.Execute("host.StringProp = 'x'; host.StringField = 'y';");
        host.StringProp.Should().Be("x");
        host.StringField.Should().Be("y");

        host.StringProp = "live";
        engine.Evaluate("host.StringProp").Should().Be("live");
    }

    [Fact]
    public void RoundTrip_JsValue()
    {
        var host = new Host { JsValueProp = JsNumber.Create(1), JsValueField = new JsString("f") };
        var engine = CreateEngine(host);

        engine.Evaluate("host.JsValueProp").Should().Be(1);
        engine.Evaluate("host.JsValueField").Should().Be("f");

        engine.Execute("host.JsValueProp = 'set'; host.JsValueField = 5;");
        host.JsValueProp!.AsString().Should().Be("set");
        host.JsValueField!.AsNumber().Should().Be(5);

        // objects survive the round-trip by identity
        engine.Execute("host.JsValueProp = { a: 1 };");
        engine.Evaluate("host.JsValueProp.a").Should().Be(1);

        // a host side change is visible, including undefined and null
        host.JsValueProp = JsValue.Undefined;
        engine.Evaluate("host.JsValueProp").Should().BeUndefined();
        host.JsValueProp = JsValue.Null;
        engine.Evaluate("host.JsValueProp === null").Should().BeTrue();
        host.JsValueProp = null;
        engine.Evaluate("host.JsValueProp === null").Should().BeTrue();
    }

    [Fact]
    public void RoundTrip_JsValueSubtype_Read()
    {
        var host = new JsSubtypeHost { JsStringProp = new JsString("s"), JsNumberField = JsNumber.Create(4) };
        var engine = new Engine();
        engine.SetValue("host", host);

        engine.Evaluate("host.JsStringProp").Should().Be("s");
        engine.Evaluate("host.JsNumberField").Should().Be(4);

        host.JsStringProp = null;
        engine.Evaluate("host.JsStringProp === null").Should().BeTrue();
    }

    [Fact]
    public void JsValueSubtypeWriteKeepsTheConversionPath()
    {
        // A member typed as a JsValue *subtype* is not `_memberType == typeof(JsValue)` in
        // ReflectionAccessor.SetValue, so it has always gone through ToObject() plus the type
        // converter - which cannot turn the resulting System.String back into a JsString. The fast
        // lane must not "fix" that, or the write would start succeeding where it used to throw.
        var host = new JsSubtypeHost();
        var engine = new Engine();
        engine.SetValue("host", host);

        Invoking(() => engine.Execute("host.JsStringProp = 'abc';"))
            .Should().ThrowExactly<InvalidCastException>();
        host.JsStringProp.Should().BeNull();

        Invoking(() => engine.Execute("host.JsNumberField = 1;"))
            .Should().ThrowExactly<InvalidCastException>();
        host.JsNumberField.Should().BeNull();
    }

    #endregion

    #region 2. unsupported member types still work (raw lane)

    [Fact]
    public void RawLane_IntArray()
    {
        var host = new RawHost { IntArrayProp = [1, 2, 3], IntArrayField = [4, 5] };
        var engine = CreateEngine(host);

        engine.Evaluate("host.IntArrayProp.length").Should().Be(3);
        engine.Evaluate("host.IntArrayProp[1]").Should().Be(2);
        engine.Evaluate("host.IntArrayField[0]").Should().Be(4);

        engine.Execute("host.IntArrayProp = [9, 8];");
        host.IntArrayProp.Should().BeEquivalentTo(new[] { 9, 8 });
    }

    [Fact]
    public void RawLane_Enum()
    {
        var host = new RawHost { EnumProp = Level.Two, EnumField = Level.One };
        var engine = CreateEngine(host);

        engine.Evaluate("host.EnumProp").Should().Be(2);
        engine.Evaluate("host.EnumField").Should().Be(1);

        engine.Execute("host.EnumProp = 1; host.EnumField = 2;");
        host.EnumProp.Should().Be(Level.One);
        host.EnumField.Should().Be(Level.Two);
    }

    [Fact]
    public void RawLane_NullableInt()
    {
        var host = new RawHost { NullableIntProp = 5, NullableIntField = null };
        var engine = CreateEngine(host);

        engine.Evaluate("host.NullableIntProp").Should().Be(5);
        engine.Evaluate("host.NullableIntField === null").Should().BeTrue();

        engine.Execute("host.NullableIntProp = 9;");
        host.NullableIntProp.Should().Be(9);
    }

    [Fact]
    public void RawLane_CustomClass()
    {
        var host = new RawHost { NestedProp = new Nested { N = 3 }, NestedField = null };
        var engine = CreateEngine(host);

        engine.Evaluate("host.NestedProp.N").Should().Be(3);
        // a null reference member reads as null, not undefined
        engine.Evaluate("host.NestedField === null").Should().BeTrue();
        engine.Evaluate("typeof host.NestedField").Should().Be("object");
    }

    [Fact]
    public void RawLane_Object()
    {
        var host = new RawHost { ObjectProp = "text", ObjectField = 12 };
        var engine = CreateEngine(host);

        engine.Evaluate("host.ObjectProp").Should().Be("text");
        engine.Evaluate("host.ObjectField").Should().Be(12);

        host.ObjectProp = null;
        engine.Evaluate("host.ObjectProp === null").Should().BeTrue();
    }

    [Fact]
    public void RawLane_DateTime()
    {
        var host = new RawHost { DateTimeProp = new DateTime(2020, 5, 6, 0, 0, 0, DateTimeKind.Utc) };
        var engine = CreateEngine(host);

        engine.Evaluate("host.DateTimeProp.getUTCFullYear()").Should().Be(2020);
        engine.Evaluate("host.DateTimeProp.getUTCMonth()").Should().Be(4);
    }

    [Fact]
    public void RawLane_Decimal()
    {
        var host = new RawHost { DecimalProp = 1.5m, DecimalField = 2m };
        var engine = CreateEngine(host);

        engine.Evaluate("host.DecimalProp").Should().Be(1.5);
        engine.Evaluate("host.DecimalField").Should().Be(2);

        engine.Execute("host.DecimalProp = 3.25;");
        host.DecimalProp.Should().Be(3.25m);
    }

    #endregion

    #region 3. write conversion edge cases - int

    [Fact]
    public void IntWrite_Bounds()
    {
        var host = Assign("host.IntProp = 2147483647; host.IntField = -2147483648;");
        host.IntProp.Should().Be(int.MaxValue);
        host.IntField.Should().Be(int.MinValue);
    }

    [Fact]
    public void IntWrite_JustPastUpperBound_Throws()
    {
        // 2^31 is not representable, the reflection path's Convert.ChangeType overflows and the
        // default ExceptionHandler lets the CLR exception bubble
        AssignThrows("host.IntProp = 2147483648;").Should().BeOfType<OverflowException>();
        AssignThrows("host.IntField = 2147483648;").Should().BeOfType<OverflowException>();
    }

    [Fact]
    public void IntWrite_JustPastLowerBound_Throws()
    {
        AssignThrows("host.IntProp = -2147483649;").Should().BeOfType<OverflowException>();
    }

    [Fact]
    public void IntWrite_FractionalUsesBankersRoundingByDefault()
    {
        // the default (String-only) coercion routes fractional values through
        // Convert.ChangeType, which rounds half to even
        Assign("host.IntProp = 1.5;").IntProp.Should().Be(2);
        Assign("host.IntProp = 2.5;").IntProp.Should().Be(2);
        Assign("host.IntProp = -1.5;").IntProp.Should().Be(-2);

        Assign("host.IntField = 1.5;").IntField.Should().Be(2);
        Assign("host.IntField = 2.5;").IntField.Should().Be(2);
        Assign("host.IntField = -1.5;").IntField.Should().Be(-2);
    }

    [Fact]
    public void IntWrite_FractionalTruncatesUnderNumberCoercion()
    {
        // ValueCoercionType.Number takes the AsNumberOfType path instead, which is a C# cast
        Assign("host.IntProp = 1.5;", ValueCoercionType.All).IntProp.Should().Be(1);
        Assign("host.IntProp = 2.5;", ValueCoercionType.All).IntProp.Should().Be(2);
        Assign("host.IntProp = -1.5;", ValueCoercionType.All).IntProp.Should().Be(-1);

        Assign("host.IntField = 1.5;", ValueCoercionType.All).IntField.Should().Be(1);
        Assign("host.IntField = -1.5;", ValueCoercionType.All).IntField.Should().Be(-1);
    }

    [Fact]
    public void IntWrite_NaNAndInfinities_Throw()
    {
        AssignThrows("host.IntProp = NaN;").Should().BeOfType<OverflowException>();
        AssignThrows("host.IntProp = Infinity;").Should().BeOfType<OverflowException>();
        AssignThrows("host.IntProp = -Infinity;").Should().BeOfType<OverflowException>();
        AssignThrows("host.IntField = NaN;").Should().BeOfType<OverflowException>();
    }

    [Fact]
    public void IntWrite_NaNAndInfinitiesUnderNumberCoercion()
    {
        // the coercion path is a plain C# cast, so the runtime's own double->int semantics apply
        Assign("host.IntProp = NaN;", ValueCoercionType.All).IntProp.Should().Be(ToInt(double.NaN));
        Assign("host.IntProp = Infinity;", ValueCoercionType.All).IntProp.Should().Be(ToInt(double.PositiveInfinity));
        Assign("host.IntProp = -Infinity;", ValueCoercionType.All).IntProp.Should().Be(ToInt(double.NegativeInfinity));
        Assign("host.IntProp = 2147483648;", ValueCoercionType.All).IntProp.Should().Be(ToInt(2147483648d));
    }

    [Fact]
    public void IntWrite_NegativeZero()
    {
        Assign("host.IntProp = -0; host.IntField = -0;").IntProp.Should().Be(0);
        Assign("host.IntProp = -0;", ValueCoercionType.All).IntProp.Should().Be(0);
    }

    [Fact]
    public void IntWrite_NumericString()
    {
        Assign("host.IntProp = '42'; host.IntField = '43';").IntProp.Should().Be(42);
        Assign("host.IntProp = '42';").IntProp.Should().Be(42);
        Assign("host.IntProp = '42';", ValueCoercionType.All).IntProp.Should().Be(42);
    }

    [Fact]
    public void IntWrite_Boolean()
    {
        Assign("host.IntProp = true; host.IntField = true;").IntProp.Should().Be(1);
        Assign("host.IntProp = true;", ValueCoercionType.All).IntProp.Should().Be(1);
    }

    [Fact]
    public void IntWrite_NullAndUndefined()
    {
        // null/undefined convert to a CLR null, and reflection assigns the default value of a
        // value-type member for a null - no exception, the member ends up at 0
        Assign("host.IntProp = 3; host.IntProp = null;").IntProp.Should().Be(0);
        Assign("host.IntProp = 3; host.IntProp = undefined;").IntProp.Should().Be(0);
        Assign("host.IntField = 3; host.IntField = null;").IntField.Should().Be(0);
        Assign("host.IntField = 3; host.IntField = undefined;").IntField.Should().Be(0);
    }

    [Fact]
    public void IntWrite_NullAndUndefinedUnderNumberCoercion()
    {
        Assign("host.IntProp = null;", ValueCoercionType.All).IntProp.Should().Be(0);
        Assign("host.IntProp = undefined;", ValueCoercionType.All).IntProp.Should().Be(ToInt(double.NaN));
    }

    #endregion

    #region 3. write conversion edge cases - long

    [Fact]
    public void LongWrite_SmallAndLargeValues()
    {
        // a small integral value reaches the member as a boxed int on the reflection path and is
        // widened by reflection; a large one arrives as a double
        var host = Assign("host.LongProp = 5; host.LongField = 6;");
        host.LongProp.Should().Be(5L);
        host.LongField.Should().Be(6L);

        host = Assign("host.LongProp = 4294967296; host.LongField = -4294967296;");
        host.LongProp.Should().Be(4294967296L);
        host.LongField.Should().Be(-4294967296L);
    }

    [Fact]
    public void LongWrite_Boundaries()
    {
        Assign("host.LongProp = -9223372036854775808;").LongProp.Should().Be(long.MinValue);
        Assign("host.LongProp = 1e18;").LongProp.Should().Be(1000000000000000000L);
        Assign("host.LongProp = 1e18;", ValueCoercionType.All).LongProp.Should().Be(1000000000000000000L);
    }

    [Fact]
    public void LongWrite_TwoToThe63_Throws()
    {
        AssignThrows("host.LongProp = Math.pow(2, 63);").Should().BeOfType<OverflowException>();
        AssignThrows("host.LongField = Math.pow(2, 63);").Should().BeOfType<OverflowException>();
    }

    [Fact]
    public void LongWrite_TwoToThe63UnderNumberCoercion()
    {
        Assign("host.LongProp = Math.pow(2, 63);", ValueCoercionType.All).LongProp.Should().Be(ToLong(9223372036854775808d));
    }

    [Fact]
    public void LongWrite_Fractional()
    {
        Assign("host.LongProp = 1.5;").LongProp.Should().Be(2L);
        Assign("host.LongProp = 1.5;", ValueCoercionType.All).LongProp.Should().Be(1L);
    }

    #endregion

    #region 3. write conversion edge cases - double

    [Fact]
    public void DoubleWrite_NonIntegral()
    {
        var host = Assign("host.DoubleProp = 1.25; host.DoubleField = -7.125;");
        host.DoubleProp.Should().Be(1.25);
        host.DoubleField.Should().Be(-7.125);
    }

    [Fact]
    public void DoubleWrite_NaNAndInfinities()
    {
        var host = Assign("host.DoubleProp = NaN;");
        double.IsNaN(host.DoubleProp).Should().BeTrue();

        Assign("host.DoubleProp = Infinity;").DoubleProp.Should().Be(double.PositiveInfinity);
        Assign("host.DoubleProp = -Infinity;").DoubleProp.Should().Be(double.NegativeInfinity);
        Assign("host.DoubleField = Infinity;").DoubleField.Should().Be(double.PositiveInfinity);

        var coerced = Assign("host.DoubleProp = NaN;", ValueCoercionType.All);
        double.IsNaN(coerced.DoubleProp).Should().BeTrue();
        Assign("host.DoubleProp = Infinity;", ValueCoercionType.All).DoubleProp.Should().Be(double.PositiveInfinity);
    }

    [Fact]
    public void DoubleWrite_NegativeZeroKeepsSign()
    {
        var host = Assign("host.DoubleProp = -0; host.DoubleField = -0;");
        IsNegativeZero(host.DoubleProp).Should().BeTrue();
        IsNegativeZero(host.DoubleField).Should().BeTrue();

        var engine = CreateEngine(host);
        engine.Evaluate("1 / host.DoubleProp === -Infinity").Should().BeTrue();
        engine.Evaluate("1 / host.DoubleField === -Infinity").Should().BeTrue();

        var coerced = Assign("host.DoubleProp = -0;", ValueCoercionType.All);
        IsNegativeZero(coerced.DoubleProp).Should().BeTrue();
    }

    [Fact]
    public void DoubleWrite_IntegralAndString()
    {
        Assign("host.DoubleProp = 5;").DoubleProp.Should().Be(5d);
        Assign("host.DoubleProp = '5.5';").DoubleProp.Should().Be(5.5);
        Assign("host.DoubleProp = '5.5';", ValueCoercionType.All).DoubleProp.Should().Be(5.5);
        Assign("host.DoubleProp = true;").DoubleProp.Should().Be(1d);
    }

    #endregion

    #region 3. write conversion edge cases - string

    [Fact]
    public void StringWrite_Number()
    {
        var host = Assign("host.StringProp = 42; host.StringField = 1.5;");
        host.StringProp.Should().Be("42");
        host.StringField.Should().Be("1.5");

        Assign("host.StringProp = 42;", ValueCoercionType.All).StringProp.Should().Be("42");
    }

    [Fact]
    public void StringWrite_Boolean()
    {
        Assign("host.StringProp = true;").StringProp.Should().Be("true");
        Assign("host.StringProp = true;", ValueCoercionType.All).StringProp.Should().Be("true");
    }

    [Fact]
    public void StringWrite_NullAndUndefined()
    {
        var host = Assign("host.StringProp = 'x'; host.StringProp = null;");
        host.StringProp.Should().BeNull();

        host = Assign("host.StringField = 'x'; host.StringField = undefined;");
        host.StringField.Should().BeNull();

        Assign("host.StringProp = 'x'; host.StringProp = null;", ValueCoercionType.All).StringProp.Should().BeNull();
        Assign("host.StringProp = 'x'; host.StringProp = undefined;", ValueCoercionType.All).StringProp.Should().BeNull();
    }

    [Fact]
    public void StringWrite_ConcatenatedString()
    {
        // a concatenated (lazily materialized) JsString is still a JsString; it must reach the
        // member as the same text the conversion path produced
        var host = Assign("var b = 'b'; host.StringProp = 'a' + b + 'c'; host.StringField = 'x'.repeat(3);");
        host.StringProp.Should().Be("abc");
        host.StringField.Should().Be("xxx");
    }

    [Fact]
    public void WritesBeforeAndAfterTheFirstReadAgree()
    {
        // a write before any read takes ObjectWrapper's accessor fast path, a write afterwards goes
        // through the cached ReflectionDescriptor - both must land on the same member
        var host = new Host();
        var engine = CreateEngine(host);

        engine.Execute("host.IntProp = 1; host.StringProp = 'a'; host.DoubleProp = 1.5; host.LongProp = 2; host.BoolProp = true;");
        host.IntProp.Should().Be(1);
        host.StringProp.Should().Be("a");

        engine.Evaluate("host.IntProp").Should().Be(1);
        engine.Evaluate("host.StringProp").Should().Be("a");

        engine.Execute("host.IntProp = 2; host.StringProp = 'b'; host.DoubleProp = 2.5; host.LongProp = 3; host.BoolProp = false;");
        host.IntProp.Should().Be(2);
        host.StringProp.Should().Be("b");
        host.DoubleProp.Should().Be(2.5);
        host.LongProp.Should().Be(3L);
        host.BoolProp.Should().BeFalse();
    }

    [Fact]
    public void StringWrite_PlainString()
    {
        Assign("host.StringProp = 'hello'; host.StringField = '';").StringProp.Should().Be("hello");
        Assign("host.StringField = '';").StringField.Should().Be("");
    }

    #endregion

    #region 4. bool writes under both coercion settings

    [Fact]
    public void BoolWrite_UnderBothCoercionSettings()
    {
        Assign("host.BoolProp = true; host.BoolField = true;").BoolProp.Should().BeTrue();
        Assign("host.BoolProp = true;", ValueCoercionType.All).BoolProp.Should().BeTrue();

        // non-boolean values decline the fast lane and take the pre-existing conversion
        Assign("host.BoolProp = 1;").BoolProp.Should().BeTrue();
        Assign("host.BoolProp = 1;", ValueCoercionType.All).BoolProp.Should().BeTrue();
        Assign("host.BoolProp = 0;").BoolProp.Should().BeFalse();
        Assign("host.BoolProp = 'true';").BoolProp.Should().BeTrue();
        Assign("host.BoolProp = 'dog';", ValueCoercionType.All).BoolProp.Should().BeTrue();
    }

    #endregion

    #region 5. read equivalence with JsValue.FromObjectWithType

    [Fact]
    public void Read_MatchesFromObjectWithType()
    {
        var host = new Host
        {
            IntProp = 42,
            LongProp = 8589934592L,
            DoubleProp = 1.25,
            BoolProp = true,
            StringProp = "abc",
            JsValueProp = new JsString("js"),
        };
        var engine = CreateEngine(host);

        engine.Evaluate("host.IntProp").Should().Be(JsValue.FromObjectWithType(engine, host.IntProp, typeof(int)));
        engine.Evaluate("host.LongProp").Should().Be(JsValue.FromObjectWithType(engine, host.LongProp, typeof(long)));
        engine.Evaluate("host.DoubleProp").Should().Be(JsValue.FromObjectWithType(engine, host.DoubleProp, typeof(double)));
        engine.Evaluate("host.BoolProp").Should().Be(JsValue.FromObjectWithType(engine, host.BoolProp, typeof(bool)));
        engine.Evaluate("host.StringProp").Should().Be(JsValue.FromObjectWithType(engine, host.StringProp, typeof(string)));
        engine.Evaluate("host.JsValueProp").Should().BeSameAs(host.JsValueProp);

        // int members produce a cached JsNumber, exactly as the conversion path does
        engine.Evaluate("host.IntProp").Should().BeSameAs(JsValue.FromObjectWithType(engine, host.IntProp, typeof(int)));
        engine.Evaluate("host.BoolProp").Should().BeSameAs(JsBoolean.True);
    }

    [Fact]
    public void Read_IntBehavesAsNumber()
    {
        var host = new Host { IntProp = 42, IntField = 7 };
        var engine = CreateEngine(host);

        engine.Evaluate("typeof host.IntProp").Should().Be("number");
        engine.Evaluate("host.IntProp === 42").Should().BeTrue();
        engine.Evaluate("host.IntProp + 1").Should().Be(43);
        engine.Evaluate("host.IntProp / 4").Should().Be(10.5);
        engine.Evaluate("host.IntField.toFixed(2)").Should().Be("7.00");
        engine.Evaluate("host.IntProp | 0").Should().Be(42);
    }

    [Fact]
    public void Read_NullStringIsNullNotUndefined()
    {
        var host = new Host { StringProp = null, StringField = null };
        var engine = CreateEngine(host);

        engine.Evaluate("host.StringProp === null").Should().BeTrue();
        engine.Evaluate("host.StringProp === undefined").Should().BeFalse();
        engine.Evaluate("typeof host.StringProp").Should().Be("object");
        engine.Evaluate("host.StringField === null").Should().BeTrue();

        engine.Evaluate("host.StringProp").Should().Be(JsValue.FromObjectWithType(engine, null, typeof(string)));
    }

    [Fact]
    public void Read_LongPrecision()
    {
        var host = new Host { LongProp = 9007199254740993L }; // 2^53 + 1, not representable as a double
        var engine = CreateEngine(host);

        engine.Evaluate("host.LongProp").Should().Be(JsValue.FromObjectWithType(engine, host.LongProp, typeof(long)));
    }

    #endregion

    #region 6. exception fidelity

    [Fact]
    public void ThrowingGetter_JsValueLane()
    {
        var engine = CreateEngine(new ThrowingHost());
        Invoking(() => engine.Evaluate("host.ThrowingIntGetter"))
            .Should().ThrowExactly<InvalidOperationException>()
            .WithMessage("int getter boom");
    }

    [Fact]
    public void ThrowingSetter_JsValueLane()
    {
        var engine = CreateEngine(new ThrowingHost());
        Invoking(() => engine.Execute("host.ThrowingIntSetter = 1;"))
            .Should().ThrowExactly<InvalidOperationException>()
            .WithMessage("int setter boom");
    }

    [Fact]
    public void ThrowingGetter_RawLane()
    {
        var engine = CreateEngine(new ThrowingHost());
        Invoking(() => engine.Evaluate("host.ThrowingDecimalGetter"))
            .Should().ThrowExactly<InvalidOperationException>()
            .WithMessage("decimal getter boom");
    }

    [Fact]
    public void ThrowingSetter_RawLane()
    {
        var engine = CreateEngine(new ThrowingHost());
        Invoking(() => engine.Execute("host.ThrowingDecimalSetter = 1;"))
            .Should().ThrowExactly<InvalidOperationException>()
            .WithMessage("decimal setter boom");
    }

    [Fact]
    public void ThrowingMembers_ExceptionHandlerStillConsulted()
    {
        var seen = new List<Exception>();
        var engine = CreateEngine(new ThrowingHost(), options => options.Interop.ExceptionHandler = e =>
        {
            seen.Add(e);
            return true;
        });

        engine.Evaluate("try { host.ThrowingIntGetter; 'no throw' } catch (e) { e.message }").Should().Be("int getter boom");
        engine.Evaluate("try { host.ThrowingDecimalGetter; 'no throw' } catch (e) { e.message }").Should().Be("decimal getter boom");
        engine.Evaluate("try { host.ThrowingIntSetter = 1; 'no throw' } catch (e) { e.message }").Should().Be("int setter boom");
        engine.Evaluate("try { host.ThrowingDecimalSetter = 1; 'no throw' } catch (e) { e.message }").Should().Be("decimal setter boom");

        seen.Should().AllSatisfy(e => e.Should().BeOfType<InvalidOperationException>());
        seen.Should().HaveCount(4);
    }

    #endregion

    #region 7. declining conditions

    [Fact]
    public void ObjectConverterStillConsultedForSupportedType()
    {
        var host = new Host { IntProp = 41, StringProp = "raw", BoolProp = true, LongProp = 1, DoubleProp = 1.5 };
        var engine = CreateEngine(host, options => options.Interop.ObjectConverters.Add(new PlusOneIntConverter()));

        engine.Evaluate("host.IntProp").Should().Be(42);
        // the converter declines everything else, which still converts as before
        engine.Evaluate("host.StringProp").Should().Be("raw");
        engine.Evaluate("host.BoolProp").Should().BeTrue();
        engine.Evaluate("host.DoubleProp").Should().Be(1.5);
    }

    private sealed class PlusOneIntConverter : IObjectConverter
    {
        public bool TryConvert(Engine engine, object value, [NotNullWhen(true)] out JsValue? result)
        {
            if (value is int i)
            {
                result = JsNumber.Create(i + 1);
                return true;
            }

            result = null;
            return false;
        }
    }

    [Fact]
    public void CustomTypeConverterStillConsultedOnWrite()
    {
        var host = new Host();
        var engine = CreateEngine(host, options => options.SetTypeConverter(e => new LongPinningTypeConverter(e)));

        // an integral value above int range reaches ConvertValueToSet on the reflection path
        engine.Execute("host.LongProp = 4294967296;");
        host.LongProp.Should().Be(42L);

        // -0 is a Number-typed JsNumber, so the int member also reaches the converter
        engine.Execute("host.IntProp = -0;");
        host.IntProp.Should().Be(7);

        // and the conversions the converter does not touch keep working
        engine.Execute("host.StringProp = 'ok'; host.BoolProp = true; host.IntField = 3;");
        host.StringProp.Should().Be("ok");
        host.BoolProp.Should().BeTrue();
        host.IntField.Should().Be(3);
    }

    private sealed class LongPinningTypeConverter : DefaultTypeConverter
    {
        public LongPinningTypeConverter(Engine engine) : base(engine)
        {
        }

        public override bool TryConvert(object? value, Type type, IFormatProvider formatProvider, [NotNullWhen(true)] out object? converted)
        {
            if (type == typeof(long))
            {
                converted = 42L;
                return true;
            }

            if (type == typeof(int))
            {
                converted = 7;
                return true;
            }

            return base.TryConvert(value, type, formatProvider, out converted);
        }
    }

    [Fact]
    public void IndexerIsResolvedBeforeTheMember()
    {
        var host = new IndexerHost();
        var engine = new Engine();
        engine.SetValue("host", host);

        // the indexer is probed first and answers for "Value", shadowing the int property
        engine.Evaluate("host.Value").Should().Be(99);

        // a write still lands on the property
        engine.Execute("host.Value = 9;");
        host.Value.Should().Be(9);
    }

    [Fact]
    public void StructHostStillWorks()
    {
        var engine = new Engine();
        engine.SetValue("host", new StructHost(3));

        engine.Evaluate("host.IntProp").Should().Be(3);
        engine.Evaluate("host.IntField").Should().Be(3);

        engine.Execute("host.IntProp = 8; host.StringProp = 'v';");
        engine.Evaluate("host.IntProp").Should().Be(8);
        engine.Evaluate("host.StringProp").Should().Be("v");
    }

    [Fact]
    public void ReadOnlyFieldAndPropertyWritesFail()
    {
        var host = new ReadOnlyHost();
        var engine = new Engine();
        engine.SetValue("host", host);

        engine.Evaluate("host.ReadOnlyIntProp").Should().Be(11);
        engine.Evaluate("host.ReadOnlyIntField").Should().Be(22);
        engine.Evaluate("host.ReadOnlyStringField").Should().Be("fixed");

        // sloppy mode silently ignores the write
        engine.Execute("host.ReadOnlyIntProp = 1; host.ReadOnlyIntField = 2; host.ReadOnlyStringField = 'x';");
        host.ReadOnlyIntProp.Should().Be(11);
        host.ReadOnlyIntField.Should().Be(22);
        host.ReadOnlyStringField.Should().Be("fixed");

        // strict mode throws a TypeError
        Invoking(() => engine.Execute("'use strict'; host.ReadOnlyIntProp = 1;"))
            .Should().Throw<JavaScriptException>();
        Invoking(() => engine.Execute("'use strict'; host.ReadOnlyIntField = 2;"))
            .Should().Throw<JavaScriptException>();
    }

    [Fact]
    public void StaticMembersStillWork()
    {
        StaticHost.StaticIntProp = 3;
        StaticHost.StaticStringField = "s";

        var engine = new Engine();
        engine.SetValue("Statics", TypeReference.CreateTypeReference(engine, typeof(StaticHost)));

        engine.Evaluate("Statics.StaticIntProp").Should().Be(3);
        engine.Evaluate("Statics.StaticStringField").Should().Be("s");

        engine.Execute("Statics.StaticIntProp = 9; Statics.StaticStringField = 't';");
        StaticHost.StaticIntProp.Should().Be(9);
        StaticHost.StaticStringField.Should().Be("t");
    }

    [Fact]
    public void InterfaceDeclaredPropertyWorksThroughTheInterface()
    {
        var owner = new HolderOwner();
        var engine = new Engine();
        engine.SetValue("owner", owner);

        engine.Execute("owner.Holder.Value = 5; owner.Holder.Name = 'n';");
        owner.Holder.Value.Should().Be(5);
        owner.Holder.Name.Should().Be("n");

        engine.Evaluate("owner.Holder.Value").Should().Be(5);
        engine.Evaluate("owner.Holder.Name").Should().Be("n");

        owner.Holder.Value = 6;
        engine.Evaluate("owner.Holder.Value").Should().Be(6);
    }

    #endregion

    #region 8. cross-engine isolation

    [Fact]
    public void TypeConverterPolicyIsPerEngine_ConverterFirst()
    {
        var withConverter = new IsolationHost();
        var engineWithConverter = CreateEngine(withConverter, options => options.SetTypeConverter(e => new LongPinningTypeConverter(e)));
        engineWithConverter.Execute("host.LongProp = 4294967296;");
        withConverter.LongProp.Should().Be(42L);

        var plain = new IsolationHost();
        var plainEngine = CreateEngine(plain);
        plainEngine.Execute("host.LongProp = 4294967296;");
        plain.LongProp.Should().Be(4294967296L);
    }

    [Fact]
    public void TypeConverterPolicyIsPerEngine_PlainFirst()
    {
        var plain = new IsolationHost();
        var plainEngine = CreateEngine(plain);
        plainEngine.Execute("host.LongProp = 8589934592;");
        plain.LongProp.Should().Be(8589934592L);

        var withConverter = new IsolationHost();
        var engineWithConverter = CreateEngine(withConverter, options => options.SetTypeConverter(e => new LongPinningTypeConverter(e)));
        engineWithConverter.Execute("host.LongProp = 8589934592;");
        withConverter.LongProp.Should().Be(42L);
    }

    [Fact]
    public void ObjectConverterPolicyIsPerEngine_ConverterFirst()
    {
        var host = new IsolationHost { IntProp = 41 };

        var converting = CreateEngine(host, options => options.Interop.ObjectConverters.Add(new PlusOneIntConverter()));
        converting.Evaluate("host.IntProp").Should().Be(42);

        var plain = CreateEngine(host);
        plain.Evaluate("host.IntProp").Should().Be(41);
    }

    [Fact]
    public void ObjectConverterPolicyIsPerEngine_PlainFirst()
    {
        var host = new IsolationHost { IntProp = 41 };

        var plain = CreateEngine(host);
        plain.Evaluate("host.IntProp").Should().Be(41);

        var converting = CreateEngine(host, options => options.Interop.ObjectConverters.Add(new PlusOneIntConverter()));
        converting.Evaluate("host.IntProp").Should().Be(42);
    }

    #endregion

    #region 9. non-public getter

    [Fact]
    public void PropertyWithNonPublicGetter()
    {
        var host = new NonPublicGetterHost();
        var engine = new Engine();
        engine.SetValue("host", host);

        engine.Execute("host.HalfHidden = 9;");
        host.Observe().Should().Be(9);

        engine.Evaluate("host.HalfHidden").Should().Be(9);
    }

    #endregion
}
