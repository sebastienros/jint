namespace Jint.Tests.Runtime;

public class NumberTests
{
    private readonly Engine _engine;

    public NumberTests()
    {
        _engine = new Engine()
            .SetValue("log", new Action<object>(Console.WriteLine))
            .SetValue("assert", new Action<bool>(Assert.True))
            .SetValue("equal", new Action<object, object>(Assert.Equal));
    }

    private void RunTest(string source)
    {
        _engine.Execute(source);
    }

    [Theory]
    [InlineData(1, "3.0e+0")]
    [InlineData(50, "3.00000000000000000000000000000000000000000000000000e+0")]
    [InlineData(100, "3.0000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000e+0")]
    public void ToExponential(int fractionDigits, string result)
    {
        var value = _engine.Evaluate($"(3).toExponential({fractionDigits}).toString()").AsString();
        Assert.Equal(result, value);
    }

    [Theory]
    [InlineData(1, "3.0")]
    [InlineData(50, "3.00000000000000000000000000000000000000000000000000")]
    [InlineData(99, "3.000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000")]
    public void ToFixed(int fractionDigits, string result)
    {
        var value = _engine.Evaluate($"(3).toFixed({fractionDigits}).toString()").AsString();
        Assert.Equal(result, value);
    }

    [Fact]
    public void ToFixedWith100FractionDigitsWorks()
    {
        var value = _engine.Evaluate("(3).toFixed(100)").AsString();
        Assert.Equal("3." + new string('0', 100), value);
    }

    [Theory]
    [InlineData(1, "3")]
    [InlineData(50, "3.0000000000000000000000000000000000000000000000000")]
    [InlineData(100, "3.000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000")]
    public void ToPrecision(int fractionDigits, string result)
    {
        var value = _engine.Evaluate($"(3).toPrecision({fractionDigits}).toString()").AsString();
        Assert.Equal(result, value);
    }

    [Theory]
    [InlineData("1.7976931348623157e+308", double.MaxValue)]
    public void ParseFloat(string input, double result)
    {
        var value = _engine.Evaluate($"parseFloat('{input}')").AsNumber();
        Assert.Equal(result, value);
    }

    // Results from node -v v18.18.0.
    [Theory]
    // Thousand separators.
    [InlineData("1000000", "en-US", "1,000,000")]
    [InlineData("1000000", "en-GB", "1,000,000")]
    [InlineData("1000000", "de-DE", "1.000.000")]
    // TODO. Fails in Win CI due to U+2009
    // Check https://learn.microsoft.com/en-us/dotnet/core/extensions/globalization-icu
    // [InlineData("1000000", "fr-FR", "1 000 000")] 
    [InlineData("1000000", "es-ES", "1.000.000")]
    [InlineData("1000000", "es-LA", "1.000.000")]
    [InlineData("1000000", "es-MX", "1,000,000")]
    [InlineData("1000000", "es-AR", "1.000.000")]
    [InlineData("1000000", "es-CL", "1.000.000")]
    // Comma separator.
    [InlineData("1,23", "en-US", "23")]
    [InlineData("1,23", "en-GB", "23")]
    [InlineData("1,23", "de-DE", "23")]
    [InlineData("1,23", "fr-FR", "23")]
    [InlineData("1,23", "es-ES", "23")]
    [InlineData("1,23", "es-LA", "23")]
    [InlineData("1,23", "es-MX", "23")]
    [InlineData("1,23", "es-AR", "23")]
    [InlineData("1,23", "es-CL", "23")]
    // Dot deicimal separator.
    [InlineData("1.23", "en-US", "1.23")]
    [InlineData("1.23", "en-GB", "1.23")]
    [InlineData("1.23", "de-DE", "1,23")]
    [InlineData("1.23", "fr-FR", "1,23")]
    [InlineData("1.23", "es-ES", "1,23")]
    [InlineData("1.23", "es-LA", "1,23")]
    [InlineData("1.23", "es-MX", "1.23")]
    [InlineData("1.23", "es-AR", "1,23")]
    [InlineData("1.23", "es-CL", "1,23")]
    // Scientific notation.
    [InlineData("1e6", "en-US", "1,000,000")]
    [InlineData("1e6", "en-GB", "1,000,000")]
    [InlineData("1e6", "de-DE", "1.000.000")]
    // TODO. Fails in Win CI due to U+2009
    // Check https://learn.microsoft.com/en-us/dotnet/core/extensions/globalization-icu
    // [InlineData("1000000", "fr-FR", "1 000 000")]
    [InlineData("1e6", "es-ES", "1.000.000")]
    [InlineData("1e6", "es-LA", "1.000.000")]
    [InlineData("1e6", "es-MX", "1,000,000")]
    [InlineData("1e6", "es-AR", "1.000.000")]
    [InlineData("1e6", "es-CL", "1.000.000")]
    // Returns the correct max decimal degits for the respective cultures, rounded down.
    [InlineData("1.234444449", "en-US", "1.234")]
    [InlineData("1.234444449", "en-GB", "1.234")]
    [InlineData("1.234444449", "de-DE", "1,234")]
    [InlineData("1.234444449", "fr-FR", "1,234")]
    [InlineData("1.234444449", "es-ES", "1,234")]
    [InlineData("1.234444449", "es-LA", "1,234")]
    [InlineData("1.234444449", "es-MX", "1.234")]
    [InlineData("1.234444449", "es-AR", "1,234")]
    [InlineData("1.234444449", "es-CL", "1,234")]
    // Returns the correct max decimal degits for the respective cultures, rounded up.
    [InlineData("1.234500001", "en-US", "1.235")]
    [InlineData("1.234500001", "en-GB", "1.235")]
    [InlineData("1.234500001", "de-DE", "1,235")]
    [InlineData("1.234500001", "fr-FR", "1,235")]
    [InlineData("1.234500001", "es-ES", "1,235")]
    [InlineData("1.234500001", "es-LA", "1,235")]
    [InlineData("1.234500001", "es-MX", "1.235")]
    [InlineData("1.234500001", "es-AR", "1,235")]
    [InlineData("1.234500001", "es-CL", "1,235")]
    public void ToLocaleString(string parseNumber, string culture, string result)
    {
        var value = _engine.Evaluate($"({parseNumber}).toLocaleString('{culture}')").AsString();
        Assert.Equal(result, value);
    }

