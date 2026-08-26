using Jint.Runtime;

namespace Jint.Tests.Runtime;

public class NumberTests
{
    private readonly Engine _engine;

    public NumberTests()
    {
        _engine = new Engine()
            .SetValue("log", new Action<object>(Console.WriteLine))
            .SetValue("assert", new Action<bool>(static value => value.Should().BeTrue()))
            .SetValue("equal", new Action<object, object>(static (expected, actual) =>
                    actual.Should().BeEquivalentTo(expected, static options => options.WithStrictOrdering())));
    }

    private void RunTest(string source)
    {
        _engine.Execute(source);
    }

    [TestCase(1, "3.0e+0")]
    [TestCase(50, "3.00000000000000000000000000000000000000000000000000e+0")]
    [TestCase(100, "3.0000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000e+0")]
    public void ToExponential(int fractionDigits, string result)
    {
        var value = _engine.Evaluate($"(3).toExponential({fractionDigits}).toString()").AsString();
        value.Should().Be(result);
    }

    [TestCase(1, "3.0")]
    [TestCase(50, "3.00000000000000000000000000000000000000000000000000")]
    [TestCase(99, "3.000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000")]
    public void ToFixed(int fractionDigits, string result)
    {
        var value = _engine.Evaluate($"(3).toFixed({fractionDigits}).toString()").AsString();
        value.Should().Be(result);
    }

    [Test]
    public void ToFixedWith100FractionDigitsWorks()
    {
        var value = _engine.Evaluate("(3).toFixed(100)").AsString();
        value.Should().Be("3." + new string('0', 100));
    }

    [TestCase(1, "3")]
    [TestCase(50, "3.0000000000000000000000000000000000000000000000000")]
    [TestCase(100, "3.000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000")]
    public void ToPrecision(int fractionDigits, string result)
    {
        var value = _engine.Evaluate($"(3).toPrecision({fractionDigits}).toString()").AsString();
        value.Should().Be(result);
    }

    [TestCase("1.7976931348623157e+308", double.MaxValue)]
    public void ParseFloat(string input, double result)
    {
        var value = _engine.Evaluate($"parseFloat('{input}')").AsNumber();
        value.Should().Be(result);
    }

    // Results from node -v v18.18.0.
    // Thousand separators.
    [TestCase("1000000", "en-US", "1,000,000")]
    [TestCase("1000000", "en-GB", "1,000,000")]
    [TestCase("1000000", "de-DE", "1.000.000")]
    // TODO. Fails in Win CI due to U+2009
    // Check https://learn.microsoft.com/en-us/dotnet/core/extensions/globalization-icu
    // [TestCase("1000000", "fr-FR", "1 000 000")] 
    [TestCase("1000000", "es-ES", "1.000.000")]
    [TestCase("1000000", "es-LA", "1.000.000")]
    [TestCase("1000000", "es-MX", "1,000,000")]
    [TestCase("1000000", "es-AR", "1.000.000")]
    [TestCase("1000000", "es-CL", "1.000.000")]
    // Comma separator.
    [TestCase("1,23", "en-US", "23")]
    [TestCase("1,23", "en-GB", "23")]
    [TestCase("1,23", "de-DE", "23")]
    [TestCase("1,23", "fr-FR", "23")]
    [TestCase("1,23", "es-ES", "23")]
    [TestCase("1,23", "es-LA", "23")]
    [TestCase("1,23", "es-MX", "23")]
    [TestCase("1,23", "es-AR", "23")]
    [TestCase("1,23", "es-CL", "23")]
    // Dot deicimal separator.
    [TestCase("1.23", "en-US", "1.23")]
    [TestCase("1.23", "en-GB", "1.23")]
    [TestCase("1.23", "de-DE", "1,23")]
    [TestCase("1.23", "fr-FR", "1,23")]
    [TestCase("1.23", "es-ES", "1,23")]
    [TestCase("1.23", "es-LA", "1,23")]
    [TestCase("1.23", "es-MX", "1.23")]
    [TestCase("1.23", "es-AR", "1,23")]
    [TestCase("1.23", "es-CL", "1,23")]
    // Scientific notation.
    [TestCase("1e6", "en-US", "1,000,000")]
    [TestCase("1e6", "en-GB", "1,000,000")]
    [TestCase("1e6", "de-DE", "1.000.000")]
    // TODO. Fails in Win CI due to U+2009
    // Check https://learn.microsoft.com/en-us/dotnet/core/extensions/globalization-icu
    // [TestCase("1000000", "fr-FR", "1 000 000")]
    [TestCase("1e6", "es-ES", "1.000.000")]
    [TestCase("1e6", "es-LA", "1.000.000")]
    [TestCase("1e6", "es-MX", "1,000,000")]
    [TestCase("1e6", "es-AR", "1.000.000")]
    [TestCase("1e6", "es-CL", "1.000.000")]
    // Returns the correct max decimal degits for the respective cultures, rounded down.
    [TestCase("1.234444449", "en-US", "1.234")]
    [TestCase("1.234444449", "en-GB", "1.234")]
    [TestCase("1.234444449", "de-DE", "1,234")]
    [TestCase("1.234444449", "fr-FR", "1,234")]
    [TestCase("1.234444449", "es-ES", "1,234")]
    [TestCase("1.234444449", "es-LA", "1,234")]
    [TestCase("1.234444449", "es-MX", "1.234")]
    [TestCase("1.234444449", "es-AR", "1,234")]
    [TestCase("1.234444449", "es-CL", "1,234")]
    // Returns the correct max decimal degits for the respective cultures, rounded up.
    [TestCase("1.234500001", "en-US", "1.235")]
    [TestCase("1.234500001", "en-GB", "1.235")]
    [TestCase("1.234500001", "de-DE", "1,235")]
    [TestCase("1.234500001", "fr-FR", "1,235")]
    [TestCase("1.234500001", "es-ES", "1,235")]
    [TestCase("1.234500001", "es-LA", "1,235")]
    [TestCase("1.234500001", "es-MX", "1.235")]
    [TestCase("1.234500001", "es-AR", "1,235")]
    [TestCase("1.234500001", "es-CL", "1,235")]
    public void ToLocaleString(string parseNumber, string culture, string result)
    {
        var value = _engine.Evaluate($"({parseNumber}).toLocaleString('{culture}')").AsString();
        value.Should().Be(result);
    }

