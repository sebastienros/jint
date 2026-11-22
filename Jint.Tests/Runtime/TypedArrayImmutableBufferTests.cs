namespace Jint.Tests.Runtime;

/// <summary>
/// The typed array internal methods over an immutable buffer
/// (<see href="https://tc39.es/proposal-immutable-arraybuffer/#sec-typedarray-getownproperty">[[GetOwnProperty]]</see>,
/// <see href="https://tc39.es/proposal-immutable-arraybuffer/#sec-typedarray-defineownproperty">[[DefineOwnProperty]]</see>
/// and <see href="https://tc39.es/proposal-immutable-arraybuffer/#sec-typedarray-set">[[Set]]</see>):
/// an in-bounds element is non-writable and non-configurable, a definition is validated against that
/// descriptor so only a restatement of it succeeds, and <c>[[Set]]</c> returns <c>false</c> for every
/// canonical numeric key before the value is converted, whatever the receiver.
/// </summary>
public class TypedArrayImmutableBufferTests
{
    private readonly Engine _engine = new();

    private static string Setup(string ctor) => ctor == "BigInt64Array"
        ? "var ta = new BigInt64Array(new BigInt64Array([1n, 2n]).buffer.transferToImmutable()); var one = 1n; var two = 2n; var five = 5n;"
        : "var ta = new Uint8Array(new Uint8Array([1, 2]).buffer.transferToImmutable()); var one = 1; var two = 2; var five = 5;";

    [Theory]
    [InlineData("Uint8Array")]
    [InlineData("BigInt64Array")]
    public void AnInBoundsElementIsEnumerableButNotWritableOrConfigurable(string ctor)
    {
        _engine.Execute(Setup(ctor));

        _engine.Evaluate("""
            var d = Object.getOwnPropertyDescriptor(ta, "0");
            d.writable === false && d.enumerable === true && d.configurable === false && d.value === one;
            """).AsBoolean().Should().BeTrue();
    }

    [Theory]
    [InlineData("Uint8Array")]
    [InlineData("BigInt64Array")]
    public void OnlyARestatementOfTheCurrentDescriptorCanBeDefined(string ctor)
    {
        _engine.Execute(Setup(ctor));

        _engine.Evaluate("Reflect.defineProperty(ta, '0', { value: one })").AsBoolean().Should().BeTrue();
        _engine.Evaluate("Reflect.defineProperty(ta, '0', { value: one, writable: false, enumerable: true, configurable: false })")
            .AsBoolean().Should().BeTrue();

        _engine.Evaluate("Reflect.defineProperty(ta, '0', { value: two })").AsBoolean().Should().BeFalse();
        _engine.Evaluate("Reflect.defineProperty(ta, '0', { writable: true })").AsBoolean().Should().BeFalse();
        _engine.Evaluate("Reflect.defineProperty(ta, '0', { configurable: true })").AsBoolean().Should().BeFalse();

        _engine.Evaluate("""
            var d = Object.getOwnPropertyDescriptor(ta, "0");
            d.writable === false && d.enumerable === true && d.configurable === false && d.value === one && ta[1] === two;
            """).AsBoolean().Should().BeTrue();
    }

    [Fact]
    public void ANewValueIsRefusedEvenWhenItConvertsToTheCurrentOneOrIsNegativeZero()
    {
        _engine.Execute("var ta = new Uint8Array(new Uint8Array([0, 2]).buffer.transferToImmutable());");

        _engine.Evaluate("Reflect.defineProperty(ta, '0', { value: -0 })").AsBoolean().Should().BeFalse();
        _engine.Evaluate("Reflect.defineProperty(ta, '1', { value: '2' })").AsBoolean().Should().BeFalse();
        _engine.Evaluate("Object.is(ta[0], 0) && ta[1] === 2").AsBoolean().Should().BeTrue();
    }