    [Theory]
    // Does not add extra zeros of there is no culture argument.
    [InlineData("123456")]
    public void ToLocaleStringNoArg(string parseNumber)
    {
        var value = _engine.Evaluate($"({parseNumber}).toLocaleString()").AsString();
        Assert.DoesNotContain(".0", value);
    }

    [Fact]
    public void CoercingOverflowFromString()
    {
        var engine = new Engine();

        Assert.Equal(double.PositiveInfinity, engine.Evaluate("Number(1e1000)").ToObject());
        Assert.Equal(double.PositiveInfinity, engine.Evaluate("+1e1000").ToObject());
        Assert.Equal("Infinity", engine.Evaluate("(+1e1000).toString()").ToObject());

        Assert.Equal(double.PositiveInfinity, engine.Evaluate("Number('1e1000')").ToObject());
        Assert.Equal(double.PositiveInfinity, engine.Evaluate("+'1e1000'").ToObject());
        Assert.Equal("Infinity", engine.Evaluate("(+'1e1000').toString()").ToObject());

        Assert.Equal(double.NegativeInfinity, engine.Evaluate("Number(-1e1000)").ToObject());
        Assert.Equal(double.NegativeInfinity, engine.Evaluate("-1e1000").ToObject());
        Assert.Equal("-Infinity", engine.Evaluate("(-1e1000).toString()").ToObject());

        Assert.Equal(double.NegativeInfinity, engine.Evaluate("Number('-1e1000')").ToObject());
        Assert.Equal(double.NegativeInfinity, engine.Evaluate("-'1e1000'").ToObject());
        Assert.Equal("-Infinity", engine.Evaluate("(-'1e1000').toString()").ToObject());
    }

    [Fact]
    public void Int32BoundaryArithmeticDoesNotOverflow()
    {
        var engine = new Engine();

        // internally Integer-tagged int.MinValue / int.MaxValue values must widen
        // to double on arithmetic instead of wrapping or raising hardware overflows
        Assert.Equal(2147483648d, engine.Evaluate("var x = (1<<31)|0; -x").AsNumber());
        Assert.Equal(2147483648d, engine.Evaluate("var x = (1<<31)|0; x / -1").AsNumber());
        Assert.Equal(0d, engine.Evaluate("var x = (1<<31)|0; x % -1").AsNumber());
        Assert.True(engine.Evaluate("var x = (1<<31)|0; Object.is(x % -1, -0)").AsBoolean());

        Assert.Equal(2147483648d, engine.Evaluate("var a = 2147483647; a++; a").AsNumber());
        Assert.Equal(2147483648d, engine.Evaluate("var a = 2147483647; ++a").AsNumber());
        Assert.Equal(-2147483649d, engine.Evaluate("var b = (1<<31)|0; b--; b").AsNumber());
        Assert.Equal(-2147483649d, engine.Evaluate("var b = (1<<31)|0; --b").AsNumber());

        Assert.Equal(-2147483649d, engine.Evaluate("var c = (1<<31)|0; c -= 1; c").AsNumber());
        Assert.Equal(2147483648d, engine.Evaluate("var d = 2147483647; d += 1; d").AsNumber());
    }

