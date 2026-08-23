// Reads a Jint diagnostic API declared outside the compatibility contract; see Jint/JintDiagnosticIds.cs.
#pragma warning disable JINT0001

using Jint.Constraints;
using Jint.Native;
using Jint.Runtime;
using Jint.Runtime.Interop;

namespace Jint.Tests.Runtime;

public partial class InteropTests
{
    public sealed class ArrayHolder
    {
        public int[] Numbers { get; } = [10, 20, 30];
    }

    private static Engine CreateLiveViewEngine(Action<Options> additionalConfiguration = null)
    {
        return new Engine(options =>
        {
            options.AllowClrWrite();
            options.Interop.ArrayConversion = ArrayConversionMode.LiveView;
            additionalConfiguration?.Invoke(options);
        });
    }

    [Fact]
    public void ArrayConversionDefaultsToCopy()
    {
        new Options().Interop.ArrayConversion.Should().Be(ArrayConversionMode.Copy);

        var holder = new ArrayHolder();
        var engine = new Engine();
        engine.SetValue("h", holder);

        // Copy produces a native JsArray snapshot. The default recent-wrapper cache preserves the
        // snapshot's identity and script-side mutations, but the CLR array stays disconnected.
        engine.Evaluate("Array.isArray(h.Numbers)").AsBoolean().Should().BeTrue();
        engine.Evaluate("h.Numbers === h.Numbers").AsBoolean().Should().BeTrue();
        engine.Execute("h.Numbers[0] = 42;");
        holder.Numbers[0].Should().Be(10);
        engine.Evaluate("h.Numbers[0]").AsNumber().Should().Be(42);

        holder.Numbers[1] = 99;
        engine.Evaluate("h.Numbers[1]").AsNumber().Should().Be(20);
    }

    [Fact]
    public void LiveViewElementReadsConvertPrimitiveItemTypes()
    {
        var engine = CreateLiveViewEngine();
        engine.SetValue("ints", new[] { 1, int.MaxValue, -1 });
        engine.SetValue("doubles", new[] { 0.5, double.MaxValue, -0.0 });
        engine.SetValue("longs", new[] { 1L, long.MaxValue });
        engine.SetValue("floats", new[] { 0.1f, float.MaxValue });
        engine.SetValue("bools", new[] { true, false });
        engine.SetValue("bytes", new byte[] { 0, 255 });
        engine.SetValue("chars", new[] { 'a', 'ö' });
        engine.SetValue("strings", new[] { "x", null, "" });
        engine.SetValue("intList", new List<int> { 7, 8 });
        engine.SetValue("stringList", (IReadOnlyList<string>) new List<string> { "a", null });

        engine.Evaluate("ints[1]").AsNumber().Should().Be(int.MaxValue);
        engine.Evaluate("ints[2]").AsNumber().Should().Be(-1);
        engine.Evaluate("doubles[0]").AsNumber().Should().Be(0.5);
        engine.Evaluate("doubles[1]").AsNumber().Should().Be(double.MaxValue);
        engine.Evaluate("Object.is(doubles[2], -0)").AsBoolean().Should().BeTrue();
        engine.Evaluate("longs[1]").AsNumber().Should().Be((double) long.MaxValue);
        engine.Evaluate("floats[0]").AsNumber().Should().Be((double) 0.1f);
        engine.Evaluate("floats[1]").AsNumber().Should().Be((double) float.MaxValue);
        engine.Evaluate("bools[0]").AsBoolean().Should().BeTrue();
        engine.Evaluate("bools[1]").AsBoolean().Should().BeFalse();
        engine.Evaluate("bytes[1]").AsNumber().Should().Be(255);
        engine.Evaluate("chars[1]").AsString().Should().Be("ö");
        engine.Evaluate("strings[0]").AsString().Should().Be("x");
        engine.Evaluate("strings[1] === null").AsBoolean().Should().BeTrue();
        engine.Evaluate("strings[2]").AsString().Should().Be("");
        engine.Evaluate("intList[1]").AsNumber().Should().Be(8);
        engine.Evaluate("stringList[1] === null").AsBoolean().Should().BeTrue();

        // out-of-range and negative indices read like JS array holes
        engine.Evaluate("ints[99] === undefined").AsBoolean().Should().BeTrue();
        engine.Evaluate("ints[-1] === undefined").AsBoolean().Should().BeTrue();
        engine.Evaluate("intList[99] === undefined").AsBoolean().Should().BeTrue();
    }