    // Does not add extra zeros of there is no culture argument.
    [TestCase("123456")]
    public void ToLocaleStringNoArg(string parseNumber)
    {
        var value = _engine.Evaluate($"({parseNumber}).toLocaleString()").AsString();
        value.Should().NotContain(".0");
    }

    [Test]
    public void CoercingOverflowFromString()
    {
        var engine = new Engine();

        engine.Evaluate("Number(1e1000)").ToObject().Should().Be(double.PositiveInfinity);
        engine.Evaluate("+1e1000").ToObject().Should().Be(double.PositiveInfinity);
        engine.Evaluate("(+1e1000).toString()").ToObject().Should().Be("Infinity");

        engine.Evaluate("Number('1e1000')").ToObject().Should().Be(double.PositiveInfinity);
        engine.Evaluate("+'1e1000'").ToObject().Should().Be(double.PositiveInfinity);
        engine.Evaluate("(+'1e1000').toString()").ToObject().Should().Be("Infinity");

        engine.Evaluate("Number(-1e1000)").ToObject().Should().Be(double.NegativeInfinity);
        engine.Evaluate("-1e1000").ToObject().Should().Be(double.NegativeInfinity);
        engine.Evaluate("(-1e1000).toString()").ToObject().Should().Be("-Infinity");

        engine.Evaluate("Number('-1e1000')").ToObject().Should().Be(double.NegativeInfinity);
        engine.Evaluate("-'1e1000'").ToObject().Should().Be(double.NegativeInfinity);
        engine.Evaluate("(-'1e1000').toString()").ToObject().Should().Be("-Infinity");
    }

    [Test]
    public void Int32BoundaryArithmeticDoesNotOverflow()
    {
        var engine = new Engine();

        // internally Integer-tagged int.MinValue / int.MaxValue values must widen
        // to double on arithmetic instead of wrapping or raising hardware overflows
        engine.Evaluate("var x = (1<<31)|0; -x").AsNumber().Should().Be(2147483648d);
        engine.Evaluate("var x = (1<<31)|0; x / -1").AsNumber().Should().Be(2147483648d);
        engine.Evaluate("var x = (1<<31)|0; x % -1").AsNumber().Should().Be(0d);
        engine.Evaluate("var x = (1<<31)|0; Object.is(x % -1, -0)").AsBoolean().Should().BeTrue();

        engine.Evaluate("var a = 2147483647; a++; a").AsNumber().Should().Be(2147483648d);
        engine.Evaluate("var a = 2147483647; ++a").AsNumber().Should().Be(2147483648d);
        engine.Evaluate("var b = (1<<31)|0; b--; b").AsNumber().Should().Be(-2147483649d);
        engine.Evaluate("var b = (1<<31)|0; --b").AsNumber().Should().Be(-2147483649d);

        engine.Evaluate("var c = (1<<31)|0; c -= 1; c").AsNumber().Should().Be(-2147483649d);
        engine.Evaluate("var d = 2147483647; d += 1; d").AsNumber().Should().Be(2147483648d);
    }

