namespace Jint.Tests.Runtime;

/// <summary>
/// <c>[[DefineOwnProperty]]</c> on a typed array
/// (<see href="https://tc39.es/ecma262/#sec-typedarray-defineownproperty">10.4.5.4</see>) writes an element
/// in its last-but-one step, and only <em>if the descriptor has a <c>[[Value]]</c> field</em>. A descriptor
/// that merely restates the attributes an integer index already carries has to validate the flags and then
/// succeed without touching the buffer.
/// <para>
/// test262 exercises every flag rejection and the with-value success, but each of its accepting cases passes
/// a value, so the valueless path had no coverage anywhere and dereferenced the absent value instead:
/// <c>Object.defineProperty(new Int32Array(2), 0, {})</c> escaped as a
/// <c>System.NullReferenceException</c>. These pin the whole descriptor matrix, both halves of every flag,
/// so a future rearrangement of the step order is caught by the same file.
/// </para>
/// </summary>
public class TypedArrayDefinePropertyTests
{
    private readonly Engine _engine = new();

    /// <summary>
    /// The constructor name of whatever <paramref name="source"/> throws, or <c>did not throw</c>. Written in
    /// script rather than with <c>Assert.Throws</c> so that a CLR exception leaking out of the engine fails
    /// the test as an escaping exception instead of being caught and reported as a JavaScript error.
    /// </summary>
    private string ThrownErrorName(string source) => _engine
        .Evaluate($"(function () {{ try {{ {source}; return 'did not throw'; }} catch (e) {{ return e.constructor.name; }} }})()")
        .AsString();

