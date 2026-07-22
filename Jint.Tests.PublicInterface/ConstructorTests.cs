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
        date.Year.Should().Be(1982);
        date.Month.Should().Be(6);
        date.Day.Should().Be(28);

        _engine.Evaluate("new DateOnly(1982, 6, 28).year").AsNumber().Should().Be(1982);
        _engine.Evaluate("new DateOnly(1982, 6, 28).month").AsNumber().Should().Be(6);
        _engine.Evaluate("new DateOnly(1982, 6, 28).day").AsNumber().Should().Be(28);
    }

    [Fact]
    public void CanConstructWithoutParameters()
    {
        _engine.Evaluate("new DateOnly().year").AsNumber().Should().Be(DateTime.Today.Year);
        _engine.Evaluate("new DateOnly().month").AsNumber().Should().Be(DateTime.Today.Month);
        _engine.Evaluate("new DateOnly().day").AsNumber().Should().Be(DateTime.Today.Day);
    }

    [Fact]
    public void CallThrows()
    {
        var ex = Invoking(() => _engine.Evaluate("DateOnly(1982, 6, 28);")).Should().ThrowExactly<JavaScriptException>().Which;
        ex.Message.Should().Be("Constructor DateOnly requires 'new'");
    }

    [Fact]
    public void CanThrowTypeError()
    {
        var ex = Invoking(() => _engine.Evaluate("new DateOnly(undefined, 1, 1);")).Should().ThrowExactly<JavaScriptException>().Which;
        ex.Message.Should().Be("Invalid year NaN");
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