    [Test]
    public void CompoundAssignmentMatchesBinaryOperatorSemantics()
    {
        var engine = new Engine();

        // undefined operands must coerce to NaN, not stay undefined
        engine.Evaluate("var u; u *= 2; Number.isNaN(u)").AsBoolean().Should().BeTrue();
        engine.Evaluate("var u; u /= 2; Number.isNaN(u)").AsBoolean().Should().BeTrue();
        engine.Evaluate("var u; u -= 2; Number.isNaN(u)").AsBoolean().Should().BeTrue();

        // ** has spec semantics that differ from IEEE pow: (+/-1) ** Infinity is NaN
        engine.Evaluate("var x = 1; x **= Infinity; Number.isNaN(x)").AsBoolean().Should().BeTrue();
        engine.Evaluate("var x = -1; x **= -Infinity; Number.isNaN(x)").AsBoolean().Should().BeTrue();
        engine.Evaluate("var x = 2; x **= 3; x").AsNumber().Should().Be(8d);

        // compound bitwise/shift operators support BigInt like their binary forms
        engine.Evaluate("var b = 3n; b &= 1n; b.toString()").AsString().Should().Be("1");
        engine.Evaluate("var b = 2n; b |= 1n; b.toString()").AsString().Should().Be("3");
        engine.Evaluate("var b = 3n; b ^= 1n; b.toString()").AsString().Should().Be("2");
        engine.Evaluate("var b = 2n; b <<= 2n; b.toString()").AsString().Should().Be("8");
        engine.Evaluate("var b = 8n; b >>= 2n; b.toString()").AsString().Should().Be("2");

        // mixing BigInt and Number must throw TypeError for compound forms too
        Invoking(() => engine.Evaluate("var b = 3n; b &= 1;")).Should().ThrowExactly<Jint.Runtime.JavaScriptException>();
        Invoking(() => engine.Evaluate("var b = 3; b &= 1n;")).Should().ThrowExactly<Jint.Runtime.JavaScriptException>();
        Invoking(() => engine.Evaluate("var b = 1n; b >>>= 1n;")).Should().ThrowExactly<Jint.Runtime.JavaScriptException>();

        // compound assignment to an uninitialized (TDZ) binding must be a ReferenceError,
        // not a NullReferenceException from the identifier fast path
        var tdz = Invoking(() => engine.Evaluate("(function() { { x += 1; let x; } })()")).Should().ThrowExactly<Jint.Runtime.JavaScriptException>().Which;
        tdz.Error.AsObject().Get("constructor").AsObject().Get("name").AsString().Should().Be("ReferenceError");

        var tdzUpdate = Invoking(() => engine.Evaluate("(function() { { y++; let y; } })()")).Should().ThrowExactly<Jint.Runtime.JavaScriptException>().Which;
        tdzUpdate.Error.AsObject().Get("constructor").AsObject().Get("name").AsString().Should().Be("ReferenceError");
    }

    [Test]
    public void IntegerMultiplicationPreservesNegativeZero()
    {
        var engine = new Engine();
        // Number::multiply: a zero product takes a negative sign when the operand signs differ.
        // The integer fast paths (binary * and compound *=) cannot represent -0 and must route
        // zero products through double arithmetic; test every operand shape the lanes serve.
        var result = engine.Evaluate("""
            (function () {
                var zero = 0, negFive = -5, five = 5;
                var r = [];
                r.push(Object.is(zero * negFive, -0));
                r.push(Object.is(negFive * zero, -0));
                r.push(Object.is(zero * five, 0));
                r.push(Object.is(zero * zero, 0));
                r.push(Object.is(0 * -5, -0));
                var arr = [0, -5];
                r.push(Object.is(arr[0] * arr[1], -0));
                var o = { a: 0, b: -5 };
                r.push(Object.is(o.a * o.b, -0));
                function f(x, y) { return x * y; }
                r.push(Object.is(f(0, -5), -0));
                var acc = 0; acc *= -5;
                r.push(Object.is(acc, -0));
                var acc2 = -5; acc2 *= 0;
                r.push(Object.is(acc2, -0));
                var acc3 = 0; acc3 *= 5;
                r.push(Object.is(acc3, 0));
                // discard-mode numeric-assignment shape (x = a * b as a statement, LHS already
                // a number so the unboxed-slot store engages) and a first-assignment variant
                // that exercises the value-producing binary lane instead
                var prod = 1; prod = zero * negFive;
                r.push(Object.is(prod, -0));
                var prod2 = 1; prod2 = negFive * zero;
                r.push(Object.is(prod2, -0));
                var prod3; prod3 = zero * five;
                r.push(Object.is(prod3, 0));
                return r.join(',');
            })()
            """).AsString();

        result.Should().Be("true,true,true,true,true,true,true,true,true,true,true,true,true,true");
    }