    [Fact]
    public void LiveViewHonorsNonArrayDeclaredType()
    {
        var engine = CreateLiveViewEngine();
        var host = new ReadOnlyDeclaredArrayHolder();
        engine.SetValue("h", host);

        // reads are live views over the underlying array
        engine.Evaluate("h.Data[1]").AsNumber().Should().Be(2);
        host.Mutate(1, 42);
        engine.Evaluate("h.Data[1]").AsNumber().Should().Be(42);

        // an array exposed under a read-only declared type must not produce a writable view
        engine.Evaluate("h.Data[0] = 99;");
        host.Raw[0].Should().Be(1);
        var ex = Invoking(() => engine.Evaluate("'use strict'; h.Data[0] = 99;")).Should().ThrowExactly<JavaScriptException>().Which;
        ex.Message.Should().ContainEquivalentOf("read only");
        host.Raw[0].Should().Be(1);
    }

    private class ReadOnlyDeclaredArrayHolder
    {
        private readonly int[] _data = [1, 2, 3];
        public IReadOnlyList<int> Data => _data;
        public int[] Raw => _data;
        public void Mutate(int index, int value) => _data[index] = value;
    }

    [Fact]
    public void ArrayUnderNonArrayDeclaredTypeDoesNotPoisonTypeMapper()
    {
        // the static type-mapper memoization must not register the array lane under a non-array
        // declared type: a later non-array value of the same declared type would hit an
        // InvalidCastException — and poison every engine in the process
        var engine = CreateLiveViewEngine();
        engine.SetValue("a", new EnumerableHolder { Data = [1] });
        engine.Evaluate("a.Data[0]").AsNumber().Should().Be(1);

        engine.SetValue("b", new EnumerableHolder { Data = new List<int> { 2 } });
        engine.Evaluate("b.Data[0]").AsNumber().Should().Be(2);
    }

    private class EnumerableHolder
    {
        public IEnumerable<int> Data { get; set; }
    }

    [Fact]
    public void InOperatorMatchesJsArraySemantics()
    {
        var engine = CreateLiveViewEngine();
        engine.SetValue("a", new[] { 1, 2, 3 });
        engine.SetValue("l", new List<int> { 1, 2, 3 });

        engine.Evaluate("0 in a").AsBoolean().Should().BeTrue();
        engine.Evaluate("2 in a").AsBoolean().Should().BeTrue();
        engine.Evaluate("3 in a").AsBoolean().Should().BeFalse();
        engine.Evaluate("-1 in a").AsBoolean().Should().BeFalse();
        engine.Evaluate("'-1' in a").AsBoolean().Should().BeFalse();
        engine.Evaluate("4000000000 in a").AsBoolean().Should().BeFalse();
        engine.Evaluate("'1' in a").AsBoolean().Should().BeTrue();
        engine.Evaluate("'08' in a").AsBoolean().Should().BeFalse();

        engine.Evaluate("0 in l").AsBoolean().Should().BeTrue();
        engine.Evaluate("3 in l").AsBoolean().Should().BeFalse();
        engine.Evaluate("-1 in l").AsBoolean().Should().BeFalse();
    }

