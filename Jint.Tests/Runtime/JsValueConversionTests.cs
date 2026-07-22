using Jint.Native;
using Jint.Runtime;

namespace Jint.Tests.Runtime;

public class JsValueConversionTests
{
    private readonly Engine _engine;

    public JsValueConversionTests()
    {
        _engine = new Engine();
    }

    [Fact]
    public void ShouldBeAnArray()
    {
        var value = new JsArray(_engine);
        value.IsBoolean().Should().BeFalse();
        value.IsArray().Should().BeTrue();
        value.IsDate().Should().BeFalse();
        value.IsNull().Should().BeFalse();
        value.IsNumber().Should().BeFalse();
        value.IsObject().Should().BeTrue();
        value.IsPrimitive().Should().BeFalse();
        value.IsRegExp().Should().BeFalse();
        value.IsString().Should().BeFalse();
        value.IsUndefined().Should().BeFalse();

        (value.AsArray() != null).Should().BeTrue();
    }

    [Fact]
    public void ShouldBeABoolean()
    {
        var value = JsBoolean.True;
        value.IsBoolean().Should().BeTrue();
        value.IsArray().Should().BeFalse();
        value.IsDate().Should().BeFalse();
        value.IsNull().Should().BeFalse();
        value.IsNumber().Should().BeFalse();
        value.IsObject().Should().BeFalse();
        value.IsPrimitive().Should().BeTrue();
        value.IsRegExp().Should().BeFalse();
        value.IsString().Should().BeFalse();
        value.IsUndefined().Should().BeFalse();

        value.AsBoolean().Should().BeTrue();
    }

    [Fact]
    public void ShouldBeADate()
    {
        var value = new JsDate(_engine, double.NaN);
        value.IsBoolean().Should().BeFalse();
        value.IsArray().Should().BeFalse();
        value.IsDate().Should().BeTrue();
        value.IsNull().Should().BeFalse();
        value.IsNumber().Should().BeFalse();
        value.IsObject().Should().BeTrue();
        value.IsPrimitive().Should().BeFalse();
        value.IsRegExp().Should().BeFalse();
        value.IsString().Should().BeFalse();
        value.IsUndefined().Should().BeFalse();

        (value.AsDate() != null).Should().BeTrue();
    }

    [Fact]
    public void ShouldBeNull()
    {
        var value = JsValue.Null;
        value.IsBoolean().Should().BeFalse();
        value.IsArray().Should().BeFalse();
        value.IsDate().Should().BeFalse();
        value.IsNull().Should().BeTrue();
        value.IsNumber().Should().BeFalse();
        value.IsObject().Should().BeFalse();
        value.IsPrimitive().Should().BeTrue();
        value.IsRegExp().Should().BeFalse();
        value.IsString().Should().BeFalse();
        value.IsUndefined().Should().BeFalse();
    }

    [Fact]
    public void ShouldBeANumber()
    {
        var value = new JsNumber(2);
        value.IsBoolean().Should().BeFalse();
        value.IsArray().Should().BeFalse();
        value.IsDate().Should().BeFalse();
        value.IsNull().Should().BeFalse();
        value.IsNumber().Should().BeTrue();
        value.AsNumber().Should().Be(2);
        value.IsObject().Should().BeFalse();
        value.IsPrimitive().Should().BeTrue();
        value.IsRegExp().Should().BeFalse();
        value.IsString().Should().BeFalse();
        value.IsUndefined().Should().BeFalse();
    }

    [Fact]
    public void ShouldBeAnObject()
    {
        var value = new JsObject(_engine);
        value.IsBoolean().Should().BeFalse();
        value.IsArray().Should().BeFalse();
        value.IsDate().Should().BeFalse();
        value.IsNull().Should().BeFalse();
        value.IsNumber().Should().BeFalse();
        value.IsObject().Should().BeTrue();
        (value.AsObject() != null).Should().BeTrue();
        value.IsPrimitive().Should().BeFalse();
        value.IsRegExp().Should().BeFalse();
        value.IsString().Should().BeFalse();
        value.IsUndefined().Should().BeFalse();
    }

    [Fact]
    public void ShouldBeARegExp()
    {
        var value = new JsRegExp(_engine);
        value.IsBoolean().Should().BeFalse();
        value.IsArray().Should().BeFalse();
        value.IsDate().Should().BeFalse();
        value.IsNull().Should().BeFalse();
        value.IsNumber().Should().BeFalse();
        value.IsObject().Should().BeTrue();
        value.IsPrimitive().Should().BeFalse();
        value.IsRegExp().Should().BeTrue();
        (value.AsRegExp() != null).Should().BeTrue();
        value.IsString().Should().BeFalse();
        value.IsUndefined().Should().BeFalse();
    }

    [Fact]
    public void ShouldBeAString()
    {
        var value = new JsString("a");
        value.IsBoolean().Should().BeFalse();
        value.IsArray().Should().BeFalse();
        value.IsDate().Should().BeFalse();
        value.IsNull().Should().BeFalse();
        value.IsNumber().Should().BeFalse();
        value.IsObject().Should().BeFalse();
        value.IsPrimitive().Should().BeTrue();
        value.IsRegExp().Should().BeFalse();
        value.IsString().Should().BeTrue();
        value.AsString().Should().Be("a");
        value.IsUndefined().Should().BeFalse();
    }

    [Fact]
    public void ShouldBeUndefined()
    {
        var value = JsValue.Undefined;
        value.IsBoolean().Should().BeFalse();
        value.IsArray().Should().BeFalse();
        value.IsDate().Should().BeFalse();
        value.IsNull().Should().BeFalse();
        value.IsNumber().Should().BeFalse();
        value.IsObject().Should().BeFalse();
        value.IsPrimitive().Should().BeTrue();
        value.IsRegExp().Should().BeFalse();
        value.IsString().Should().BeFalse();
        value.IsUndefined().Should().BeTrue();
    }

    [Fact]
    public void ShouldConvertArrayBuffer()
    {
        var value = _engine.Evaluate("new Uint8Array([102, 111, 111]).buffer");
        value.IsArrayBuffer().Should().BeTrue();
        value.AsArrayBuffer().Should().Equal([102, 111, 111]);
        (value.ToObject() as byte[]).Should().Equal([102, 111, 111]);

        (value as JsArrayBuffer).DetachArrayBuffer();

        value.IsArrayBuffer().Should().BeTrue();
        value.AsArrayBuffer().Should().BeNull();
        Invoking(value.ToObject).Should().ThrowExactly<JavaScriptException>();
        Invoking(JsValue.Undefined.AsArrayBuffer).Should().ThrowExactly<ArgumentException>();
    }

    [Fact]
    public void ShouldConvertDataView()
    {
        var value = _engine.Evaluate("new DataView(new Uint8Array([102, 102, 111, 111, 111]).buffer, 1, 3)");

        value.IsDataView().Should().BeTrue();
        value.AsDataView().Should().Equal([102, 111, 111]);
        (value.ToObject() as byte[]).Should().Equal([102, 111, 111]);

        (value as JsDataView)._viewedArrayBuffer.DetachArrayBuffer();

        value.IsDataView().Should().BeTrue();
        value.AsDataView().Should().BeNull();
        Invoking(value.ToObject).Should().ThrowExactly<JavaScriptException>();
        Invoking(JsValue.Undefined.AsDataView).Should().ThrowExactly<ArgumentException>();
    }
}