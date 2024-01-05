using System.Globalization;
using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Interop;

namespace Jint.Tests.PublicInterface;

public class ConstructorTests
{
    private readonly Engine _engine;

    public ConstructorTests()
    {
        _engine = new Engine();
        _engine.SetValue("DateOnly", new DateOnlyConstructor(_engine));
    }

    [Fact]
    public void CanConstructWithParameters()
    {
        var wrapper = (IObjectWrapper) _engine.Evaluate("new DateOnly(1982, 6, 28);");
        var date = (DateOnly) wrapper.Target;
        Assert.Equal(1982, date.Year);
        Assert.Equal(6, date.Month);
        Assert.Equal(28, date.Day);

        Assert.Equal(1982, _engine.Evaluate("new DateOnly(1982, 6, 28).year").AsNumber());
        Assert.Equal(6, _engine.Evaluate("new DateOnly(1982, 6, 28).month").AsNumber());
        Assert.Equal(28, _engine.Evaluate("new DateOnly(1982, 6, 28).day").AsNumber());
    }

    [Fact]
    public void CanConstructWithoutParameters()
    {
        Assert.Equal(DateTime.Today.Year, _engine.Evaluate("new DateOnly().year").AsNumber());
        Assert.Equal(DateTime.Today.Month, _engine.Evaluate("new DateOnly().month").AsNumber());
        Assert.Equal(DateTime.Today.Day, _engine.Evaluate("new DateOnly().day").AsNumber());
    }

    [Fact]
    public void CallThrows()
    {
        var ex = Assert.Throws<JavaScriptException>(() => _engine.Evaluate("DateOnly(1982, 6, 28);"));
        Assert.Equal("Constructor DateOnly requires 'new'", ex.Message);
    }

    [Fact]
    public void CanThrowTypeError()
    {
        var ex = Assert.Throws<JavaScriptException>(() => _engine.Evaluate("new DateOnly(undefined, 1, 1);"));
        Assert.Equal("Invalid year NaN", ex.Message);
    }
}

file sealed class DateOnlyConstructor : Constructor
{
    public DateOnlyConstructor(Engine engine) : base(engine, "DateOnly")
    {
    }

    public override ObjectInstance Construct(JsValue[] arguments, JsValue newTarget)
    {
        DateOnly result;
        if (arguments.Length == 0)
        {
            result = new DateOnly(DateTime.Today);
        }
        else if (arguments.Length == 1)
        {
            var days = TypeConverter.ToNumber(arguments[0]);
            var ticks = (long) (days * TimeSpan.TicksPerDay);
            result = new DateOnly(new DateTime(ticks));
        }
        else
        {
            var year = TypeConverter.ToNumber(arguments.At(0));
            var month = TypeConverter.ToNumber(arguments.At(1)) - 1;
            var day = arguments.Length == 2 ? 0 : TypeConverter.ToNumber(arguments[2]) - 1;
            if (double.IsNaN(year))
            {
                throw new JavaScriptException(_engine.Intrinsics.TypeError, $"Invalid year {year.ToString(CultureInfo.InvariantCulture)}");
            }

            if (double.IsNaN(month))
            {
                throw new JavaScriptException(_engine.Intrinsics.TypeError, $"Invalid month {month.ToString(CultureInfo.InvariantCulture)}");
            }

            if (double.IsNaN(day))
            {
                throw new JavaScriptException(_engine.Intrinsics.TypeError, $"Invalid day {day.ToString(CultureInfo.InvariantCulture)}");
            }

            var dt = new DateTime((int) year, 1, 1);
            dt = dt.AddMonths((int) month);
            dt = dt.AddDays((int) day);

            result = new DateOnly(dt);
        }

        return (ObjectInstance) FromObject(_engine, result);
    }
}

file sealed class DateOnly
{
    internal DateOnly(DateTime value)
    {
        Year = value.Year;
        Month = value.Month;
        Day = value.Day;
    }

    public JsValue Year { get; set; }

    public JsValue Month { get; set; }

    public JsValue Day { get; set; }
}
