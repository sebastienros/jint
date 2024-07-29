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
        Assert.Equal(false, value.IsBoolean());
        Assert.Equal(true, value.IsArray());
        Assert.Equal(false, value.IsDate());
        Assert.Equal(false, value.IsNull());
        Assert.Equal(false, value.IsNumber());
        Assert.Equal(true, value.IsObject());
        Assert.Equal(false, value.IsPrimitive());
        Assert.Equal(false, value.IsRegExp());
        Assert.Equal(false, value.IsString());
        Assert.Equal(false, value.IsUndefined());

        Assert.Equal(true, value.AsArray() != null);
    }

    [Fact]
    public void ShouldBeABoolean()
    {
        var value = JsBoolean.True;
        Assert.Equal(true, value.IsBoolean());
        Assert.Equal(false, value.IsArray());
        Assert.Equal(false, value.IsDate());
        Assert.Equal(false, value.IsNull());
        Assert.Equal(false, value.IsNumber());
        Assert.Equal(false, value.IsObject());
        Assert.Equal(true, value.IsPrimitive());
        Assert.Equal(false, value.IsRegExp());
        Assert.Equal(false, value.IsString());
        Assert.Equal(false, value.IsUndefined());

        Assert.Equal(true, value.AsBoolean());
    }

    [Fact]
    public void ShouldBeADate()
    {
        var value = new JsDate(_engine, double.NaN);
        Assert.Equal(false, value.IsBoolean());
        Assert.Equal(false, value.IsArray());
        Assert.Equal(true, value.IsDate());
        Assert.Equal(false, value.IsNull());
        Assert.Equal(false, value.IsNumber());
        Assert.Equal(true, value.IsObject());
        Assert.Equal(false, value.IsPrimitive());
        Assert.Equal(false, value.IsRegExp());
        Assert.Equal(false, value.IsString());
        Assert.Equal(false, value.IsUndefined());

        Assert.Equal(true, value.AsDate() != null);
    }

    [Fact]
    public void ShouldBeNull()
    {
        var value = JsValue.Null;
        Assert.Equal(false, value.IsBoolean());
        Assert.Equal(false, value.IsArray());
        Assert.Equal(false, value.IsDate());
        Assert.Equal(true, value.IsNull());
        Assert.Equal(false, value.IsNumber());
        Assert.Equal(false, value.IsObject());
        Assert.Equal(true, value.IsPrimitive());
        Assert.Equal(false, value.IsRegExp());
        Assert.Equal(false, value.IsString());
        Assert.Equal(false, value.IsUndefined());
    }

    [Fact]
    public void ShouldBeANumber()
    {
        var value = new JsNumber(2);
        Assert.Equal(false, value.IsBoolean());
        Assert.Equal(false, value.IsArray());
        Assert.Equal(false, value.IsDate());
        Assert.Equal(false, value.IsNull());
        Assert.Equal(true, value.IsNumber());
        Assert.Equal(2, value.AsNumber());
        Assert.Equal(false, value.IsObject());
        Assert.Equal(true, value.IsPrimitive());
        Assert.Equal(false, value.IsRegExp());
        Assert.Equal(false, value.IsString());
        Assert.Equal(false, value.IsUndefined());
    }

    [Fact]
    public void ShouldBeAnObject()
    {
        var value = new JsObject(_engine);
        Assert.Equal(false, value.IsBoolean());
        Assert.Equal(false, value.IsArray());
        Assert.Equal(false, value.IsDate());
        Assert.Equal(false, value.IsNull());
        Assert.Equal(false, value.IsNumber());
        Assert.Equal(true, value.IsObject());
        Assert.Equal(true, value.AsObject() != null);
        Assert.Equal(false, value.IsPrimitive());
        Assert.Equal(false, value.IsRegExp());
        Assert.Equal(false, value.IsString());
        Assert.Equal(false, value.IsUndefined());
    }

    [Fact]
    public void ShouldBeARegExp()
    {
        var value = new JsRegExp(_engine);
        Assert.Equal(false, value.IsBoolean());
        Assert.Equal(false, value.IsArray());
        Assert.Equal(false, value.IsDate());
        Assert.Equal(false, value.IsNull());
        Assert.Equal(false, value.IsNumber());
        Assert.Equal(true, value.IsObject());
        Assert.Equal(false, value.IsPrimitive());
        Assert.Equal(true, value.IsRegExp());
        Assert.Equal(true, value.AsRegExp() != null);
        Assert.Equal(false, value.IsString());
        Assert.Equal(false, value.IsUndefined());
    }

    [Fact]
    public void ShouldBeAString()
    {
        var value = new JsString("a");
        Assert.Equal(false, value.IsBoolean());
        Assert.Equal(false, value.IsArray());
        Assert.Equal(false, value.IsDate());
        Assert.Equal(false, value.IsNull());
        Assert.Equal(false, value.IsNumber());
        Assert.Equal(false, value.IsObject());
        Assert.Equal(true, value.IsPrimitive());
        Assert.Equal(false, value.IsRegExp());
        Assert.Equal(true, value.IsString());
        Assert.Equal("a", value.AsString());
        Assert.Equal(false, value.IsUndefined());
    }

    [Fact]
    public void ShouldBeUndefined()
    {
        var value = JsValue.Undefined;
        Assert.Equal(false, value.IsBoolean());
        Assert.Equal(false, value.IsArray());
        Assert.Equal(false, value.IsDate());
        Assert.Equal(false, value.IsNull());
        Assert.Equal(false, value.IsNumber());
        Assert.Equal(false, value.IsObject());
        Assert.Equal(true, value.IsPrimitive());
        Assert.Equal(false, value.IsRegExp());
        Assert.Equal(false, value.IsString());
        Assert.Equal(true, value.IsUndefined());
    }

    [Fact]
    public void ShouldConvertArrayBuffer()
    {
        var value = _engine.Evaluate("new Uint8Array([102, 111, 111]).buffer");
        Assert.Equal(true, value.IsArrayBuffer());
        Assert.Equal([102, 111, 111], value.AsArrayBuffer());
        Assert.Equal([102, 111, 111], value.ToObject() as byte[]);

        (value as JsArrayBuffer).DetachArrayBuffer();

        Assert.Equal(true, value.IsArrayBuffer());
        Assert.Equal(null, value.AsArrayBuffer());
        Assert.Throws<JavaScriptException>(value.ToObject);
        Assert.Throws<ArgumentException>(JsValue.Undefined.AsArrayBuffer);
    }

    [Fact]
    public void ShouldConvertDataView()
    {
        var value = _engine.Evaluate("new DataView(new Uint8Array([102, 102, 111, 111, 111]).buffer, 1, 3)");

        Assert.Equal(true, value.IsDataView());
        Assert.Equal([102, 111, 111], value.AsDataView());
        Assert.Equal([102, 111, 111], value.ToObject() as byte[]);

        (value as JsDataView)._viewedArrayBuffer.DetachArrayBuffer();

        Assert.Equal(true, value.IsDataView());
        Assert.Equal(null, value.AsDataView());
        Assert.Throws<JavaScriptException>(value.ToObject);
        Assert.Throws<ArgumentException>(JsValue.Undefined.AsDataView);
    }
}