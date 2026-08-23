using Jint.Native;

namespace Jint.Tests.Runtime.ExtensionMethods;

/// <summary>
/// A member that resolves purely to registered extension methods must not shadow a same-named
/// Array.prototype native on an indexed array-like wrapper (https://github.com/sebastienros/jint/issues/2976).
/// Real CLR instance members keep beating the prototype, and extension methods keep winning on
/// receivers the index-driven natives cannot serve.
/// </summary>
public class ExtensionMethodShadowingTests
{
    private static Engine CreateEngine(Action<Options> configure = null)
    {
        return new Engine(options =>
        {
            options.AddExtensionMethods(typeof(ShadowingMapExtensions));
            configure?.Invoke(options);
        });
    }

    [Theory]
    [InlineData(ArrayConversionMode.LiveView)]
    [InlineData(ArrayConversionMode.Copy)]
    public void ArrayPrototypeMapWinsOverExtensionOnWrappedArray(ArrayConversionMode mode)
    {
        var engine = CreateEngine(options => options.Interop.ArrayConversion = mode);
        engine.SetValue("coll", new[] { "Hello", "World" });

        engine.Evaluate("Array.isArray(coll.map(x => x))").AsBoolean().Should().BeTrue();
        engine.Evaluate("coll.map(x => x).includes('Hello')").AsBoolean().Should().BeTrue();
    }

    [Fact]
    public void ArrayPrototypeMapWinsOverExtensionOnWrappedList()
    {
        var engine = CreateEngine();
        engine.SetValue("coll", new List<string> { "Hello", "World" });

        engine.Evaluate("Array.isArray(coll.map(x => x))").AsBoolean().Should().BeTrue();
        engine.Evaluate("coll.map(x => x).includes('Hello')").AsBoolean().Should().BeTrue();
    }

    [Fact]
    public void ExtensionMethodStillReachableThroughItsClrCasing()
    {
        var engine = CreateEngine();
        engine.SetValue("coll", new List<string> { "Hello", "World" });

        // 'Map' has no exact-cased counterpart among Array.prototype's own keys, so the extension
        // method binds; its result is a wrapped lazy enumerable, not a JS array
        engine.Evaluate("Array.isArray(coll.Map(x => x))").AsBoolean().Should().BeFalse();
        engine.Evaluate("typeof coll.Map(x => x).includes").AsString().Should().Be("undefined");
        engine.Evaluate("Array.from(coll.Map(x => x + '!')).join()").AsString().Should().Be("Hello!,World!");
    }

    [Fact]
    public void ExtensionMethodStillAppliesToPlainEnumerable()
    {
        var engine = CreateEngine();
        IEnumerable<string> lazy = new List<string> { "Hello", "World" }.Where(x => x.Length > 0);
        engine.SetValue("coll", lazy);

        // no Array.prototype here, the extension method is the only provider of 'map'
        engine.Evaluate("typeof coll.map").AsString().Should().Be("function");
        engine.Evaluate("Array.from(coll.map(x => x + '!')).join()").AsString().Should().Be("Hello!,World!");
    }

    [Fact]
    public void ExtensionMethodStillAppliesToNonIndexableArrayLike()
    {
        var engine = CreateEngine();
        engine.SetValue("coll", new HashSet<string> { "Hello", "World" });

        // HashSet<T> is array-like enough to carry Array.prototype (it has a Count), but it has
        // no indexer for the native map to read - the registered extension must keep winning here
        engine.Evaluate("Object.getPrototypeOf(coll) === Array.prototype").AsBoolean().Should().BeTrue();
        engine.Evaluate("Array.isArray(coll.map(x => x))").AsBoolean().Should().BeFalse();
        engine.Evaluate("Array.from(coll.map(x => x + '!')).sort().join()").AsString().Should().Be("Hello!,World!");
    }

    [Fact]
    public void ClrInstanceMethodStillWinsWhenMixedWithExtensions()
    {
        var engine = new Engine(options => options.AddExtensionMethods(typeof(Enumerable)));
        var list = new List<int> { 1, 2, 3 };
        engine.SetValue("list", list);

        // List<T>.Reverse is a real instance method and Enumerable.Reverse a registered extension;
        // a mixed candidate set keeps CLR-first behavior: void return (null), reversal in place
        engine.Evaluate("list.reverse()").Should().Be(JsValue.Null);
        list.Should().Equal(3, 2, 1);
    }

    [Fact]
    public void DeferredNameIsInheritedNotOwn()
    {
        var engine = CreateEngine();
        engine.SetValue("coll", new List<string> { "Hello", "World" });

        engine.Evaluate("coll.hasOwnProperty('map')").AsBoolean().Should().BeFalse();
        engine.Evaluate("'map' in coll").AsBoolean().Should().BeTrue();
        engine.Evaluate("Object.keys(coll).every(x => !isNaN(parseInt(x)))").AsBoolean().Should().BeTrue();

        // sloppy-mode assignment does not shadow the deferred native
        engine.Evaluate("coll.map = function() { return 1; }; Array.isArray(coll.map(x => x))").AsBoolean().Should().BeTrue();
    }

    [Fact]
    public void PreferJsPrototypeMethodsUnaffected()
    {
        var engine = new Engine(options =>
        {
            options.AddExtensionMethods(typeof(Enumerable));
            options.Interop.PreferJsPrototypeMethods = true;
            options.Interop.AllowWrite = true;
        });
        engine.SetValue("list", new List<int> { 1, 2, 3 });

        engine.Evaluate("list.reverse() === list").AsBoolean().Should().BeTrue();
    }

    [Fact]
    public void LinqChainWorksUnderExplicitLiveView()
    {
        var engine = new Engine(options =>
        {
            options.AddExtensionMethods(typeof(Enumerable));
            options.Interop.ArrayConversion = ArrayConversionMode.LiveView;
        });
        engine.SetValue("stringList", new List<string> { "working", "linq" });

        // the ToArray() result is a live array wrapper whose pure-extension 'Enumerable.Join'
        // candidate defers to native Array.prototype.join
        var result = engine.Evaluate("stringList.Select((x) => x + 'a').ToArray().join()").AsString();
        result.Should().Be("workinga,linqa");
    }
}

public static class ShadowingMapExtensions
{
    // mirrors the lower-case registration from https://github.com/sebastienros/jint/issues/2976
    public static IEnumerable<TResult> map<T, TResult>(this IEnumerable<T> enumerable, Func<T, TResult> selector)
    {
        return enumerable.Select(selector);
    }
}
