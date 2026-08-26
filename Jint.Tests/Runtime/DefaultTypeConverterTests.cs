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

    [Test]
    public void ShouldUseOverriddenTryConvertWhenConvertingDelegateArguments()
    {
        var engine = CreateEngine();

        Point? received = null;
        engine.SetValue("process", new Action<Point>(p => received = p));

        engine.Execute("process({ x: 10, y: 20 });");

        received.Should().Be(new Point(10, 20));
    }

    [Test]
    public void ShouldUseOverriddenTryConvertForNestedElementConversions()
    {
        var engine = CreateEngine();

        Point[]? receivedArray = null;
        List<Point>? receivedList = null;
        engine.SetValue("processArray", new Action<Point[]>(p => receivedArray = p));
        engine.SetValue("processList", new Action<List<Point>>(p => receivedList = p));

        engine.Execute("processArray([{ x: 1, y: 2 }, { x: 3, y: 4 }]);");
        engine.Execute("processList([{ x: 5, y: 6 }]);");

        receivedArray.Should().Equal(new[] { new Point(1, 2), new Point(3, 4) });
        (receivedList!).Should().Equal(new[] { new Point(5, 6) });
    }

    [Test]
    public void ShouldUseOverriddenTryConvertWhenConvertCalledDirectly()
    {
        var converter = new PointTypeConverter(new Engine());

        IDictionary<string, object?> dict = new ExpandoObject();
        dict["x"] = 1;
        dict["y"] = 2;

        var point = converter.Convert(dict, typeof(Point), CultureInfo.InvariantCulture).Should().BeOfType<Point>().Which;
        point.Should().Be(new Point(1, 2));

        // built-in conversions still work
        converter.Convert("42", typeof(int), CultureInfo.InvariantCulture).Should().Be(42);
    }

    [Test]
    public void ShouldReportDetailedErrorWhenOverriddenTryConvertCannotConvert()
    {
        var engine = new Engine(options => options
            .SetTypeConverter(e => new PointTypeConverter(e))
            .CatchClrExceptions()
            .Interop.AllowWrite = true);

        engine.SetValue("a", new Person());

        var ex = Invoking(() => engine.Execute("a.age = 'not a number'")).Should().ThrowExactly<JavaScriptException>().Which;
        ex.Message.Should().ContainEquivalentOf("input string");
        ex.Message.Should().ContainEquivalentOf(" was not in a correct format");
    }

    [Test]
    public void ShouldPropagateConversionExceptionWhenClrExceptionsAreNotCaught()
    {
        var engine = CreateEngine();

        engine.SetValue("callInt", new Action<int>(_ => { }));

        Invoking(() => engine.Execute("callInt('not a number')")).Should().ThrowExactly<FormatException>();
    }

    /// <summary>
    /// The generic pass-through matches on the generic type *definition*, so on its own it would call a
    /// <c>List&lt;object&gt;</c> assignable to <c>IEnumerable&lt;string&gt;</c> and hand it over unconverted -
    /// which then dies inside a reflection invoke as a raw ArgumentException
    /// (https://github.com/sebastienros/jint/issues/2987).
    /// </summary>
    [Test]
    public void ShouldNotPassIncompatibleTypeArgumentsThrough()
    {
        var converter = new DefaultTypeConverter(new Engine());

        converter.TryConvert(new List<object> { "a" }, typeof(IEnumerable<string>), CultureInfo.InvariantCulture, out _)
            .Should().BeFalse();
        converter.TryConvert(new List<int> { 1 }, typeof(IEnumerable<object>), CultureInfo.InvariantCulture, out _)
            .Should().BeFalse("boxing is not variance");
    }

    [Test]
    public void ShouldStillPassGenuinelyAssignableValuesThrough()
    {
        var converter = new DefaultTypeConverter(new Engine());

        // reference-type covariance is real assignability and short-circuits before the generic check
        var source = new List<string> { "a" };
        converter.TryConvert(source, typeof(IEnumerable<object>), CultureInfo.InvariantCulture, out var covariant)
            .Should().BeTrue();
        covariant.Should().BeSameAs(source);

        IDictionary<string, object?> expando = new ExpandoObject();
        converter.TryConvert(expando, typeof(IDictionary<string, object?>), CultureInfo.InvariantCulture, out var same)
            .Should().BeTrue();
        same.Should().BeSameAs(expando);

        // a JS array still materializes into the requested collection type, that branch runs first
        converter.TryConvert(new object?[] { 1d, 2d }, typeof(IList<int>), CultureInfo.InvariantCulture, out var list)
            .Should().BeTrue();
        list.Should().BeOfType<List<int>>().Which.Should().Equal(1, 2);
    }

    /// <summary>
    /// Accepted consequence of the tightened pass-through: a member the value cannot legally be assigned to
    /// used to be converted "successfully" and then silently dropped by ReflectionExtensions.SetValue.
    /// An error is the better answer, but it is a behavior change worth pinning.
    /// </summary>
    [Test]
    public void ObjectLiteralMemberThatCannotBeAssignedNowErrorsInsteadOfBeingDropped()
    {
        var engine = new Engine();
        engine.SetValue("Holder", typeof(SequenceHolder));
        engine.SetValue("objects", new List<object> { "a", "b" });

        // a List<object> is not an IEnumerable<string>, and unlike a JS array it has no element-wise
        // conversion path - it used to be "converted" unchanged and then dropped by the member write
        Invoking(() => engine.Evaluate("Holder.Describe({ Items: objects })"))
            .Should().Throw<Exception>();

        // the assignable shapes still map
        engine.Evaluate("Holder.Describe({ Items: ['a', 'b'] })").AsString().Should().Be("a,b");
        engine.SetValue("strings", new List<string> { "a", "b" });
        engine.Evaluate("Holder.Describe({ Items: strings })").AsString().Should().Be("a,b");
    }

    public class SequenceHolder
    {
        public IEnumerable<string>? Items { get; set; }

        public static string Describe(SequenceHolder holder) => string.Join(",", holder.Items ?? []);
    }
}
