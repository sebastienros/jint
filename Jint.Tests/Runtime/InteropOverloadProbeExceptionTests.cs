#nullable enable
using System.Globalization;
using System.Runtime.ExceptionServices;
using Jint.Runtime.Interop;

namespace Jint.Tests.Runtime;

/// <summary>
/// Pins that scoring an overload never throws and catches an exception (#4337). A candidate whose parameter type
/// the scorer has no rule for is probed with <c>Convert.ChangeType</c> twice - by the scorer's own
/// <c>CanChangeType</c> and by <c>TypeConverter.TryConvert</c> after it - and both used to reach it with a value
/// whose types alone already decide the call throws: a host object that is not <see cref="IConvertible"/>, a
/// number, string or boolean asked for a host type, a string that does not parse as the number asked for.
/// </summary>
public class InteropOverloadProbeExceptionTests
{
    public readonly record struct Vec2(double X, double Y);

    public sealed class Entity
    {
        public string Name { get; set; } = "e";
    }

    public enum Color
    {
        Red,
        Green,
    }

    public sealed class Api
    {
        public string Move(Vec2 position) => "vec";

        public string Move(Entity entity) => "entity:" + entity.Name;

        public string Text(Vec2 position) => "vec";

        public string Text(string value) => "string";

        public string Number(Vec2 position) => "vec";

        public string Number(double value) => "double";

        public string Flag(Vec2 position) => "vec";

        public string Flag(bool value) => "bool";

        public string Get(int index) => "index";

        public string Get(string name) => "name";

        public string At(DateTime time) => "time";

        public string At(double seconds) => "seconds";
    }

    [TestCase("api.Move(ent)", "entity:e")]
    [TestCase("api.Text('abc')", "string")]
    [TestCase("api.Number(1.5)", "double")]
    [TestCase("api.Number(5)", "double")]
    [TestCase("api.Flag(true)", "bool")]
    [TestCase("api.Get('sword')", "name")]
    [TestCase("api.Get(2)", "index")]
    [TestCase("api.At(1.5)", "seconds")]
    public void OverloadedCallDoesNotThrowFirstChanceExceptions(string call, string expected)
    {
        var engine = new Engine();
        engine.SetValue("api", new Api());
        engine.SetValue("ent", new Entity());

        var script = Engine.PrepareScript(call);
        Assert.That(engine.Evaluate(script).AsString(), Is.EqualTo(expected));

        var thread = Environment.CurrentManagedThreadId;
        var count = 0;
        void Handler(object? sender, FirstChanceExceptionEventArgs e)
        {
            if (Environment.CurrentManagedThreadId == thread)
            {
                count++;
            }
        }

        AppDomain.CurrentDomain.FirstChanceException += Handler;
        try
        {
            for (var i = 0; i < 100; i++)
            {
                Assert.That(engine.Evaluate(script).AsString(), Is.EqualTo(expected));
            }
        }
        finally
        {
            AppDomain.CurrentDomain.FirstChanceException -= Handler;
        }

        Assert.That(count, Is.EqualTo(0), "scoring an overload must not throw and catch");
    }

    private static readonly object[] Values =
    [
        true, false, 'c', (sbyte) -1, (byte) 1, (short) 2, (ushort) 3, 42, 7u, 8L, 9ul, 1.5f, 1.5d, double.NaN, 300d,
        1.5m, new DateTime(2026, 11, 21), Color.Green, DBNull.Value,
        "", "abc", "a", "12", "-12", "1.5", "1,000", " 42 ", "true", "2026-11-21", "1e400", "300",
        new Vec2(1, 2), new Entity(), new HostConvertible(),
    ];

    private static readonly Type[] TypeCodeTargets =
    [
        typeof(bool), typeof(char), typeof(sbyte), typeof(byte), typeof(short), typeof(ushort), typeof(int),
        typeof(uint), typeof(long), typeof(ulong), typeof(float), typeof(double), typeof(decimal),
        typeof(DateTime), typeof(string),
    ];

