#nullable enable

using System.Collections.ObjectModel;
using System.Globalization;
using Jint.Runtime.Interop;

namespace Jint.Tests.Runtime;

/// <summary>
/// <see cref="DefaultTypeConverter.TryConvert"/> declines a composite whose parts cannot be converted, and
/// <see cref="DefaultTypeConverter.Convert"/> still throws for the very same input.
/// </summary>
/// <remarks>
/// The composite branches — <c>List&lt;T&gt;</c>, <c>Collection&lt;T&gt;</c>, <c>T[]</c>, a target dictionary's
/// values and the members of a POCO built from a dictionary — converted their parts through the public,
/// throwing <c>Convert</c> whatever their own frame had been asked, so a <c>Try</c> method documented as
/// returning false threw a CLR exception instead. That is what defeated the candidate loop in
/// <c>MethodInfoFunction.Call</c>, which asks the converter per candidate and is meant to move on when one
/// declines (<see href="https://github.com/sebastienros/jint/issues/3754">#3754</see>).
/// </remarks>
public class CompositeConversionDeclineTests
{
    public sealed class Widget
    {
        public string Name { get; set; } = "widget";
    }

    public sealed class Gadget
    {
        public string Name { get; set; } = "gadget";
    }

    public sealed class Holder
    {
        public Gadget? Part { get; set; }
    }

    private static DefaultTypeConverter NewConverter() => new DefaultTypeConverter(new Engine());

    private static void ShouldDeclineRatherThanThrow(object value, Type type)
    {
        var converter = NewConverter();
        var declined = false;
        object? converted = null;

        Invoking(() => declined = !converter.TryConvert(value, type, CultureInfo.InvariantCulture, out converted))
            .Should().NotThrow();

        declined.Should().BeTrue();
        converted.Should().BeNull();
    }

    private static void ConvertShouldStillThrow(object value, Type type)
    {
        var converter = NewConverter();

        Invoking(() => converter.Convert(value, type, CultureInfo.InvariantCulture))
            .Should().Throw<InvalidCastException>();
    }

    private static object[] ArrayWithUnconvertibleElement() => [new Widget()];

    private static Dictionary<string, object> DictionaryWithUnconvertibleValue() => new() { ["part"] = new Widget() };

    private static Dictionary<string, object> DictionaryForPoco() => new() { ["Part"] = new Widget() };

    [Fact]
    public void AnArrayWithAnUnconvertibleElementDeclines()
    {
        ShouldDeclineRatherThanThrow(ArrayWithUnconvertibleElement(), typeof(Gadget[]));
    }

    [Fact]
    public void AListWithAnUnconvertibleItemDeclines()
    {
        ShouldDeclineRatherThanThrow(ArrayWithUnconvertibleElement(), typeof(List<Gadget>));
    }

    [Fact]
    public void ACollectionWithAnUnconvertibleItemDeclines()
    {
        ShouldDeclineRatherThanThrow(ArrayWithUnconvertibleElement(), typeof(Collection<Gadget>));
    }

    [Fact]
    public void ADictionaryWithAnUnconvertibleValueDeclines()
    {
        ShouldDeclineRatherThanThrow(DictionaryWithUnconvertibleValue(), typeof(Dictionary<string, Gadget>));
    }

    [Fact]
    public void APocoWithAnUnconvertibleMemberDeclines()
    {
        ShouldDeclineRatherThanThrow(DictionaryForPoco(), typeof(Holder));
    }

    [Fact]
    public void ConvertStillThrowsForAnArray()
    {
        ConvertShouldStillThrow(ArrayWithUnconvertibleElement(), typeof(Gadget[]));
    }

    [Fact]
    public void ConvertStillThrowsForAList()
    {
        ConvertShouldStillThrow(ArrayWithUnconvertibleElement(), typeof(List<Gadget>));
    }

    [Fact]
    public void ConvertStillThrowsForACollection()
    {
        ConvertShouldStillThrow(ArrayWithUnconvertibleElement(), typeof(Collection<Gadget>));
    }

    [Fact]
    public void ConvertStillThrowsForADictionary()
    {
        ConvertShouldStillThrow(DictionaryWithUnconvertibleValue(), typeof(Dictionary<string, Gadget>));
    }

    [Fact]
    public void ConvertStillThrowsForAPoco()
    {
        ConvertShouldStillThrow(DictionaryForPoco(), typeof(Holder));
    }

    [Fact]
    public void AConvertibleCompositeStillConverts()
    {
        // The control: nothing about a composite whose parts do convert changes.
        var converter = NewConverter();

        converter.TryConvert(new object[] { 1d, 2d }, typeof(int[]), CultureInfo.InvariantCulture, out var array)
            .Should().BeTrue();
        ((int[]) array!).Should().Equal(1, 2);

        converter.TryConvert(new object[] { 1d, 2d }, typeof(List<int>), CultureInfo.InvariantCulture, out var list)
            .Should().BeTrue();
        ((List<int>) list!).Should().Equal(1, 2);

        converter.TryConvert(new Dictionary<string, object> { ["Part"] = "1" }, typeof(Dictionary<string, int>), CultureInfo.InvariantCulture, out var dictionary)
            .Should().BeTrue();
        ((Dictionary<string, int>) dictionary!)["Part"].Should().Be(1);
    }
}