    [Test]
    public void IntegerRemainderLanesPreserveSignAndSpecialCases()
    {
        var engine = new Engine();
        // Number::remainder: the result takes the dividend's sign, so a zero remainder from a
        // negative (or -0) dividend is -0. The unboxed raw-double lanes (compound `x %= y`,
        // statement `lhs = a % b`, fused `x % c === c`) compute integral operands with int32
        // math and must reproduce this exactly, deferring NaN/fractional/zero-divisor/out-of-range
        // operands to fmod. The loop over slot-stored locals keeps the lanes engaged and their
        // slot caches hot; every check pushes true.
        var result = engine.Evaluate("""
            (function () {
                var r = [];
                for (var i = 0; i < 3; i++) {
                    // compound `x %= y` shape
                    var a = -7, b = 2;
                    a %= b;
                    r.push(a === -1);
                    var c = 7, d = -2;
                    c %= d;
                    r.push(c === 1);
                    var negFour = -4, two = 2;
                    negFour %= two;
                    r.push(Object.is(negFour, -0));
                    var four = 4;
                    four %= two;
                    r.push(Object.is(four, 0));
                    var n = 5;
                    n %= 0;
                    r.push(Number.isNaN(n));
                    var f = 5.5;
                    f %= two;
                    r.push(f === 1.5);
                    var min = -2147483648, negOne = -1;
                    min %= negOne;
                    r.push(Object.is(min, -0));
                    var negZero = -0;
                    negZero %= two;
                    r.push(Object.is(negZero, -0));

                    // `lhs = a % b` statement shape
                    var res = 0;
                    var negFourB = -4, fourB = 4, sevenB = 7, negTwoB = -2, minB = -2147483648, fracB = 5.5;
                    res = negFourB % 2;
                    r.push(Object.is(res, -0));
                    res = fourB % 2;
                    r.push(Object.is(res, 0));
                    res = sevenB % negTwoB;
                    r.push(res === 1);
                    res = minB % negOne;
                    r.push(Object.is(res, -0));
                    res = fracB % 2;
                    r.push(res === 1.5);
                    res = sevenB % 0;
                    r.push(Number.isNaN(res));

                    // fused `x % constant === constant` equality shape (a -0 remainder
                    // compares equal to 0 under IEEE ===)
                    var e1 = -4, e2 = 4, e3 = -7, e4 = 7, e5 = 5, e6 = 5.5, e7 = -2147483648;
                    r.push(e1 % 2 === 0);
                    r.push(e2 % 2 === 0);
                    r.push(e3 % 2 === -1);
                    r.push(e4 % -2 === 1);
                    r.push(e5 % 0 !== 0);
                    r.push(e6 % 2 === 1.5);
                    r.push(e7 % -1 === 0);
                }
                return r.join(',');
            })()
            """).AsString();

        result.Should().Be(string.Join(",", Enumerable.Repeat("true", 63)));
    }

    // The following tests guard the primitive-receiver method resolution that skips allocating a
    // Number/Boolean/BigInt wrapper on `primitive.method()`. The wrapper was only ever a lookup
    // vehicle (the this-value passed to the callee is the primitive, boxed at call time for sloppy
    // functions), so removing it must not change any observable behavior.

