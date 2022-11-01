namespace Jint.Tests.Runtime;

public class DateTests
{
    private readonly Engine _engine;

    public DateTests()
    {
        _engine = new Engine()
            .SetValue("log", new Action<object>(Console.WriteLine))
            .SetValue("assert", new Action<bool>(Assert.True))
            .SetValue("equal", new Action<object, object>(Assert.Equal));
    }

    [Fact]
    public void NaNToString()
    {
        var value = _engine.Evaluate("new Date(NaN).toString();").AsString();
        Assert.Equal("Invalid Date", value);
    }

    [Fact]
    public void NaNToDateString()
    {
        var value = _engine.Evaluate("new Date(NaN).toDateString();").AsString();
        Assert.Equal("Invalid Date", value);
    }

    [Fact]
    public void NaNToTimeString()
    {
        var value = _engine.Evaluate("new Date(NaN).toTimeString();").AsString();
        Assert.Equal("Invalid Date", value);
    }

    [Fact]
    public void NaNToLocaleString()
    {
        var value = _engine.Evaluate("new Date(NaN).toLocaleString();").AsString();
        Assert.Equal("Invalid Date", value);
    }

    [Fact]
    public void NaNToLocaleDateString()
    {
        var value = _engine.Evaluate("new Date(NaN).toLocaleDateString();").AsString();
        Assert.Equal("Invalid Date", value);
    }

    [Fact]
    public void NaNToLocaleTimeString()
    {
        var value = _engine.Evaluate("new Date(NaN).toLocaleTimeString();").AsString();
        Assert.Equal("Invalid Date", value);
    }

    [Fact]
    public void ToJsonFromNaNObject()
    {
        var result = _engine.Evaluate("JSON.stringify({ date: new Date(NaN) });");
        Assert.Equal("{\"date\":null}", result.ToString());
    }

    [Fact]
    public void ValuePrecisionIsIntegral()
    {
        var number = _engine.Evaluate("new Date() / 1").AsNumber();
        Assert.Equal((long) number, number);

        var dateInstance  = _engine.Realm.Intrinsics.Date.Construct(123.456d);
        Assert.Equal((long) dateInstance.DateValue, dateInstance.DateValue);
    }
}
