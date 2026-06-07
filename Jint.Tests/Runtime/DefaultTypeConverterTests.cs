#nullable enable

using System.Diagnostics.CodeAnalysis;
using System.Dynamic;
using System.Globalization;
using Jint.Runtime;
using Jint.Runtime.Interop;
using Jint.Tests.Runtime.Domain;

namespace Jint.Tests.Runtime;

public class DefaultTypeConverterTests
{
    private record Point(int X, int Y);

    // Mirrors the scenario from https://github.com/sebastienros/jint/issues/2495 - overriding only
    // TryConvert should be enough to intercept conversions that are triggered via Convert.
    private sealed class PointTypeConverter(Engine engine) : DefaultTypeConverter(engine)
    {
        public override bool TryConvert(object? value, Type type, IFormatProvider formatProvider, [NotNullWhen(true)] out object? converted)
        {
            if (type == typeof(Point) && value is IDictionary<string, object?> d)
            {
                converted = new Point(
                    X: System.Convert.ToInt32(d["x"], formatProvider),
                    Y: System.Convert.ToInt32(d["y"], formatProvider));
                return true;
            }

            return base.TryConvert(value, type, formatProvider, out converted);
        }
    }

    private static Engine CreateEngine() => new(options => options.SetTypeConverter(e => new PointTypeConverter(e)));

    [Fact]
    public void ShouldUseOverriddenTryConvertWhenConvertingDelegateArguments()
    {
        var engine = CreateEngine();

        Point? received = null;
        engine.SetValue("process", new Action<Point>(p => received = p));

        engine.Execute("process({ x: 10, y: 20 });");

        Assert.Equal(new Point(10, 20), received);
    }

    [Fact]
    public void ShouldUseOverriddenTryConvertForNestedElementConversions()
    {
        var engine = CreateEngine();

        Point[]? receivedArray = null;
        List<Point>? receivedList = null;
        engine.SetValue("processArray", new Action<Point[]>(p => receivedArray = p));
        engine.SetValue("processList", new Action<List<Point>>(p => receivedList = p));

        engine.Execute("processArray([{ x: 1, y: 2 }, { x: 3, y: 4 }]);");
        engine.Execute("processList([{ x: 5, y: 6 }]);");

        Assert.Equal(new[] { new Point(1, 2), new Point(3, 4) }, receivedArray);
        Assert.Equal(new[] { new Point(5, 6) }, receivedList!);
    }

    [Fact]
    public void ShouldUseOverriddenTryConvertWhenConvertCalledDirectly()
    {
        var converter = new PointTypeConverter(new Engine());

        IDictionary<string, object?> dict = new ExpandoObject();
        dict["x"] = 1;
        dict["y"] = 2;

        var point = Assert.IsType<Point>(converter.Convert(dict, typeof(Point), CultureInfo.InvariantCulture));
        Assert.Equal(new Point(1, 2), point);

        // built-in conversions still work
        Assert.Equal(42, converter.Convert("42", typeof(int), CultureInfo.InvariantCulture));
    }

    [Fact]
    public void ShouldReportDetailedErrorWhenOverriddenTryConvertCannotConvert()
    {
        var engine = new Engine(options => options
            .SetTypeConverter(e => new PointTypeConverter(e))
            .CatchClrExceptions());

        engine.SetValue("a", new Person());

        var ex = Assert.Throws<JavaScriptException>(() => engine.Execute("a.age = 'not a number'"));
        Assert.Contains("input string", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(" was not in a correct format", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ShouldPropagateConversionExceptionWhenClrExceptionsAreNotCaught()
    {
        var engine = CreateEngine();

        engine.SetValue("callInt", new Action<int>(_ => { }));

        Assert.Throws<FormatException>(() => engine.Execute("callInt('not a number')"));
    }
}
