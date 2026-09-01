using System.Collections.Generic;
using System.Reflection;
using Jint.Native;
using Jint.Runtime;
using Jint.Runtime.Interop;

namespace Jint.Tests.PublicInterface;

/// <summary>
/// A member filter that rejects an indexer must actually hide indexed access — on a plain wrapped object
/// and on a wrapped collection alike, and in every lane: reads, existence, writes, growth, deletion and the
/// <c>Array.prototype</c> generics an array-like view attracts.
/// </summary>
/// <remarks>
/// <see cref="TypeResolver.MemberFilter"/> is CLR-containment configuration: it is how a host says which
/// members script may reach. Every engine below sets <c>AllowWrite = true</c> — which is this branch's
/// default and is spelled out anyway, because that is the configuration the containment question is
/// actually asked in: with writes off, a write is refused for a reason that has nothing to do with the
/// filter and proves nothing about it (#3558).
/// </remarks>
public class HostIndexerFilterTests
{
    private sealed class IndexedHost
    {
        public int this[int index] => index * 2;

        public string Name => "host";
    }

    /// <summary>
    /// The filter the reproduction uses: everything except a property that takes index parameters.
    /// </summary>
    private static bool ExcludesIndexers(MemberInfo member)
        => member is not PropertyInfo property || property.GetIndexParameters().Length == 0;

    private static Engine BuildEngine(bool allowIndexer)
    {
        var resolver = allowIndexer
            ? new TypeResolver()
            : new TypeResolver { MemberFilter = ExcludesIndexers };

        var engine = new Engine(options =>
        {
            options.Interop.TypeResolver = resolver;
            options.Interop.AllowWrite = true;
        });
        engine.SetValue("host", new IndexedHost());
        return engine;
    }

    private static (Engine Engine, List<long> List) BuildListEngine(bool allowIndexer)
    {
        var engine = BuildEngine(allowIndexer);
        var list = new List<long> { 1, 2, 3 };
        engine.SetValue("list", list);
        return (engine, list);
    }

    [Fact]
    public void AMemberFilterExcludingTheIndexerHidesIndexedReads()
    {
        var engine = BuildEngine(allowIndexer: false);

        engine.Evaluate("host[1]").Should().Be(JsValue.Undefined);
        engine.Evaluate("host[0]").Should().Be(JsValue.Undefined);
    }

    [Fact]
    public void AMemberFilterExcludingTheIndexerKeepsNamedMembersReachable()
    {
        var engine = BuildEngine(allowIndexer: false);

        engine.Evaluate("host.Name").AsString().Should().Be("host");
    }

    [Fact]
    public void TheDefaultConfigurationServesTheIndexer()
    {
        var engine = BuildEngine(allowIndexer: true);

        engine.Evaluate("host[3]").AsNumber().Should().Be(6);
    }

    [Fact]
    public void AMemberFilterExcludingTheIndexerBlocksIndexedWrites()
    {
        var (engine, list) = BuildListEngine(allowIndexer: false);

        engine.Evaluate("list[0] = 42;");

        list[0].Should().Be(1, "a filter-excluded indexer must not be written through");
    }

    [Fact]
    public void AMemberFilterExcludingTheIndexerBlocksTheStringSpellingOfAnIndexedWrite()
    {
        var (engine, list) = BuildListEngine(allowIndexer: false);

        engine.Evaluate("list['0'] = 42;");

        list[0].Should().Be(1, "x[0] and x['0'] are one property key, so one filter decision answers both");
    }

    [Fact]
    public void AMemberFilterExcludingTheIndexerBlocksGrowth()
    {
        var (engine, list) = BuildListEngine(allowIndexer: false);

        engine.Evaluate("list[3] = 42;");

        list.Should().HaveCount(3, "a write past the end reaches the collection through the same hidden indexer");
    }

