#nullable enable
using Jint.Native;

namespace Jint.Tests.Runtime;

/// <summary>
/// Pins the CLR-enum to <see cref="JsValue"/> conversion across every underlying-type route.
/// An enum is an <see cref="IConvertible"/> whose <see cref="IConvertible.GetTypeCode"/> reports the
/// underlying type, so the conversion is served by the shared convertible lane in
/// <c>DefaultObjectConverter</c> rather than by any enum-specific handling.
/// </summary>
public class InteropEnumConversionTests
{
    private enum PlainEnum
    {
        Zero,
        One,
        Big = int.MaxValue,
        Negative = -5,
    }

    [Flags]
    private enum FlagsEnum
    {
        None = 0,
        A = 1,
        B = 2,
        C = 4,
    }

    private enum ByteEnum : byte
    {
        Zero,
        One,
        Max = byte.MaxValue,
    }

    private enum SByteEnum : sbyte
    {
        Min = sbyte.MinValue,
        Zero = 0,
        Max = sbyte.MaxValue,
    }

    private enum ShortEnum : short
    {
        Min = short.MinValue,
        Max = short.MaxValue,
    }

    private enum UShortEnum : ushort
    {
        Zero,
        Max = ushort.MaxValue,
    }

    private enum UIntEnum : uint
    {
        Zero,
        Max = uint.MaxValue,
    }

    private enum LongEnum : long
    {
        Min = long.MinValue,
        Zero = 0,
        Max = long.MaxValue,
    }

    private enum ULongEnum : ulong
    {
        Zero,
        Max = ulong.MaxValue,
    }

    private sealed class Host
    {
        public PlainEnum PlainProperty { get; set; } = PlainEnum.One;
        public ByteEnum ByteField = ByteEnum.Max;
        public LongEnum? NullableProperty { get; set; } = LongEnum.Max;
        public LongEnum? NullProperty { get; set; }

        public FlagsEnum ReturnsFlags() => FlagsEnum.A | FlagsEnum.C;
        public ULongEnum ReturnsULong() => ULongEnum.Max;
        public UIntEnum? ReturnsNullableUInt() => UIntEnum.Max;
    }

    public static TestCases<object, double> EnumValues() => new()
    {
        { PlainEnum.Zero, 0 },
        { PlainEnum.One, 1 },
        { PlainEnum.Big, int.MaxValue },
        { PlainEnum.Negative, -5 },
        { FlagsEnum.None, 0 },
        { FlagsEnum.A | FlagsEnum.B, 3 },
        { FlagsEnum.A | FlagsEnum.B | FlagsEnum.C, 7 },
        { ByteEnum.Zero, 0 },
        { ByteEnum.Max, byte.MaxValue },
        { SByteEnum.Min, sbyte.MinValue },
        { SByteEnum.Max, sbyte.MaxValue },
        { ShortEnum.Min, short.MinValue },
        { ShortEnum.Max, short.MaxValue },
        { UShortEnum.Max, ushort.MaxValue },
        { UIntEnum.Max, uint.MaxValue },
        { LongEnum.Min, long.MinValue },
        { LongEnum.Max, long.MaxValue },
        { ULongEnum.Max, ulong.MaxValue },
    };

    [TestCaseSource(nameof(EnumValues))]
    public void FromObjectConvertsEnumToNumber(object enumValue, double expected)
    {
        var engine = new Engine();

        var converted = JsValue.FromObject(engine, enumValue);

        converted.IsNumber().Should().BeTrue();
        converted.AsNumber().Should().Be(expected);
    }

    [TestCaseSource(nameof(EnumValues))]
    public void NullableEnumConvertsLikeItsUnderlyingValue(object enumValue, double expected)
    {
        var engine = new Engine();

        // a boxed Nullable<TEnum> with a value boxes as the enum itself, so it takes the same route
        var nullable = Activator.CreateInstance(typeof(Nullable<>).MakeGenericType(enumValue.GetType()), enumValue);

        var converted = JsValue.FromObject(engine, nullable);

        converted.IsNumber().Should().BeTrue();
        converted.AsNumber().Should().Be(expected);
    }

    [Test]
    public void NullNullableEnumConvertsToNull()
    {
        var engine = new Engine();
        LongEnum? value = null;

        JsValue.FromObject(engine, value).Should().Be(JsValue.Null);
    }

    [Test]
    public void EnumMembersAndReturnValuesConvertToNumbers()
    {
        var engine = new Engine();
        engine.SetValue("host", new Host());

        engine.Evaluate("host.PlainProperty").AsNumber().Should().Be(1);
        engine.Evaluate("host.ByteField").AsNumber().Should().Be(byte.MaxValue);
        engine.Evaluate("host.NullableProperty").AsNumber().Should().Be(long.MaxValue);
        engine.Evaluate("host.NullProperty").Should().Be(JsValue.Null);
        engine.Evaluate("host.ReturnsFlags()").AsNumber().Should().Be(5);
        engine.Evaluate("host.ReturnsULong()").AsNumber().Should().Be(ulong.MaxValue);
        engine.Evaluate("host.ReturnsNullableUInt()").AsNumber().Should().Be(uint.MaxValue);
    }

    [Test]
    public void EnumInsideCollectionsConvertsToNumbers()
    {
        var engine = new Engine();
        engine.SetValue("values", new object[] { PlainEnum.One, ByteEnum.Max, ULongEnum.Max });

        engine.Evaluate("values[0]").AsNumber().Should().Be(1);
        engine.Evaluate("values[1]").AsNumber().Should().Be(byte.MaxValue);
        engine.Evaluate("values[2]").AsNumber().Should().Be(ulong.MaxValue);
    }

    [Test]
    public void EnumExposedAsObjectTypeStillConvertsToNumber()
    {
        var engine = new Engine();

        // an exposed type of object resolves the CLR type from the value itself
        JsValue.FromObjectWithType(engine, PlainEnum.Big, typeof(object)).AsNumber().Should().Be(int.MaxValue);
        JsValue.FromObjectWithType(engine, PlainEnum.Big, typeof(PlainEnum)).AsNumber().Should().Be(int.MaxValue);
        JsValue.FromObjectWithType(engine, PlainEnum.Big, typeof(Enum)).AsNumber().Should().Be(int.MaxValue);
        JsValue.FromObjectWithType(engine, LongEnum.Max, typeof(LongEnum?)).AsNumber().Should().Be(long.MaxValue);
    }
}
