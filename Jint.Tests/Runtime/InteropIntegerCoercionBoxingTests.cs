#nullable enable
using Jint.Extensions;
using Jint.Native;
using Jint.Runtime.Interop;

namespace Jint.Tests.Runtime;

/// <summary>
/// A JavaScript integer bound to a CLR member, parameter or collection element declared as
/// <see cref="long"/> must be boxed as a <see cref="long"/>, not as an <see cref="int"/> that merely
/// carries the right numeric value. Reflection binders widen a boxed <see cref="int"/> to a
/// <see cref="long"/> member or parameter silently, so the difference is invisible on those paths,
/// but a consumer that unboxes directly — an element write into a wrapped <c>IList&lt;long&gt;</c> or
/// <c>long[]</c> — rejects it with an <see cref="InvalidCastException"/>.
/// </summary>
public class InteropIntegerCoercionBoxingTests
{
    private static Engine CreateEngine() => new(options => options.Interop.AllowWrite = true);

    #region hosts

    public sealed class Host
    {
        public long LongField;
        public int IntField;

        public long LongProperty { get; set; }
        public int IntProperty { get; set; }

        public Type? LastParameterType;

        public void TakeLong(long value) => LastParameterType = ((object) value).GetType();

        public void TakeInt(int value) => LastParameterType = ((object) value).GetType();
    }

    public sealed class CollectionHost
    {
        public List<long> Longs { get; } = [0L, 0L];
        public List<int> Ints { get; } = [0, 0];
        public long[] LongArray { get; } = new long[2];
        public int[] IntArray { get; } = new int[2];
    }

    public delegate void LongCallback(long value);

    public delegate void IntCallback(int value);

    /// <summary>
    /// A converter that is not <see cref="DefaultTypeConverter"/> itself, which is what turns the
    /// compiled member-write fast lane off.
    /// </summary>
    private sealed class CustomTypeConverter : DefaultTypeConverter
    {
        public CustomTypeConverter(Engine engine) : base(engine)
        {
        }
    }

    #endregion

    /// <summary>
    /// The coercion helper is the origin of the boxed value; assert its runtime type directly,
    /// because every downstream consumer that widens hides a wrong one.
    /// </summary>
    [Fact]
    public void CoercionBoxesIntegerAsTheDeclaredMemberType()
    {
        Assert.True(ReflectionExtensions.TryConvertViaTypeCoercion(typeof(long), ValueCoercionType.None, JsNumber.Create(42), out var asLong));
        asLong.Should().BeOfType<long>().And.Be(42L);

        Assert.True(ReflectionExtensions.TryConvertViaTypeCoercion(typeof(int), ValueCoercionType.None, JsNumber.Create(42), out var asInt));
        asInt.Should().BeOfType<int>().And.Be(42);
    }

    /// <summary>
    /// <see cref="Nullable{T}"/> member types are declined by the helper (they are neither
    /// <c>typeof(int)</c>/<c>typeof(long)</c> nor CLR-numeric-coercible) and keep converting through
    /// the general <c>ToObject</c> + type-converter path.
    /// </summary>
    [Fact]
    public void CoercionDeclinesNullableIntegerMemberTypes()
    {
        Assert.False(ReflectionExtensions.TryConvertViaTypeCoercion(typeof(long?), ValueCoercionType.All, JsNumber.Create(42), out _));
        Assert.False(ReflectionExtensions.TryConvertViaTypeCoercion(typeof(int?), ValueCoercionType.All, JsNumber.Create(42), out _));
    }

    [Fact]
    public void CanAssignIntegerToLongField()
    {
        var host = new Host();
        var engine = CreateEngine().SetValue("host", host);

        engine.Execute("host.LongField = 42;");
        host.LongField.Should().Be(42L);

        engine.Execute("host.IntField = 42;");
        host.IntField.Should().Be(42);
    }