    [Theory]
    [InlineData("{}")]
    [InlineData("{ configurable: true }")]
    [InlineData("{ enumerable: true }")]
    [InlineData("{ writable: true }")]
    [InlineData("{ configurable: true, enumerable: true, writable: true }")]
    public void ADescriptorWithoutAValueSucceedsAndLeavesTheElementAlone(string descriptor)
    {
        _engine.Evaluate($$"""
            var ta = new Int32Array(2);
            ta[0] = 7;
            var result = Object.defineProperty(ta, 0, {{descriptor}});
            """);

        _engine.Evaluate("result === ta").AsBoolean().Should().BeTrue("defineProperty returns the target");
        _engine.Evaluate("ta[0]").AsNumber().Should().Be(7, "the element is untouched when [[Value]] is absent");
        _engine.Evaluate("ta[1]").AsNumber().Should().Be(0);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{ configurable: true }")]
    [InlineData("{ enumerable: true }")]
    [InlineData("{ writable: true }")]
    public void ReflectDefinePropertyReportsSuccessForADescriptorWithoutAValue(string descriptor)
    {
        _engine.Evaluate($"Reflect.defineProperty(new Int32Array(2), 0, {descriptor})").AsBoolean().Should().BeTrue();
    }

    [Theory]
    [InlineData("Int8Array")]
    [InlineData("Uint8Array")]
    [InlineData("Uint8ClampedArray")]
    [InlineData("Int16Array")]
    [InlineData("Uint16Array")]
    [InlineData("Int32Array")]
    [InlineData("Uint32Array")]
    [InlineData("Float16Array")]
    [InlineData("Float32Array")]
    [InlineData("Float64Array")]
    [InlineData("BigInt64Array")]
    [InlineData("BigUint64Array")]
    public void EveryElementTypeAcceptsADescriptorWithoutAValue(string constructor)
    {
        // The BigInt element types matter on their own: their value conversion is ToBigInt rather than
        // ToNumber, so they reach the absent value down a different branch.
        _engine.Evaluate($"Reflect.defineProperty(new {constructor}(2), 0, {{}})").AsBoolean().Should().BeTrue();
    }

    [Theory]
    [InlineData("{ configurable: false }")]
    [InlineData("{ enumerable: false }")]
    [InlineData("{ writable: false }")]
    [InlineData("{ get: function () { return 1; } }")]
    [InlineData("{ set: function () { } }")]
    public void TheFlagRejectionsStillApplyWhenNoValueIsSupplied(string descriptor)
    {
        _engine.Evaluate($"Reflect.defineProperty(new Int32Array(2), 0, {descriptor})").AsBoolean().Should().BeFalse();
        ThrownErrorName($"Object.defineProperty(new Int32Array(2), 0, {descriptor})").Should().Be("TypeError");
    }

    [Fact]
    public void AnAccessorDescriptorIsRejectedBeforeTheValueStep()
    {
        // The accessor rejection sits between the enumerable and writable ones, so a descriptor that is an
        // accessor and otherwise entirely acceptable is only caught there - and must never reach the write.
        _engine.Evaluate("""
            var ta = new Int32Array(2);
            ta[0] = 7;
            var accessorRejected = Reflect.defineProperty(ta, 0, { get: function () { return 1; }, configurable: true });
            """);

        _engine.Evaluate("accessorRejected").AsBoolean().Should().BeFalse();
        _engine.Evaluate("ta[0]").AsNumber().Should().Be(7);
    }

    [Fact]
    public void ADescriptorWithAValueStillWritesTheElement()
    {
        _engine.Evaluate("""
            var ta = new Int32Array(2);
            var result = Object.defineProperty(ta, 0, { value: 18 });
            """);

        _engine.Evaluate("result === ta").AsBoolean().Should().BeTrue();
        _engine.Evaluate("ta[0]").AsNumber().Should().Be(18);
    }

    [Fact]
    public void AnExplicitUndefinedValueIsAValueAndIsConverted()
    {
        // { value: undefined } does have a [[Value]] field, so the element is written with
        // ToNumber(undefined), which is NaN and stores as 0 in an Int32Array. This is the case the
        // absent-value guard must not swallow.
        _engine.Evaluate("""
            var ta = new Int32Array(2);
            ta[0] = 7;
            Object.defineProperty(ta, 0, { value: undefined });
            """);

        _engine.Evaluate("ta[0]").AsNumber().Should().Be(0);
    }

    [Fact]
    public void ADescriptorWithAValueStillReportsAConversionFailure()
    {
        ThrownErrorName("Object.defineProperty(new Int32Array(2), 0, { value: Symbol() })").Should().Be("TypeError");
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{ value: 1 }")]
    [InlineData("{ configurable: true }")]
    public void AnOutOfBoundsIndexIsRejectedBeforeAnythingElse(string descriptor)
    {
        _engine.Evaluate($"Reflect.defineProperty(new Int32Array(2), 5, {descriptor})").AsBoolean().Should().BeFalse();
        _engine.Evaluate($"Reflect.defineProperty(new Int32Array(2), -1, {descriptor})").AsBoolean().Should().BeFalse();
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{ value: 1 }")]
    [InlineData("{ configurable: true }")]
    public void ADetachedBufferIsRejectedBeforeAnythingElse(string descriptor)
    {
        _engine.Evaluate($$"""
            var ta = new Int32Array(2);
            ta.buffer.transfer();
            var result = Reflect.defineProperty(ta, 0, {{descriptor}});
            """);

        _engine.Evaluate("result").AsBoolean().Should().BeFalse();
    }

    [Fact]
    public void AShrunkResizableBufferPutsTheLaterIndexOutOfBounds()
    {
        _engine.Evaluate("""
            var buffer = new ArrayBuffer(8, { maxByteLength: 8 });
            var ta = new Int32Array(buffer);
            buffer.resize(4);
            var inBounds = Reflect.defineProperty(ta, 0, {});
            var outOfBounds = Reflect.defineProperty(ta, 1, {});
            """);

        _engine.Evaluate("inBounds").AsBoolean().Should().BeTrue();
        _engine.Evaluate("outOfBounds").AsBoolean().Should().BeFalse();
    }

    [Fact]
    public void ANonIndexKeyKeepsTheOrdinaryBehaviour()
    {
        _engine.Evaluate("""
            var ta = new Int32Array(2);
            Object.defineProperty(ta, "foo", {});
            var descriptor = Object.getOwnPropertyDescriptor(ta, "foo");
            """);

        _engine.Evaluate("descriptor.value").IsUndefined().Should().BeTrue();
        _engine.Evaluate("descriptor.writable").AsBoolean().Should().BeFalse();
        _engine.Evaluate("descriptor.enumerable").AsBoolean().Should().BeFalse();
        _engine.Evaluate("descriptor.configurable").AsBoolean().Should().BeFalse();
    }
}
