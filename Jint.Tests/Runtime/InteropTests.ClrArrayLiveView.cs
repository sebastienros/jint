using Jint.Native;
using Jint.Runtime;

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
            options.Interop.ArrayConversion = ArrayConversionMode.LiveView;
            additionalConfiguration?.Invoke(options);
        });
    }

    [Fact]
    public void ArrayConversionDefaultsToLiveView()
    {
        Assert.Equal(ArrayConversionMode.LiveView, new Options().Interop.ArrayConversion);

        // LiveView mode (the default since 4.14): a CLR array crosses as a live wrapper view, not a
        // native JS array snapshot, so it is not an Array and repeated crossings share wrapper identity
        var engine = new Engine();
        engine.SetValue("h", new ArrayHolder());
        Assert.False(engine.Evaluate("Array.isArray(h.Numbers)").AsBoolean());
        Assert.True(engine.Evaluate("h.Numbers === h.Numbers").AsBoolean());
    }

    [Fact]
    public void LiveViewElementReadsConvertPrimitiveItemTypes()
    {
        var engine = new Engine();
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

        Assert.Equal(int.MaxValue, engine.Evaluate("ints[1]").AsNumber());
        Assert.Equal(-1, engine.Evaluate("ints[2]").AsNumber());
        Assert.Equal(0.5, engine.Evaluate("doubles[0]").AsNumber());
        Assert.Equal(double.MaxValue, engine.Evaluate("doubles[1]").AsNumber());
        Assert.True(engine.Evaluate("Object.is(doubles[2], -0)").AsBoolean());
        Assert.Equal((double) long.MaxValue, engine.Evaluate("longs[1]").AsNumber());
        Assert.Equal((double) 0.1f, engine.Evaluate("floats[0]").AsNumber());
        Assert.Equal((double) float.MaxValue, engine.Evaluate("floats[1]").AsNumber());
        Assert.True(engine.Evaluate("bools[0]").AsBoolean());
        Assert.False(engine.Evaluate("bools[1]").AsBoolean());
        Assert.Equal(255, engine.Evaluate("bytes[1]").AsNumber());
        Assert.Equal("ö", engine.Evaluate("chars[1]").AsString());
        Assert.Equal("x", engine.Evaluate("strings[0]").AsString());
        Assert.True(engine.Evaluate("strings[1] === null").AsBoolean());
        Assert.Equal("", engine.Evaluate("strings[2]").AsString());
        Assert.Equal(8, engine.Evaluate("intList[1]").AsNumber());
        Assert.True(engine.Evaluate("stringList[1] === null").AsBoolean());

        // out-of-range keeps the general converter's result
        Assert.True(engine.Evaluate("ints[99] === null").AsBoolean());
        Assert.True(engine.Evaluate("intList[99] === null").AsBoolean());
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
        // opting back into Copy restores the pre-4.14 behavior: each crossing produces an independent JsArray snapshot
        var engine = CreateCopyEngine();
        engine.SetValue("h", new ArrayHolder());
        Assert.True(engine.Evaluate("Array.isArray(h.Numbers)").AsBoolean());
        Assert.False(engine.Evaluate("h.Numbers === h.Numbers").AsBoolean());
    }

    [Fact]
    public void CopyModeScriptWritesDoNotAffectClrArray()
    {
        var engine = CreateCopyEngine();
        var numbers = new[] { 1, 2, 3 };
        engine.SetValue("arr", numbers);

        engine.Execute("arr[1] = 42;");

        Assert.Equal(2, numbers[1]);
    }

    [Fact]
    public void LiveViewArrayMaintainsIdentity()
    {
        // wrapper equality is by wrapped target, so === holds even without any identity cache flag
        var engine = CreateLiveViewEngine();
        engine.SetValue("h", new ArrayHolder());
        Assert.True(engine.Evaluate("h.Numbers === h.Numbers").AsBoolean());

        // with the identity map the same wrapper instance is also reused
        var trackingEngine = CreateLiveViewEngine(options => options.Interop.TrackObjectWrapperIdentity = true);
        trackingEngine.SetValue("h", new ArrayHolder());
        Assert.True(trackingEngine.Evaluate("h.Numbers === h.Numbers").AsBoolean());
        Assert.Same(trackingEngine.Evaluate("h.Numbers"), trackingEngine.Evaluate("h.Numbers"));

        var recentCacheEngine = CreateLiveViewEngine(options => options.Interop.CacheRecentObjectWrappers = true);
        recentCacheEngine.SetValue("h", new ArrayHolder());
        Assert.True(recentCacheEngine.Evaluate("h.Numbers === h.Numbers").AsBoolean());
        Assert.Same(recentCacheEngine.Evaluate("h.Numbers"), recentCacheEngine.Evaluate("h.Numbers"));
    }

    [Fact]
    public void LiveViewArrayScriptWritesAreVisibleInClr()
    {
        var engine = CreateLiveViewEngine();
        var numbers = new[] { 1, 2, 3 };
        engine.SetValue("arr", numbers);

        engine.Execute("arr[1] = 42;");

        Assert.Equal(42, numbers[1]);
    }

    [Fact]
    public void LiveViewArrayClrWritesAreVisibleInScript()
    {
        var engine = CreateLiveViewEngine();
        var numbers = new[] { 1, 2, 3 };
        engine.SetValue("arr", numbers);

        Assert.Equal(3, engine.Evaluate("arr[2]").AsNumber());

        numbers[2] = 99;

        Assert.Equal(99, engine.Evaluate("arr[2]").AsNumber());
    }

    [Fact]
    public void LiveViewArrayWriteConvertsToElementType()
    {
        var engine = CreateLiveViewEngine();
        var strings = new[] { "a", "b" };
        engine.SetValue("arr", strings);

        engine.Execute("arr[0] = 'hello';");

        Assert.Equal("hello", strings[0]);
        Assert.Equal("b", strings[1]);
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

        Assert.Equal("TypeError", result.AsString());
        Assert.Equal(3, engine.Evaluate("arr.length").AsNumber());
    }

    [Fact]
    public void LiveViewArrayLengthWriteWithSameValueIsNoOp()
    {
        var engine = CreateLiveViewEngine();
        engine.SetValue("arr", new[] { 1, 2, 3 });

        engine.Execute("arr.length = 3;");

        Assert.Equal(3, engine.Evaluate("arr.length").AsNumber());
    }

    [Fact]
    public void LiveViewArrayIsNotJsArray()
    {
        var engine = CreateLiveViewEngine();
        engine.SetValue("arr", new[] { 1, 2, 3 });

        Assert.False(engine.Evaluate("Array.isArray(arr)").AsBoolean());
        Assert.Equal("object", engine.Evaluate("typeof arr").AsString());
    }

    [Fact]
    public void LiveViewArraySerializesAsJsonArray()
    {
        // JsonSerializer treats ObjectWrapper { IsArrayLike: true } as an array
        var engine = CreateLiveViewEngine();
        engine.SetValue("arr", new[] { 1, 2, 3 });
        engine.SetValue("holder", new ArrayHolder());

        Assert.Equal("[1,2,3]", engine.Evaluate("JSON.stringify(arr)").AsString());
        Assert.Equal("""{"Numbers":[10,20,30]}""", engine.Evaluate("JSON.stringify(holder)").AsString());
    }

    [Fact]
    public void LiveViewArraySupportsIteration()
    {
        var engine = CreateLiveViewEngine();
        engine.SetValue("arr", new[] { 1, 2, 3 });

        Assert.Equal("1,2,3,", engine.Evaluate("var s = ''; for (var x of arr) { s += x + ','; } s").AsString());
        Assert.Equal("[1,2,3]", engine.Evaluate("JSON.stringify([...arr])").AsString());

        // Array.prototype is attached by default, so array methods work against the live view
        Assert.Equal("[2,4,6]", engine.Evaluate("JSON.stringify(arr.map(function (x) { return x * 2; }))").AsString());
        Assert.Equal(1, engine.Evaluate("arr.indexOf(2)").AsNumber());
    }

    [Fact]
    public void LiveViewArrayDeleteBehavesLikeIntegerIndexedExotic()
    {
        var engine = CreateLiveViewEngine();
        var numbers = new[] { 1, 2, 3 };
        engine.SetValue("arr", numbers);

        // in-range elements cannot be removed from a fixed-size array: delete returns false
        // and must not reset the slot to a default value
        Assert.False(engine.Evaluate("delete arr[0]").AsBoolean());
        Assert.Equal(1, numbers[0]);

        // out-of-range delete is a JS no-op that reports success
        Assert.True(engine.Evaluate("delete arr[100]").AsBoolean());

        // strict mode surfaces the refusal as a TypeError
        Assert.Equal("TypeError", engine.Evaluate(
            "(function() { 'use strict'; try { delete arr[0]; return 'no-throw'; } catch (e) { return e instanceof TypeError ? 'TypeError' : 'unexpected: ' + e; } })()").AsString());
    }

    [Fact]
    public void LiveViewArrayRespectsAllowWriteFalse()
    {
        var engine = CreateLiveViewEngine(options => options.Interop.AllowWrite = false);
        var numbers = new[] { 1, 2, 3 };
        engine.SetValue("arr", numbers);

        engine.Execute("arr[1] = 42;");

        Assert.Equal(2, numbers[1]);
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

        Assert.Same(numbers, received);
    }

    [Fact]
    public void MultiRankArraysKeepCopyModeFailureUnderBothModes()
    {
        // multi-dimensional arrays have never been convertible (Array.GetValue with a single
        // flat index throws) and LiveView must not change that failure mode
        var copyEngine = new Engine();
        Assert.Throws<ArgumentException>(() => copyEngine.SetValue("md", new int[2, 2]));

        var liveViewEngine = CreateLiveViewEngine();
        Assert.Throws<ArgumentException>(() => liveViewEngine.SetValue("md", new int[2, 2]));
    }

    [Fact]
    public void LiveViewDoesNotAffectListWrapping()
    {
        var engine = CreateLiveViewEngine();
        var list = new List<int> { 1, 2, 3 };
        engine.SetValue("list", list);

        engine.Execute("list.push(4);");

        Assert.Equal(4, list.Count);
        Assert.Equal(4, list[3]);
    }
}
