using Jint.Native;
using Jint.Runtime.Interop;

namespace Jint.Tests.PublicInterface;

/// <summary>
/// A member filter that rejects an indexer must actually hide indexed access —
/// the resolver's default-indexer fast path must consult the filter with the
/// same polarity as the candidate scan below it.
/// </summary>
public class HostIndexerFilterTests
{
    private sealed class IndexedHost
    {
        public int this[int index] => index * 2;

        public string Name => "host";
    }

    private static Engine BuildEngine(bool allowIndexer)
    {
        var resolver = allowIndexer
            ? new TypeResolver()
            : new TypeResolver
            {
                MemberFilter = static member => member is not System.Reflection.PropertyInfo property || property.GetIndexParameters().Length == 0,
            };

        var engine = new Engine(options => options.Interop.TypeResolver = resolver);
        engine.SetValue("host", new IndexedHost());
        return engine;
    }

    [Test]
    public void AMemberFilterExcludingTheIndexerHidesIndexedReads()
    {
        var engine = BuildEngine(allowIndexer: false);

        engine.Evaluate("host[1]").Should().Be(JsValue.Undefined);
        engine.Evaluate("host[0]").Should().Be(JsValue.Undefined);
    }

    [Test]
    public void AMemberFilterExcludingTheIndexerKeepsNamedMembersReachable()
    {
        var engine = BuildEngine(allowIndexer: false);

        engine.Evaluate("host.Name").AsString().Should().Be("host");
    }

    [Test]
    public void TheDefaultConfigurationServesTheIndexer()
    {
        var engine = BuildEngine(allowIndexer: true);

        engine.Evaluate("host[3]").AsNumber().Should().Be(6);
    }

    [Test]
    public void AMemberFilterExcludingTheIndexerBlocksIndexedWrites()
    {
        var engine = BuildEngine(allowIndexer: false);
        var list = new System.Collections.Generic.List<long> { 1, 2, 3 };
        engine.SetValue("list", list);

        try
        {
            engine.Evaluate("list[0] = 42;");
        }
        catch (Jint.Runtime.JavaScriptException)
        {
            // a script-level rejection is fine
        }
        catch (InvalidOperationException)
        {
            // the wrapper's pre-existing surface for writes that resolve to nothing;
            // what this test pins is that the filtered-out indexer is never written through
        }

        list[0].Should().Be(1, "a filter-excluded indexer must not be written through");
    }
}
