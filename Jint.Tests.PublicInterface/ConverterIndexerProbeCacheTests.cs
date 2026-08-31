#nullable enable

using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Jint.Native;
using Jint.Runtime.Interop;

namespace Jint.Tests.PublicInterface;

/// <summary>
/// Two engines whose <see cref="ClrTypeConverter"/>s differ must not answer one another's member
/// resolutions out of the accessor cache every engine on <see cref="TypeResolver.Default"/> shares.
/// </summary>
/// <remarks>
/// <para>
/// The converter decides more than an <c>IndexerAccessor</c>'s baked-in key. It also decides whether a
/// declared property or field is given an <em>indexer to try</em> — and that indexer is probed
/// <b>before</b> the declared member — so a member read can resolve to two different values depending on
/// which engine warmed the cache first.
/// </para>
/// <para>
/// These live in the public-interface suite because they are the embedder's view: two engines in one
/// process, one host type, one member name, and an answer that must not depend on evaluation order.
/// One host type per test — the shared cache never evicts, so a type reused across tests would make them
/// order-dependent on each other rather than self-contained.
/// </para>
/// </remarks>
public class ConverterIndexerProbeCacheTests
{
    /// <summary>
    /// Declines every conversion, including <c>string</c> -&gt; <c>string</c>, which the stock converter
    /// accepts outright. Written against the public abstract base rather than derived from
    /// <see cref="DefaultTypeConverter"/>, which is what a host writing its own conversion policy does.
    /// </summary>
    private sealed class NarrowConverter : ClrTypeConverter
    {
        public override object? Convert(object? value, Type type, IFormatProvider formatProvider)
            => throw new NotSupportedException();

        public override bool TryConvert(object? value, Type type, IFormatProvider formatProvider, [NotNullWhen(true)] out object? converted)
        {
            converted = null;
            return false;
        }
    }

    public sealed class BagA : BagBase;

    public sealed class BagB : BagBase;

    public sealed class BagC : BagBase;

    public sealed class BagD : BagBase;

    /// <summary>
    /// Carries a declared <c>Name</c> property <em>and</em> a string-keyed indexer that answers the same
    /// name differently. The indexer is probed first, so which of the two answers a script sees is exactly
    /// the question the converter decides.
    /// </summary>
    public abstract class BagBase
    {
        private readonly Dictionary<string, string> _entries = new() { ["Name"] = "from-indexer" };

        public string Name => "from-property";

        public string? this[string key] => _entries.TryGetValue(key, out var v) ? v : null;
    }

    private static Engine StockEngine() => new();

    private static Engine ConverterEngine() => new(options => options.SetTypeConverter(_ => new NarrowConverter()));

    [Test]
    public void StockEngineAloneReadsTheIndexer()
    {
        var engine = StockEngine();
        engine.SetValue("bag", new BagA());

        engine.Evaluate("bag.Name").AsString().Should().Be("from-indexer");
    }

    [Test]
    public void ConverterEngineAloneReadsTheProperty()
    {
        var engine = ConverterEngine();
        engine.SetValue("bag", new BagB());

        engine.Evaluate("bag.Name").AsString().Should().Be("from-property");
    }

    [Test]
    public void AConverterEngineDoesNotDecideForAStockEngine()
    {
        var withConverter = ConverterEngine();
        withConverter.SetValue("bag", new BagC());
        withConverter.Evaluate("bag.Name").AsString().Should().Be("from-property");

        var stock = StockEngine();
        stock.SetValue("bag", new BagC());
        stock.Evaluate("bag.Name").AsString().Should().Be("from-indexer");
    }

    [Test]
    public void AStockEngineDoesNotDecideForAConverterEngine()
    {
        var stock = StockEngine();
        stock.SetValue("bag", new BagD());
        stock.Evaluate("bag.Name").AsString().Should().Be("from-indexer");

        var withConverter = ConverterEngine();
        withConverter.SetValue("bag", new BagD());
        withConverter.Evaluate("bag.Name").AsString().Should().Be("from-property");
    }

    #region an indexer reached only through an interface

    public interface IEntries
    {
        string? this[string key] { get; }
    }

    /// <summary>
    /// The indexer is an <em>explicit</em> interface implementation, so the type's own properties do not
    /// report it and only the interface probe finds it. There is no declared member of this name at all, so
    /// the converter's answer decides between the indexer and "no such member" — and "no such member" is a
    /// <c>ConstantValueAccessor</c>, which is not an indexer accessor either.
    /// </summary>
    public abstract class ExplicitBagBase : IEntries
    {
        private readonly Dictionary<string, string> _entries = new() { ["Name"] = "from-indexer" };

        string? IEntries.this[string key] => _entries.TryGetValue(key, out var v) ? v : null;
    }

    public sealed class ExplicitBagA : ExplicitBagBase;

    public sealed class ExplicitBagB : ExplicitBagBase;

    public sealed class ExplicitBagC : ExplicitBagBase;

    [Test]
    public void StockEngineAloneReadsTheInterfaceIndexer()
    {
        var engine = StockEngine();
        engine.SetValue("bag", new ExplicitBagA());

        engine.Evaluate("bag.Name").AsString().Should().Be("from-indexer");
    }

    [Test]
    public void ConverterEngineAloneFindsNoMember()
    {
        var engine = ConverterEngine();
        engine.SetValue("bag", new ExplicitBagB());

        engine.Evaluate("bag.Name").Should().Be(JsValue.Undefined);
    }

    [Test]
    public void AConverterEngineDoesNotDecideForAStockEngineThroughAnInterface()
    {
        var withConverter = ConverterEngine();
        withConverter.SetValue("bag", new ExplicitBagC());
        withConverter.Evaluate("bag.Name").Should().Be(JsValue.Undefined);

        var stock = StockEngine();
        stock.SetValue("bag", new ExplicitBagC());
        stock.Evaluate("bag.Name").AsString().Should().Be("from-indexer");
    }

    #endregion
}
