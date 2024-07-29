using Jint.Native;
using Jint.Runtime;
using Xunit.Abstractions;

namespace Jint.Tests.Runtime;

public class TypeConverterTests
{
    private readonly Engine _engine;

    public TypeConverterTests(ITestOutputHelper output)
    {
        _engine = new Engine()
                .SetValue("log", new Action<object>(o => output.WriteLine(o.ToString())))
                .SetValue("assert", new Action<bool>(Assert.True))
                .SetValue("equal", new Action<object, object>(Assert.Equal))
            ;
    }

    public static readonly IEnumerable<object[]> ConvertNumberToInt32AndUint32TestData = new TheoryData<double, int>()
    {
        { 0.0, 0 },
        { -0.0, 0 },
        { double.Epsilon, 0 },
        { 0.5, 0 },
        { -0.5, 0 },
        { 0.9999999999999999, 0 },
        { 1.0, 1 },
        { 1.5, 1 },
        { 10.0, 10 },
        { -12.3, -12 },
        { 1485772.6, 1485772 },
        { -984737183.8, -984737183 },

        { Math.Pow(2, 31) - 1.0, int.MaxValue },
        { Math.Pow(2, 31) - 0.5, int.MaxValue },
        { Math.Pow(2, 32) - 1.0, -1 },
        { Math.Pow(2, 32) - 0.5, -1 },
        { Math.Pow(2, 32), 0 },
        { -Math.Pow(2, 32), 0 },
        { -Math.Pow(2, 32) - 0.5, 0 },
        { Math.Pow(2, 32) + 1.0, 1 },
        { Math.Pow(2, 45) + 17.56, 17 },
        { Math.Pow(2, 45) - 17.56, -18 },
        { -Math.Pow(2, 45) + 17.56, 18 },
        { Math.Pow(2, 51) + 17.5, 17 },
        { Math.Pow(2, 51) - 17.5, -18 },

        { Math.Pow(2, 53) - 1.0, -1 },
        { -Math.Pow(2, 53) + 1.0, 1 },
        { Math.Pow(2, 53), 0 },
        { -Math.Pow(2, 53), 0 },
        { Math.Pow(2, 53) + 12.0, 12 },
        { -Math.Pow(2, 53) - 12.0, -12 },

        { (Math.Pow(2, 53) - 1.0) * Math.Pow(2, 1), -2 },
        { -(Math.Pow(2, 53) - 1.0) * Math.Pow(2, 3), 8 },
        { -(Math.Pow(2, 53) - 1.0) * Math.Pow(2, 11), 1 << 11 },
        { (Math.Pow(2, 53) - 1.0) * Math.Pow(2, 20), -(1 << 20) },
        { (Math.Pow(2, 53) - 1.0) * Math.Pow(2, 31), int.MinValue },
        { -(Math.Pow(2, 53) - 1.0) * Math.Pow(2, 31), int.MinValue },
        { (Math.Pow(2, 53) - 1.0) * Math.Pow(2, 32), 0 },
        { -(Math.Pow(2, 53) - 1.0) * Math.Pow(2, 32), 0 },
        { (Math.Pow(2, 53) - 1.0) * Math.Pow(2, 36), 0 },

        { double.MaxValue, 0 },
        { double.MinValue, 0 },
        { double.PositiveInfinity, 0 },
        { double.NegativeInfinity, 0 },
        { double.NaN, 0 },
    };

    [Theory]
    [MemberData(nameof(ConvertNumberToInt32AndUint32TestData))]
    public void ConvertNumberToInt32AndUint32(double value, int expectedResult)
    {
        JsValue jsval = value;
        Assert.Equal(expectedResult, TypeConverter.ToInt32(jsval));
        Assert.Equal((uint)expectedResult, TypeConverter.ToUint32(jsval));
    }

    [Fact]
    public void ToPrimitiveShouldEvaluateOnlyOnceDuringInExpression()
    {
        _engine.Execute(@"
                var b = {};
                var bval = 0;
                b[Symbol.toPrimitive] = function(hint) { return bval++; };

                b in {};
                equal(1, bval);
            ");
    }
}