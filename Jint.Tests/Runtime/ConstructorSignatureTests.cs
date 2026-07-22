using System.Globalization;
using Jint.Runtime.Interop;

namespace Jint.Tests.Runtime;

public class ConstructorSignatureTests
{
    [Fact]
    public void OptionalConstructorParametersShouldBeSupported()
    {
        var engine = new Engine();

        engine.SetValue("A", TypeReference.CreateTypeReference(engine, typeof(A)));

        // ParamArray tests
        engine.Evaluate("new A(1, 2).Result").AsString().Should().Be("3");
        engine.Evaluate("new A(1, 2, null).Result").AsString().Should().Be("3");
        engine.Evaluate("new A(1, 2, undefined).Result").AsString().Should().Be("3");
        engine.Evaluate("new A(1, 2, null, undefined).Result").AsString().Should().Be("5");
        engine.Evaluate("new A(1, 2, ...'packed').Result").AsString().Should().Be("9");
        engine.Evaluate("new A(1, 2, []).Result").AsString().Should().Be("3");
        engine.Evaluate("new A(1, 2, [...'abcd']).Result").AsString().Should().Be("7");

        // Optional parameter tests
        engine.Evaluate("new A(1, 2).Result").AsString().Should().Be("3");
        engine.Evaluate("new A(1).Result").AsString().Should().Be("6");
        engine.Evaluate("new A(2, undefined).Result").AsString().Should().Be("7");
        engine.Evaluate("new A(3, undefined).Result").AsString().Should().Be("8");
        engine.Evaluate("new A('a').Result").AsString().Should().Be("ab");
        engine.Evaluate("new A('a', undefined).Result").AsString().Should().Be("ab");
        engine.Evaluate("new A('a', 'c').Result").AsString().Should().Be("ac");
        engine.Evaluate("new A('a', 'd', undefined).Result").AsString().Should().Be("adc");
        engine.Evaluate("new A('a', 'd', 'e').Result").AsString().Should().Be("ade");
    }

    [Fact]
    public void CorrectOverloadShouldBeSelected()
    {
        var engine = new Engine();
        engine.SetValue("B", typeof(B));

        engine.Evaluate("new B('A', 30).Result").Should().Be("A-30");
    }


    [Fact]
    public void CanConstructWithMixedFloatAndEnumConstructorParameters()
    {
        var engine = new Engine();
        engine.SetValue("Length", TypeReference.CreateTypeReference<Length>(engine));
        engine.Evaluate("new Length(12.3).Value").AsNumber().Should().BeApproximately(12.3, 0.005);
        engine.Evaluate("new Length(12.3, 0).Value").AsNumber().Should().BeApproximately(12.3, 0.005);
        engine.Evaluate("new Length(12.3, 0).UnitValue").AsInteger().Should().Be(0);
        ((LengthUnit) engine.Evaluate("new Length(12.3, 42).UnitValue").AsInteger()).Should().Be(LengthUnit.Pixel);
    }

    [Fact]
    public void ShouldBeAbleToIgnoreDefaultParameters()
    {
        var engine = new Engine();
        engine.SetValue("C", TypeReference.CreateTypeReference(engine, typeof(C)));

        // Should not throw an error
        engine.Evaluate("new C(1, 2)");
        engine.Evaluate("new C(1, 2, 'context')");
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

    private class C
    {
        public C(int tileCoordsX, int tileCoordsY, string context = null)
        {
        }
    }
}
