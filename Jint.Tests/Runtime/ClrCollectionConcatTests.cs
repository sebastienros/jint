using System.Dynamic;

namespace Jint.Tests.Runtime;

/// <summary>
/// <c>Array.prototype.concat</c> spreads a host collection that script sees as an array, as it did in 4.13
/// (<see href="https://github.com/sebastienros/jint/issues/4201">#4201</see>).
/// </summary>
/// <remarks>
/// IsConcatSpreadable (https://tc39.es/ecma262/#sec-isconcatspreadable) asks IsArray when the object has no
/// <c>Symbol.isConcatSpreadable</c>, and a wrapper for a CLR array or list is not an array exotic object, so
/// the answer was false and the wrapper was appended as a single element.
/// </remarks>
public class ClrCollectionConcatTests
{
    private static Engine NewEngine() => new(options => { options.AllowClr(); options.Strict = false; });

    private static Engine NewLiveViewEngine() => new(options =>
    {
        options.AllowClr();
        options.Strict = false;
        options.Interop.ArrayConversion = ArrayConversionMode.LiveView;
    });

    [Fact]
    public void ConcatSpreadsTwoClrArraysOfExpandoObjects()
    {
        var engine = NewEngine();

        var a = new ExpandoObject() as IDictionary<string, object>;
        a.Add("id", Guid.NewGuid());
        var b = new ExpandoObject() as IDictionary<string, object>;
        b.Add("id", Guid.NewGuid());

        engine.SetValue("a", new[] { a });
        engine.SetValue("b", new[] { b });
        engine.Execute("var c = a.concat(b);");

        engine.Evaluate("c.length").AsNumber().Should().Be(2);

        var c = engine.GetValue("c").ToObject() as object[];
        c.Should().NotBeNull();
        c!.Length.Should().Be(2);
        c[0].Should().BeSameAs(a);
        c[1].Should().BeSameAs(b);
    }

    [Fact]
    public void ConcatSpreadsTwoLiveViewClrArraysOfExpandoObjects()
    {
        var engine = NewLiveViewEngine();

        var a = new ExpandoObject() as IDictionary<string, object>;
        a.Add("id", Guid.NewGuid());
        var b = new ExpandoObject() as IDictionary<string, object>;
        b.Add("id", Guid.NewGuid());

        engine.SetValue("a", new[] { a });
        engine.SetValue("b", new[] { b });
        engine.Execute("var c = a.concat(b);");

        engine.Evaluate("c.length").AsNumber().Should().Be(2);

        var c = engine.GetValue("c").ToObject() as object[];
        c.Should().NotBeNull();
        c!.Length.Should().Be(2);
        c[0].Should().BeSameAs(a);
        c[1].Should().BeSameAs(b);
    }

    [Fact]
    public void ConcatSpreadsLiveViewClrArraysOnBothSides()
    {
        var engine = NewLiveViewEngine();
        engine.SetValue("a", new object[] { 1, 2 });
        engine.SetValue("b", new object[] { 3, "x" });

        engine.Evaluate("a.concat(b).join(',')").AsString().Should().Be("1,2,3,x");
        engine.Evaluate("[0].concat(a, b).join(',')").AsString().Should().Be("0,1,2,3,x");
        engine.Evaluate("a.concat([9], 8).join(',')").AsString().Should().Be("1,2,9,8");
    }

    [Fact]
    public void ConcatSpreadsClrObjectArrays()
    {
        var engine = NewEngine();
        engine.SetValue("a", new object[] { 1, 2 });
        engine.SetValue("b", new object[] { 3, "x" });

        engine.Evaluate("a.concat(b).length").AsNumber().Should().Be(4);
        engine.Evaluate("a.concat(b).join(',')").AsString().Should().Be("1,2,3,x");
    }

    [Fact]
    public void ConcatSpreadsGenericLists()
    {
        var engine = NewEngine();
        engine.SetValue("a", new List<int> { 1, 2 });
        engine.SetValue("b", new List<int> { 3 });

        engine.Evaluate("a.concat(b).length").AsNumber().Should().Be(3);
        engine.Evaluate("a.concat(b).join(',')").AsString().Should().Be("1,2,3");
    }

    [Fact]
    public void ConcatSpreadsAClrArrayPassedToAnArray()
    {
        var engine = NewEngine();
        engine.SetValue("b", new object[] { 3, 4 });

        engine.Evaluate("[1, 2].concat(b).join(',')").AsString().Should().Be("1,2,3,4");
        engine.Evaluate("[1, 2].concat(b, [5]).length").AsNumber().Should().Be(5);
    }

    [Fact]
    public void ConcatDoesNotSpreadACollectionWithoutIndexedElements()
    {
        var engine = NewEngine();
        engine.SetValue("q", new Queue<int>([1, 2]));

        // a Queue<T> has a Count but no element at index 0, so it is not something script sees as an array
        engine.Evaluate("[0].concat(q).length").AsNumber().Should().Be(2);
    }
}