    [Fact]
    public void CompoundAssignmentMatchesBinaryOperatorSemantics()
    {
        var engine = new Engine();

        // undefined operands must coerce to NaN, not stay undefined
        Assert.True(engine.Evaluate("var u; u *= 2; Number.isNaN(u)").AsBoolean());
        Assert.True(engine.Evaluate("var u; u /= 2; Number.isNaN(u)").AsBoolean());
        Assert.True(engine.Evaluate("var u; u -= 2; Number.isNaN(u)").AsBoolean());

        // ** has spec semantics that differ from IEEE pow: (+/-1) ** Infinity is NaN
        Assert.True(engine.Evaluate("var x = 1; x **= Infinity; Number.isNaN(x)").AsBoolean());
        Assert.True(engine.Evaluate("var x = -1; x **= -Infinity; Number.isNaN(x)").AsBoolean());
        Assert.Equal(8d, engine.Evaluate("var x = 2; x **= 3; x").AsNumber());

        // compound bitwise/shift operators support BigInt like their binary forms
        Assert.Equal("1", engine.Evaluate("var b = 3n; b &= 1n; b.toString()").AsString());
        Assert.Equal("3", engine.Evaluate("var b = 2n; b |= 1n; b.toString()").AsString());
        Assert.Equal("2", engine.Evaluate("var b = 3n; b ^= 1n; b.toString()").AsString());
        Assert.Equal("8", engine.Evaluate("var b = 2n; b <<= 2n; b.toString()").AsString());
        Assert.Equal("2", engine.Evaluate("var b = 8n; b >>= 2n; b.toString()").AsString());

        // mixing BigInt and Number must throw TypeError for compound forms too
        Assert.Throws<Jint.Runtime.JavaScriptException>(() => engine.Evaluate("var b = 3n; b &= 1;"));
        Assert.Throws<Jint.Runtime.JavaScriptException>(() => engine.Evaluate("var b = 3; b &= 1n;"));
        Assert.Throws<Jint.Runtime.JavaScriptException>(() => engine.Evaluate("var b = 1n; b >>>= 1n;"));

        // compound assignment to an uninitialized (TDZ) binding must be a ReferenceError,
        // not a NullReferenceException from the identifier fast path
        var tdz = Assert.Throws<Jint.Runtime.JavaScriptException>(() => engine.Evaluate("(function() { { x += 1; let x; } })()"));
        Assert.Equal("ReferenceError", tdz.Error.AsObject().Get("constructor").AsObject().Get("name").AsString());

        var tdzUpdate = Assert.Throws<Jint.Runtime.JavaScriptException>(() => engine.Evaluate("(function() { { y++; let y; } })()"));
        Assert.Equal("ReferenceError", tdzUpdate.Error.AsObject().Get("constructor").AsObject().Get("name").AsString());
    }

    [Fact]
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