    [Fact]
    public void AMemberFilterExcludingTheIndexerRefusesAnIndexedWriteInStrictMode()
    {
        var (engine, list) = BuildListEngine(allowIndexer: false);

        Invoking(() => engine.Evaluate("'use strict'; list[0] = 42;"))
            .Should().Throw<JavaScriptException>("a refused [[Set]] is a TypeError in strict mode");

        list[0].Should().Be(1);
    }

    [Fact]
    public void AMemberFilterExcludingTheIndexerHidesCollectionElements()
    {
        var (engine, _) = BuildListEngine(allowIndexer: false);

        engine.Evaluate("list[0]").Should().Be(JsValue.Undefined);
        engine.Evaluate("list['0']").Should().Be(JsValue.Undefined);
    }

    [Fact]
    public void AMemberFilterExcludingTheIndexerLeavesNoElementProperties()
    {
        var (engine, _) = BuildListEngine(allowIndexer: false);

        // "in" is defined in terms of [[GetOwnProperty]], so these three may not disagree
        engine.Evaluate("0 in list").AsBoolean().Should().BeFalse();
        engine.Evaluate("list.hasOwnProperty(0)").AsBoolean().Should().BeFalse();
        engine.Evaluate("Object.keys(list).length").AsNumber().Should().Be(0);
    }

    [Fact]
    public void AMemberFilterExcludingTheIndexerLeavesNothingToDelete()
    {
        var (engine, list) = BuildListEngine(allowIndexer: false);

        engine.Evaluate("delete list[0]").AsBoolean().Should().BeTrue("deleting an absent property succeeds");

        list[0].Should().Be(1, "and it must not reach the collection to zero the slot");
    }

    [Fact]
    public void AMemberFilterExcludingTheIndexerBlocksTheArrayPrototypeGenerics()
    {
        var (engine, list) = BuildListEngine(allowIndexer: false);

        Invoking(() => engine.Evaluate("Array.prototype.push.call(list, 9);"))
            .Should().Throw<JavaScriptException>("push writes through the element lane the filter closed");

        list.Should().Equal(1, 2, 3);
    }

    [Fact]
    public void AMemberFilterExcludingTheIndexerBlocksASortFromReachingTheCollection()
    {
        var engine = BuildEngine(allowIndexer: false);
        var list = new List<long> { 3, 1, 2 };
        engine.SetValue("list", list);

        try
        {
            engine.Evaluate("Array.prototype.sort.call(list);");
        }
        catch (JavaScriptException)
        {
            // a script-level refusal is the shape a hidden element lane owes a mutating generic
        }

        list.Should().Equal(new long[] { 3, 1, 2 }, "sort reorders through the element lane the filter closed");
    }

    [Fact]
    public void AFixedSizeArrayIsCoveredByTheSameDecision()
    {
        // LiveView so that the array crosses as a wrapper: it is this branch's default, and is spelled out
        // because the decision only has anything to answer for a wrapper. Under ArrayConversionMode.Copy the
        // array becomes a JsArray before any member is accessed, and a copy is a conversion of the value
        // rather than an access to a member, so no member filter speaks for it.
        var resolver = new TypeResolver { MemberFilter = ExcludesIndexers };
        var engine = new Engine(options =>
        {
            options.Interop.TypeResolver = resolver;
            options.Interop.AllowWrite = true;
            options.Interop.ArrayConversion = ArrayConversionMode.LiveView;
        });

        var array = new long[] { 1, 2, 3 };
        engine.SetValue("array", array);

        engine.Evaluate("array[0]").Should().Be(JsValue.Undefined);
        engine.Evaluate("array[0] = 42;");

        array[0].Should().Be(1, "a CLR array declares no indexer of its own, so the decision is the one its IList indexer gets");
    }

    [Fact]
    public void AReadOnlyExposureIsCoveredByTheSameDecision()
    {
        var engine = BuildEngine(allowIndexer: false);
        IReadOnlyList<long> view = new List<long> { 1, 2, 3 };
        engine.SetValue("view", view);

        engine.Evaluate("view[0]").Should().Be(JsValue.Undefined);
    }