    [Fact]
    public void KeyEnumerationYieldsIndexKeys()
    {
        var engine = CreateLiveViewEngine();
        engine.SetValue("a", new[] { 1, 2, 3 });
        engine.SetValue("l", new List<string> { "x", "y" });

        engine.Evaluate("Object.keys(a).join()").AsString().Should().Be("0,1,2");
        engine.Evaluate("Object.keys(l).join()").AsString().Should().Be("0,1");
        engine.Evaluate("var s=[];for(var k in a)s.push(k);s.join()").AsString().Should().Be("0,1,2");
        engine.Evaluate("Object.values(a).join()").AsString().Should().Be("1,2,3");
        engine.Evaluate("JSON.stringify({...a})").AsString().Should().Be("""{"0":1,"1":2,"2":3}""");

        // members stay accessible even though they no longer enumerate
        engine.Evaluate("a.length").AsNumber().Should().Be(3);
    }

    private static Engine CreateCopyEngine(Action<Options> additionalConfiguration = null)
    {
        return new Engine(options =>
        {
            options.Interop.ArrayConversion = ArrayConversionMode.Copy;
            additionalConfiguration?.Invoke(options);
        });
    }

    [Fact]
    public void CopyModeArrayIsJsArraySnapshot()
    {
        // Opting out of the recent-wrapper cache makes each crossing produce an independent snapshot.
        var engine = CreateCopyEngine(options => options.Interop.CacheRecentObjectWrappers = false);
        engine.SetValue("h", new ArrayHolder());
        engine.Evaluate("Array.isArray(h.Numbers)").AsBoolean().Should().BeTrue();
        engine.Evaluate("h.Numbers === h.Numbers").AsBoolean().Should().BeFalse();

        // with the default (cached) Copy engine the same snapshot is reused while cached
        var cachedEngine = CreateCopyEngine();
        cachedEngine.SetValue("h", new ArrayHolder());
        cachedEngine.Evaluate("Array.isArray(h.Numbers)").AsBoolean().Should().BeTrue();
        cachedEngine.Evaluate("h.Numbers === h.Numbers").AsBoolean().Should().BeTrue();
    }

    [Fact]
    public void CopyModeScriptWritesDoNotAffectClrArray()
    {
        var engine = CreateCopyEngine();
        var numbers = new[] { 1, 2, 3 };
        engine.SetValue("arr", numbers);

        engine.Execute("arr[1] = 42;");

        numbers[1].Should().Be(2);
    }