    [Test]
    public void PrimitiveNumberMethodsResolveWithoutWrapper()
    {
        var engine = new Engine();

        engine.Evaluate("(3.14159).toFixed(2)").AsString().Should().Be("3.14");
        engine.Evaluate("(3.14159).toPrecision(3)").AsString().Should().Be("3.14");
        engine.Evaluate("(255).toString(16)").AsString().Should().Be("ff");
        engine.Evaluate("(5).toString(2)").AsString().Should().Be("101");
        engine.Evaluate("(10).toString()").AsString().Should().Be("10");
        engine.Evaluate("(42).valueOf()").AsNumber().Should().Be(42d);
        engine.Evaluate("(1234).toExponential(2)").AsString().Should().Be("1.23e+3");
        engine.Evaluate("(-2.5).toFixed(0)").AsString().Should().Be("-3");

        // computed-key and identifier-base (read-then-call) resolution paths
        engine.Evaluate("(255)['toString'](16)").AsString().Should().Be("ff");
        engine.Evaluate("var n = 255; var f = n.toString; f.call(255, 16)").AsString().Should().Be("ff");

        // absent property resolves to undefined (not a throw)
        engine.Evaluate("typeof (5).nope").AsString().Should().Be("undefined");
    }

    [Test]
    public void PatchedNumberPrototypeMethodSeesBoxedThisInSloppyMode()
    {
        var engine = new Engine();
        engine.Execute("Number.prototype.typ = function () { return typeof this; };");

        // Sloppy-mode user function boxes the primitive this-value, so it must observe an object.
        engine.Evaluate("(5).typ()").AsString().Should().Be("object");
        // and can still unwrap the underlying number
        engine.Execute("Number.prototype.dbl = function () { return this.valueOf() * 2; };");
        engine.Evaluate("(21).dbl()").AsNumber().Should().Be(42d);

        // resolution stays live after the method is reassigned
        engine.Execute("Number.prototype.typ = function () { return 'reassigned'; };");
        engine.Evaluate("(5).typ()").AsString().Should().Be("reassigned");
    }

    [Test]
    public void PatchedNumberPrototypeMethodSeesPrimitiveThisInStrictMode()
    {
        var engine = new Engine();
        engine.Execute("Number.prototype.styp = function () { 'use strict'; return typeof this; };");

        // Strict-mode user function does not box the this-value, so it must observe the primitive.
        engine.Evaluate("(5).styp()").AsString().Should().Be("number");
        engine.Execute("Number.prototype.sinc = function () { 'use strict'; return this + 1; };");
        engine.Evaluate("(41).sinc()").AsNumber().Should().Be(42d);
    }

    [Test]
    public void GetterOnNumberPrototypeReceivesCorrectReceiver()
    {
        var engine = new Engine();
        engine.Execute("Object.defineProperty(Number.prototype, 'gs', { get: function () { return typeof this; }, configurable: true });");
        engine.Execute("Object.defineProperty(Number.prototype, 'gt', { get: function () { 'use strict'; return typeof this; }, configurable: true });");

        engine.Evaluate("(7).gs").AsString().Should().Be("object");
        engine.Evaluate("(7).gt").AsString().Should().Be("number");
    }

    [Test]
    public void NumberMethodResolvesThroughObjectPrototype()
    {
        var engine = new Engine();
        engine.Execute("Object.prototype.op = function () { return 'fromObjectProto'; };");

        // property inherited from Object.prototype (past Number.prototype) still resolves
        engine.Evaluate("(5).op()").AsString().Should().Be("fromObjectProto");
        engine.Evaluate("(5).hasOwnProperty('x')").AsBoolean().Should().BeFalse();
    }

    [Test]
    public void ProxyInNumberPrototypeChainReceivesPrimitiveReceiver()
    {
        var engine = new Engine();
        engine.Execute("""
            var handler = { get: function (t, p, receiver) { return p === 'trapped' ? 'viaProxy:' + (typeof receiver) : Reflect.get(t, p, receiver); } };
            Object.setPrototypeOf(Number.prototype, new Proxy({ base: 1 }, handler));
            """);

        // spec: GetValue passes the primitive base as the receiver to [[Get]], so a Proxy get trap
        // on the prototype chain observes the primitive - identical to the boxed path.
        engine.Evaluate("(5).trapped").AsString().Should().Be("viaProxy:number");
        engine.Evaluate("(5).base").AsNumber().Should().Be(1d);
    }

    [Test]
    public void BooleanPrimitiveMethodsResolveWithoutWrapper()
    {
        var engine = new Engine();

        engine.Evaluate("(true).toString()").AsString().Should().Be("true");
        engine.Evaluate("(false).toString()").AsString().Should().Be("false");
        engine.Evaluate("(true).valueOf()").AsBoolean().Should().BeTrue();

        engine.Execute("Boolean.prototype.typ = function () { return typeof this; };");
        engine.Evaluate("(true).typ()").AsString().Should().Be("object");
        engine.Execute("Boolean.prototype.styp = function () { 'use strict'; return typeof this; };");
        engine.Evaluate("(true).styp()").AsString().Should().Be("boolean");
    }

