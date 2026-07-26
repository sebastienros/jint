#nullable enable

using System.Collections.Generic;
using Jint.Native;
using Jint.Native.Array;
using Jint.Runtime.Descriptors;

namespace Jint.Tests.PublicInterface;

/// <summary>
/// Small public-surface gaps that host code hits when bridging values in and out of the engine. These tests
/// would not compile if the members they exercise were still non-public.
/// </summary>
public class HostValueBridgingTests
{
    // ArrayInstance.Length is not part of the public surface; hosts read the length the same way script
    // does. Kept as a helper so the tests below stay about TryGetValue.
    private static uint ArrayLength(ArrayInstance array) => (uint) array.Get("length").AsNumber();

    [Fact]
    public void PropertyDescriptorCanBeConstructedFromFlagsDirectly()
    {
        var engine = new Engine();

        var descriptor = new PropertyDescriptor(JsNumber.Create(7), PropertyFlag.ConfigurableEnumerableWritable);

        descriptor.Value.AsNumber().Should().Be(7);
        descriptor.Writable.Should().BeTrue();
        descriptor.Enumerable.Should().BeTrue();
        descriptor.Configurable.Should().BeTrue();

        var target = engine.Evaluate("({})").AsObject();
        target.DefineOwnProperty("answer", descriptor).Should().BeTrue();
        engine.SetValue("target", target);
        engine.Evaluate("target.answer").AsNumber().Should().Be(7);
        engine.Evaluate("Object.keys(target).join(',')").AsString().Should().Be("answer");
    }

    [Fact]
    public void PropertyDescriptorFlagsOverloadMatchesTheNullableBoolOverload()
    {
        var byFlags = new PropertyDescriptor(new JsString("v"), PropertyFlag.ConfigurableEnumerableWritable);
        var byBools = new PropertyDescriptor(new JsString("v"), writable: true, enumerable: true, configurable: true);

        byFlags.Writable.Should().Be(byBools.Writable);
        byFlags.Enumerable.Should().Be(byBools.Enumerable);
        byFlags.Configurable.Should().Be(byBools.Configurable);
        byFlags.Value.Should().Be(byBools.Value);
    }

    [Fact]
    public void PropertyDescriptorSupportsNonEnumerableCombinations()
    {
        var engine = new Engine();

        var hidden = new PropertyDescriptor(JsNumber.Create(1), PropertyFlag.Configurable | PropertyFlag.Writable);
        hidden.Enumerable.Should().BeFalse();

        var target = engine.Evaluate("({})").AsObject();
        target.DefineOwnProperty("hidden", hidden);
        engine.SetValue("target", target);

        engine.Evaluate("Object.keys(target).length").AsNumber().Should().Be(0);
        engine.Evaluate("target.hidden").AsNumber().Should().Be(1);
    }

    [Fact]
    public void ArrayTryGetValueDistinguishesHolesFromStoredUndefined()
    {
        var engine = new Engine();
        var array = (ArrayInstance) engine.Evaluate("[0, 1, , undefined, 4]").AsObject();

        ArrayLength(array).Should().Be(5);

        array.TryGetValue(0, out var zero).Should().BeTrue();
        zero.AsNumber().Should().Be(0);

        // index 2 is a hole
        array.TryGetValue(2, out var hole).Should().BeFalse();
        hole.IsUndefined().Should().BeTrue();

        // index 3 holds a real undefined
        array.TryGetValue(3, out var stored).Should().BeTrue();
        stored.IsUndefined().Should().BeTrue();

        array.TryGetValue(4, out var four).Should().BeTrue();
        four.AsNumber().Should().Be(4);

        // past the end
        array.TryGetValue(5, out var beyond).Should().BeFalse();
        beyond.IsUndefined().Should().BeTrue();
    }

    [Fact]
    public void ArrayTryGetValueResolvesInheritedIndices()
    {
        var engine = new Engine();
        var array = (ArrayInstance) engine.Evaluate("""
            var proto = { 1: 'from-proto' };
            var a = [ 'own' ];
            a.length = 3;
            Object.setPrototypeOf(a, proto);
            a
            """).AsObject();

        array.TryGetValue(0, out var own).Should().BeTrue();
        own.AsString().Should().Be("own");

        array.TryGetValue(1, out var inherited).Should().BeTrue();
        inherited.AsString().Should().Be("from-proto");

        array.TryGetValue(2, out _).Should().BeFalse();
    }

    [Fact]
    public void ArrayTryGetValueBridgesASparseArrayInOnePass()
    {
        var engine = new Engine();
        var array = (ArrayInstance) engine.Evaluate("var a = []; a[0] = 'a'; a[3] = 'd'; a").AsObject();

        var bridged = new List<string?>();
        for (uint i = 0; i < ArrayLength(array); i++)
        {
            bridged.Add(array.TryGetValue(i, out var value) ? value.ToString() : null);
        }

        bridged.Should().Equal("a", null, null, "d");
    }
}