        Assert.Equal("true,true,true,true,true,true,true,true,true,true,true,true,true,true", result);
    }

    [Fact]
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

        Assert.Equal(string.Join(",", Enumerable.Repeat("true", 63)), result);
    }

    // The following tests guard the primitive-receiver method resolution that skips allocating a
    // Number/Boolean/BigInt wrapper on `primitive.method()`. The wrapper was only ever a lookup
    // vehicle (the this-value passed to the callee is the primitive, boxed at call time for sloppy
    // functions), so removing it must not change any observable behavior.

    [Fact]
    public void PrimitiveNumberMethodsResolveWithoutWrapper()
    {
        var engine = new Engine();

        Assert.Equal("3.14", engine.Evaluate("(3.14159).toFixed(2)").AsString());
        Assert.Equal("3.14", engine.Evaluate("(3.14159).toPrecision(3)").AsString());
        Assert.Equal("ff", engine.Evaluate("(255).toString(16)").AsString());
        Assert.Equal("101", engine.Evaluate("(5).toString(2)").AsString());
        Assert.Equal("10", engine.Evaluate("(10).toString()").AsString());
        Assert.Equal(42d, engine.Evaluate("(42).valueOf()").AsNumber());
        Assert.Equal("1.23e+3", engine.Evaluate("(1234).toExponential(2)").AsString());
        Assert.Equal("-3", engine.Evaluate("(-2.5).toFixed(0)").AsString());

        // computed-key and identifier-base (read-then-call) resolution paths
        Assert.Equal("ff", engine.Evaluate("(255)['toString'](16)").AsString());
        Assert.Equal("ff", engine.Evaluate("var n = 255; var f = n.toString; f.call(255, 16)").AsString());

        // absent property resolves to undefined (not a throw)
        Assert.Equal("undefined", engine.Evaluate("typeof (5).nope").AsString());
    }

    [Fact]
    public void PatchedNumberPrototypeMethodSeesBoxedThisInSloppyMode()
    {
        var engine = new Engine();
        engine.Execute("Number.prototype.typ = function () { return typeof this; };");

        // Sloppy-mode user function boxes the primitive this-value, so it must observe an object.
        Assert.Equal("object", engine.Evaluate("(5).typ()").AsString());
        // and can still unwrap the underlying number
        engine.Execute("Number.prototype.dbl = function () { return this.valueOf() * 2; };");
        Assert.Equal(42d, engine.Evaluate("(21).dbl()").AsNumber());

        // resolution stays live after the method is reassigned
        engine.Execute("Number.prototype.typ = function () { return 'reassigned'; };");
        Assert.Equal("reassigned", engine.Evaluate("(5).typ()").AsString());
    }

    [Fact]
    public void PatchedNumberPrototypeMethodSeesPrimitiveThisInStrictMode()
    {
        var engine = new Engine();
        engine.Execute("Number.prototype.styp = function () { 'use strict'; return typeof this; };");

        // Strict-mode user function does not box the this-value, so it must observe the primitive.
        Assert.Equal("number", engine.Evaluate("(5).styp()").AsString());
        engine.Execute("Number.prototype.sinc = function () { 'use strict'; return this + 1; };");
        Assert.Equal(42d, engine.Evaluate("(41).sinc()").AsNumber());
    }

    [Fact]
    public void GetterOnNumberPrototypeReceivesCorrectReceiver()
    {
        var engine = new Engine();
        engine.Execute("Object.defineProperty(Number.prototype, 'gs', { get: function () { return typeof this; }, configurable: true });");
        engine.Execute("Object.defineProperty(Number.prototype, 'gt', { get: function () { 'use strict'; return typeof this; }, configurable: true });");

        Assert.Equal("object", engine.Evaluate("(7).gs").AsString());
        Assert.Equal("number", engine.Evaluate("(7).gt").AsString());
    }

    [Fact]
    public void NumberMethodResolvesThroughObjectPrototype()
    {
        var engine = new Engine();
        engine.Execute("Object.prototype.op = function () { return 'fromObjectProto'; };");

        // property inherited from Object.prototype (past Number.prototype) still resolves
        Assert.Equal("fromObjectProto", engine.Evaluate("(5).op()").AsString());
        Assert.False(engine.Evaluate("(5).hasOwnProperty('x')").AsBoolean());
    }

    [Fact]
    public void ProxyInNumberPrototypeChainReceivesPrimitiveReceiver()
    {
        var engine = new Engine();
        engine.Execute("""
            var handler = { get: function (t, p, receiver) { return p === 'trapped' ? 'viaProxy:' + (typeof receiver) : Reflect.get(t, p, receiver); } };
            Object.setPrototypeOf(Number.prototype, new Proxy({ base: 1 }, handler));
            """);

        // spec: GetValue passes the primitive base as the receiver to [[Get]], so a Proxy get trap
        // on the prototype chain observes the primitive - identical to the boxed path.
        Assert.Equal("viaProxy:number", engine.Evaluate("(5).trapped").AsString());
        Assert.Equal(1d, engine.Evaluate("(5).base").AsNumber());
    }

    [Fact]
    public void BooleanPrimitiveMethodsResolveWithoutWrapper()
    {
        var engine = new Engine();

        Assert.Equal("true", engine.Evaluate("(true).toString()").AsString());
        Assert.Equal("false", engine.Evaluate("(false).toString()").AsString());
        Assert.True(engine.Evaluate("(true).valueOf()").AsBoolean());

        engine.Execute("Boolean.prototype.typ = function () { return typeof this; };");
        Assert.Equal("object", engine.Evaluate("(true).typ()").AsString());
        engine.Execute("Boolean.prototype.styp = function () { 'use strict'; return typeof this; };");
        Assert.Equal("boolean", engine.Evaluate("(true).styp()").AsString());
    }

    [Fact]
    public void BigIntPrimitiveMethodsResolveWithoutWrapper()
    {
        var engine = new Engine();

        Assert.Equal("ff", engine.Evaluate("(255n).toString(16)").AsString());
        Assert.Equal("10", engine.Evaluate("(10n).valueOf().toString()").AsString());
        Assert.Equal("12345678901234567890", engine.Evaluate("(12345678901234567890n).toString()").AsString());

        engine.Execute("BigInt.prototype.typ = function () { return typeof this; };");
        Assert.Equal("object", engine.Evaluate("(5n).typ()").AsString());
        engine.Execute("BigInt.prototype.styp = function () { 'use strict'; return typeof this; };");
        Assert.Equal("bigint", engine.Evaluate("(5n).styp()").AsString());
    }
}