    [Test]
    public void BigIntPrimitiveMethodsResolveWithoutWrapper()
    {
        var engine = new Engine();

        engine.Evaluate("(255n).toString(16)").AsString().Should().Be("ff");
        engine.Evaluate("(10n).valueOf().toString()").AsString().Should().Be("10");
        engine.Evaluate("(12345678901234567890n).toString()").AsString().Should().Be("12345678901234567890");

        engine.Execute("BigInt.prototype.typ = function () { return typeof this; };");
        engine.Evaluate("(5n).typ()").AsString().Should().Be("object");
        engine.Execute("BigInt.prototype.styp = function () { 'use strict'; return typeof this; };");
        engine.Evaluate("(5n).styp()").AsString().Should().Be("bigint");
    }

    // The following tests cover Number.prototype's exact-value formatting and its ThisNumberValue
    // step. They run on net472 as well as net10.0, which is the point of several of them: the BCL
    // "F" format specifier carries only 15 significant digits on .NET Framework and refuses a
    // precision above 99 there, so routing toFixed through it produced a different string per target
    // framework.

    // The double nearest 3.141592653589793 is exactly 3.14159265358979311599796346854418516159057617187500.
    [TestCase("(3.141592653589793).toFixed(50)", "3.14159265358979311599796346854418516159057617187500")]
    [TestCase("(3.141592653589793).toFixed(20)", "3.14159265358979311600")]
    // ... and the one nearest 123456.78 is exactly 123456.77999999999883584678173065185546875.
    [TestCase("(123456.78).toFixed(50)", "123456.77999999999883584678173065185546875000000000000000")]
    [TestCase("(-123456.78).toFixed(10)", "-123456.7800000000")]
    [TestCase("(3.141592653589793).toFixed(100)", "3.1415926535897931159979634685441851615905761718750000000000000000000000000000000000000000000000000000")]
    public void ToFixedProducesTheExactMathematicalValueOfTheDouble(string source, string expected)
    {
        new Engine().Evaluate(source).AsString().Should().Be(expected);
    }

    // Casting through long saturated here: .NET answered long.MaxValue and .NET Framework long.MinValue.
    [TestCase("(1e20).toFixed(0)", "100000000000000000000")]
    [TestCase("(1e20).toFixed()", "100000000000000000000")]
    [TestCase("(1e19).toFixed(0)", "10000000000000000000")]
    [TestCase("(-1e20).toFixed(0)", "-100000000000000000000")]
    [TestCase("(1e20).toFixed(1)", "100000000000000000000.0")]
    // 2^63 is where the exact-integer shortcut has to hand over to the digit generator.
    [TestCase("(9223372036854775807).toFixed(0)", "9223372036854775808")]
    [TestCase("(9223372036854775807).toFixed(2)", "9223372036854775808.00")]
    [TestCase("(9.9e18).toFixed(0)", "9900000000000000000")]
    // At 10^21 the fixed notation gives way to Number::toString.
    [TestCase("(1e21).toFixed(2)", "1e+21")]
    public void ToFixedIsExactPastTheRangeOfLong(string source, string expected)
    {
        new Engine().Evaluate(source).AsString().Should().Be(expected);
    }

    // toFixed step 8 works on the real value, for which -0 is not negative, but -Number.MIN_VALUE is.
    [TestCase("(-0).toFixed(0)", "0")]
    [TestCase("(-0).toFixed(3)", "0.000")]
    [TestCase("(-Number.MIN_VALUE).toFixed(0)", "-0")]
    [TestCase("(-Number.MIN_VALUE).toFixed(3)", "-0.000")]
    [TestCase("(Number.MIN_VALUE).toFixed(3)", "0.000")]
    [TestCase("(0.5).toFixed(0)", "1")]
    [TestCase("(-0.5).toFixed(0)", "-1")]
    [TestCase("(1.45).toFixed(1)", "1.4")]
    [TestCase("(-0.4).toFixed(0)", "-0")]
    [TestCase("(-1e-7).toFixed(0)", "-0")]
    [TestCase("(-0.000001).toFixed(2)", "-0.00")]
    // The stored double is a hair above 930.9805, so exact rounding goes up.
    [TestCase("(930.9805).toFixed(3)", "930.981")]
    // ... and a hair below 1.005, so exact rounding goes down.
    [TestCase("(1.005).toFixed(2)", "1.00")]
    public void ToFixedRoundsTiesToTheLargerIntegerAndKeepsTheSignOfTheValue(string source, string expected)
    {
        new Engine().Evaluate(source).AsString().Should().Be(expected);
    }

