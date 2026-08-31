#nullable enable

using System.Diagnostics.CodeAnalysis;
using Jint.Runtime.Interop;

namespace Jint.Tests.PublicInterface;

/// <summary>
/// Which operator overload a <c>+</c> over host types selects is resolved once and remembered, and the
/// resolution reads two things the embedder configures: <see cref="Options.InteropOptions.ValueCoercion"/>
/// and the installed <see cref="ClrTypeConverter"/>. Two engines configured differently must therefore not
/// answer one another's operator resolutions.
/// </summary>
/// <remarks>
/// One host type per test — the resolution cache never evicts, so a type reused across tests would make
/// them order-dependent on each other rather than self-contained.
/// </remarks>
public class OperatorOverloadResolutionCacheTests
{
    #region converter-decided resolution

    /// <summary>
    /// A host type whose only <c>+</c> is <c>(T, T)</c>. Evaluating <c>'s' + v</c> therefore asks whether a
    /// string can become a <c>T</c> — a question no structural scoring rule can answer, so the installed
    /// converter answers it and decides between an operator call and plain string concatenation.
    /// </summary>
    public sealed class MoneyA
    {
        public static string operator +(MoneyA left, MoneyA right) => "operator";
    }

    public sealed class MoneyB
    {
        public static string operator +(MoneyB left, MoneyB right) => "operator";
    }

    public sealed class MoneyC
    {
        public static string operator +(MoneyC left, MoneyC right) => "operator";
    }

    public sealed class MoneyD
    {
        public static string operator +(MoneyD left, MoneyD right) => "operator";
    }

    /// <summary>
    /// Converts a string to whatever target type is asked for by handing back that type's default instance,
    /// and defers everything else to the stock conversions. That is the shape of a host converter that
    /// teaches the engine one of its own types — and it is what makes <c>'s' + v</c> resolve to the operator.
    /// </summary>
    private sealed class StringToHostTypeConverter : DefaultTypeConverter
    {
        public StringToHostTypeConverter(Engine engine) : base(engine)
        {
        }

        public override bool TryConvert(object? value, Type type, IFormatProvider formatProvider, [NotNullWhen(true)] out object? converted)
        {
            if (value is string && (type == typeof(MoneyA) || type == typeof(MoneyB) || type == typeof(MoneyC) || type == typeof(MoneyD)))
            {
                converted = Activator.CreateInstance(type)!;
                return true;
            }

            return base.TryConvert(value, type, formatProvider, out converted);
        }
    }

    private static Engine StockEngine() => new(options => options.Interop.AllowOperatorOverloading = true);

    private static Engine ConverterEngine() => new(options =>
    {
        options.Interop.AllowOperatorOverloading = true;
        options.SetTypeConverter(engine => new StringToHostTypeConverter(engine));
    });

    private static string Add(Engine engine, object host)
    {
        engine.SetValue("v", host);
        return engine.Evaluate("'s' + v").AsString();
    }

    [Test]
    public void StockEngineAloneConcatenates()
    {
        Add(StockEngine(), new MoneyA()).Should().NotBe("operator");
    }

    [Test]
    public void ConverterEngineAloneCallsTheOperator()
    {
        Add(ConverterEngine(), new MoneyB()).Should().Be("operator");
    }

    [Test]
    public void AConverterEngineDoesNotDecideForAStockEngine()
    {
        Add(ConverterEngine(), new MoneyC()).Should().Be("operator");
        Add(StockEngine(), new MoneyC()).Should().NotBe("operator");
    }

    [Test]
    public void AStockEngineDoesNotDecideForAConverterEngine()
    {
        Add(StockEngine(), new MoneyD()).Should().NotBe("operator");
        Add(ConverterEngine(), new MoneyD()).Should().Be("operator");
    }

    #endregion

    #region coercion-decided resolution

    /// <summary>
    /// A host type whose only <c>+</c> takes a <see cref="string"/> on the right. Handing it an opaque host
    /// object is a pair no structural rule recognizes, so whether the candidate survives at all is decided
    /// by <see cref="Options.InteropOptions.ValueCoercion"/>'s string rule.
    /// </summary>
    public sealed class CoercedA
    {
        public static string operator +(CoercedA left, string right) => "operator";
    }

    public sealed class CoercedB
    {
        public static string operator +(CoercedB left, string right) => "operator";
    }

    public sealed class CoercedC
    {
        public static string operator +(CoercedC left, string right) => "operator";
    }

    public sealed class CoercedD
    {
        public static string operator +(CoercedD left, string right) => "operator";
    }

    /// <summary>Carries nothing the engine can convert, so only the coercion rule can bind it to a string.</summary>
    public sealed class Opaque;

    private static Engine CoercingEngine() => new(options => options.Interop.AllowOperatorOverloading = true);

    private static Engine NonCoercingEngine() => new(options =>
    {
        options.Interop.AllowOperatorOverloading = true;
        options.Interop.ValueCoercion = ValueCoercionType.None;
    });

    private static string AddOpaque(Engine engine, object host)
    {
        engine.SetValue("v", host);
        engine.SetValue("o", new Opaque());
        return engine.Evaluate("v + o").AsString();
    }

    [Test]
    public void CoercingEngineAloneCallsTheOperator()
    {
        AddOpaque(CoercingEngine(), new CoercedA()).Should().Be("operator");
    }

    [Test]
    public void NonCoercingEngineAloneConcatenates()
    {
        AddOpaque(NonCoercingEngine(), new CoercedB()).Should().NotBe("operator");
    }

    [Test]
    public void ACoercingEngineDoesNotDecideForANonCoercingOne()
    {
        AddOpaque(CoercingEngine(), new CoercedC()).Should().Be("operator");
        AddOpaque(NonCoercingEngine(), new CoercedC()).Should().NotBe("operator");
    }

    [Test]
    public void ANonCoercingEngineDoesNotDecideForACoercingOne()
    {
        AddOpaque(NonCoercingEngine(), new CoercedD()).Should().NotBe("operator");
        AddOpaque(CoercingEngine(), new CoercedD()).Should().Be("operator");
    }

    #endregion
}