    [Fact]
    public void TheDefaultConfigurationServesCollectionElements()
    {
        var (engine, list) = BuildListEngine(allowIndexer: true);

        engine.Evaluate("list[0]").AsNumber().Should().Be(1);
        engine.Evaluate("0 in list").AsBoolean().Should().BeTrue();

        engine.Evaluate("list[0] = 42;");
        list[0].Should().Be(42, "nothing here may cost an unfiltered engine its element lane");

        engine.Evaluate("list[3] = 7;");
        list.Should().Equal(42, 2, 3, 7);

        engine.Evaluate("Array.prototype.push.call(list, 9);");
        list.Should().Equal(42, 2, 3, 7, 9);
    }

    /// <summary>
    /// Containment and writability are separate facts, and this branch is where that can be seen: writes are
    /// on by default here, so a filtered engine that also turns them off must still hide the elements rather
    /// than merely refuse to change them. On <c>main</c>, where <c>AllowWrite</c> defaults to
    /// <see langword="false"/>, this is the configuration every write assertion above would silently have
    /// been making, which is why they all say <c>AllowWrite = true</c>.
    /// </summary>
    [Fact]
    public void TheContainmentDecisionDoesNotDependOnTheWriteConfiguration()
    {
        var resolver = new TypeResolver { MemberFilter = ExcludesIndexers };
        var engine = new Engine(options =>
        {
            options.Interop.TypeResolver = resolver;
            options.Interop.AllowWrite = false;
        });

        var list = new List<long> { 1, 2, 3 };
        engine.SetValue("list", list);

        engine.Evaluate("list[0]").Should().Be(JsValue.Undefined, "a hidden member reads undefined whether or not writes are on");
        engine.Evaluate("0 in list").AsBoolean().Should().BeFalse();
        engine.Evaluate("list.hasOwnProperty(0)").AsBoolean().Should().BeFalse();
        engine.Evaluate("delete list[0]").AsBoolean().Should().BeTrue("there is no property here to refuse a delete of");

        engine.Evaluate("list[0] = 42;");
        list[0].Should().Be(1);
    }

    /// <summary>
    /// The same, on the configuration an embedder of this branch gets without asking: <c>AllowWrite</c>
    /// defaults to <see langword="true"/> here, so the filter is the only thing standing between script and
    /// the collection.
    /// </summary>
    [Fact]
    public void TheContainmentDecisionHoldsOnThisBranchsDefaultWriteConfiguration()
    {
        var resolver = new TypeResolver { MemberFilter = ExcludesIndexers };
        var allowWrite = false;
        var engine = new Engine(options =>
        {
            options.Interop.TypeResolver = resolver;
            allowWrite = options.Interop.AllowWrite;
        });
        allowWrite.Should().BeTrue("this branch ships writes on, which is what makes the filter load-bearing");

        var list = new List<long> { 1, 2, 3 };
        engine.SetValue("list", list);

        engine.Evaluate("list[0]").Should().Be(JsValue.Undefined);
        engine.Evaluate("list[0] = 42;");
        engine.Evaluate("list[3] = 42;");

        list.Should().Equal(1, 2, 3);
    }

    [Fact]
    public void AFilterThatKeepsTheIndexerKeepsTheElementLane()
    {
        // a filter that rejects something else entirely must not cost the collection its elements
        var resolver = new TypeResolver { MemberFilter = static member => !string.Equals(member.Name, "Capacity", StringComparison.Ordinal) };
        var engine = new Engine(options =>
        {
            options.Interop.TypeResolver = resolver;
            options.Interop.AllowWrite = true;
        });

        var list = new List<long> { 1, 2, 3 };
        engine.SetValue("list", list);

        engine.Evaluate("list[0]").AsNumber().Should().Be(1);
        engine.Evaluate("list[0] = 42;");
        list[0].Should().Be(42);
    }
}
