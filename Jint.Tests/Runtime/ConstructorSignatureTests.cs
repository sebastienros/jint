using System.Globalization;
using Jint.Runtime.Interop;

namespace Jint.Tests.Runtime;

public class ConstructorSignature
{
    [Fact]
    public void OptionalConstructorParametersShouldBeSupported()
    {
        var engine = new Engine();

        engine.SetValue("A", TypeReference.CreateTypeReference(engine, typeof(A)));

        // ParamArray tests
        Assert.Equal("3", engine.Evaluate("new A(1, 2).Result").AsString());
        Assert.Equal("3", engine.Evaluate("new A(1, 2, null).Result").AsString());
        Assert.Equal("3", engine.Evaluate("new A(1, 2, undefined).Result").AsString());
        Assert.Equal("5", engine.Evaluate("new A(1, 2, null, undefined).Result").AsString());
        Assert.Equal("9", engine.Evaluate("new A(1, 2, ...'packed').Result").AsString());
        Assert.Equal("3", engine.Evaluate("new A(1, 2, []).Result").AsString());
        Assert.Equal("7", engine.Evaluate("new A(1, 2, [...'abcd']).Result").AsString());

        // Optional parameter tests
        Assert.Equal("3", engine.Evaluate("new A(1, 2).Result").AsString());
        Assert.Equal("6", engine.Evaluate("new A(1).Result").AsString());
        Assert.Equal("7", engine.Evaluate("new A(2, undefined).Result").AsString());
        Assert.Equal("8", engine.Evaluate("new A(3, undefined).Result").AsString());
        Assert.Equal("ab", engine.Evaluate("new A('a').Result").AsString());
        Assert.Equal("ab", engine.Evaluate("new A('a', undefined).Result").AsString());
        Assert.Equal("ac", engine.Evaluate("new A('a', 'c').Result").AsString());
        Assert.Equal("adc", engine.Evaluate("new A('a', 'd', undefined).Result").AsString());
        Assert.Equal("ade", engine.Evaluate("new A('a', 'd', 'e').Result").AsString());
    }

    [Fact]
    public void CorrectOverloadShouldBeSelected()
    {
        var engine = new Engine();
        engine.SetValue("B", typeof(B));

        Assert.Equal("A-30", engine.Evaluate("new B('A', 30).Result"));
    }


    [Fact]
    public void CanConstructWithMixedFloatAndEnumConstructorParameters()
    {
        var engine = new Engine();
        engine.SetValue("Length", TypeReference.CreateTypeReference<Length>(engine));
        Assert.Equal(12.3, engine.Evaluate("new Length(12.3).Value").AsNumber(), precision: 2);
        Assert.Equal(12.3, engine.Evaluate("new Length(12.3, 0).Value").AsNumber(), precision: 2);
        Assert.Equal(0, engine.Evaluate("new Length(12.3, 0).UnitValue").AsInteger());
        Assert.Equal(LengthUnit.Pixel, (LengthUnit) engine.Evaluate("new Length(12.3, 42).UnitValue").AsInteger());
    }

    private class A
    {
        public A(int param1, int param2 = 5) => Result = (param1 + param2).ToString();
        public A(string param1, string param2 = "b") => Result = string.Concat(param1, param2);
        public A(string param1, string param2 = "b", string param3 = "c") => Result = string.Concat(param1, param2, param3);
        public A(int param1, int param2, params object[] param3) => Result = (param1 + param2 + param3?.Length).ToString();

        public string Result { get; }
    }

    private class B
    {
        public B(string param1, float param2, string param3)
        {
            Result = string.Join("-", param1, param2.ToString(CultureInfo.InvariantCulture), param3);
        }

        public B(string param1, float param2)
        {
            Result = string.Join("-", param1, param2.ToString(CultureInfo.InvariantCulture));
        }

        public string Result { get; }
    }

    private enum LengthUnit { Pixel = 42 }

    private class Length
    {
        public Length(float value)
            : this(value, LengthUnit.Pixel)
        {
        }

        public Length(float value, LengthUnit unit)
        {
            Value = value;
            UnitValue = unit;
        }

        public float Value { get; }
        public LengthUnit UnitValue { get; }
    }
}