    // The integrality test behind ToString(double) used to accept anything below ~4.94e-322 as zero.
    [TestCase("String(Number.MIN_VALUE)", "5e-324")]
    [TestCase("String(-Number.MIN_VALUE)", "-5e-324")]
    [TestCase("JSON.stringify(Number.MIN_VALUE)", "5e-324")]
    [TestCase("(Number.MIN_VALUE).toPrecision()", "5e-324")]
    [TestCase("(Number.MIN_VALUE).toPrecision(10)", "4.940656458e-324")]
    [TestCase("`${1e-323}`", "1e-323")]
    public void SubnormalsKeepTheirValueWhenStringified(string source, string expected)
    {
        new Engine().Evaluate(source).AsString().Should().Be(expected);
    }

    [TestCase("(Number.MAX_VALUE).toString()", "1.7976931348623157e+308")]
    [TestCase("(Number.MAX_VALUE).toString(10)", "1.7976931348623157e+308")]
    [TestCase("(-Number.MAX_VALUE).toString()", "-1.7976931348623157e+308")]
    [TestCase("(Number.MAX_VALUE).toString(16).length", "256")]
    public void TheLargestFiniteDoubleDoesNotReportItselfAsInfinity(string source, string expected)
    {
        new Engine().Evaluate(source + ".toString()").AsString().Should().Be(expected);
    }

    // ThisNumberValue reads [[NumberData]] out of the wrapper, so an own valueOf cannot hijack it.
    [TestCase("var n = new Number(); n.valueOf = function () { return 17; }; n.toString()", "0")]
    [TestCase("var n = new Number(); n.valueOf = function () { return 17; }; n.toFixed(1)", "0.0")]
    [TestCase("var n = new Number(); n.valueOf = function () { return 17; }; n.toPrecision(2)", "0.0")]
    [TestCase("var n = new Number(); n.valueOf = function () { return 17; }; n.toExponential(1)", "0.0e+0")]
    [TestCase("var n = new Number(); n.valueOf = function () { return 17; }; String(n)", "0")]
    // ... while ToPrimitive with a string hint still consults the (unmodified) toString first, so an
    // own valueOf does not decide a property key either.
    [TestCase("var o = { 0: 17, 8: 42 }; var n = new Number(); n.valueOf = function () { return 8; }; o[n]", "17")]
    [TestCase("var o = { 0: 17, 9: 42 }; var n = new Number(); n.toString = function () { return 9; }; o[n]", "42")]
    // ... and the default hint does consult valueOf, which is what makes these two differ.
    [TestCase("var n = new Number(); n.valueOf = function () { return 8.5; }; n + n", "17")]
    [TestCase("var n = new Number(); n.toString = function () { return 5; }; n + n", "0")]
    public void NumberPrototypeMethodsReadTheSlotRatherThanCallingValueOf(string source, string expected)
    {
        new Engine().Evaluate(source).ToString().Should().Be(expected);
    }

    [TestCase("Number.prototype.toFixed.call('Hello')")]
    [TestCase("Number.prototype.toFixed.call({})")]
    // ThisNumberValue is step 1, so it outranks the fractionDigits range check of step 4.
    [TestCase("Number.prototype.toFixed.call('Hello', 555)")]
    [TestCase("Number.prototype.toLocaleString.call('Hello')")]
    public void NumberPrototypeMethodsRejectANonNumberReceiver(string source)
    {
        var engine = new Engine();
        var exception = Assert.Throws<JavaScriptException>(() => engine.Evaluate(source));
        exception.Error.Get("constructor").Get("name").AsString().Should().Be("TypeError");
    }

    [TestCase("Number.prototype.toFixed.call(NaN, 555)")]
    [TestCase("Number.prototype.toFixed.call(1, -1)")]
    [TestCase("Number.prototype.toFixed.call(1, 101)")]
    [TestCase("Number.prototype.toFixed.call(1, Infinity)")]
    public void ToFixedRejectsFractionDigitsOutsideZeroToOneHundred(string source)
    {
        var engine = new Engine();
        var exception = Assert.Throws<JavaScriptException>(() => engine.Evaluate(source));
        exception.Error.Get("constructor").Get("name").AsString().Should().Be("RangeError");
    }