    [Theory]
    [InlineData("Uint8Array")]
    [InlineData("BigInt64Array")]
    public void ObjectDefinePropertyThrowsATypeErrorForARefusedDefinition(string ctor)
    {
        _engine.Execute(Setup(ctor));

        _engine.Evaluate("""
            var thrown = [];
            [{ value: two }, { writable: true }, { configurable: true }].forEach(function (desc) {
                try { Object.defineProperty(ta, '0', desc); thrown.push('did not throw'); }
                catch (e) { thrown.push(e.constructor.name); }
            });
            thrown.join();
            """).AsString().Should().Be("TypeError,TypeError,TypeError");
        _engine.Evaluate("ta[0] === one").AsBoolean().Should().BeTrue();
    }

    [Theory]
    [InlineData("Uint8Array")]
    [InlineData("BigInt64Array")]
    public void ReflectSetRefusesEveryCanonicalNumericKey(string ctor)
    {
        _engine.Execute(Setup(ctor));

        _engine.Evaluate("Reflect.set(ta, '0', five)").AsBoolean().Should().BeFalse();
        _engine.Evaluate("Reflect.set(ta, '0', one)").AsBoolean().Should().BeFalse();
        _engine.Evaluate("Reflect.set(ta, '10', one)").AsBoolean().Should().BeFalse();
        _engine.Evaluate("Reflect.set(ta, '-0', one)").AsBoolean().Should().BeFalse();
        _engine.Evaluate("ta[0] === one && ta[1] === two").AsBoolean().Should().BeTrue();
    }

    [Theory]
    [InlineData("Uint8Array")]
    [InlineData("BigInt64Array")]
    public void ReflectSetWithAnotherReceiverIsRefusedAndTheReceiverGainsNothing(string ctor)
    {
        _engine.Execute(Setup(ctor));

        _engine.Evaluate("""
            var receiver = Object.create(ta);
            Reflect.set(ta, '0', five, receiver) === false && !Object.prototype.hasOwnProperty.call(receiver, '0') && ta[0] === one;
            """).AsBoolean().Should().BeTrue();
    }

    [Theory]
    [InlineData("Uint8Array")]
    [InlineData("BigInt64Array")]
    public void StrictAssignmentThrowsAndSloppyAssignmentIsANoOpWithoutConvertingTheValue(string ctor)
    {
        _engine.Execute(Setup(ctor));
        _engine.Execute("""
            var calls = 0;
            var spy = { valueOf: function () { calls++; return one; } };
            """);

        _engine.Evaluate("""
            (function () {
                'use strict';
                try { ta[0] = spy; return 'did not throw'; } catch (e) { return e.constructor.name; }
            })();
            """).AsString().Should().Be("TypeError");

        _engine.Evaluate("""
            (function () {
                ta[0] = spy;
                ta[10] = spy;
                return ta[0] === one && ta[1] === two && ta[10] === undefined;
            })();
            """).AsBoolean().Should().BeTrue();

        _engine.Evaluate("calls").AsNumber().Should().Be(0);
    }

    [Theory]
    [InlineData("Uint8Array")]
    [InlineData("BigInt64Array")]
    public void AMutableTypedArrayKeepsTheOldBehaviour(string ctor)
    {
        _engine.Execute(ctor == "BigInt64Array"
            ? "var ta = new BigInt64Array([1n, 2n]); var one = 1n; var five = 5n;"
            : "var ta = new Uint8Array([1, 2]); var one = 1; var five = 5;");

        _engine.Evaluate("""
            var d = Object.getOwnPropertyDescriptor(ta, "0");
            d.writable === true && d.enumerable === true && d.configurable === true;
            """).AsBoolean().Should().BeTrue();

        _engine.Evaluate("Reflect.defineProperty(ta, '0', { value: five })").AsBoolean().Should().BeTrue();
        _engine.Evaluate("ta[0] === five").AsBoolean().Should().BeTrue();

        _engine.Evaluate("ta[0] = one; ta[0] === one").AsBoolean().Should().BeTrue();
        _engine.Evaluate("Reflect.set(ta, '0', five)").AsBoolean().Should().BeTrue();
        _engine.Evaluate("ta[0] = one; ta[0] === one").AsBoolean().Should().BeTrue();

        // A receiver that has its own writable "0" takes the write; the typed array is left alone.
        _engine.Evaluate("""
            var receiver = { '0': one };
            Reflect.set(ta, '0', five, receiver) === true && receiver[0] === five && ta[0] === one;
            """).AsBoolean().Should().BeTrue();
    }
}