    [Fact]
    public void CopyModeKeepsClrSnapshotsOutsideTheJsArraySizeConstraint()
    {
        var engine = CreateCopyEngine(options => options.MaxArraySize(2));

        engine.SetValue("arr", new[] { 1, 2, 3 });

        engine.Evaluate("arr.length").AsNumber().Should().Be(3);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void CopyModePreservesSelfReferentialArrays(bool cacheRecentWrappers)
    {
        var engine = CreateCopyEngine(options => options.Interop.CacheRecentObjectWrappers = cacheRecentWrappers);
        var source = new object[1];
        source[0] = source;

        engine.SetValue("arr", source);

        engine.Evaluate("Array.isArray(arr) && arr[0] === arr").AsBoolean().Should().BeTrue();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void CopyModePreservesMutuallyReferentialArrays(bool cacheRecentWrappers)
    {
        var engine = CreateCopyEngine(options => options.Interop.CacheRecentObjectWrappers = cacheRecentWrappers);
        var first = new object[1];
        var second = new object[1];
        first[0] = second;
        second[0] = first;

        engine.SetValue("arr", first);

        engine.Evaluate("Array.isArray(arr[0]) && arr[0][0] === arr").AsBoolean().Should().BeTrue();
    }

    [Fact]
    public void CopyModeDoesNotSpliceACachedDescendantFromAnOlderCyclicGraph()
    {
        var first = new object[1];
        var second = new object[1];
        first[0] = second;
        second[0] = first;

        var engine = CreateCopyEngine();
        engine.SetValue("oldSecond", second);
        for (var i = 0; i < 7; i++)
        {
            engine.SetValue($"filler{i}", new ArrayHolder());
        }

        engine.SetValue("newFirst", first);

        engine.Evaluate("newFirst !== oldSecond[0] && newFirst[0][0] === newFirst")
            .AsBoolean().Should().BeTrue();
    }

    [Fact]
    public void ReentrantCopyDoesNotSpliceACachedDescendantFromAnOlderCyclicGraph()
    {
        var first = new object[1];
        var second = new object[1];
        first[0] = second;
        second[0] = first;

        var engine = CreateCopyEngine(options =>
            options.AddObjectConverter(new ReentrantArrayConverter(second)));
        engine.SetValue("oldSecond", second);
        for (var i = 0; i < 7; i++)
        {
            engine.SetValue($"filler{i}", new ArrayHolder());
        }

        first[0] = new ReentrantArrayProbe();
        engine.SetValue("newFirst", first);

        engine.Evaluate("newFirst !== oldSecond[0] && newFirst[0][0] === newFirst")
            .AsBoolean().Should().BeTrue();
    }

    [Fact]
    public void ConverterDependencyDoesNotSpliceACachedDescendantFromAnOlderCyclicGraph()
    {
        var root = new object[1];
        var child = new object[1];
        root[0] = child;
        child[0] = new ConverterCycleProbe();

        var engine = CreateCopyEngine(options =>
            options.AddObjectConverter(new ConverterCycleConverter(root)));
        engine.SetValue("oldChild", child);
        for (var i = 0; i < 7; i++)
        {
            engine.SetValue($"filler{i}", new ArrayHolder());
        }

        engine.SetValue("newRoot", root);

        engine.Evaluate("newRoot !== oldChild[0] && newRoot[0][0] === newRoot")
            .AsBoolean().Should().BeTrue();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CopyModeReusesCachedAcyclicDescendants(bool trackIdentity)
    {
        var child = new object[] { 1 };
        var parent = new object[] { child };
        var engine = CreateCopyEngine(options =>
            options.Interop.TrackObjectWrapperIdentity = trackIdentity);
        engine.SetValue("child", child);
        engine.SetValue("parent", parent);

        engine.Evaluate("child === parent[0]").AsBoolean().Should().BeTrue();
    }

    [Fact]
    public void CachedDescendantProvenanceHandlesMultidimensionalArrays()
    {
        var child = new object[] { new int[2, 2] };
        var engine = CreateCopyEngine(options =>
            options.AddObjectConverter(new MultidimensionalArrayConverter()));
        engine.SetValue("child", child);
        engine.SetValue("parent", new object[] { child });

        engine.Evaluate("child === parent[0]").AsBoolean().Should().BeTrue();
    }

    [Fact]
    public void FailedIdentityTrackedCopyDoesNotPublishSnapshot()
    {
        var deadline = new OperationDeadlineConstraint();
        var converter = new OneTimeSlowConverter();
        var engine = CreateCopyEngine(options =>
        {
            options.Interop.TrackObjectWrapperIdentity = true;
            options.Constraint(deadline);
            options.AddObjectConverter(converter);
        });
        var source = new object[] { new SlowCopyProbe() };

        deadline.Begin(TimeSpan.FromMilliseconds(10));
        Invoking(() => engine.SetValue("failed", source))
            .Should().ThrowExactly<TimeoutException>();
        engine.Advanced.GetInteropConversionDiagnostics().ArrayCopyConversions.Should().Be(0);
        deadline.End();

        engine.SetValue("retry", source);
        engine.Evaluate("retry.length === 1 && retry[0] === 1").AsBoolean().Should().BeTrue();
        engine.Advanced.GetInteropConversionDiagnostics().ArrayCopyConversions.Should().Be(1);
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public void CopyModeAppliesCacheIdentityWithinOneArrayGraph(bool cacheRecentWrappers, bool expectedSame)
    {
        var engine = CreateCopyEngine(options => options.Interop.CacheRecentObjectWrappers = cacheRecentWrappers);
        var child = new object[1];
        var source = new object[] { child, child };

        engine.SetValue("arr", source);

        engine.Evaluate("arr[0] === arr[1]").AsBoolean().Should().Be(expectedSame);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CopyModePreservesBoundedRecentCacheSemanticsWithinOneArrayGraph(bool trackIdentity)
    {
        var child = new object[1];
        var source = new object[10];
        source[0] = child;
        for (var i = 1; i <= 8; i++)
        {
            source[i] = new object[1];
        }
        source[9] = child;

        var engine = CreateCopyEngine(options => options.Interop.TrackObjectWrapperIdentity = trackIdentity);
        engine.SetValue("arr", source);

        engine.Evaluate("arr[0] === arr[9]").AsBoolean().Should().Be(trackIdentity);
    }

    [Fact]
    public void CopyModeSharesRecentCacheEvictionWithNonArrayWrappersWithinOneGraph()
    {
        var child = new object[1];
        var source = new object[10];
        source[0] = child;
        for (var i = 1; i <= 8; i++)
        {
            source[i] = new ArrayHolder();
        }
        source[9] = child;

        var engine = CreateCopyEngine();
        engine.SetValue("arr", source);

        engine.Evaluate("arr[0] === arr[9]").AsBoolean().Should().BeFalse();
    }

    [Fact]
    public void FailedCyclicCopyDoesNotPublishPartialSnapshotsToTheRecentCache()
    {
        var engine = CreateCopyEngine();
        var first = new object[2];
        var second = new object[1];
        first[0] = second;
        first[1] = new int[2, 2];
        second[0] = first;

        Invoking(() => engine.SetValue("first", first)).Should().ThrowExactly<ArgumentException>();
        engine.Advanced.GetInteropConversionDiagnostics().ArrayCopyConversions.Should().Be(0);

        // If the nested snapshot escaped from the failed conversion, this would return that partial
        // graph instead of reaching the still-unsupported multidimensional element and throwing again.
        Invoking(() => engine.SetValue("second", second)).Should().ThrowExactly<ArgumentException>();
        engine.Advanced.GetInteropConversionDiagnostics().ArrayCopyConversions.Should().Be(0);
    }

    [Fact]
    public void FailedCopyRestoresThePreviousRecentCacheTimeline()
    {
        var cached = new object[1];
        var failed = new object[10];
        for (var i = 0; i < failed.Length - 1; i++)
        {
            failed[i] = new ArrayHolder();
        }
        failed[^1] = new int[2, 2];

        var engine = CreateCopyEngine();
        engine.SetValue("before", cached);
        Invoking(() => engine.SetValue("failed", failed)).Should().ThrowExactly<ArgumentException>();
        engine.SetValue("after", cached);

        engine.Evaluate("before === after").AsBoolean().Should().BeTrue();
    }

    [Fact]
    public void CaughtReentrantCopyFailureRollsBackItsStagedCachesAndDiagnostics()
    {
        var failedRoot = new object[2];
        var failedChild = new object[1];
        failedRoot[0] = failedChild;
        failedRoot[1] = new int[2, 2];
        failedChild[0] = failedRoot;

        var engine = CreateCopyEngine(options =>
            options.AddObjectConverter(new CaughtArrayFailureConverter(failedRoot)));
        engine.SetValue("outer", new object[] { new ReentrantCopyProbe() });

        engine.Evaluate("outer[0]").AsNumber().Should().Be(1);
        engine.Advanced.GetInteropConversionDiagnostics().ArrayCopyConversions.Should().Be(1);

        Invoking(() => engine.SetValue("failed", failedRoot)).Should().ThrowExactly<ArgumentException>();
        engine.Advanced.GetInteropConversionDiagnostics().ArrayCopyConversions.Should().Be(1);
    }

    [Fact]
    public void CopyModeConvertsDeepAcyclicArrayGraphsWithoutUsingTheClrStack()
    {
        const int Depth = 10_000;
        object value = 42;
        for (var i = 0; i < Depth; i++)
        {
            value = new object[] { value };
        }

        var engine = CreateCopyEngine();
        engine.SetValue("value", value);

        engine.Evaluate(
            """
            var depth = 0;
            while (Array.isArray(value)) {
                depth++;
                value = value[0];
            }
            depth + ':' + value
            """).AsString().Should().Be($"{Depth}:42");
    }

    [Fact]
    public void LiveViewArrayMaintainsIdentity()
    {
        // wrapper equality is by wrapped target, so === holds even without any identity cache flag
        var engine = CreateLiveViewEngine();
        engine.SetValue("h", new ArrayHolder());
        engine.Evaluate("h.Numbers === h.Numbers").AsBoolean().Should().BeTrue();

        // with the identity map the same wrapper instance is also reused
        var trackingEngine = CreateLiveViewEngine(options => options.Interop.TrackObjectWrapperIdentity = true);
        trackingEngine.SetValue("h", new ArrayHolder());
        trackingEngine.Evaluate("h.Numbers === h.Numbers").AsBoolean().Should().BeTrue();
        trackingEngine.Evaluate("h.Numbers").Should().BeSameAs(trackingEngine.Evaluate("h.Numbers"));

        var recentCacheEngine = CreateLiveViewEngine(options => options.Interop.CacheRecentObjectWrappers = true);
        recentCacheEngine.SetValue("h", new ArrayHolder());
        recentCacheEngine.Evaluate("h.Numbers === h.Numbers").AsBoolean().Should().BeTrue();
        recentCacheEngine.Evaluate("h.Numbers").Should().BeSameAs(recentCacheEngine.Evaluate("h.Numbers"));
    }

    [Fact]
    public void LiveViewArrayScriptWritesAreVisibleInClr()
    {
        var engine = CreateLiveViewEngine();
        var numbers = new[] { 1, 2, 3 };
        engine.SetValue("arr", numbers);

        engine.Execute("arr[1] = 42;");

        numbers[1].Should().Be(42);
    }

    [Fact]
    public void LiveViewArrayClrWritesAreVisibleInScript()
    {
        var engine = CreateLiveViewEngine();
        var numbers = new[] { 1, 2, 3 };
        engine.SetValue("arr", numbers);

        engine.Evaluate("arr[2]").AsNumber().Should().Be(3);

        numbers[2] = 99;

        engine.Evaluate("arr[2]").AsNumber().Should().Be(99);
    }

    [Fact]
    public void LiveViewArrayWriteConvertsToElementType()
    {
        var engine = CreateLiveViewEngine();
        var strings = new[] { "a", "b" };
        engine.SetValue("arr", strings);

        engine.Execute("arr[0] = 'hello';");

        strings[0].Should().Be("hello");
        strings[1].Should().Be("b");
    }

    [Theory]
    [InlineData("arr.push(4)")]
    [InlineData("arr.pop()")]
    [InlineData("arr.shift()")]
    [InlineData("arr.unshift(0)")]
    [InlineData("arr.splice(0, 1)")]
    [InlineData("arr.length = 1")]
    [InlineData("arr.length = 10")]
    [InlineData("arr[3] = 4")]
    [InlineData("arr[100] = 1")]
    public void LiveViewArrayResizeAttemptsThrowTypeError(string operation)
    {
        var engine = CreateLiveViewEngine();
        var numbers = new[] { 1, 2, 3 };
        engine.SetValue("arr", numbers);

        // a script-side catch proves the failure surfaces as a JS TypeError instead of
        // a CLR exception (NotSupportedException etc.) bubbling past the script
        var result = engine.Evaluate(
            $"(function() {{ try {{ {operation}; return 'no-throw'; }} catch (e) {{ return e instanceof TypeError ? 'TypeError' : 'unexpected: ' + e; }} }})()");

        result.AsString().Should().Be("TypeError");
        engine.Evaluate("arr.length").AsNumber().Should().Be(3);
    }

    [Fact]
    public void LiveViewArrayLengthWriteWithSameValueIsNoOp()
    {
        var engine = CreateLiveViewEngine();
        engine.SetValue("arr", new[] { 1, 2, 3 });

        engine.Execute("arr.length = 3;");

        engine.Evaluate("arr.length").AsNumber().Should().Be(3);
    }

    [Fact]
    public void LiveViewArrayIsNotJsArray()
    {
        var engine = CreateLiveViewEngine();
        engine.SetValue("arr", new[] { 1, 2, 3 });

        engine.Evaluate("Array.isArray(arr)").AsBoolean().Should().BeFalse();
        engine.Evaluate("typeof arr").AsString().Should().Be("object");
    }

    [Fact]
    public void LiveViewArraySerializesAsJsonArray()
    {
        // JsonSerializer treats ObjectWrapper { IsArrayLike: true } as an array
        var engine = CreateLiveViewEngine();
        engine.SetValue("arr", new[] { 1, 2, 3 });
        engine.SetValue("holder", new ArrayHolder());

        engine.Evaluate("JSON.stringify(arr)").AsString().Should().Be("[1,2,3]");
        engine.Evaluate("JSON.stringify(holder)").AsString().Should().Be("""{"Numbers":[10,20,30]}""");
    }

    [Fact]
    public void LiveViewArraySupportsIteration()
    {
        var engine = CreateLiveViewEngine();
        engine.SetValue("arr", new[] { 1, 2, 3 });

        engine.Evaluate("var s = ''; for (var x of arr) { s += x + ','; } s").AsString().Should().Be("1,2,3,");
        engine.Evaluate("JSON.stringify([...arr])").AsString().Should().Be("[1,2,3]");

        // Array.prototype is attached by default, so array methods work against the live view
        engine.Evaluate("JSON.stringify(arr.map(function (x) { return x * 2; }))").AsString().Should().Be("[2,4,6]");
        engine.Evaluate("arr.indexOf(2)").AsNumber().Should().Be(1);
    }

    [Fact]
    public void LiveViewArrayDeleteBehavesLikeIntegerIndexedExotic()
    {
        var engine = CreateLiveViewEngine();
        var numbers = new[] { 1, 2, 3 };
        engine.SetValue("arr", numbers);

        // in-range elements cannot be removed from a fixed-size array: delete returns false
        // and must not reset the slot to a default value
        engine.Evaluate("delete arr[0]").AsBoolean().Should().BeFalse();
        numbers[0].Should().Be(1);

        // out-of-range delete is a JS no-op that reports success
        engine.Evaluate("delete arr[100]").AsBoolean().Should().BeTrue();

        // strict mode surfaces the refusal as a TypeError
        engine.Evaluate(
            "(function() { 'use strict'; try { delete arr[0]; return 'no-throw'; } catch (e) { return e instanceof TypeError ? 'TypeError' : 'unexpected: ' + e; } })()").AsString().Should().Be("TypeError");
    }

    [Fact]
    public void LiveViewArrayRespectsAllowWriteFalse()
    {
        var engine = CreateLiveViewEngine(options => options.Interop.AllowWrite = false);
        var numbers = new[] { 1, 2, 3 };
        engine.SetValue("arr", numbers);

        engine.Execute("arr[1] = 42;");

        numbers[1].Should().Be(2);
    }

    [Fact]
    public void LiveViewArrayRoundTripsOriginalReferenceToClr()
    {
        var engine = CreateLiveViewEngine();
        var numbers = new[] { 1, 2, 3 };
        int[] received = null;
        engine.SetValue("arr", numbers);
        engine.SetValue("receive", new Action<int[]>(a => received = a));

        engine.Execute("receive(arr);");

        received.Should().BeSameAs(numbers);
    }

    [Fact]
    public void MultiRankArraysKeepCopyModeFailureUnderBothModes()
    {
        // Non-empty multidimensional arrays are not convertible (Array.GetValue with a single
        // flat index throws), and LiveView must not change that failure mode.
        var copyEngine = new Engine();
        Invoking(() => copyEngine.SetValue("md", new int[2, 2])).Should().ThrowExactly<ArgumentException>();

        var liveViewEngine = CreateLiveViewEngine();
        Invoking(() => liveViewEngine.SetValue("md", new int[2, 2])).Should().ThrowExactly<ArgumentException>();
    }

    [Fact]
    public void ZeroLengthMultiRankArraysKeepCopyFallbackUnderBothModes()
    {
        var copyEngine = new Engine();
        copyEngine.SetValue("md", new int[0, 2]);
        copyEngine.Evaluate("Array.isArray(md) && md.length === 0").AsBoolean().Should().BeTrue();

        var liveViewEngine = CreateLiveViewEngine();
        liveViewEngine.SetValue("md", new int[0, 2]);
        liveViewEngine.Evaluate("Array.isArray(md) && md.length === 0").AsBoolean().Should().BeTrue();
    }

    [Fact]
    public void LiveViewDoesNotAffectListWrapping()
    {
        var engine = CreateLiveViewEngine();
        var list = new List<int> { 1, 2, 3 };
        engine.SetValue("list", list);

        engine.Execute("list.push(4);");

        list.Should().HaveCount(4);
        list[3].Should().Be(4);
    }

    private sealed class ReentrantCopyProbe
    {
    }

    private sealed class ReentrantArrayProbe
    {
    }

    private sealed class ReentrantArrayConverter(Array source) : IObjectConverter
    {
        public bool TryConvert(Engine engine, object value, out JsValue result)
        {
            if (value is not ReentrantArrayProbe)
            {
                result = null;
                return false;
            }

            result = JsValue.FromObject(engine, source);
            return true;
        }
    }

    private sealed class MultidimensionalArrayConverter : IObjectConverter
    {
        public bool TryConvert(Engine engine, object value, out JsValue result)
        {
            if (value is not Array { Rank: > 1 })
            {
                result = null;
                return false;
            }

            result = JsString.Create("multidimensional");
            return true;
        }
    }

    private sealed class ConverterCycleProbe
    {
    }

    private sealed class ConverterCycleConverter(Array source) : IObjectConverter
    {
        public bool TryConvert(Engine engine, object value, out JsValue result)
        {
            if (value is not ConverterCycleProbe)
            {
                result = null;
                return false;
            }

            result = JsValue.FromObject(engine, source);
            return true;
        }
    }

    private sealed class SlowCopyProbe
    {
    }

    private sealed class OneTimeSlowConverter : IObjectConverter
    {
        private bool _delayed;

        public bool TryConvert(Engine engine, object value, out JsValue result)
        {
            if (value is not SlowCopyProbe)
            {
                result = null;
                return false;
            }

            if (!_delayed)
            {
                _delayed = true;
                Thread.Sleep(30);
            }

            result = JsNumber.Create(1);
            return true;
        }
    }

    private sealed class CaughtArrayFailureConverter(object[] failedRoot) : IObjectConverter
    {
        public bool TryConvert(Engine engine, object value, out JsValue result)
        {
            if (value is not ReentrantCopyProbe)
            {
                result = null;
                return false;
            }

            try
            {
                JsValue.FromObject(engine, failedRoot);
            }
            catch (ArgumentException)
            {
                result = JsNumber.Create(1);
                return true;
            }

            throw new InvalidOperationException("The nested array conversion should fail.");
        }
    }
}