    [Test]
    public void ToStringConvertsItsRadixArgumentExactlyOnce()
    {
        var engine = new Engine();
        engine.Execute("var calls = 0; var radix = { valueOf: function () { calls++; return 16; } };");

        // The negative case used to recurse into toString with the original argument, converting twice.
        engine.Evaluate("(-255).toString(radix)").AsString().Should().Be("-ff");
        engine.Evaluate("calls").AsNumber().Should().Be(1d);
    }

    [TestCase("Number.prototype.toFixed.call(NaN)", "NaN")]
    [TestCase("Number.prototype.toFixed.call(Infinity)", "Infinity")]
    [TestCase("Number.prototype.toFixed.call(-Infinity)", "-Infinity")]
    [TestCase("Number.prototype.toExponential.call(new Number(Infinity))", "Infinity")]
    [TestCase("Number.prototype.toPrecision.call(new Number(-Infinity), 3)", "-Infinity")]
    public void NonFiniteValuesFormatAsNumberToString(string source, string expected)
    {
        new Engine().Evaluate(source).AsString().Should().Be(expected);
    }

    /// <summary>
    /// <see href="https://tc39.es/ecma262/#sec-number.prototype.tofixed">Number.prototype.toFixed</see> opens
    /// with <c>ThisNumberValue(this value)</c>, which is a <c>TypeError</c> for any receiver that is neither a
    /// Number nor a Number object. Jint coerced the receiver with <c>ToNumber</c> instead, so a null receiver
    /// silently formatted 0 and an undefined one formatted NaN. test262 covers the whole family in
    /// <c>staging/sm/misc/builtin-methods-reject-null-undefined-this.js</c>, where <c>toFixed</c> was the only
    /// entry in a table of roughly a hundred built-ins that Jint got wrong.
    /// </summary>
    [TestCase("Number.prototype.toFixed.call(null)")]
    [TestCase("Number.prototype.toFixed.call(undefined)")]
    [TestCase("Number.prototype.toFixed.apply(null)")]
    [TestCase("Number.prototype.toFixed.call(null, 2)")]
    [TestCase("Number.prototype.toFixed.call('1')")]
    [TestCase("Number.prototype.toFixed.call({})")]
    [TestCase("Number.prototype.toFixed.call(Symbol())")]
    [TestCase("Number.prototype.toFixed.call(1n)")]
    [TestCase("(0, Number.prototype.toFixed)()")]
    public void ToFixedRequiresANumberReceiver(string script)
    {
        Invoking(() => new Engine().Evaluate(script))
            .Should().Throw<JavaScriptException>()
            .WithMessage("Number.prototype.toFixed requires that 'this' be a Number");
    }

    /// <summary>
    /// The receiver check is step 1 and the argument coercion is step 2, so a bad receiver is reported even
    /// when the argument would also have thrown.
    /// </summary>
    [Test]
    public void ToFixedChecksItsReceiverBeforeCoercingItsArgument()
    {
        const string Script = """
            var argumentWasCoerced = false;
            var poisoned = { valueOf: function () { argumentWasCoerced = true; throw new Error('argument'); } };
            var message;
            try { Number.prototype.toFixed.call(null, poisoned); message = 'did not throw'; }
            catch (e) { message = e.constructor.name; }
            message + ' ' + argumentWasCoerced;
            """;

        new Engine().Evaluate(Script).AsString().Should().Be("TypeError false");
    }

    /// <summary>
    /// The control: every receiver that is a Number still formats, and the digit range is still the one the
    /// spec's steps 4 and 5 describe.
    /// </summary>
    [Test]
    public void ToFixedStillFormatsEveryNumberReceiver()
    {
        var engine = new Engine();

        engine.Evaluate("(3).toFixed(2)").AsString().Should().Be("3.00");
        engine.Evaluate("new Number(3).toFixed(2)").AsString().Should().Be("3.00");
        engine.Evaluate("Number.prototype.toFixed()").AsString().Should().Be("0");
        engine.Evaluate("(3).toFixed(-0)").AsString().Should().Be("3");
        engine.Evaluate("(3).toFixed(100).length").AsNumber().Should().Be(102);

        Invoking(() => engine.Evaluate("(3).toFixed(-1)")).Should().Throw<JavaScriptException>();
        Invoking(() => engine.Evaluate("(3).toFixed(101)")).Should().Throw<JavaScriptException>();
        Invoking(() => engine.Evaluate("(3).toFixed(Infinity)")).Should().Throw<JavaScriptException>();
    }
}