    [Fact]
    public void CanAssignIntegerToLongProperty()
    {
        var host = new Host();
        var engine = CreateEngine().SetValue("host", host);

        engine.Execute("host.LongProperty = 42;");
        host.LongProperty.Should().Be(42L);

        engine.Execute("host.IntProperty = 42;");
        host.IntProperty.Should().Be(42);
    }

    [Fact]
    public void CanPassIntegerToLongParameter()
    {
        var host = new Host();
        var engine = CreateEngine().SetValue("host", host);

        engine.Execute("host.TakeLong(42);");
        host.LastParameterType.Should().Be(typeof(long));

        engine.Execute("host.TakeInt(42);");
        host.LastParameterType.Should().Be(typeof(int));
    }

    [Fact]
    public void CanPassIntegerToLongDelegateParameter()
    {
        Type? observed = null;
        var engine = CreateEngine()
            .SetValue("takeLong", new LongCallback(value => observed = ((object) value).GetType()))
            .SetValue("takeInt", new IntCallback(value => observed = ((object) value).GetType()));

        engine.Execute("takeLong(42);");
        observed.Should().Be(typeof(long));

        engine.Execute("takeInt(42);");
        observed.Should().Be(typeof(int));
    }

    [Fact]
    public void CanAssignIntegerToGenericListOfLongElement()
    {
        var host = new CollectionHost();
        var engine = CreateEngine().SetValue("host", host);

        engine.Execute("host.Longs[0] = 42;");
        host.Longs[0].Should().Be(42L);

        engine.Execute("host.Ints[0] = 42;");
        host.Ints[0].Should().Be(42);
    }

    [Fact]
    public void CanAppendIntegerToGenericListOfLong()
    {
        var host = new CollectionHost();
        var engine = CreateEngine().SetValue("host", host);

        engine.Execute("host.Longs.push(42);");
        host.Longs[2].Should().Be(42L);

        engine.Execute("host.Ints.push(42);");
        host.Ints[2].Should().Be(42);
    }

    /// <summary>
    /// Array.prototype operations write elements back through the same element-conversion path as a
    /// direct index assignment on a fixed-size view, so they need the element type just as much.
    /// </summary>
    [Fact]
    public void CanFillGenericListOfLong()
    {
        var host = new CollectionHost();
        var engine = CreateEngine().SetValue("host", host);

        engine.Execute("host.Longs.fill(42);");
        host.Longs.Should().Equal(42L, 42L);

        engine.Execute("host.Ints.fill(42);");
        host.Ints.Should().Equal(42, 42);
    }

    [Fact]
    public void CanAssignIntegerToLongArrayElement()
    {
        var host = new CollectionHost();
        var engine = new Engine(options =>
            {
                options.Interop.AllowWrite = true;
                options.Interop.ArrayConversion = ArrayConversionMode.LiveView;
            })
            .SetValue("host", host);

        engine.Execute("host.LongArray[0] = 42;");
        host.LongArray[0].Should().Be(42L);

        engine.Execute("host.IntArray[0] = 42;");
        host.IntArray[0].Should().Be(42);
    }

    /// <summary>
    /// A host-installed <see cref="ClrTypeConverter"/> turns off the compiled member-write fast lane,
    /// so the field and property writes below really do take the coercion path (and then the
    /// reflection setter, which widens either way).
    /// </summary>
    [Fact]
    public void CanAssignIntegerToLongMemberWithCustomTypeConverter()
    {
        var host = new Host();
        var engine = new Engine(options =>
            {
                options.Interop.AllowWrite = true;
                options.SetTypeConverter(e => new CustomTypeConverter(e));
            })
            .SetValue("host", host);

        engine.Execute("host.LongField = 42; host.LongProperty = 43; host.IntField = 44; host.IntProperty = 45;");

        host.LongField.Should().Be(42L);
        host.LongProperty.Should().Be(43L);
        host.IntField.Should().Be(44);
        host.IntProperty.Should().Be(45);
    }
}