    private static readonly Type[] OtherTargets =
    [
        typeof(object), typeof(Enum), typeof(DBNull), typeof(Color), typeof(int?), typeof(IComparable),
        typeof(Guid), typeof(TimeSpan), typeof(Vec2), typeof(Entity),
    ];

    /// <summary>
    /// The refusal is a prediction of <c>Convert.ChangeType</c>, so the call itself is the oracle, asked on every
    /// target framework: a refused pair must throw, and where the prediction claims to be exact it must agree.
    /// </summary>
    [Test]
    public void ChangeTypeIsRefusedOnlyWhereItThrows()
    {
        var falseRefusals = new List<string>();
        var missedRefusals = new List<string>();
        var refused = 0;

        foreach (var value in Values)
        {
            var isBclConvertible = value is IConvertible && Type.GetTypeCode(value.GetType()) != TypeCode.Object;
            foreach (var target in TypeCodeTargets.Concat(OtherTargets))
            {
                var mustFail = InteropHelper.ChangeTypeMustFail(value, target, CultureInfo.InvariantCulture);
                var threw = Throws(value, target);
                var pair = $"{value.GetType().Name} '{value}' -> {target.Name}";

                if (mustFail)
                {
                    refused++;
                    if (!threw)
                    {
                        falseRefusals.Add(pair);
                    }

                    continue;
                }

                // exact where it claims to be: a string asked for a TypeCode type is the target's own Parse, and a
                // BCL convertible asked for a type ChangeType does not convert to itself is Convert.DefaultToType
                var claimsExact = value is string
                    ? TypeCodeTargets.Contains(target)
                    : isBclConvertible && OtherTargets.Contains(target) && target != typeof(object) && target != typeof(Enum);

                if (threw && claimsExact)
                {
                    missedRefusals.Add(pair);
                }
            }
        }

        Assert.That(falseRefusals, Is.Empty, "refused a conversion Convert.ChangeType performs");
        Assert.That(missedRefusals, Is.Empty, "let through a conversion the prediction claims to decide");
        Assert.That(refused, Is.GreaterThan(300), "the matrix must exercise the refusals");
    }

    private static bool Throws(object value, Type target)
    {
        try
        {
            Convert.ChangeType(value, target, CultureInfo.InvariantCulture);
            return false;
        }
        catch (Exception)
        {
            return true;
        }
    }

    /// <summary>A host <see cref="IConvertible"/> answers <c>ToType</c> itself, so nothing about it is predictable.</summary>
    private sealed class HostConvertible : IConvertible
    {
        public TypeCode GetTypeCode() => TypeCode.Object;

        public object ToType(Type conversionType, IFormatProvider? provider)
            => conversionType == typeof(Vec2) ? new Vec2(3, 4) : throw new InvalidCastException();

        public bool ToBoolean(IFormatProvider? provider) => true;

        public byte ToByte(IFormatProvider? provider) => 1;

        public char ToChar(IFormatProvider? provider) => 'h';

        public DateTime ToDateTime(IFormatProvider? provider) => new(2026, 1, 1);

        public decimal ToDecimal(IFormatProvider? provider) => 1m;

        public double ToDouble(IFormatProvider? provider) => 1d;

        public short ToInt16(IFormatProvider? provider) => 1;

        public int ToInt32(IFormatProvider? provider) => 1;

        public long ToInt64(IFormatProvider? provider) => 1;

        public sbyte ToSByte(IFormatProvider? provider) => 1;

        public float ToSingle(IFormatProvider? provider) => 1f;

        public string ToString(IFormatProvider? provider) => "host";

        public ushort ToUInt16(IFormatProvider? provider) => 1;

        public uint ToUInt32(IFormatProvider? provider) => 1;

        public ulong ToUInt64(IFormatProvider? provider) => 1;
    }
}
