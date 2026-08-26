using System.Globalization;
using System.Reflection;
using Jint.Native;
using Jint.Native.Array;
using Jint.Native.Number;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Debugger;
using Jint.Tests.Runtime.Debugger;

#pragma warning disable 618

namespace Jint.Tests.Runtime;

public partial class EngineTests : IDisposable
{
    private readonly Engine _engine;
    private int countBreak = 0;
    private StepMode stepMode;
    private static readonly TimeZoneInfo _pacificTimeZone;
    private static readonly TimeZoneInfo _tongaTimeZone;
    private static readonly TimeZoneInfo _easternTimeZone;

    static EngineTests()
    {
        // https://stackoverflow.com/questions/47848111/how-should-i-fetch-timezoneinfo-in-a-platform-agnostic-way
        // should be natively supported soon https://github.com/dotnet/runtime/issues/18644
        try
        {
            _pacificTimeZone = TimeZoneInfo.FindSystemTimeZoneById("America/Los_Angeles");
        }
        catch (TimeZoneNotFoundException)
        {
            _pacificTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Pacific Standard Time");
        }

        try
        {
            _tongaTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Pacific/Tongatapu");
        }
        catch (TimeZoneNotFoundException)
        {
            _tongaTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Tonga Standard Time");
        }

        try
        {
            _easternTimeZone = TimeZoneInfo.FindSystemTimeZoneById("America/New_York");
        }
        catch (TimeZoneNotFoundException)
        {
            _easternTimeZone = TimeZoneInfo.FindSystemTimeZoneById("US Eastern Standard Time");
        }
    }

    public EngineTests()
    {
        _engine = new Engine()
                .SetValue("log", new Action<object>(o => TestContext.Out.WriteLine(o.ToString())))
                .SetValue("assert", new Action<bool>(static value => value.Should().BeTrue()))
                .SetValue("equal", new Action<object, object>(static (expected, actual) =>
                    actual.Should().BeEquivalentTo(expected, static options => options.WithStrictOrdering())))
            ;
    }

    void IDisposable.Dispose()
    {
    }


    private void RunTest(string source)
    {
        _engine.Execute(source);
    }

    internal static string GetEmbeddedFile(string filename)
    {
        const string Prefix = "Jint.Tests.Runtime.Scripts.";

        var assembly = typeof(EngineTests).GetTypeInfo().Assembly;
        var scriptPath = Prefix + filename;

        using var stream = assembly.GetManifestResourceStream(scriptPath);
        using var sr = new StreamReader(stream);
        return sr.ReadToEnd();
    }

    [TestCase(42d, "42")]
    [TestCase("Hello", "'Hello'")]
    public void ShouldInterpretLiterals(object expected, string source)
    {
        var engine = new Engine();
        var result = engine.Evaluate(source).ToObject();

        result.Should().Be(expected);
    }

    [Test]
    public void ShouldInterpretVariableDeclaration()
    {
        var engine = new Engine();
        var result = engine
            .Evaluate("var foo = 'bar'; foo;")
            .ToObject();

        result.Should().Be("bar");
    }

    [TestCase(4d, "1 + 3")]
    [TestCase(-2d, "1 - 3")]
    [TestCase(3d, "1 * 3")]
    [TestCase(2d, "6 / 3")]
    [TestCase(9d, "15 & 9")]
    [TestCase(15d, "15 | 9")]
    [TestCase(6d, "15 ^ 9")]
    [TestCase(36d, "9 << 2")]
    [TestCase(2d, "9 >> 2")]
    [TestCase(4d, "19 >>> 2")]
    public void ShouldInterpretBinaryExpression(object expected, string source)
    {
        var engine = new Engine();
        var result = engine.Evaluate(source).ToObject();

        result.Should().Be(expected);
    }

    [TestCase(-59d, "~58")]
    [TestCase(58d, "~~58")]
    public void ShouldInterpretUnaryExpression(object expected, string source)
    {
        var engine = new Engine();
        var result = engine.Evaluate(source).ToObject();

        result.Should().Be(expected);
    }

    [Test]
    public void ShouldHaveProperReferenceErrorMessage()
    {
        RunTest(@"
                'use strict';
                var arr = [1, 2];
                try {
                    for (i in arr) { }
                    assert(false);
                }
                catch (ex) {
                    assert(ex.message === 'i is not defined');
                }
            ");
    }

    [Test]
    public void ShouldHaveProperNotAFunctionErrorMessage()
    {
        RunTest(@"
                try {
                    var example = {};
                    example();
                    assert(false);
                }
                catch (ex) {
                    assert(ex.message === 'example is not a function');
                }
            ");
    }

    [Test]
    public void ShouldEvaluateHasOwnProperty()
    {
        RunTest(@"
                var x = {};
                x.Bar = 42;
                assert(x.hasOwnProperty('Bar'));
            ");
    }

    [Test]
    public void ShouldAllowNullAsStringValue()
    {
        var engine = new Engine().SetValue("name", (string) null);
        engine.Evaluate("name").IsNull().Should().BeTrue();
    }

    [Test]
    public void FunctionConstructorsShouldCreateNewObjects()
    {
        RunTest(@"
                var Vehicle = function () {};
                var vehicle = new Vehicle();
                assert(vehicle != undefined);
            ");
    }

    [Test]
    public void NewObjectsInheritFunctionConstructorProperties()
    {
        RunTest(@"
                var Vehicle = function () {};
                var vehicle = new Vehicle();
                Vehicle.prototype.wheelCount = 4;
                assert(vehicle.wheelCount == 4);
                assert((new Vehicle()).wheelCount == 4);
            ");
    }

    [Test]
    public void PrototypeFunctionIsInherited()
    {
        RunTest(@"
                function Body(mass){
                   this.mass = mass;
                }

                Body.prototype.offsetMass = function(dm) {
                   this.mass += dm;
                   return this;
                }

                var b = new Body(36);
                b.offsetMass(6);
                assert(b.mass == 42);
            ");

    }


    [Test]
    public void FunctionConstructorCall()
    {
        RunTest(@"
                function Body(mass){
                   this.mass = mass;
                }

                var john = new Body(36);
                assert(john.mass == 36);
            ");
    }

    [Test]
    public void ArrowFunctionCall()
    {
        RunTest(@"
                var add = (a, b) => {
                    return a + b;
                }

                var x = add(1, 2);
                assert(x == 3);
            ");
    }

    [Test]
    public void ArrowFunctionExpressionCall()
    {
        RunTest(@"
                var add = (a, b) => a + b;

                var x = add(1, 2);
                assert(x === 3);
            ");
    }

    [Test]
    public void ArrowFunctionScope()
    {
        RunTest(@"
                var bob = {
                    _name: ""Bob"",
                    _friends: [""Alice""],
                    printFriends() {
                        this._friends.forEach(f => assert(this._name === ""Bob"" && f === ""Alice""))
                    }
                };
                bob.printFriends();
            ");
    }

    [Test]
    public void NewObjectsShouldUsePrivateProperties()
    {
        RunTest(@"
                var Vehicle = function (color) {
                    this.color = color;
                };
                var vehicle = new Vehicle('tan');
                assert(vehicle.color == 'tan');
            ");
    }

    [Test]
    public void FunctionConstructorsShouldDefinePrototypeChain()
    {
        RunTest(@"
                function Vehicle() {};
                var vehicle = new Vehicle();
                assert(vehicle.hasOwnProperty('constructor') == false);
            ");
    }

    [Test]
    public void NewObjectsConstructorIsObject()
    {
        RunTest(@"
                var o = new Object();
                assert(o.constructor == Object);
            ");
    }

    [Test]
    public void NewObjectsIntanceOfConstructorObject()
    {
        RunTest(@"
                var o = new Object();
                assert(o instanceof Object);
            ");
    }

    [Test]
    public void NewObjectsConstructorShouldBeConstructorObject()
    {
        RunTest(@"
                var Vehicle = function () {};
                var vehicle = new Vehicle();
                assert(vehicle.constructor == Vehicle);
            ");
    }

    [Test]
    public void NewObjectsIntanceOfConstructorFunction()
    {
        RunTest(@"
                var Vehicle = function () {};
                var vehicle = new Vehicle();
                assert(vehicle instanceof Vehicle);
            ");
    }

    [Test]
    public void ShouldEvaluateForLoops()
    {
        RunTest(@"
                var foo = 0;
                for (var i = 0; i < 5; i++) {
                    foo += i;
                }
                assert(foo == 10);
            ");
    }

    [Test]
    public void ShouldEvaluateRecursiveFunctions()
    {
        RunTest(@"
                function fib(n) {
                    if (n < 2) {
                        return n;
                    }
                    return fib(n - 1) + fib(n - 2);
                }
                var result = fib(6);
                assert(result == 8);
            ");
    }

    [Test]
    public void ShouldAccessObjectProperties()
    {
        RunTest(@"
                var o = {};
                o.Foo = 'bar';
                o.Baz = 42;
                o.Blah = o.Foo + o.Baz;
                assert(o.Blah == 'bar42');
            ");
    }


    [Test]
    public void ShouldConstructArray()
    {
        RunTest(@"
                var o = [];
                assert(o.length == 0);
            ");
    }

    [Test]
    public void ArrayPushShouldIncrementLength()
    {
        RunTest(@"
                var o = [];
                o.push(1);
                assert(o.length == 1);
            ");
    }

    [Test]
    public void ArrayFunctionInitializesLength()
    {
        RunTest(@"
                assert(Array(3).length == 3);
                assert(Array('3').length == 1);
            ");
    }

    [Test]
    public void ArrayIndexerIsAssigned()
    {
        RunTest(@"
                var n = 8;
                var o = Array(n);
                for (var i = 0; i < n; i++) o[i] = i;
                equal(0, o[0]);
                equal(7, o[7]);
            ");
    }

    [Test]
    public void DenseArrayTurnsToSparseArrayWhenSizeGrowsTooMuch()
    {
        RunTest(@"
                var n = 1024*10+2;
                var o = Array(n);
                for (var i = 0; i < n; i++) o[i] = i;
                equal(0, o[0]);
                equal(n -1, o[n - 1]);
            ");
    }

    [Test]
    public void DenseArrayTurnsToSparseArrayWhenSparseIndexed()
    {
        RunTest(@"
                var o = Array();
                o[100] = 1;
                assert(o[100] == 1);
            ");
    }

    [Test]
    public void ArrayPopShouldDecrementLength()
    {
        RunTest(@"
                var o = [42, 'foo'];
                var pop = o.pop();
                assert(o.length == 1);
                assert(pop == 'foo');
            ");
    }

    [Test]
    public void ArrayConstructor()
    {
        RunTest(@"
                var o = [];
                assert(o.constructor == Array);
            ");
    }

    [Test]
    public void DateConstructor()
    {
        RunTest(@"
                var o = new Date();
                assert(o.constructor == Date);
                assert(o.hasOwnProperty('constructor') == false);
            ");
    }

    [Test]
    public void DateConstructorWithInvalidParameters()
    {
        RunTest(@"
                var dt = new Date (1,  Infinity);
                assert(isNaN(dt.getTime()));
            ");
    }

    [Test]
    public void ShouldConvertDateToNumber()
    {
        RunTest(@"
                assert(Number(new Date(0)) === 0);
            ");
    }

    [Test]
    public void MathObjectIsDefined()
    {
        RunTest(@"
                var o = Math.abs(-1)
                assert(o == 1);
            ");
    }

    [Test]
    public void VoidShouldReturnUndefined()
    {
        RunTest(@"
                assert(void 0 === undefined);
                var x = '1';
                assert(void x === undefined);
                x = 'x';
                assert (isNaN(void x) === true);
                x = new String('-1');
                assert (void x === undefined);
            ");
    }

    [Test]
    public void TypeofObjectShouldReturnString()
    {
        RunTest(@"
                assert(typeof x === 'undefined');
                assert(typeof 0 === 'number');
                var x = 0;
                assert (typeof x === 'number');
                var x = new Object();
                assert (typeof x === 'object');
            ");
    }

    [Test]
    public void MathAbsReturnsAbsolute()
    {
        RunTest(@"
                assert(1 == Math.abs(-1));
            ");
    }

    [Test]
    public void NaNIsNan()
    {
        RunTest(@"
                var x = NaN;
                assert(isNaN(NaN));
                assert(isNaN(Math.abs(x)));
            ");
    }

    [TestCase(2147483647, 1, 2147483648)]
    [TestCase(-2147483647, -2, -2147483649)]
    public void IntegerAdditionShouldNotOverflow(int lhs, int rhs, long result)
    {
        RunTest($"assert({lhs} + {rhs} == {result})");
    }

    [TestCase(2147483647, -1, 2147483648)]
    [TestCase(-2147483647, 2, -2147483649)]
    public void IntegerSubtractionShouldNotOverflow(int lhs, int rhs, long result)
    {
        RunTest($"assert({lhs} - {rhs} == {result})");
    }

    [Test]
    public void ToNumberHandlesStringObject()
    {
        RunTest(@"
                x = new String('1');
                x *= undefined;
                assert(isNaN(x));
            ");
    }

    [Test]
    public void FunctionScopesAreChained()
    {
        RunTest(@"
                var x = 0;

                function f1(){
                  function f2(){
                    return x;
                  };
                  return f2();

                  var x = 1;
                }

                assert(f1() === undefined);
            ");
    }

    [Test]
    public void EvalFunctionParseAndExecuteCode()
    {
        RunTest(@"
                var x = 0;
                eval('assert(x == 0)');
            ");
    }

    [Test]
    public void EvalFunctionWithTargetNewParse()
    {
        RunTest(@"
                const code = `function MyClass() {
                   if (!new.target) throw new Error('Use MyClass as constructor!');
                }`;
                eval(code);
                const code2 = `var x = function () {
                   if (!new.target) throw new Error('Use as constructor!');
                }`;
                eval(code2);
            ");
    }

    [Test]
    public void ForInStatement()
    {
        var engine = new Engine();
        var result = engine.Evaluate("""
                                            var x, y, str = '';
                                            for(var z in this) {
                                             str += z;
                                            }
                                            return str;
                                     """);
        result.Should().Be("xystrz");
    }

    [Test]
    public void ForInStatementEnumeratesKeys()
    {
        RunTest(@"
                for(var i in 'abc');
				log(i);
                assert(i === '2');
            ");
    }

    [Test]
    public void WithStatement()
    {
        RunTest(@"
                with (Math) {
                  assert(cos(0) == 1);
                }
            ");
    }

    [Test]
    public void ObjectExpression()
    {
        RunTest(@"
                var o = { x: 1 };
                assert(o.x == 1);
            ");
    }

    [Test]
    public void StringFunctionCreatesString()
    {
        RunTest(@"
                assert(String(NaN) === 'NaN');
            ");
    }

    [Test]
    public void ScopeChainInWithStatement()
    {
        RunTest(@"
                var x = 0;
                var myObj = {x : 'obj'};

                function f1(){
                  var x = 1;
                  function f2(){
                    with(myObj){
                      return x;
                    }
                  };
                  return f2();
                }

                assert(f1() === 'obj');
            ");
    }

    [Test]
    public void TryCatchBlockStatement()
    {
        RunTest(@"
                var x, y, z;
                try {
                    x = 1;
                    throw new TypeError();
                    x = 2;
                }
                catch(e) {
                    assert(x == 1);
                    assert(e instanceof TypeError);
                    y = 1;
                }
                finally {
                    assert(x == 1);
                    z = 1;
                }

                assert(x == 1);
                assert(y == 1);
                assert(z == 1);
            ");
    }

    [Test]
    public void FunctionsCanBeAssigned()
    {
        RunTest(@"
                var sin = Math.sin;
                assert(sin(0) == 0);
            ");
    }

    [Test]
    public void FunctionArgumentsIsDefined()
    {
        RunTest(@"
                function f() {
                    assert(arguments.length > 0);
                }

                f(42);
            ");
    }

    [Test]
    public void PrimitiveValueFunctions()
    {
        RunTest(@"
                var s = (1).toString();
                assert(s == '1');
            ");
    }

    [TestCase(true, "'ab' == 'a' + 'b'")]
    public void OperatorsPrecedence(object expected, string source)
    {
        var engine = new Engine();
        var result = engine.Evaluate(source).ToObject();

        result.Should().Be(expected);
    }

    [Test]
    public void FunctionPrototypeShouldHaveApplyMethod()
    {
        RunTest(@"
                var numbers = [5, 6, 2, 3, 7];
                var max = Math.max.apply(null, numbers);
                assert(max == 7);
            ");
    }

    [TestCase(double.NaN, "parseInt(NaN)")]
    [TestCase(double.NaN, "parseInt(null)")]
    [TestCase(double.NaN, "parseInt(undefined)")]
    [TestCase(double.NaN, "parseInt(new Boolean(true))")]
    [TestCase(double.NaN, "parseInt(Infinity)")]
    [TestCase(-1d, "parseInt(-1)")]
    [TestCase(-1d, "parseInt('-1')")]
    [TestCase(double.NaN, "parseInt(new Array(100000).join('Z'))")]
    public void ShouldEvaluateParseInt(object expected, string source)
    {
        var engine = new Engine();
        var result = engine.Evaluate(source).ToObject();

        result.Should().Be(expected);
    }

    [Test]
    public void ShouldNotExecuteDebuggerStatement()
    {
        new Engine().Evaluate("debugger");
    }

    [Test]
    public void ShouldConvertDoubleToStringWithoutLosingPrecision()
    {
        RunTest(@"
                assert(String(14.915832707045631) === '14.915832707045631');
                assert(String(-14.915832707045631) === '-14.915832707045631');
                assert(String(0.5) === '0.5');
                assert(String(0.00000001) === '1e-8');
                assert(String(0.000001) === '0.000001');
                assert(String(-1.0) === '-1');
                assert(String(30.0) === '30');
                assert(String(0.2388906159889881) === '0.2388906159889881');
            ");
    }

    [Test]
    public void ShouldWriteNumbersUsingBases()
    {
        RunTest(@"
                assert(15.0.toString() === '15');
                assert(15.0.toString(2) === '1111');
                assert(15.0.toString(8) === '17');
                assert(15.0.toString(16) === 'f');
                assert(15.0.toString(17) === 'f');
                assert(15.0.toString(36) === 'f');
                assert(15.1.toString(36) === 'f.3llllllllkau6snqkpygsc3di');
            ");
    }

    [Test]
    public void ShouldWriteLargeNumbersUsingBasesWithoutOverflow()
    {
        // Values above long.MaxValue (~9.22e18) previously overflowed when cast to long,
        // producing wrong results or runtime errors. The expected strings below are the
        // mathematically exact base-r representation of the stored double value.
        RunTest(@"
                // Power-of-2 radices: bit-exact, matches V8/SpiderMonkey output.
                assert((-12345e+30).toString(2) === '-100110000010100111110011100101010111110111100101011010000000000000000000000000000000000000000000000000000000000000');
                assert((-12345e+30).toString(16) === '-260a7ce55f795a000000000000000');
                assert((1e+20).toString(16) === '56bc75e2d63100000');
                assert((1e+20).toString(8) === '12657072742654304000000');

                // Non-power-of-2 radices: previously gave wrong digits (or threw).
                // Jint emits the mathematically exact representation of the stored double,
                // which differs from V8's approximation for these values - both are valid
                // per the 'implementation-approximated' clause of the spec.
                assert((1e+20).toString(3) === '220200020122120112010222022122002000100201');
                assert((1e+20).toString(7) === '344015313561621001452562');
                assert((1e+25).toString(36) === '198exbvshgq9n8mps');
                assert((-12345e+30).toString(5) === '-3214133420230123110000004012030333321240102013132');

                // Small values must still go through the fast (long) path.
                assert((255).toString(16) === 'ff');
                assert((-255).toString(16) === '-ff');
                assert((0).toString(2) === '0');
                assert((1).toString(2) === '1');
                assert((15.1).toString(36) === 'f.3llllllllkau6snqkpygsc3di');

                // Around the long.MaxValue boundary (2^63 = 9223372036854775808 as double).
                assert((9223372036854775000).toString(16) === '7ffffffffffffc00');
                assert((-9223372036854775000).toString(16) === '-7ffffffffffffc00');
            ");
    }

    [Test]
    public void ShouldNotAlterSlashesInRegex()
    {
        RunTest(@"
                equal('/\\//', new RegExp('/').toString());
            ");
    }

    [Test]
    public void ShouldHandleEscapedSlashesInRegex()
    {
        RunTest(@"
                var regex = /[a-z]\/[a-z]/;
                assert(regex.test('a/b') === true);
                assert(regex.test('a\\/b') === false);
            ");
    }

    [Test]
    public void ShouldComputeFractionInBase()
    {
        NumberPrototype.ToFractionBase(0.375, 2).Should().Be("011");
        NumberPrototype.ToFractionBase(0.375, 5).Should().Be("14141414141414141414141414141414141414141414141414");
    }

    [Test]
    public void ShouldInvokeAFunctionValue()
    {
        RunTest(@"
                function add(x, y) { return x + y; }
            ");

        var add = _engine.GetValue("add");

        _engine.Invoke(add, 1, 2).Should().Be(3);
    }

    [Test]
    public void ShouldAllowInvokeAFunctionValueWithNullValueAsArgument()
    {
        RunTest(@"
                function get(x) { return x; }
            ");

        var add = _engine.GetValue("get");
        string str = null;
        _engine.Invoke(add, str).Should().Be(Native.JsValue.Null);
    }


    [Test]
    public void ShouldNotInvokeNonFunctionValue()
    {
        RunTest(@"
                var x= 10;
            ");

        var x = _engine.GetValue("x");

        var exception = Invoking(() => _engine.Invoke(x, 1, 2)).Should().ThrowExactly<JavaScriptException>().Which;
        exception.Message.Should().Be("Can only invoke functions");
    }

    [Test]
    public void ShouldInvokeAFunctionValueThatBelongsToAnObject()
    {
        RunTest(@"
                var obj = { foo: 5, getFoo: function (bar) { return 'foo is ' + this.foo + ', bar is ' + bar; } };
            ");

        var obj = _engine.GetValue("obj").AsObject();
        var getFoo = obj.Get("getFoo");

        _engine.Invoke(getFoo, obj, new object[] { 7 }).AsString().Should().Be("foo is 5, bar is 7");
    }

    [Test]
    public void ShouldNotInvokeNonFunctionValueThatBelongsToAnObject()
    {
        RunTest(@"
                var obj = { foo: 2 };
            ");

        var obj = _engine.GetValue("obj").AsObject();
        var foo = obj.Get("foo");

        Invoking(() => _engine.Invoke(foo, obj, new object[] { })).Should().ThrowExactly<JavaScriptException>();
    }

    [Test]
    public void ShouldNotAllowModifyingSharedUndefinedDescriptor()
    {
        var e = new Engine();
        e.Evaluate("var x = { literal: true };");

        var pd = e.GetValue("x").AsObject().GetOwnProperty("doesNotExist");
        Invoking(() => pd.Value = "oh no, assigning this breaks things").Should().ThrowExactly<InvalidOperationException>();
    }

    [TestCase("0", 0, 16)]
    [TestCase("1", 1, 16)]
    [TestCase("100", 100, 10)]
    [TestCase("1100100", 100, 2)]
    [TestCase("2s", 100, 36)]
    [TestCase("2qgpckvng1s", 10000000000000000L, 36)]
    public void ShouldConvertNumbersToDifferentBase(string expected, long number, int radix)
    {
        var result = NumberPrototype.ToBase(number, radix);
        result.Should().Be(expected);
    }

    [Test]
    public void JsonParserShouldParseNegativeNumber()
    {
        RunTest(@"
                var a = JSON.parse('{ ""x"":-1 }');
                assert(a.x === -1);

                var b = JSON.parse('{ ""x"": -1 }');
                assert(b.x === -1);
            ");
    }

    [Test]
    public void JsonParserShouldUseToString()
    {
        RunTest(@"
                var a = JSON.parse(null); // Equivalent to JSON.parse('null')
                assert(a === null);
            ");

        RunTest(@"
                var a = JSON.parse(true); // Equivalent to JSON.parse('true')
                assert(a === true);
            ");

        RunTest(@"
                var a = JSON.parse(false); // Equivalent to JSON.parse('false')
                assert(a === false);
            ");

        RunTest(@"
                try {
                    JSON.parse(undefined); // Equivalent to JSON.parse('undefined')
                    assert(false);
                }
                catch(ex) {
                    assert(ex instanceof SyntaxError);
                }
            ");

        RunTest(@"
                try {
                    JSON.parse({}); // Equivalent to JSON.parse('[object Object]')
                    assert(false);
                }
                catch(ex) {
                    assert(ex instanceof SyntaxError);
                }
            ");

        RunTest(@"
                try {
                    JSON.parse(function() { }); // Equivalent to JSON.parse('function () {}')
                    assert(false);
                }
                catch(ex) {
                    assert(ex instanceof SyntaxError);
                }
            ");
    }

    [Test]
    public void JsonParserShouldDetectInvalidNegativeNumberSyntax()
    {
        RunTest(@"
                try {
                    JSON.parse('{ ""x"": -.1 }'); // Not allowed
                    assert(false);
                }
                catch(ex) {
                    assert(ex instanceof SyntaxError);
                }
            ");

        RunTest(@"
                try {
                    JSON.parse('{ ""x"": - 1 }'); // Not allowed
                    assert(false);
                }
                catch(ex) {
                    assert(ex instanceof SyntaxError);
                }
            ");
    }

    [Test]
    public void JsonParserShouldUseReviverFunction()
    {
        RunTest(@"
                var jsonObj = JSON.parse('{""p"": 5}', function (key, value){
                    return typeof value === 'number' ? value * 2 : value;
                });
                assert(jsonObj.p === 10);
            ");

        RunTest(@"
                var expectedKeys = [""1"", ""2"", ""4"", ""6"", ""5"", ""3"", """"];
                var actualKeys = [];
                JSON.parse('{""1"": 1, ""2"": 2, ""3"": {""4"": 4, ""5"": {""6"": 6}}}', function (key, value){
                    actualKeys.push(key);
                    return value;// return the unchanged property value.
                });
                expectedKeys.forEach(function (val, i){
                    assert(actualKeys[i] === val);
                });
            ");
    }

    [Test]
    public void JsonParserShouldHandleEmptyString()
    {
        var ex = Invoking(() => _engine.Evaluate("JSON.parse('');")).Should().ThrowExactly<JavaScriptException>().Which;
        ex.Message.Should().Be("Unexpected end of JSON input at position 0");
    }

    [Test]
    [SetCulture("fr-FR"), SetUICulture("fr-FR")]
    public void ShouldBeCultureInvariant()
    {
        // decimals in french are separated by commas
        var engine = new Engine();

        var result = engine.Evaluate("1.2 + 2.1").AsNumber();
        result.Should().Be(3.3d);

        result = engine.Evaluate("JSON.parse('{\"x\" : 3.3}').x").AsNumber();
        result.Should().Be(3.3d);
    }

    [Test]
    public void ShouldGetParseErrorLocation()
    {
        var engine = new Engine();
        try
        {
            engine.Evaluate("1.2+ new", "jQuery.js");
        }
        catch (JavaScriptException e)
        {
            e.Location.Start.Line.Should().Be(1);
            e.Location.Start.Column.Should().Be(8);
            e.Location.SourceFile.Should().Be("jQuery.js");
        }
    }
    #region DateParsingAndStrings
    [Test]
    public void ParseShouldReturnNumber()
    {
        var engine = new Engine();

        var result = engine.Evaluate("Date.parse('1970-01-01');").AsNumber();
        result.Should().Be(0);
    }

    [Test]
    public void TimeWithinDayShouldHandleNegativeValues()
    {
        RunTest(@"
                // using a date < 1970 so that the primitive value is negative
                var d = new Date(1958, 0, 1);
                d.setMonth(-1);
                assert(d.getDate() == 1);
            ");
    }

    [Test]
    public void LocalDateTimeShouldNotLoseTimezone()
    {
        var date = new DateTime(2016, 1, 1, 13, 0, 0, DateTimeKind.Local);
        var engine = new Engine().SetValue("localDate", date);
        var actual = engine.Evaluate(@"localDate").AsDate().ToDateTime();
        actual.ToUniversalTime().Should().Be(date.ToUniversalTime());
        actual.ToLocalTime().Should().Be(date.ToLocalTime());
    }

    [Test]
    public void UtcShouldUseUtc()
    {
        var customTimeZone = _tongaTimeZone;

        var engine = new Engine(cfg => cfg.TimeZone = customTimeZone);

        var result = engine.Evaluate("Date.UTC(1970,0,1)").AsNumber();
        result.Should().Be(0);
    }

    [Test]
    public void ShouldUseLocalTimeZoneOverride()
    {
        const string customName = "Custom Time";
        var customTimeZone = TimeZoneInfo.CreateCustomTimeZone(customName, new TimeSpan(0, 11, 0), customName, customName, customName, null, false);

        var engine = new Engine(cfg => cfg.TimeZone = customTimeZone);

        var epochGetLocalMinutes = engine.Evaluate("var d = new Date(0); d.getMinutes();").AsNumber();
        epochGetLocalMinutes.Should().Be(11);

        var localEpochGetUtcMinutes = engine.Evaluate("var d = new Date(1970,0,1); d.getUTCMinutes();").AsNumber();
        localEpochGetUtcMinutes.Should().Be(49);

        var parseLocalEpoch = engine.Evaluate("Date.parse('January 1, 1970');").AsNumber();
        parseLocalEpoch.Should().Be(-11 * 60 * 1000);

        var epochToLocalString = engine.Evaluate("var d = new Date(0); d.toString();").AsString();
        epochToLocalString.Should().Be("Thu Jan 01 1970 00:11:00 GMT+0011 (Custom Time)");

        var epochToUTCString = engine.Evaluate("var d = new Date(0); d.toUTCString();").AsString();
        epochToUTCString.Should().Be("Thu, 01 Jan 1970 00:00:00 GMT");
    }

    [TestCase("1970")]
    [TestCase("1970-01")]
    [TestCase("1970-01-01")]
    [TestCase("1970-01-01T00:00Z")]
    [TestCase("1970-01-01T00:00:00Z")]
    [TestCase("1970-01-01T00:00:00.000Z")]
    [TestCase("1970Z")]
    [TestCase("1970-1Z")]
    [TestCase("1970-1-1Z")]
    [TestCase("1970-1-1T0:0Z")]
    [TestCase("1970-1-1T0:0:0Z")]
    [TestCase("1970-1-1T0:0:0.0Z")]
    [TestCase("1970/1Z")]
    [TestCase("1970/1/1Z")]
    [TestCase("1970/1/1 0:0Z")]
    [TestCase("1970/1/1 0:0:0Z")]
    [TestCase("1970/1/1 0:0:0.0Z")]
    [TestCase("January 1, 1970 GMT")]
    [TestCase("1970-01-01T00:00:00.000-00:00")]
    public void ShouldParseAsUtc(string date)
    {
        var customTimeZone = _tongaTimeZone;
        var engine = new Engine(cfg => cfg.TimeZone = customTimeZone);

        engine.SetValue("d", date);
        var result = engine.Evaluate("Date.parse(d);").AsNumber();

        result.Should().Be(0);
    }

    [TestCase("1970-01-01T00:00")]
    [TestCase("1970-01-01T00:00:00")]
    [TestCase("1970-01-01T00:00:00.000")]
    [TestCase("1970/01")]
    [TestCase("1970/01/01")]
    [TestCase("1970/01/01T00:00")]
    [TestCase("1970/01/01 00:00")]
    [TestCase("1970-1")]
    [TestCase("1970-1-1")]
    [TestCase("1970-1-1T0:0")]
    [TestCase("1970-1-1 0:0")]
    [TestCase("1970/1")]
    [TestCase("1970/1/1")]
    [TestCase("1970/1/1T0:0")]
    [TestCase("1970/1/1 0:0")]
    [TestCase("01-1970")]
    [TestCase("01-01-1970")]
    [TestCase("January 1, 1970")]
    [TestCase("1970-01-01T00:00:00.000+00:11")]
    public void ShouldParseAsLocalTime(string date)
    {
        const int timespanMinutes = 11;
        const int msPriorMidnight = -timespanMinutes * 60 * 1000;
        const string customName = "Custom Time";
        var customTimeZone = TimeZoneInfo.CreateCustomTimeZone(customName, new TimeSpan(0, timespanMinutes, 0), customName, customName, customName, null, false);
        var engine = new Engine(cfg => cfg.TimeZone = customTimeZone).SetValue("d", date);

        var result = engine.Evaluate("Date.parse(d);").AsNumber();

        result.Should().Be(msPriorMidnight);
    }

    /// <summary>
    /// Wall-clock readings in the Pacific zone, carried as text rather than as <see cref="DateTime"/>, so
    /// that nothing between the source and the test method can reinterpret a
    /// <see cref="DateTimeKind.Unspecified"/> value as the runner machine's local time and shift it. A
    /// test-case argument that survives as text is one the runner cannot round-trip differently.
    /// </summary>
    public static System.Collections.Generic.IEnumerable<object[]> TestDates
    {
        get
        {
            yield return ["2000-01-01T00:00:00.000"];
            yield return ["2000-01-01T00:15:15.015"];
            yield return ["2000-06-01T00:15:15.015"];
            yield return ["1900-01-01T00:00:00.000"];
            yield return ["1900-01-01T00:15:15.015"];
            yield return ["1900-06-01T00:15:15.015"];
        }
    }

    private static DateTime ParseTestDate(string testDate)
        => DateTime.ParseExact(testDate, "yyyy-MM-dd'T'HH:mm:ss.fff", CultureInfo.InvariantCulture);

    [TestCaseSource(nameof(TestDates))]
    public void TestDateToISOStringFormat(string testDate)
    {
        var customTimeZone = _pacificTimeZone;

        var engine = new Engine(ctx => ctx.TimeZone = customTimeZone);
        var date = ParseTestDate(testDate);
        var testDateTimeOffset = new DateTimeOffset(date, customTimeZone.GetUtcOffset(date));
        engine.Execute(
            string.Format("var d = new Date({0},{1},{2},{3},{4},{5},{6});", testDateTimeOffset.Year, testDateTimeOffset.Month - 1, testDateTimeOffset.Day, testDateTimeOffset.Hour, testDateTimeOffset.Minute, testDateTimeOffset.Second, testDateTimeOffset.Millisecond));
        engine.Evaluate("d.toISOString();").ToString().Should().Be(testDateTimeOffset.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture));
    }

    [TestCaseSource(nameof(TestDates))]
    public void TestDateToStringFormat(string testDate)
    {
        var customTimeZone = _pacificTimeZone;

        var engine = new Engine(ctx => ctx.TimeZone = customTimeZone);
        var date = ParseTestDate(testDate);
        var dt = new DateTimeOffset(date, customTimeZone.GetUtcOffset(date));
        var dateScript = $"var d = new Date({dt.Year}, {dt.Month - 1}, {dt.Day}, {dt.Hour}, {dt.Minute}, {dt.Second}, {dt.Millisecond});";
        engine.Execute(dateScript);

        var expected = dt.ToString("ddd MMM dd yyyy HH:mm:ss", CultureInfo.InvariantCulture);
        expected += dt.ToString(" 'GMT'zzz", CultureInfo.InvariantCulture).Replace(":", "");
        var tzName = customTimeZone.IsDaylightSavingTime(dt) ? customTimeZone.DaylightName : customTimeZone.StandardName;
        expected += " (" + tzName + ")";
        var actual = engine.Evaluate("d.toString();").ToString();

        actual.Should().Be(expected);
    }

    #endregion

    //DateParsingAndStrings
    [Test]
    public void EmptyStringShouldMatchRegex()
    {
        RunTest(@"
                var regex = /^(?:$)/g;
                assert(''.match(regex) instanceof Array);
            ");
    }

    [Test]
    public void ShouldExecuteHandlebars()
    {
        var content = GetEmbeddedFile("handlebars.js");

        RunTest(content);

        RunTest(@"
                var source = 'Hello {{name}}';
                var template = Handlebars.compile(source);
                var context = {name: 'Paul'};
                var html = template(context);

                assert('Hello Paul' == html);
            ");
    }

    [Test]
    public void ShouldExecutePrism()
    {
        var content = GetEmbeddedFile("prism.js");

        RunTest(content);

        RunTest(@"
                var input = 'using System; public class Person { public int Name { get; set; } }';
                var lang = 'csharp';
                var highlighted = Prism.highlight(input, Prism.languages.csharp, lang);

                assert(highlighted.includes('System'));
                assert(highlighted.includes('Person'));
                assert(highlighted.includes('Name'));

                log(highlighted);
            ");

        _engine.SetValue("input", File.ReadAllText("../../../../Jint/Engine.cs"));
        RunTest("Prism.highlight(input, Prism.languages.csharp, lang);");
    }

    [Test]
    public void ShouldExecuteDromaeoBase64()
    {
        RunTest(@"
var startTest = function () { };
var test = function (name, fn) { fn(); };
var endTest = function () { };
var prep = function (fn) { fn(); };
            ");

        var content = GetEmbeddedFile("dromaeo-string-base64.js");
        RunTest(content);
    }

    [Test]
    public void ShouldExecuteKnockoutWithoutErrorWhetherTolerantOrIntolerant()
    {
        var content = GetEmbeddedFile("knockout-3.4.0.js");
        _engine.Execute(content, parsingOptions: new ScriptParsingOptions { Tolerant = true });
        _engine.Execute(content, parsingOptions: new ScriptParsingOptions { Tolerant = false });
    }

    [Test]
    public void ShouldAllowProtoProperty()
    {
        var code = "if({ __proto__: [] } instanceof Array) {}";
        _engine.Execute(code);
        _engine.Execute($"eval('{code}')");
        _engine.Execute($"new Function('{code}')");
    }

    [Test]
    public void ShouldNotAllowDuplicateProtoProperty()
    {
        var code = "if({ __proto__: [], __proto__:[] } instanceof Array) {}";

        Exception ex = Invoking(() => _engine.Execute(code, parsingOptions: new ScriptParsingOptions { Tolerant = false })).Should().ThrowExactly<JavaScriptException>().Which;
        ex.Message.Should().Contain("Duplicate __proto__ fields are not allowed in object literals");

        ex = Invoking(() => _engine.Execute($"eval('{code}')")).Should().ThrowExactly<JavaScriptException>().Which;
        ex.Message.Should().Contain("Duplicate __proto__ fields are not allowed in object literals");

        Invoking(() => _engine.Execute($"new Function('{code}')")).Should().ThrowExactly<JavaScriptException>();
        ex.Message.Should().Contain("Duplicate __proto__ fields are not allowed in object literals");
    }

    [Test]
    public void ShouldExecuteLodash()
    {
        var content = GetEmbeddedFile("lodash.min.js");

        RunTest(content);
    }

    [Test]
    public void DateParseReturnsNaN()
    {
        RunTest(@"
                var d = Date.parse('not a date');
                assert(isNaN(d));
            ");
    }

    [Test]
    public void ShouldIgnoreHtmlComments()
    {
        RunTest(@"
                var d = Date.parse('not a date'); <!-- a comment -->
                assert(isNaN(d));
            ");
    }

    [Test]
    public void DateShouldAllowEntireDotNetDateRange()
    {
        var engine = new Engine();

        var minValue = engine.Evaluate("new Date('0001-01-01T00:00:00.000Z')").ToObject();
        minValue.Should().Be(new DateTime(1, 1, 1, 0, 0, 0, DateTimeKind.Utc));

        // The .NET Core arm here used to expect .998. That was never about the framework's date range; it
        // was the parser losing a millisecond to a double division, and only on the frameworks whose
        // TotalMilliseconds divides by 10000 rather than multiplying by 1e-4. Counting ticks removes the
        // split, so both target frameworks now read back the millisecond the string names.
        var maxValue = engine.Evaluate("new Date('9999-12-31T23:59:59.999Z')").ToObject();
        maxValue.Should().Be(new DateTime(9999, 12, 31, 23, 59, 59, 999, DateTimeKind.Utc));
    }

    [Test]
    public void ShouldConstructNewArrayWithInteger()
    {
        RunTest(@"
                var a = new Array(3);
                assert(a.length === 3);
                assert(a[0] == undefined);
                assert(a[1] == undefined);
                assert(a[2] == undefined);
            ");
    }

    [Test]
    public void ShouldConstructNewArrayWithString()
    {
        RunTest(@"
                var a = new Array('foo');
                assert(a.length === 1);
                assert(a[0] === 'foo');
            ");
    }

    [Test]
    public void ShouldThrowRangeExceptionWhenConstructedWithNonInteger()
    {
        RunTest(@"
                var result = false;
                try {
                    var a = new Array(3.4);
                }
                catch(e) {
                    result = e instanceof RangeError;
                }

                assert(result);
            ");
    }

    [Test]
    public void ShouldInitializeArrayWithSingleIngegerValue()
    {
        RunTest(@"
                var a = [3];
                assert(a.length === 1);
                assert(a[0] === 3);
            ");
    }

    [Test]
    public void ShouldInitializeJsonObjectArrayWithSingleIntegerValue()
    {
        RunTest(@"
                var x = JSON.parse('{ ""a"": [3] }');
                assert(x.a.length === 1);
                assert(x.a[0] === 3);
            ");
    }

    [Test]
    public void ShouldInitializeJsonArrayWithSingleIntegerValue()
    {
        RunTest(@"
                var a = JSON.parse('[3]');
                assert(a.length === 1);
                assert(a[0] === 3);
            ");
    }

    [Test]
    public void ShouldReturnTrueForEmptyIsNaNStatement()
    {
        RunTest(@"
                assert(true === isNaN());
            ");
    }

    [TestCase(4d, 0, "4")]
    [TestCase(4d, 1, "4.0")]
    [TestCase(4d, 2, "4.00")]
    [TestCase(28.995, 2, "29.00")]
    [TestCase(-28.995, 2, "-29.00")]
    [TestCase(-28.495, 2, "-28.50")]
    [TestCase(-28.445, 2, "-28.45")]
    [TestCase(28.445, 2, "28.45")]
    [TestCase(10.995, 0, "11")]
    public void ShouldRoundToFixedDecimal(double number, int fractionDigits, string result)
    {
        var engine = new Engine();
        var value = engine.Evaluate(
                String.Format("new Number({0}).toFixed({1})",
                    number.ToString(CultureInfo.InvariantCulture),
                    fractionDigits.ToString(CultureInfo.InvariantCulture)))
            .ToObject();

        value.Should().Be(result);
    }



    [Test]
    public void ShouldSortArrayWhenCompareFunctionReturnsFloatingPointNumber()
    {
        RunTest(@"
                var nums = [1, 1.1, 1.2, 2, 2, 2.1, 2.2];
                nums.sort(function(a,b){return b-a;});
                assert(nums[0] === 2.2);
                assert(nums[1] === 2.1);
                assert(nums[2] === 2);
                assert(nums[3] === 2);
                assert(nums[4] === 1.2);
                assert(nums[5] === 1.1);
                assert(nums[6] === 1);
            ");
    }

    [Test]
    public void ShouldBreakWhenBreakpointIsReached()
    {
        countBreak = 0;
        stepMode = StepMode.None;

        var engine = new Engine(options => options.Debugger.Enabled = true);

        engine.Debugger.Break += EngineStep;

        engine.Debugger.BreakPoints.Set(new BreakPoint(1, 0));

        engine.Evaluate(@"var local = true;
                if (local === true)
                {}");

        engine.Debugger.Break -= EngineStep;

        countBreak.Should().Be(1);
    }

    [Test]
    public void ShouldExecuteStepByStep()
    {
        countBreak = 0;
        stepMode = StepMode.Into;

        var engine = new Engine(options => { options.Debugger.Enabled = true; options.Debugger.InitialStepMode = stepMode; });

        engine.Debugger.Step += EngineStep;

        engine.Evaluate(@"var local = true;
                var creatingSomeOtherLine = 0;
                var lastOneIPromise = true");

        engine.Debugger.Step -= EngineStep;

        countBreak.Should().Be(3);
    }

    [Test]
    public void ShouldNotBreakTwiceIfSteppingOverBreakpoint()
    {
        countBreak = 0;
        stepMode = StepMode.Into;

        var engine = new Engine(options => { options.Debugger.Enabled = true; options.Debugger.InitialStepMode = stepMode; });
        engine.Debugger.BreakPoints.Set(new BreakPoint(1, 1));
        engine.Debugger.Step += EngineStep;
        engine.Debugger.Break += EngineStep;

        engine.Evaluate(@"var local = true;");

        engine.Debugger.Step -= EngineStep;
        engine.Debugger.Break -= EngineStep;

        countBreak.Should().Be(1);
    }

    private StepMode EngineStep(object sender, DebugInformation debugInfo)
    {
        sender.Should().NotBeNull();
        sender.Should().BeOfType<Engine>();
        debugInfo.Should().NotBeNull();

        countBreak++;
        return stepMode;
    }

    [Test]
    public void ShouldShowProperDebugInformation()
    {
        countBreak = 0;
        stepMode = StepMode.None;

        var engine = new Engine(options => options.Debugger.Enabled = true);
        engine.Debugger.BreakPoints.Set(new BreakPoint(5, 0));
        engine.Debugger.Break += EngineStepVerifyDebugInfo;

        engine.Evaluate(@"var global = true;
                            function func1()
                            {
                                var local = false;
;
                            }
                            func1();");

        engine.Debugger.Break -= EngineStepVerifyDebugInfo;

        countBreak.Should().Be(1);
    }

    private StepMode EngineStepVerifyDebugInfo(object sender, DebugInformation debugInfo)
    {
        sender.Should().NotBeNull();
        sender.Should().BeOfType<Engine>();
        debugInfo.Should().NotBeNull();

        debugInfo.CallStack.Should().NotBeNull();
        debugInfo.CurrentNode.Should().NotBeNull();
        debugInfo.CurrentScopeChain.Should().NotBeNull();

        debugInfo.CallStack.Should().HaveCount(2);
        debugInfo.CurrentCallFrame.FunctionName.Should().Be("func1");
        var globalScope = debugInfo.CurrentScopeChain.Single(s => s.ScopeType == DebugScopeType.Global);
        var localScope = debugInfo.CurrentScopeChain.Single(s => s.ScopeType == DebugScopeType.Local);
        globalScope.BindingNames.Should().Contain("global");
        globalScope.GetBindingValue("global").AsBoolean().Should().BeTrue();
        localScope.BindingNames.Should().Contain("local");
        localScope.GetBindingValue("local").AsBoolean().Should().BeFalse();
        localScope.BindingNames.Should().NotContain("global");
        countBreak++;
        return stepMode;
    }

    [Test]
    public void ShouldBreakWhenConditionIsMatched()
    {
        countBreak = 0;
        stepMode = StepMode.None;

        var engine = new Engine(options => options.Debugger.Enabled = true);

        engine.Debugger.Break += EngineStep;

        engine.Debugger.BreakPoints.Set(new BreakPoint(5, 16, "condition === true"));
        engine.Debugger.BreakPoints.Set(new BreakPoint(6, 16, "condition === false"));

        engine.Evaluate(@"var local = true;
                var condition = true;
                if (local === true)
                {
                ;
                ;
                }");

        engine.Debugger.Break -= EngineStep;

        countBreak.Should().Be(1);
    }

    [Test]
    public void ShouldNotStepInSameLevelStatementsWhenStepOut()
    {
        countBreak = 0;
        stepMode = StepMode.Out;

        var engine = new Engine(options => { options.Debugger.Enabled = true; options.Debugger.InitialStepMode = StepMode.Into; });

        engine.Debugger.Step += EngineStep;

        engine.Evaluate(@"function func() // first step - then stepping out
                {
                    ; // shall not step
                    ; // not even here
                }
                func(); // shall not step
                ; // shall not step ");

        engine.Debugger.Step -= EngineStep;

        countBreak.Should().Be(1);
    }

    [Test]
    public void ShouldNotStepInIfRequiredToStepOut()
    {
        countBreak = 0;

        var engine = new Engine(options => { options.Debugger.Enabled = true; options.Debugger.InitialStepMode = StepMode.Into; });

        engine.Debugger.Step += EngineStepOutWhenInsideFunction;

        engine.Evaluate(@"function func() // first step
                {
                    ; // third step - now stepping out
                    ; // it should not step here
                }
                func(); // second step
                ; // fourth step ");

        engine.Debugger.Step -= EngineStepOutWhenInsideFunction;

        countBreak.Should().Be(4);
    }

    private StepMode EngineStepOutWhenInsideFunction(object sender, DebugInformation debugInfo)
    {
        sender.Should().NotBeNull();
        sender.Should().BeOfType<Engine>();
        debugInfo.Should().NotBeNull();

        countBreak++;
        if (debugInfo.CallStack.Count > 1) // CallStack always has at least one element
            return StepMode.Out;

        return StepMode.Into;
    }

    [Test]
    public void ShouldBreakWhenStatementIsMultiLine()
    {
        countBreak = 0;
        stepMode = StepMode.None;

        var engine = new Engine(options => options.Debugger.Enabled = true);
        engine.Debugger.BreakPoints.Set(new BreakPoint(4, 32));
        engine.Debugger.Break += EngineStep;

        engine.Evaluate(@"var global = true;
                            function func1()
                            {
                                var local =
                                    false;
                            }
                            func1();");

        engine.Debugger.Break -= EngineStep;

        countBreak.Should().Be(1);
    }

    [Test]
    public void ShouldNotStepInsideIfRequiredToStepOver()
    {
        countBreak = 0;
        stepMode = StepMode.Over;

        var engine = new Engine(options => { options.Debugger.Enabled = true; options.Debugger.InitialStepMode = stepMode; });

        engine.Debugger.Step += EngineStep;

        engine.Evaluate(@"function func() // first step
                {
                    ; // third step - it shall not step here
                    ; // it shall not step here
                }
                func(); // second step
                ; // third step ");

        engine.Debugger.Step -= EngineStep;

        countBreak.Should().Be(3);
    }

    [Test]
    public void ShouldStepAllStatementsWithoutInvocationsIfStepOver()
    {
        countBreak = 0;
        stepMode = StepMode.Over;

        var engine = new Engine(options => { options.Debugger.Enabled = true; options.Debugger.InitialStepMode = stepMode; });

        engine.Debugger.Step += EngineStep;

        engine.Evaluate(@"var step1 = 1; // first step
                var step2 = 2; // second step
                if (step1 !== step2) // third step
                {
                    ; // fourth step
                }");

        engine.Debugger.Step -= EngineStep;

        countBreak.Should().Be(4);
    }

    [Test]
    public void ShouldEvaluateVariableAssignmentFromLeftToRight()
    {
        RunTest(@"
                var keys = ['a']
                  , source = { a: 3}
                  , target = {}
                  , key
                  , i = 0;
                target[key = keys[i++]] = source[key];
                equal(1, i);
                equal('a', key);
                equal(3, target[key]);
            ");
    }

    [Test]
    public void ObjectShouldBeExtensible()
    {
        RunTest(@"
                try {
                    Object.defineProperty(Object.defineProperty, 'foo', { value: 1 });
                }
                catch(e) {
                    assert(false);
                }
            ");
    }

    [Test]
    public void ArrayIndexShouldBeConvertedToUint32()
    {
        // This is missing from ECMA tests suite
        // http://www.ecma-international.org/ecma-262/5.1/#sec-15.4

        RunTest(@"
                var a = [ 'foo' ];
                assert(a[0] === 'foo');
                assert(a['0'] === 'foo');
                assert(a['00'] === undefined);
            ");
    }

    [Test]
    public void HexZeroAsArrayIndexShouldWork()
    {
        var engine = new Engine();
        engine.Evaluate("var t = '1234'; var value = null;");
        engine.Execute("value = t[0x0];").GetValue("value").AsString().Should().Be("1");
        engine.Execute("value = t[0];").GetValue("value").AsString().Should().Be("1");
        engine.Execute("value = t['0'];").GetValue("value").AsString().Should().Be("1");
    }

    [Test]
    public void DatePrototypeFunctionWorkOnDateOnly()
    {
        RunTest(@"
                try {
                    var myObj = Object.create(Date.prototype);
                    myObj.toDateString();
                } catch (e) {
                    assert(e instanceof TypeError);
                }
            ");
    }

    [Test]
    public void DateToStringMethodsShouldUseCurrentTimeZoneAndCulture()
    {
        // Forcing to PDT and FR for tests
        // var PDT = TimeZoneInfo.CreateCustomTimeZone("Pacific Daylight Time", new TimeSpan(-7, 0, 0), "Pacific Daylight Time", "Pacific Daylight Time");
        var PDT = _pacificTimeZone;
        var FR = new CultureInfo("fr-FR");

        var engine = new Engine(options => { options.TimeZone = PDT; options.Culture = FR; })
                .SetValue("log", new Action<object>(Console.WriteLine))
                .SetValue("assert", new Action<bool>(static value => value.Should().BeTrue()))
                .SetValue("equal", new Action<object, object>(static (expected, actual) =>
                    actual.Should().BeEquivalentTo(expected, static options => options.WithStrictOrdering())))
            ;

        engine.Evaluate(@"
                    var d = new Date(1433160000000);

                    equal('Mon Jun 01 2015 05:00:00 GMT-0700 (Pacific Daylight Time)', d.toString());
                    equal('Mon Jun 01 2015', d.toDateString());
                    equal('05:00:00 GMT-0700 (Pacific Daylight Time)', d.toTimeString());
                    // ECMA-402 compliant: numeric defaults used when no options specified
                    equal('1/6/2015, 05:00:00', d.toLocaleString());
                    equal('1/6/2015', d.toLocaleDateString());
                    equal('05:00:00', d.toLocaleTimeString());
            ");
    }

    [Test]
    public void DateShouldHonorTimezoneDaylightSavingRules()
    {
        var EST = _easternTimeZone;
        var engine = new Engine(options => options.TimeZone = EST)
            .SetValue("log", new Action<object>(Console.WriteLine))
            .SetValue("assert", new Action<bool>(static value => value.Should().BeTrue()))
            .SetValue("equal", new Action<object, object>(static (expected, actual) =>
                    actual.Should().BeEquivalentTo(expected, static options => options.WithStrictOrdering())));

        engine.Evaluate($@"
                    var d = new Date(2016, 8, 1);
                    equal('Thu Sep 01 2016 00:00:00 GMT-0400 ({EST.DaylightName})', d.toString());
                    equal('Thu Sep 01 2016', d.toDateString());
            ");
    }

    [Test]
    public void DateShouldParseToString()
    {
        // Forcing to PDT and FR for tests
        // var PDT = TimeZoneInfo.CreateCustomTimeZone("Pacific Daylight Time", new TimeSpan(-7, 0, 0), "Pacific Daylight Time", "Pacific Daylight Time");
        var PDT = _pacificTimeZone;
        var FR = new CultureInfo("fr-FR");

        new Engine(options => { options.TimeZone = PDT; options.Culture = FR; })
            .SetValue("log", new Action<object>(Console.WriteLine))
            .SetValue("assert", new Action<bool>(static value => value.Should().BeTrue()))
            .SetValue("equal", new Action<object, object>(static (expected, actual) =>
                    actual.Should().BeEquivalentTo(expected, static options => options.WithStrictOrdering())))
            .Evaluate(@"
                    var d = new Date(1433160000000);
                    equal(Date.parse(d.toString()), d.valueOf());
                    equal(Date.parse(d.toLocaleString()), d.valueOf());
            ");
    }


    [Test]
    public void ShouldThrowErrorWhenMaxExecutionStackCountLimitExceeded()
    {
        new Engine(options => options.Constraints.MaxExecutionStackCount = 1000)
            .SetValue("assert", new Action<bool>(static value => value.Should().BeTrue()))
            .Evaluate(@"
                    var count = 0;
                    function recurse() {
                        count++;
                        recurse();
                        return null; // ensure no tail recursion
                    }
                    try {
                        count = 0; 
                        recurse();
                        assert(false);
                    } catch(err) {
                        assert(count >= 1000);
                    }
            ");

    }


    [Test]
    public void ShouldThrowJavaScriptExceptionForObjectExpressionWithAssignmentPattern()
    {
        // Before the fix, JintObjectExpression.Initialize crashed with InvalidCastException
        // because an AssignmentPattern node was being cast to Expression without a type check.
        // After the fix, it throws a catchable JavaScriptException (SyntaxError).
        var engine = new Engine();
        Invoking(() =>
            engine.Execute("a=[1,3];aaa={}={}.aap+=[1,3];aaa={}={a=-[]<= []<a.m}.aap+=[,2,3111-1]")).Should().Throw<JavaScriptException>();
    }


    [Test]
    public void LocaleNumberShouldUseLocalCulture()
    {
        // Forcing to PDT and FR for tests
        // var PDT = TimeZoneInfo.CreateCustomTimeZone("Pacific Daylight Time", new TimeSpan(-7, 0, 0), "Pacific Daylight Time", "Pacific Daylight Time");
        var PDT = _pacificTimeZone;
        var FR = new CultureInfo("fr-FR");

        var engine = new Engine(options => { options.TimeZone = PDT; options.Culture = FR; })
            .SetValue("log", new Action<object>(Console.WriteLine))
            .SetValue("assert", new Action<bool>(static value => value.Should().BeTrue()))
            .SetValue("equal", new Action<object, object>(static (expected, actual) =>
                    actual.Should().BeEquivalentTo(expected, static options => options.WithStrictOrdering())));

        engine.Evaluate("var d = new Number(-1.23);");
        engine.Evaluate("equal('-1.23', d.toString());");

        // NET 5 globalization APIs use ICU libraries on newer Windows 10 giving different result
        // build server is older Windows...
        engine.Evaluate("assert('-1,230' === d.toLocaleString() || '-1,23' === d.toLocaleString());");
    }

    [Test]
    public void DateCtorShouldAcceptDate()
    {
        RunTest(@"
                var a = new Date();
                var b = new Date(a);
                assert(String(a) === String(b));
            ");
    }

    [Test]
    public void RegExpResultIsMutable()
    {
        RunTest(@"
                var match = /quick\s(brown).+?(jumps)/ig.exec('The Quick Brown Fox Jumps Over The Lazy Dog');
                var result = match.shift();
                assert(result === 'Quick Brown Fox Jumps');
            ");
    }

    [Test]
    public void RegExpSupportsMultiline()
    {
        RunTest(@"
                var rheaders = /^(.*?):[ \t]*([^\r\n]*)$/mg;
                var headersString = 'X-AspNetMvc-Version: 4.0\r\nX-Powered-By: ASP.NET\r\n\r\n';
                match = rheaders.exec(headersString);
                assert('X-AspNetMvc-Version' === match[1]);
                assert('4.0' === match[2]);
            ");

        RunTest(@"
                var rheaders = /^(.*?):[ \t]*(.*?)$/mg;
                var headersString = 'X-AspNetMvc-Version: 4.0\r\nX-Powered-By: ASP.NET\r\n\r\n';
                match = rheaders.exec(headersString);
                assert('X-AspNetMvc-Version' === match[1]);
                assert('4.0' === match[2]);
            ");

        RunTest(@"
                var rheaders = /^(.*?):[ \t]*([^\r\n]*)$/mg;
                var headersString = 'X-AspNetMvc-Version: 4.0\nX-Powered-By: ASP.NET\n\n';
                match = rheaders.exec(headersString);
                assert('X-AspNetMvc-Version' === match[1]);
                assert('4.0' === match[2]);
            ");
    }

    [Test]
    public void RegExpPrototypeToString()
    {
        RunTest("assert(RegExp.prototype.toString() === '/(?:)/');");
    }

    [Test]
    public void ShouldSetYearBefore1970()
    {
        new Engine(options => options.TimeZone = TimeZoneInfo.Utc)
            .SetValue("equal", new Action<object, object>(static (expected, actual) =>
                    actual.Should().BeEquivalentTo(expected, static options => options.WithStrictOrdering())))
            .Execute(@"
                var d = new Date('1969-01-01T08:17:00Z');
                d.setYear(2015);
                equal('2015-01-01T08:17:00.000Z', d.toISOString());
            ");
    }

    [Test]
    public void ShouldUseReplaceMarkers()
    {
        RunTest(@"
                var re = /a/g;
                var str = 'abab';
                var newstr = str.replace(re, '$\'x');
                equal('babxbbxb', newstr);
            ");
    }

    [Test]
    public void ExceptionShouldHaveLocationOfInnerFunction()
    {
        var engine = new Engine();
        const string source = @"
                function test(s) {
                    o.boom();
                }
                test('arg');
            ";

        var ex = Invoking(() => engine.Evaluate(source)).Should().ThrowExactly<JavaScriptException>().Which;
        ex.Location.Start.Line.Should().Be(3);
    }

    [Test]
    public void GlobalRegexLiteralShouldNotKeepState()
    {
        RunTest(@"
				var url = 'https://www.example.com';

				assert(isAbsolutePath(url));
				assert(isAbsolutePath(url));
				assert(isAbsolutePath(url));

				function isAbsolutePath(path) {
					return /\.+/g.test(path);
				}
            ");
    }

    [Test]
    public void ShouldCompareInnerValueOfClrInstances()
    {
        var engine = new Engine();

        // Create two separate Guid with identical inner values.
        var guid1 = Guid.NewGuid();
        var guid2 = new Guid(guid1.ToString());

        engine.SetValue("guid1", guid1);
        engine.SetValue("guid2", guid2);

        var result = engine.Evaluate("guid1 == guid2").AsBoolean();

        result.Should().BeTrue();
    }

    [Test]
    public void CanStringifyToConsole()
    {
        var engine = new Engine(options => options.AllowClr(typeof(Console).Assembly));
        engine.Evaluate("System.Console.WriteLine(JSON.stringify({x:12, y:14}));");
    }

    [Test]
    public void ShouldNotCompareClrInstancesWithObjects()
    {
        var engine = new Engine();

        var guid1 = Guid.NewGuid();

        engine.SetValue("guid1", guid1);

        var result = engine.Evaluate("guid1 == {}").AsBoolean();

        result.Should().BeFalse();
    }

    [Test]
    public void ShouldStringifyNumWithoutV8DToA()
    {
        // 53.6841659 cannot be converted by V8's DToA => "old" DToA code will be used.
        var engine = new Engine();
        var val = engine.Evaluate("JSON.stringify(53.6841659)");

        val.AsString().Should().Be("53.6841659");
    }

    [Test]
    public void ShouldStringifyObjectWithPropertiesToSameRef()
    {
        var engine = new Engine();
        var res = engine.Evaluate(@"
                var obj = {
                    a : [],
                    a1 : ['str'],
                    a2 : {},
                    a3 : { 'prop' : 'val' }
                };
                obj.b = obj.a;
                obj.b1 = obj.a1;
                JSON.stringify(obj);
            ");

        res.Should().Be("{\"a\":[],\"a1\":[\"str\"],\"a2\":{},\"a3\":{\"prop\":\"val\"},\"b\":[],\"b1\":[\"str\"]}");
    }

    [Test]
    public void ShouldThrowOnSerializingCyclicRefObject()
    {
        var engine = new Engine();
        var res = engine.Evaluate(@"
                (function(){
                    try{
                        a = [];
                        a[0] = a;
                        my_text = JSON.stringify(a);
                    }
                    catch(ex){
                        return ex.message;
                    }
                })();
            ");

        res.Should().Be("Cyclic reference detected.");
    }

    [Test]
    public void ShouldNotStringifyFunctionValuedProperties()
    {
        var engine = new Engine();
        var res = engine.Evaluate(@"
                var obj = {
                    f: function() { }
                };
                return JSON.stringify(obj);
            ");

        res.AsString().Should().Be("{}");
    }

    [TestCase("", "escape('')")]
    [TestCase("%u0100%u0101%u0102", "escape('\u0100\u0101\u0102')")]
    [TestCase("%uFFFD%uFFFE%uFFFF", "escape('\ufffd\ufffe\uffff')")]
    [TestCase("%uD834%uDF06", "escape('\ud834\udf06')")]
    [TestCase("%00%01%02%03", "escape('\x00\x01\x02\x03')")]
    [TestCase("%2C", "escape(',')")]
    [TestCase("%3A%3B%3C%3D%3E%3F", "escape(':;<=>?')")]
    [TestCase("%60", "escape('`')")]
    [TestCase("%7B%7C%7D%7E%7F%80", "escape('{|}~\x7f\x80')")]
    [TestCase("%FD%FE%FF", "escape('\xfd\xfe\xff')")]
    [TestCase("ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789@*_+-./", "escape('ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789@*_+-./')")]
    public void ShouldEvaluateEscape(object expected, string source)
    {
        var engine = new Engine();
        var result = engine.Evaluate(source).ToObject();

        result.Should().Be(expected);
    }

    //https://github.com/tc39/test262/blob/master/test/annexB/built-ins/unescape/empty-string.js
    [TestCase("", "unescape('')")]
    //https://github.com/tc39/test262/blob/master/test/annexB/built-ins/unescape/four-ignore-bad-u.js
    [TestCase("%U0000", "unescape('%U0000')")]
    [TestCase("%t0000", "unescape('%t0000')")]
    [TestCase("%v0000", "unescape('%v0000')")]
    [TestCase("%" + "\x00" + "00", "unescape('%%0000')")]
    //https://github.com/tc39/test262/blob/master/test/annexB/built-ins/unescape/four-ignore-end-str.js
    [TestCase("%u", "unescape('%u')")]
    [TestCase("%u0", "unescape('%u0')")]
    [TestCase("%u1", "unescape('%u1')")]
    [TestCase("%u2", "unescape('%u2')")]
    [TestCase("%u3", "unescape('%u3')")]
    [TestCase("%u4", "unescape('%u4')")]
    [TestCase("%u5", "unescape('%u5')")]
    [TestCase("%u6", "unescape('%u6')")]
    [TestCase("%u7", "unescape('%u7')")]
    [TestCase("%u8", "unescape('%u8')")]
    [TestCase("%u9", "unescape('%u9')")]
    [TestCase("%ua", "unescape('%ua')")]
    [TestCase("%uA", "unescape('%uA')")]
    [TestCase("%ub", "unescape('%ub')")]
    [TestCase("%uB", "unescape('%uB')")]
    [TestCase("%uc", "unescape('%uc')")]
    [TestCase("%uC", "unescape('%uC')")]
    [TestCase("%ud", "unescape('%ud')")]
    [TestCase("%uD", "unescape('%uD')")]
    [TestCase("%ue", "unescape('%ue')")]
    [TestCase("%uE", "unescape('%uE')")]
    [TestCase("%uf", "unescape('%uf')")]
    [TestCase("%uF", "unescape('%uF')")]
    [TestCase("%u01", "unescape('%u01')")]
    [TestCase("%u02", "unescape('%u02')")]
    [TestCase("%u03", "unescape('%u03')")]
    [TestCase("%u04", "unescape('%u04')")]
    [TestCase("%u05", "unescape('%u05')")]
    [TestCase("%u06", "unescape('%u06')")]
    [TestCase("%u07", "unescape('%u07')")]
    [TestCase("%u08", "unescape('%u08')")]
    [TestCase("%u09", "unescape('%u09')")]
    [TestCase("%u0a", "unescape('%u0a')")]
    [TestCase("%u0A", "unescape('%u0A')")]
    [TestCase("%u0b", "unescape('%u0b')")]
    [TestCase("%u0B", "unescape('%u0B')")]
    [TestCase("%u0c", "unescape('%u0c')")]
    [TestCase("%u0C", "unescape('%u0C')")]
    [TestCase("%u0d", "unescape('%u0d')")]
    [TestCase("%u0D", "unescape('%u0D')")]
    [TestCase("%u0e", "unescape('%u0e')")]
    [TestCase("%u0E", "unescape('%u0E')")]
    [TestCase("%u0f", "unescape('%u0f')")]
    [TestCase("%u0F", "unescape('%u0F')")]
    [TestCase("%u000", "unescape('%u000')")]
    [TestCase("%u001", "unescape('%u001')")]
    [TestCase("%u002", "unescape('%u002')")]
    [TestCase("%u003", "unescape('%u003')")]
    [TestCase("%u004", "unescape('%u004')")]
    [TestCase("%u005", "unescape('%u005')")]
    [TestCase("%u006", "unescape('%u006')")]
    [TestCase("%u007", "unescape('%u007')")]
    [TestCase("%u008", "unescape('%u008')")]
    [TestCase("%u009", "unescape('%u009')")]
    [TestCase("%u00a", "unescape('%u00a')")]
    [TestCase("%u00A", "unescape('%u00A')")]
    [TestCase("%u00b", "unescape('%u00b')")]
    [TestCase("%u00B", "unescape('%u00B')")]
    [TestCase("%u00c", "unescape('%u00c')")]
    [TestCase("%u00C", "unescape('%u00C')")]
    [TestCase("%u00d", "unescape('%u00d')")]
    [TestCase("%u00D", "unescape('%u00D')")]
    [TestCase("%u00e", "unescape('%u00e')")]
    [TestCase("%u00E", "unescape('%u00E')")]
    [TestCase("%u00f", "unescape('%u00f')")]
    [TestCase("%u00F", "unescape('%u00F')")]
    //https://github.com/tc39/test262/blob/master/test/annexB/built-ins/unescape/four-ignore-non-hex.js
    [TestCase("%u000%0", "unescape('%u000%0')")]
    [TestCase("%u000g0", "unescape('%u000g0')")]
    [TestCase("%u000G0", "unescape('%u000G0')")]
    [TestCase("%u00g00", "unescape('%u00g00')")]
    [TestCase("%u00G00", "unescape('%u00G00')")]
    [TestCase("%u0g000", "unescape('%u0g000')")]
    [TestCase("%u0G000", "unescape('%u0G000')")]
    [TestCase("%ug0000", "unescape('%ug0000')")]
    [TestCase("%uG0000", "unescape('%uG0000')")]
    [TestCase("%u000u0", "unescape('%u000u0')")]
    [TestCase("%u000U0", "unescape('%u000U0')")]
    [TestCase("%u00u00", "unescape('%u00u00')")]
    [TestCase("%u00U00", "unescape('%u00U00')")]
    [TestCase("%u0u000", "unescape('%u0u000')")]
    [TestCase("%u0U000", "unescape('%u0U000')")]
    [TestCase("%uu0000", "unescape('%uu0000')")]
    [TestCase("%uU0000", "unescape('%uU0000')")]
    //https://github.com/tc39/test262/blob/master/test/annexB/built-ins/unescape/four.js
    [TestCase("%0" + "\x00" + "0", "unescape('%0%u00000')")]
    [TestCase("%0" + "\x01" + "0", "unescape('%0%u00010')")]
    [TestCase("%0)0", "unescape('%0%u00290')")]
    [TestCase("%0*0", "unescape('%0%u002a0')")]
    [TestCase("%0*0", "unescape('%0%u002A0')")]
    [TestCase("%0+0", "unescape('%0%u002b0')")]
    [TestCase("%0+0", "unescape('%0%u002B0')")]
    [TestCase("%0,0", "unescape('%0%u002c0')")]
    [TestCase("%0,0", "unescape('%0%u002C0')")]
    [TestCase("%0-0", "unescape('%0%u002d0')")]
    [TestCase("%0-0", "unescape('%0%u002D0')")]
    [TestCase("%090", "unescape('%0%u00390')")]
    [TestCase("%0:0", "unescape('%0%u003a0')")]
    [TestCase("%0:0", "unescape('%0%u003A0')")]
    [TestCase("%0?0", "unescape('%0%u003f0')")]
    [TestCase("%0?0", "unescape('%0%u003F0')")]
    [TestCase("%0@0", "unescape('%0%u00400')")]
    [TestCase("%0Z0", "unescape('%0%u005a0')")]
    [TestCase("%0Z0", "unescape('%0%u005A0')")]
    [TestCase("%0[0", "unescape('%0%u005b0')")]
    [TestCase("%0[0", "unescape('%0%u005B0')")]
    [TestCase("%0^0", "unescape('%0%u005e0')")]
    [TestCase("%0^0", "unescape('%0%u005E0')")]
    [TestCase("%0_0", "unescape('%0%u005f0')")]
    [TestCase("%0_0", "unescape('%0%u005F0')")]
    [TestCase("%0`0", "unescape('%0%u00600')")]
    [TestCase("%0a0", "unescape('%0%u00610')")]
    [TestCase("%0z0", "unescape('%0%u007a0')")]
    [TestCase("%0z0", "unescape('%0%u007A0')")]
    [TestCase("%0{0", "unescape('%0%u007b0')")]
    [TestCase("%0{0", "unescape('%0%u007B0')")]
    [TestCase("%0" + "\ufffe" + "0", "unescape('%0%ufffe0')")]
    [TestCase("%0" + "\ufffe" + "0", "unescape('%0%uFffe0')")]
    [TestCase("%0" + "\ufffe" + "0", "unescape('%0%ufFfe0')")]
    [TestCase("%0" + "\ufffe" + "0", "unescape('%0%uffFe0')")]
    [TestCase("%0" + "\ufffe" + "0", "unescape('%0%ufffE0')")]
    [TestCase("%0" + "\ufffe" + "0", "unescape('%0%uFFFE0')")]
    [TestCase("%0" + "\uffff" + "0", "unescape('%0%uffff0')")]
    [TestCase("%0" + "\uffff" + "0", "unescape('%0%uFfff0')")]
    [TestCase("%0" + "\uffff" + "0", "unescape('%0%ufFff0')")]
    [TestCase("%0" + "\uffff" + "0", "unescape('%0%uffFf0')")]
    [TestCase("%0" + "\uffff" + "0", "unescape('%0%ufffF0')")]
    [TestCase("%0" + "\uffff" + "0", "unescape('%0%uFFFF0')")]
    //https://github.com/tc39/test262/blob/master/test/annexB/built-ins/unescape/two-ignore-end-str.js
    [TestCase("%", "unescape('%')")]
    [TestCase("%0", "unescape('%0')")]
    [TestCase("%1", "unescape('%1')")]
    [TestCase("%2", "unescape('%2')")]
    [TestCase("%3", "unescape('%3')")]
    [TestCase("%4", "unescape('%4')")]
    [TestCase("%5", "unescape('%5')")]
    [TestCase("%6", "unescape('%6')")]
    [TestCase("%7", "unescape('%7')")]
    [TestCase("%8", "unescape('%8')")]
    [TestCase("%9", "unescape('%9')")]
    [TestCase("%a", "unescape('%a')")]
    [TestCase("%A", "unescape('%A')")]
    [TestCase("%b", "unescape('%b')")]
    [TestCase("%B", "unescape('%B')")]
    [TestCase("%c", "unescape('%c')")]
    [TestCase("%C", "unescape('%C')")]
    [TestCase("%d", "unescape('%d')")]
    [TestCase("%D", "unescape('%D')")]
    [TestCase("%e", "unescape('%e')")]
    [TestCase("%E", "unescape('%E')")]
    [TestCase("%f", "unescape('%f')")]
    [TestCase("%F", "unescape('%F')")]
    //https://github.com/tc39/test262/blob/master/test/annexB/built-ins/unescape/two-ignore-non-hex.js
    [TestCase("%0%0", "unescape('%0%0')")]
    [TestCase("%0g0", "unescape('%0g0')")]
    [TestCase("%0G0", "unescape('%0G0')")]
    [TestCase("%g00", "unescape('%g00')")]
    [TestCase("%G00", "unescape('%G00')")]
    [TestCase("%0u0", "unescape('%0u0')")]
    [TestCase("%0U0", "unescape('%0U0')")]
    [TestCase("%u00", "unescape('%u00')")]
    [TestCase("%U00", "unescape('%U00')")]
    //https://github.com/tc39/test262/blob/master/test/annexB/built-ins/unescape/two.js
    [TestCase("%0" + "\x00" + "00", "unescape('%0%0000')")]
    [TestCase("%0" + "\x01" + "00", "unescape('%0%0100')")]
    [TestCase("%0)00", "unescape('%0%2900')")]
    [TestCase("%0*00", "unescape('%0%2a00')")]
    [TestCase("%0*00", "unescape('%0%2A00')")]
    [TestCase("%0+00", "unescape('%0%2b00')")]
    [TestCase("%0+00", "unescape('%0%2B00')")]
    [TestCase("%0,00", "unescape('%0%2c00')")]
    [TestCase("%0,00", "unescape('%0%2C00')")]
    [TestCase("%0-00", "unescape('%0%2d00')")]
    [TestCase("%0-00", "unescape('%0%2D00')")]
    [TestCase("%0900", "unescape('%0%3900')")]
    [TestCase("%0:00", "unescape('%0%3a00')")]
    [TestCase("%0:00", "unescape('%0%3A00')")]
    [TestCase("%0?00", "unescape('%0%3f00')")]
    [TestCase("%0?00", "unescape('%0%3F00')")]
    [TestCase("%0@00", "unescape('%0%4000')")]
    [TestCase("%0Z00", "unescape('%0%5a00')")]
    [TestCase("%0Z00", "unescape('%0%5A00')")]
    [TestCase("%0[00", "unescape('%0%5b00')")]
    [TestCase("%0[00", "unescape('%0%5B00')")]
    [TestCase("%0^00", "unescape('%0%5e00')")]
    [TestCase("%0^00", "unescape('%0%5E00')")]
    [TestCase("%0_00", "unescape('%0%5f00')")]
    [TestCase("%0_00", "unescape('%0%5F00')")]
    [TestCase("%0`00", "unescape('%0%6000')")]
    [TestCase("%0a00", "unescape('%0%6100')")]
    [TestCase("%0z00", "unescape('%0%7a00')")]
    [TestCase("%0z00", "unescape('%0%7A00')")]
    [TestCase("%0{00", "unescape('%0%7b00')")]
    [TestCase("%0{00", "unescape('%0%7B00')")]
    public void ShouldEvaluateUnescape(object expected, string source)
    {
        var engine = new Engine();
        var result = engine.Evaluate(source).ToObject();

        result.Should().Be(expected);
    }

    [TestCase("new Date(1969,0,1,19,45,30,500).getHours()", 19)]
    [TestCase("new Date(1970,0,1,19,45,30,500).getHours()", 19)]
    [TestCase("new Date(1971,0,1,19,45,30,500).getHours()", 19)]
    [TestCase("new Date(1969,0,1,19,45,30,500).getMinutes()", 45)]
    [TestCase("new Date(1970,0,1,19,45,30,500).getMinutes()", 45)]
    [TestCase("new Date(1971,0,1,19,45,30,500).getMinutes()", 45)]
    [TestCase("new Date(1969,0,1,19,45,30,500).getSeconds()", 30)]
    [TestCase("new Date(1970,0,1,19,45,30,500).getSeconds()", 30)]
    [TestCase("new Date(1971,0,1,19,45,30,500).getSeconds()", 30)]
    //[TestCase("new Date(1969,0,1,19,45,30,500).getMilliseconds()", 500)]
    //[TestCase("new Date(1970,0,1,19,45,30,500).getMilliseconds()", 500)]
    //[TestCase("new Date(1971,0,1,19,45,30,500).getMilliseconds()", 500)]
    public void ShouldExtractDateParts(string source, double expected)
    {
        var engine = new Engine();
        var result = engine.Evaluate(source).ToObject();

        result.Should().Be(expected);
    }

    [TestCase("'abc'.padStart(10)", "       abc")]
    [TestCase("'abc'.padStart(10, \"foo\")", "foofoofabc")]
    [TestCase("'abc'.padStart(6, \"123456\")", "123abc")]
    [TestCase("'abc'.padStart(8, \"0\")", "00000abc")]
    [TestCase("'abc'.padStart(1)", "abc")]
    public void ShouldPadStart(string source, object expected)
    {
        var engine = new Engine();
        var result = engine.Evaluate(source).ToObject();

        result.Should().Be(expected);
    }

    [TestCase("'abc'.padEnd(10)", "abc       ")]
    [TestCase("'abc'.padEnd(10, \"foo\")", "abcfoofoof")]
    [TestCase("'abc'.padEnd(6, \"123456\")", "abc123")]
    [TestCase("'abc'.padEnd(1)", "abc")]
    public void ShouldPadEnd(string source, object expected)
    {
        var engine = new Engine();
        var result = engine.Evaluate(source).ToObject();
        result.Should().Be(expected);
    }

    /// <summary>
    /// Tests for startsWith - tests created from MDN and https://github.com/mathiasbynens/String.prototype.startsWith/blob/master/tests/tests.js
    /// </summary>
    [TestCase("'To be, or not to be, that is the question.'.startsWith('To be')", true)]
    [TestCase("'To be, or not to be, that is the question.'.startsWith('not to be')", false)]
    [TestCase("'To be, or not to be, that is the question.'.startsWith()", false)]
    [TestCase("'To be, or not to be, that is the question.'.startsWith('not to be', 10)", true)]
    [TestCase("'undefined'.startsWith()", true)]
    [TestCase("'undefined'.startsWith(undefined)", true)]
    [TestCase("'undefined'.startsWith(null)", false)]
    [TestCase("'null'.startsWith()", false)]
    [TestCase("'null'.startsWith(undefined)", false)]
    [TestCase("'null'.startsWith(null)", true)]
    [TestCase("'abc'.startsWith()", false)]
    [TestCase("'abc'.startsWith('')", true)]
    [TestCase("'abc'.startsWith('\0')", false)]
    [TestCase("'abc'.startsWith('a')", true)]
    [TestCase("'abc'.startsWith('b')", false)]
    [TestCase("'abc'.startsWith('ab')", true)]
    [TestCase("'abc'.startsWith('bc')", false)]
    [TestCase("'abc'.startsWith('abc')", true)]
    [TestCase("'abc'.startsWith('bcd')", false)]
    [TestCase("'abc'.startsWith('abcd')", false)]
    [TestCase("'abc'.startsWith('bcde')", false)]
    [TestCase("'abc'.startsWith('', 1)", true)]
    [TestCase("'abc'.startsWith('\0', 1)", false)]
    [TestCase("'abc'.startsWith('a', 1)", false)]
    [TestCase("'abc'.startsWith('b', 1)", true)]
    [TestCase("'abc'.startsWith('ab', 1)", false)]
    [TestCase("'abc'.startsWith('bc', 1)", true)]
    [TestCase("'abc'.startsWith('abc', 1)", false)]
    [TestCase("'abc'.startsWith('bcd', 1)", false)]
    [TestCase("'abc'.startsWith('abcd', 1)", false)]
    [TestCase("'abc'.startsWith('bcde', 1)", false)]
    public void ShouldStartWith(string source, object expected)
    {
        var engine = new Engine();
        var result = engine.Evaluate(source).ToObject();

        result.Should().Be(expected);
    }

    [TestCase("throw {}", "undefined")]
    [TestCase("throw {message:null}", "null")]
    [TestCase("throw {message:''}", "")]
    [TestCase("throw {message:2}", "2")]
    public void ShouldAllowNonStringMessage(string source, string expected)
    {
        var engine = new Engine();
        var ex = Invoking(() => engine.Execute(source)).Should().ThrowExactly<JavaScriptException>().Which;
        ex.Message.Should().Be(expected);
    }

    //Months
    [TestCase("new Date(2017, 0, 1, 0, 0, 0)", "new Date(2016, 12, 1, 0, 0, 0)")]
    [TestCase("new Date(2016, 0, 1, 23, 59, 59)", "new Date(2015, 12, 1, 23, 59, 59)")]
    [TestCase("new Date(2013, 0, 1, 0, 0, 0)", "new Date(2012, 12, 1, 0, 0, 0)")]
    [TestCase("new Date(2013, 0, 29, 23, 59, 59)", "new Date(2012, 12, 29, 23, 59, 59)")]
    [TestCase("new Date(2015, 11, 1, 0, 0, 0)", "new Date(2016, -1, 1, 0, 0, 0)")]
    [TestCase("new Date(2014, 11, 1, 23, 59, 59)", "new Date(2015, -1, 1, 23, 59, 59)")]
    [TestCase("new Date(2011, 11, 1, 0, 0, 0)", "new Date(2012, -1, 1, 0, 0, 0)")]
    [TestCase("new Date(2011, 11, 29, 23, 59, 59)", "new Date(2012, -1, 29, 23, 59, 59)")]
    [TestCase("new Date(2015, 1, 1, 0, 0, 0)", "new Date(2016, -11, 1, 0, 0, 0)")]
    [TestCase("new Date(2014, 1, 1, 23, 59, 59)", "new Date(2015, -11, 1, 23, 59, 59)")]
    [TestCase("new Date(2011, 1, 1, 0, 0, 0)", "new Date(2012, -11, 1, 0, 0, 0)")]
    [TestCase("new Date(2011, 2, 1, 23, 59, 59)", "new Date(2012, -11, 29, 23, 59, 59)")]
    [TestCase("new Date(2015, 0, 1, 0, 0, 0)", "new Date(2016, -12, 1, 0, 0, 0)")]
    [TestCase("new Date(2014, 0, 1, 23, 59, 59)", "new Date(2015, -12, 1, 23, 59, 59)")]
    [TestCase("new Date(2011, 0, 1, 0, 0, 0)", "new Date(2012, -12, 1, 0, 0, 0)")]
    [TestCase("new Date(2011, 0, 29, 23, 59, 59)", "new Date(2012, -12, 29, 23, 59, 59)")]
    [TestCase("new Date(2014, 11, 1, 0, 0, 0)", "new Date(2016, -13, 1, 0, 0, 0)")]
    [TestCase("new Date(2013, 11, 1, 23, 59, 59)", "new Date(2015, -13, 1, 23, 59, 59)")]
    [TestCase("new Date(2010, 11, 1, 0, 0, 0)", "new Date(2012, -13, 1, 0, 0, 0)")]
    [TestCase("new Date(2010, 11, 29, 23, 59, 59)", "new Date(2012, -13, 29, 23, 59, 59)")]
    [TestCase("new Date(2013, 11, 1, 0, 0, 0)", "new Date(2016, -25, 1, 0, 0, 0)")]
    [TestCase("new Date(2012, 11, 1, 23, 59, 59)", "new Date(2015, -25, 1, 23, 59, 59)")]
    [TestCase("new Date(2009, 11, 1, 0, 0, 0)", "new Date(2012, -25, 1, 0, 0, 0)")]
    [TestCase("new Date(2009, 11, 29, 23, 59, 59)", "new Date(2012, -25, 29, 23, 59, 59)")]
    //Days
    [TestCase("new Date(2016, 1, 11, 0, 0, 0)", "new Date(2016, 0, 42, 0, 0, 0)")]
    [TestCase("new Date(2016, 0, 11, 23, 59, 59)", "new Date(2015, 11, 42, 23, 59, 59)")]
    [TestCase("new Date(2012, 3, 11, 0, 0, 0)", "new Date(2012, 2, 42, 0, 0, 0)")]
    [TestCase("new Date(2012, 2, 13, 23, 59, 59)", "new Date(2012, 1, 42, 23, 59, 59)")]
    [TestCase("new Date(2015, 11, 31, 0, 0, 0)", "new Date(2016, 0, 0, 0, 0, 0)")]
    [TestCase("new Date(2015, 10, 30, 23, 59, 59)", "new Date(2015, 11, 0, 23, 59, 59)")]
    [TestCase("new Date(2012, 1, 29, 0, 0, 0)", "new Date(2012, 2, 0, 0, 0, 0)")]
    [TestCase("new Date(2012, 0, 31, 23, 59, 59)", "new Date(2012, 1, 0, 23, 59, 59)")]
    [TestCase("new Date(2015, 10, 24, 0, 0, 0)", "new Date(2016, 0, -37, 0, 0, 0)")]
    [TestCase("new Date(2015, 9, 24, 23, 59, 59)", "new Date(2015, 11, -37, 23, 59, 59)")]
    [TestCase("new Date(2012, 0, 23, 0, 0, 0)", "new Date(2012, 2, -37, 0, 0, 0)")]
    [TestCase("new Date(2011, 11, 25, 23, 59, 59)", "new Date(2012, 1, -37, 23, 59, 59)")]
    //Hours
    [TestCase("new Date(2016, 0, 2, 1, 0, 0)", "new Date(2016, 0, 1, 25, 0, 0)")]
    [TestCase("new Date(2015, 11, 2, 1, 59, 59)", "new Date(2015, 11, 1, 25, 59, 59)")]
    [TestCase("new Date(2012, 2, 2, 1, 0, 0)", "new Date(2012, 2, 1, 25, 0, 0)")]
    [TestCase("new Date(2012, 2, 1, 1, 59, 59)", "new Date(2012, 1, 29, 25, 59, 59)")]
    [TestCase("new Date(2016, 0, 19, 3, 0, 0)", "new Date(2016, 0, 1, 435, 0, 0)")]
    [TestCase("new Date(2015, 11, 19, 3, 59, 59)", "new Date(2015, 11, 1, 435, 59, 59)")]
    [TestCase("new Date(2012, 2, 19, 3, 0, 0)", "new Date(2012, 2, 1, 435, 0, 0)")]
    [TestCase("new Date(2012, 2, 18, 3, 59, 59)", "new Date(2012, 1, 29, 435, 59, 59)")]
    [TestCase("new Date(2015, 11, 31, 23, 0, 0)", "new Date(2016, 0, 1, -1, 0, 0)")]
    [TestCase("new Date(2015, 10, 30, 23, 59, 59)", "new Date(2015, 11, 1, -1, 59, 59)")]
    [TestCase("new Date(2012, 1, 29, 23, 0, 0)", "new Date(2012, 2, 1, -1, 0, 0)")]
    [TestCase("new Date(2012, 1, 28, 23, 59, 59)", "new Date(2012, 1, 29, -1, 59, 59)")]
    [TestCase("new Date(2015, 11, 3, 18, 0, 0)", "new Date(2016, 0, 1, -678, 0, 0)")]
    [TestCase("new Date(2015, 10, 2, 18, 59, 59)", "new Date(2015, 11, 1, -678, 59, 59)")]
    [TestCase("new Date(2012, 1, 1, 18, 0, 0)", "new Date(2012, 2, 1, -678, 0, 0)")]
    [TestCase("new Date(2012, 0, 31, 18, 59, 59)", "new Date(2012, 1, 29, -678, 59, 59)")]
    // Minutes
    [TestCase("new Date(2016, 0, 1, 1, 0, 0)", "new Date(2016, 0, 1, 0, 60, 0)")]
    [TestCase("new Date(2015, 11, 2, 0, 0, 59)", "new Date(2015, 11, 1, 23, 60, 59)")]
    [TestCase("new Date(2012, 2, 1, 1, 0, 0)", "new Date(2012, 2, 1, 0, 60, 0)")]
    [TestCase("new Date(2012, 2, 1, 0, 0, 59)", "new Date(2012, 1, 29, 23, 60, 59)")]
    [TestCase("new Date(2015, 11, 31, 23, 59, 0)", "new Date(2016, 0, 1, 0, -1, 0)")]
    [TestCase("new Date(2015, 11, 1, 22, 59, 59)", "new Date(2015, 11, 1, 23, -1, 59)")]
    [TestCase("new Date(2012, 1, 29, 23, 59, 0)", "new Date(2012, 2, 1, 0, -1, 0)")]
    [TestCase("new Date(2012, 1, 29, 22, 59, 59)", "new Date(2012, 1, 29, 23, -1, 59)")]
    [TestCase("new Date(2016, 0, 2, 15, 5, 0)", "new Date(2016, 0, 1, 0, 2345, 0)")]
    [TestCase("new Date(2015, 11, 3, 14, 5, 59)", "new Date(2015, 11, 1, 23, 2345, 59)")]
    [TestCase("new Date(2012, 2, 2, 15, 5, 0)", "new Date(2012, 2, 1, 0, 2345, 0)")]
    [TestCase("new Date(2012, 2, 2, 14, 5, 59)", "new Date(2012, 1, 29, 23, 2345, 59)")]
    [TestCase("new Date(2015, 11, 25, 18, 24, 0)", "new Date(2016, 0, 1, 0, -8976, 0)")]
    [TestCase("new Date(2015, 10, 25, 17, 24, 59)", "new Date(2015, 11, 1, 23, -8976, 59)")]
    [TestCase("new Date(2012, 1, 23, 18, 24, 0)", "new Date(2012, 2, 1, 0, -8976, 0)")]
    [TestCase("new Date(2012, 1, 23, 17, 24, 59)", "new Date(2012, 1, 29, 23, -8976, 59)")]
    // Seconds
    [TestCase("new Date(2016, 0, 1, 0, 1, 0)", "new Date(2016, 0, 1, 0, 0, 60)")]
    [TestCase("new Date(2015, 11, 2, 0, 0, 0)", "new Date(2015, 11, 1, 23, 59, 60)")]
    [TestCase("new Date(2012, 2, 1, 0, 1, 0)", "new Date(2012, 2, 1, 0, 0, 60)")]
    [TestCase("new Date(2012, 2, 1, 0, 0, 0)", "new Date(2012, 1, 29, 23, 59, 60)")]
    [TestCase("new Date(2015, 11, 31, 23, 59, 59)", "new Date(2016, 0, 1, 0, 0, -1)")]
    [TestCase("new Date(2015, 11, 1, 23, 58, 59)", "new Date(2015, 11, 1, 23, 59, -1)")]
    [TestCase("new Date(2012, 1, 29, 23, 59, 59)", "new Date(2012, 2, 1, 0, 0, -1)")]
    [TestCase("new Date(2012, 1, 29, 23, 58, 59)", "new Date(2012, 1, 29, 23, 59, -1)")]
    [TestCase("new Date(2016, 0, 3, 17, 9, 58)", "new Date(2016, 0, 1, 0, 0, 234598)")]
    [TestCase("new Date(2015, 11, 4, 17, 8, 58)", "new Date(2015, 11, 1, 23, 59, 234598)")]
    [TestCase("new Date(2012, 2, 3, 17, 9, 58)", "new Date(2012, 2, 1, 0, 0, 234598)")]
    [TestCase("new Date(2012, 2, 3, 17, 8, 58)", "new Date(2012, 1, 29, 23, 59, 234598)")]
    [TestCase("new Date(2015, 11, 21, 14, 39, 15)", "new Date(2016, 0, 1, 0, 0, -897645)")]
    [TestCase("new Date(2015, 10, 21, 14, 38, 15)", "new Date(2015, 11, 1, 23, 59, -897645)")]
    [TestCase("new Date(2012, 1, 19, 14, 39, 15)", "new Date(2012, 2, 1, 0, 0, -897645)")]
    [TestCase("new Date(2012, 1, 19, 14, 38, 15)", "new Date(2012, 1, 29, 23, 59, -897645)")]
    public void ShouldSupportDateConsturctorWithArgumentOutOfRange(string expected, string actual)
    {
        var engine = new Engine(o => o.TimeZone = TimeZoneInfo.Utc);
        var expectedValue = engine.Evaluate(expected).ToObject();
        var actualValue = engine.Evaluate(actual).ToObject();
        actualValue.Should().Be(expectedValue);
    }

    [Test]
    public void ShouldReturnCorrectConcatenatedStrings()
    {
        RunTest(@"
                function concat(x, a, b) {
                    x += a;
                    x += b;
                    return x;
                }");

        var concat = _engine.GetValue("concat");
        var result = _engine.Invoke(concat, "concat", "well", "done").ToObject() as string;
        result.Should().Be("concatwelldone");
    }

    [Test]
    public void ComplexMappingAndReducing()
    {
        const string program = @"
Object.map = function (o, f, ctx) {
    ctx = ctx || this;
    var result = [];
    Object.keys(o).forEach(function(k) {
        result.push(f.call(ctx, o[k], k));
	});
    return result;
};

var x1 = {""Value"":1.0,""Elements"":[{""Name"":""a"",""Value"":""b"",""Decimal"":3.2},{""Name"":""a"",""Value"":""b"",""Decimal"": 3.5}],""Values"":{""test"": 2,""test1"":3,""test2"": 4}}
var x2 = {""Value"":2.0,""Elements"":[{""Name"":""aa"",""Value"":""ba"",""Decimal"":3.5}],""Values"":{""test"":1,""test1"":2,""test2"":3}};

function output(x) {
	var elements = x.Elements.map(function(a){return a.Decimal;});
	var values = x.Values;
	var generated = x.Elements.reduce(function(_obj, _cur) {_obj[(function(a){return a.Name;})(_cur)] = (function(a){return a.Decimal;})(_cur);return _obj;}, {});
	return {
        TestDictionary1 : values,
        TestDictionary2 : x.Values,
        TestDictionaryDirectAccess1 : Object.keys(x.Values).length,
        TestDictionaryDirectAccess2 : Object.keys(x.Values),
        TestDictionaryDirectAccess4 : Object.keys(x.Values).map(function(a){return x.Values[a];}),
        TestDictionarySum1 : Object.keys(values).map(function(a){return{Key: a,Value:values[a]};}).map(function(a){return a.Value;}).reduce(function(a, b) { return a + b; }, 0),
        TestDictionarySum2 : Object.keys(x.Values).map(function(a){return{Key: a,Value:x.Values[a]};}).map(function(a){return a.Value;}).reduce(function(a, b) { return a + b; }, 0),
        TestDictionarySum3 : Object.keys(x.Values).map(function(a){return x.Values[a];}).reduce(function(a, b) { return a + b; }, 0),
        TestDictionaryAverage1 : Object.keys(values).map(function(a){return{Key: a,Value:values[a]};}).map(function(a){return a.Value;}).reduce(function(a, b) { return a + b; }, 0)/(Object.keys(values).length||1),
        TestDictionaryAverage2 : Object.keys(x.Values).map(function(a){return{Key: a,Value:x.Values[a]};}).map(function(a){return a.Value;}).reduce(function(a, b) { return a + b; }, 0)/(Object.keys(x.Values).length||1),
        TestDictionaryAverage3 : Object.keys(x.Values).map(function(a){return x.Values[a];}).reduce(function(a, b) { return a + b; }, 0)/(Object.keys(x.Values).map(function(a){return x.Values[a];}).length||1),
        TestDictionaryFunc1 : Object.keys(x.Values).length,
        TestDictionaryFunc2 : Object.map(x.Values, function(v, k){ return v;}),
        TestGeneratedDictionary1 : generated,
        TestGeneratedDictionary2 : x.Elements.reduce(function(_obj, _cur) {_obj[(function(a){return a.Name;})(_cur)] = (function(a){return a.Decimal;})(_cur);return _obj;}, {}),
        TestGeneratedDictionary3 : Object.keys(generated).length,
        TestGeneratedDictionarySum1 : Object.keys(generated).map(function(a){return{Key: a,Value:generated[a]};}).map(function(a){return a.Value;}).reduce(function(a, b) { return a + b; }, 0),
        TestGeneratedDictionarySum2 : Object.keys(x.Elements.reduce(function(_obj, _cur) {_obj[(function(a){return a.Name;})(_cur)] = (function(a){return a.Decimal;})(_cur);return _obj;}, {})).map(function(a){return{Key: a,Value:x.Elements.reduce(function(_obj, _cur) {_obj[(function(a){return a.Name;})(_cur)] = (function(a){return a.Decimal;})(_cur);return _obj;}, {})[a]};}).map(function(a){return a.Value;}).reduce(function(a, b) { return a + b; }, 0),
        TestGeneratedDictionaryAverage1 : Object.keys(generated).map(function(a){return{Key: a,Value:generated[a]};}).map(function(a){return a.Value;}).reduce(function(a, b) { return a + b; }, 0)/(Object.keys(generated).length||1),
        TestGeneratedDictionaryAverage2 : Object.keys(x.Elements.reduce(function(_obj, _cur) {_obj[(function(a){return a.Name;})(_cur)] = (function(a){return a.Decimal;})(_cur);return _obj;}, {})).map(function(a){return{Key: a,Value:x.Elements.reduce(function(_obj, _cur) {_obj[(function(a){return a.Name;})(_cur)] = (function(a){return a.Decimal;})(_cur);return _obj;}, {})[a]};}).map(function(a){return a.Value;}).reduce(function(a, b) { return a + b; }, 0)/(Object.keys(x.Elements.reduce(function(_obj, _cur) {_obj[(function(a){return a.Name;})(_cur)] = (function(a){return a.Decimal;})(_cur);return _obj;}, {})).length||1),
        TestGeneratedDictionaryDirectAccess1 : Object.keys(generated),
        TestGeneratedDictionaryDirectAccess2 : Object.keys(generated).map(function(a){return generated[a];}),
        TestGeneratedDictionaryDirectAccess3 : Object.keys(generated).length,
        TestList1 : elements.reduce(function(a, b) { return a + b; }, 0),
        TestList2 : x.Elements.map(function(a){return a.Decimal;}).reduce(function(a, b) { return a + b; }, 0),
        TestList3 : x.Elements.map(function(a){return a.Decimal;}).reduce(function(a, b) { return a + b; }, 0),
        TestList4 : x.Elements.map(function(a){return a.Decimal;}).reduce(function(a, b) { return a + b; }, 0)/(x.Elements.length||1),
        TestList5 : x.Elements.map(function(a){return a.Decimal;}).reduce(function(a, b) { return a + b; }, 0)/(x.Elements.map((function(a){return a.Decimal;})).length||1)
    };
};
";
        _engine.Execute(program);
        var result1 = (ObjectInstance) _engine.Evaluate("output(x1)");
        var result2 = (ObjectInstance) _engine.Evaluate("output(x2)");

        TypeConverter.ToNumber(result1.Get("TestDictionarySum1")).Should().Be(9);
        TypeConverter.ToNumber(result1.Get("TestDictionarySum2")).Should().Be(9);
        TypeConverter.ToNumber(result1.Get("TestDictionarySum3")).Should().Be(9);

        TypeConverter.ToNumber(result1.Get("TestDictionaryAverage1")).Should().Be(3);
        TypeConverter.ToNumber(result1.Get("TestDictionaryAverage2")).Should().Be(3);
        TypeConverter.ToNumber(result1.Get("TestDictionaryAverage3")).Should().Be(3);

        TypeConverter.ToNumber(result1.Get("TestDictionaryFunc1")).Should().Be(3);
        TypeConverter.ToNumber(result1.Get("TestGeneratedDictionary3")).Should().Be(1);

        TypeConverter.ToNumber(result1.Get("TestGeneratedDictionarySum1")).Should().Be(3.5);
        TypeConverter.ToNumber(result1.Get("TestGeneratedDictionarySum2")).Should().Be(3.5);
        TypeConverter.ToNumber(result1.Get("TestGeneratedDictionaryAverage1")).Should().Be(3.5);
        TypeConverter.ToNumber(result1.Get("TestGeneratedDictionaryAverage2")).Should().Be(3.5);

        TypeConverter.ToNumber(result1.Get("TestGeneratedDictionaryDirectAccess3")).Should().Be(1);

        TypeConverter.ToNumber(result1.Get("TestList1")).Should().Be(6.7);
        TypeConverter.ToNumber(result1.Get("TestList2")).Should().Be(6.7);
        TypeConverter.ToNumber(result1.Get("TestList3")).Should().Be(6.7);
        TypeConverter.ToNumber(result1.Get("TestList4")).Should().Be(3.35);
        TypeConverter.ToNumber(result1.Get("TestList5")).Should().Be(3.35);

        TypeConverter.ToNumber(result2.Get("TestDictionarySum1")).Should().Be(6);
        TypeConverter.ToNumber(result2.Get("TestDictionarySum2")).Should().Be(6);
        TypeConverter.ToNumber(result2.Get("TestDictionarySum3")).Should().Be(6);

        TypeConverter.ToNumber(result2.Get("TestDictionaryAverage1")).Should().Be(2);
        TypeConverter.ToNumber(result2.Get("TestDictionaryAverage2")).Should().Be(2);
        TypeConverter.ToNumber(result2.Get("TestDictionaryAverage3")).Should().Be(2);
    }
    [Test]
    public void ShouldBeAbleToSpreadArrayLiteralsAndFunctionParameters()
    {
        RunTest(@"
                function concat(x, a, b) {
                    x += a;
                    x += b;
                    return x;
                }
                var s = [...'abc'];
                var c = concat(1, ...'ab');
                var arr1 = [1, 2];
                var arr2 = [3, 4 ];
                var r = [...arr2, ...arr1];
            ");

        var arrayInstance = (ArrayInstance) _engine.GetValue("r");
        arrayInstance[0].Should().Be(3);
        arrayInstance[1].Should().Be(4);
        arrayInstance[2].Should().Be(1);
        arrayInstance[3].Should().Be(2);

        arrayInstance = (ArrayInstance) _engine.GetValue("s");
        arrayInstance[0].Should().Be('a');
        arrayInstance[1].Should().Be('b');
        arrayInstance[2].Should().Be('c');

        var c = _engine.GetValue("c").ToString();
        c.Should().Be("1ab");
    }

    [Test]
    public void ShouldSpreadPrimitivesInObjectLiteralsViaToObject()
    {
        // PropertyDefinitionEvaluation for `...AssignmentExpression` performs CopyDataProperties
        // (https://tc39.es/ecma262/#sec-copydataproperties): step 1 skips undefined/null sources,
        // step 2 ToObject's everything else — so spreading a string primitive copies its index properties.
        _engine.Evaluate("JSON.stringify({...'ab'})").AsString().Should().Be("""{"0":"a","1":"b"}""");
        _engine.Evaluate("Object.keys({...'ab'}).join()").AsString().Should().Be("0,1");
        _engine.Evaluate("JSON.stringify({...'ab', x: 1})").AsString().Should().Be("""{"0":"a","1":"b","x":1}""");

        // Number/boolean/symbol wrappers have no enumerable own keys.
        _engine.Evaluate("JSON.stringify({...42})").AsString().Should().Be("{}");
        _engine.Evaluate("Object.keys({...42}).length").AsNumber().Should().Be(0);
        _engine.Evaluate("JSON.stringify({...true})").AsString().Should().Be("{}");
        _engine.Evaluate("Object.getOwnPropertyNames({...Symbol()}).length + Object.getOwnPropertySymbols({...Symbol()}).length").AsNumber().Should().Be(0);

        // CopyDataProperties step 1: undefined/null sources are skipped, not thrown.
        _engine.Evaluate("JSON.stringify({...null})").AsString().Should().Be("{}");
        _engine.Evaluate("JSON.stringify({...undefined})").AsString().Should().Be("{}");

        // Object.assign skips undefined/null and ToObject's other sources the same way
        // (https://tc39.es/ecma262/#sec-object.assign step 3.a).
        _engine.Evaluate("JSON.stringify(Object.assign({}, 'ab'))").AsString().Should().Be("""{"0":"a","1":"b"}""");
        _engine.Evaluate("JSON.stringify(Object.assign({}, null, undefined, 42, true))").AsString().Should().Be("{}");

        // Resume path: the literal build suspends inside the spread argument and resumes with a primitive.
        _engine.Evaluate("""
            (function() {
                function* g() { return { ...(yield), x: 2 }; }
                var it = g();
                it.next();
                return JSON.stringify(it.next('ab').value);
            })()
            """).AsString().Should().Be("""{"0":"a","1":"b","x":2}""");
    }

    [Test]
    public void ShouldSupportDefaultsInFunctionParameters()
    {
        RunTest(@"
                function f(x, y=12) {
                  // y is 12 if not passed (or passed as undefined)
                  return x + y;
                }
            ");

        var function = _engine.GetValue("f");
        var result = _engine.Invoke(function, 3).ToString();
        result.Should().Be("15");

        result = _engine.Invoke(function, 3, JsValue.Undefined).ToString();
        result.Should().Be("15");
    }

    [Test]
    public void ShouldReportErrorForInvalidJson()
    {
        var engine = new Engine();
        var ex = Invoking(() => engine.Evaluate("JSON.parse('[01]')")).Should().ThrowExactly<JavaScriptException>().Which;
        ex.Message.Should().Be("Unexpected token '1' in JSON at position 2");

        var voidCompletion = engine.Evaluate("try { JSON.parse('01') } catch (e) {}");
        voidCompletion.Should().BeUndefined();
    }

    [Test]
    public void ShouldParseAnonymousToTypeObject()
    {
        var obj = new Wrapper();
        var engine = new Engine(options => options.Interop.AllowWrite = true)
            .SetValue("x", obj);
        var js = @"
x.test = {
    name: 'Testificate',
    init (a, b) {
        return a + b
    }
}";
        engine.Execute(js);

        obj.Test.Name.Should().Be("Testificate");
        obj.Test.Init(2, 3).Should().Be(5);
    }

    [Test]
    public void ShouldOverrideDefaultTypeConverter()
    {
        var engine = new Engine(options => options
            .SetTypeConverter(e => new TestTypeConverter())
        );
        engine.TypeConverter.Should().BeOfType<TestTypeConverter>();
        engine.SetValue("x", new Testificate());
        Invoking(() => engine.Evaluate("c.Name")).Should().ThrowExactly<JavaScriptException>();
    }

    [Test]
    public void ShouldAllowDollarPrefixForProperties()
    {
        _engine.SetValue("str", "Hello");
        _engine.Evaluate("equal(undefined, str.$ref);");
        _engine.Evaluate("equal(undefined, str.ref);");
        _engine.Evaluate("equal(undefined, str.$foo);");
        _engine.Evaluate("equal(undefined, str.foo);");
        _engine.Evaluate("equal(undefined, str['$foo']);");
        _engine.Evaluate("equal(undefined, str['foo']);");

        _engine.Evaluate("equal(false, str.hasOwnProperty('$foo'));");
        _engine.Evaluate("equal(false, str.hasOwnProperty('foo'));");
    }

    [Test]
    public void ShouldProvideEngineForOptionsAsOverload()
    {
        new Engine((e, options) =>
            {
                e.Should().BeOfType<Engine>();
                options
                    .AddObjectConverter(new TestObjectConverter())
                    .AddObjectConverter<TestObjectConverter>();
            })
            .SetValue("a", 1);
    }

    [Test]
    public void ShouldReuseOptions()
    {
        var options = new Options().Configure(e => e.SetValue("x", 1));

        var engine1 = new Engine(options);
        var engine2 = new Engine(options);

        Convert.ToInt32(engine1.GetValue("x").ToObject()).Should().Be(1);
        Convert.ToInt32(engine2.GetValue("x").ToObject()).Should().Be(1);
    }

    [Test]
    public void RecursiveCallStack()
    {
        var engine = new Engine();
        Func<string, object> evaluateCode = code => engine.Evaluate(code);
        var evaluateCodeValue = JsValue.FromObject(engine, evaluateCode);

        engine.SetValue("evaluateCode", evaluateCodeValue);
        var result = (int) engine.Evaluate(@"evaluateCode('678 + 711')").AsNumber();

        result.Should().Be(1389);
    }

    [Test]
    public void NestedEvaluateDuringContinuationDrainDoesNotClobberOuterResult()
    {
        // Regression test for https://github.com/sebastienros/jint/issues/2492
        // A queued microtask, drained while a nested Evaluate() is running, used to overwrite
        // the engine-level completion value that the nested Evaluate() was about to return.
        var engine = new Engine();
        engine.SetValue("evalB", new Func<JsValue>(() => engine.Evaluate("'B'")));
        engine.SetValue("evalC", new Func<JsValue>(() => engine.Evaluate("'C'")));

        // Promise.resolve().then(evalC) queues a microtask. evalB()'s nested Evaluate drains it
        // (RunAvailableContinuations), running evalC() -> a deeper Evaluate that completes with 'C'.
        // Pre-fix, 'C' leaked into the shared field that evalB()'s Evaluate read back, so the
        // outer script observed 'C' instead of 'B'.
        var result = engine.Evaluate(@"
            var captured;
            Promise.resolve().then(() => { evalC(); });
            captured = evalB();
            captured;
        ");

        result.AsString().Should().Be("B");
    }

    [Test]
    public void MemberExpressionInObjectProperty()
    {
        var engine = new Engine();
        dynamic result = engine.Evaluate(@"
                const colorMap = {
                    Red: ""red"",
                    Orange: ""orange"",
                    White: ""white"",
                };

                Object
                    .keys(colorMap)
                    .reduce((agg, next) => {
                          return {...agg, ...{ [colorMap[next]]: next } };
                    },
                    {});
                ")
            .ToObject();

        ((string) result.red).Should().Be("Red");
        ((string) result.orange).Should().Be("Orange");
        ((string) result.white).Should().Be("White");
    }

    [Test]
    public void TypeofShouldEvaluateOnce()
    {
        var engine = new Engine();
        var result = engine.Evaluate(@"
                let res = 0;
                const fn = () => res++;
                typeof fn();
                res;
                ")
            .AsNumber();

        result.Should().Be(1);
    }

    [Test]
    public void MemberExpressionOnAssignmentShouldEvaluateRightHandSideOnce()
    {
        var engine = new Engine();

        engine.Execute(@"
            var callCount = 0;
            function produce() { callCount++; return 'abc'; }
            var x;
            var len = (x = produce()).length;
            ");

        engine.Evaluate("callCount").AsNumber().Should().Be(1);
        engine.Evaluate("len").AsNumber().Should().Be(3);
        engine.Evaluate("x").AsString().Should().Be("abc");
    }

    [Test]
    public void ClassDeclarationHoisting()
    {
        var ex = Invoking(() => _engine.Evaluate("typeof MyClass; class MyClass {}")).Should().ThrowExactly<JavaScriptException>().Which;
        ex.Message.Should().Be("Cannot access 'MyClass' before initialization");
    }

    [Test]
    public void ShouldObeyScriptLevelStrictModeInFunctions()
    {
        var engine = new Engine();
        const string source = "'use strict'; var x = () => { delete Boolean.prototype; }; x();";
        var ex = Invoking(() => engine.Evaluate(source)).Should().ThrowExactly<JavaScriptException>().Which;
        ex.Message.Should().Be("Cannot delete property 'prototype' of Object");

        const string source2 = "'use strict'; delete foobar;";
        ex = Invoking(() => engine.Evaluate(source2)).Should().ThrowExactly<JavaScriptException>().Which;
        ex.Message.Should().Be("Delete of an unqualified identifier in strict mode (<anonymous>:1:22)");
    }

    [Test]
    public void ShouldSupportThisInSubclass()
    {
        var engine = new Engine();
        var script = "class MyClass1 { } class MyClass2 extends MyClass1 { constructor() { } } const x = new MyClass2();";

        var ex = Invoking(() => engine.Evaluate(script)).Should().ThrowExactly<JavaScriptException>().Which;
        ex.Message.Should().Be("Must call super constructor in derived class before accessing 'this' or returning from derived constructor");
    }

    [Test]
    public void ShouldGetZeroPrefixedNumericKeys()
    {
        var engine = new Engine();
        engine.Evaluate("const testObj = { '02100' : true };");
        engine.Evaluate("Object.keys(testObj).length;").AsNumber().Should().Be(1);
        engine.Evaluate("JSON.stringify(Object.getOwnPropertyNames(testObj));").AsString().Should().Be("[\"02100\"]");
    }

    [Test]
    public void ShouldAllowOptionalChainingForMemberCall()
    {
        var engine = new Engine();
        const string Script = @"
                const adventurer = {  name: 'Alice', cat: { name: 'Dinah' } };
                const dogName = adventurer.dog?.name;
                const methodResult = adventurer.someNonExistentMethod?.();
                return [ dogName, methodResult ];
            ";
        var array = engine.Evaluate(Script).AsArray();

        array.Length.Should().Be(2);
        array[0].IsUndefined().Should().BeTrue();
        array[1].IsUndefined().Should().BeTrue();
    }

    [Test]
    public void CanDisableCompilation()
    {
        var engine = new Engine(options =>
        {
            options.Host.StringCompilationAllowed = false;
        });

        const string ExpectedExceptionMessage = "String compilation has been disabled in engine options";

        var ex = Invoking(() => engine.Evaluate("eval('1+1');")).Should().ThrowExactly<JavaScriptException>().Which;
        ex.Message.Should().Be(ExpectedExceptionMessage);

        ex = Invoking(() => engine.Evaluate("new Function('1+1');")).Should().ThrowExactly<JavaScriptException>().Which;
        ex.Message.Should().Be(ExpectedExceptionMessage);
    }

    [Test]
    public void ExecuteShouldTriggerBeforeEvaluateEvent()
    {
        TestBeforeEvaluateEvent(
            (engine, code) => engine.Execute(code),
            expectedSource: "<anonymous>"
        );
    }

    [Test]
    public void ExecuteWithSourceShouldTriggerBeforeEvaluateEvent()
    {
        TestBeforeEvaluateEvent(
            (engine, code) => engine.Execute(code, "mysource"),
            expectedSource: "mysource"
        );
    }

    [Test]
    public void ExecuteWithParserOptionsShouldTriggerBeforeEvaluateEvent()
    {
        TestBeforeEvaluateEvent(
            (engine, code) => engine.Execute(code, parsingOptions: ScriptParsingOptions.Default),
            expectedSource: "<anonymous>"
        );
    }

    [Test]
    public void ExecuteWithSourceAndParserOptionsShouldTriggerBeforeEvaluateEvent()
    {
        TestBeforeEvaluateEvent(
            (engine, code) => engine.Execute(code, "mysource", ScriptParsingOptions.Default),
            expectedSource: "mysource"
        );
    }

    [Test]
    public void EvaluateShouldTriggerBeforeEvaluateEvent()
    {
        TestBeforeEvaluateEvent(
            (engine, code) => engine.Evaluate(code),
            expectedSource: "<anonymous>"
        );
    }

    [Test]
    public void EvaluateWithSourceShouldTriggerBeforeEvaluateEvent()
    {
        TestBeforeEvaluateEvent(
            (engine, code) => engine.Evaluate(code, "mysource"),
            expectedSource: "mysource"
        );
    }

    [Test]
    public void EvaluateWithParserOptionsShouldTriggerBeforeEvaluateEvent()
    {
        TestBeforeEvaluateEvent(
            (engine, code) => engine.Evaluate(code, parsingOptions: ScriptParsingOptions.Default),
            expectedSource: "<anonymous>"
        );
    }

    [Test]
    public void EvaluateWithSourceAndParserOptionsShouldTriggerBeforeEvaluateEvent()
    {
        TestBeforeEvaluateEvent(
            (engine, code) => engine.Evaluate(code, "mysource", ScriptParsingOptions.Default),
            expectedSource: "mysource"
        );
    }

    [Test]
    public void ImportModuleShouldTriggerBeforeEvaluateEvents()
    {
        var engine = new Engine();

        const string module1 = "import dummy from 'module2';";
        const string module2 = "export default 'dummy';";

        var beforeEvaluateTriggeredCount = 0;
        engine.Debugger.BeforeEvaluate += (sender, ast) =>
        {
            beforeEvaluateTriggeredCount++;
            sender.Should().Be(engine);

            switch (beforeEvaluateTriggeredCount)
            {
                case 1:
                    ast.Location.SourceFile.Should().Be("module1");
                    ast.Body.Should().SatisfyRespectively(
                        node => node.Should().BeOfType<ImportDeclaration>()
                    );
                    break;
                case 2:
                    ast.Location.SourceFile.Should().Be("module2");
                    ast.Body.Should().SatisfyRespectively(
                        node => node.Should().BeOfType<ExportDefaultDeclaration>()
                    );
                    break;
            }
        };

        engine.Modules.Add("module1", module1);
        engine.Modules.Add("module2", module2);
        engine.Modules.Import("module1");

        beforeEvaluateTriggeredCount.Should().Be(2);
    }

    [Test]
    public void ShouldConvertJsTypedArraysCorrectly()
    {
        var engine = new Engine();
            
        var float32 = new float [] { 42f, 23 };
            
        engine.SetValue("float32", float32); 
        engine.SetValue("testFloat32Array", new Action<float[]>(v => float32.Should().Equal(v)));
            
        engine.Evaluate(@"
                testFloat32Array(new Float32Array(float32));
            ");
    }

    private static void TestBeforeEvaluateEvent(Action<Engine, string> call, string expectedSource)
    {
        var engine = new Engine();

        const string script = "'dummy';";

        var beforeEvaluateTriggered = false;
        engine.Debugger.BeforeEvaluate += (sender, ast) =>
        {
            beforeEvaluateTriggered = true;
            sender.Should().Be(engine);
            ast.Location.SourceFile.Should().Be(expectedSource);
            ast.Body.Should().SatisfyRespectively(node => TestHelpers.IsLiteral(node, "dummy").Should().BeTrue());
        };

        call(engine, script);

        beforeEvaluateTriggered.Should().BeTrue();
    }

    [Test]
    public void ShouldHandleFixedSlotFunctionWithLetConst()
    {
        var engine = new Engine();
        var result = engine.Evaluate(@"
            function compute(a, b) {
                let sum = a + b;
                const product = a * b;
                let result = sum + product;
                return result;
            }
            compute(3, 4);
        ");
        result.AsNumber().Should().Be(19d);
    }

    [Test]
    public void ShouldHandleFixedSlotFunctionWithConstReassignmentError()
    {
        var engine = new Engine();
        var ex = Invoking(() => engine.Evaluate(@"
            function test(x) {
                const y = x + 1;
                y = 10;
                return y;
            }
            test(5);
        ")).Should().ThrowExactly<JavaScriptException>().Which;
        ex.Message.Should().ContainEquivalentOf("constant");
    }

    [Test]
    public void ShouldHandleFixedSlotFunctionWithLetTemporalDeadZone()
    {
        var engine = new Engine();
        var ex = Invoking(() => engine.Evaluate(@"
            function test() {
                var x = y;
                let y = 10;
                return x;
            }
            test();
        ")).Should().ThrowExactly<JavaScriptException>().Which;
        ex.Message.Should().ContainEquivalentOf("has not been initialized");
    }

    [Test]
    public void ShouldHandleFixedSlotFunctionWithClosureOverLetConst()
    {
        var engine = new Engine();
        var result = engine.Evaluate(@"
            function makeCounter(start) {
                let count = start;
                const increment = 1;
                return function() {
                    count += increment;
                    return count;
                };
            }
            var counter = makeCounter(10);
            counter() + counter() + counter();
        ");
        result.AsNumber().Should().Be(36d);
    }

    [Test]
    public void ShouldHandleFixedSlotFunctionWithMultipleLetDeclarations()
    {
        var engine = new Engine();
        var result = engine.Evaluate(@"
            function swap(a, b) {
                let temp = a;
                let x = b;
                let y = temp;
                return x * 100 + y;
            }
            swap(3, 7);
        ");
        result.AsNumber().Should().Be(703d);
    }

    [Test]
    public void ShouldHandleSlotCachingWithNonEscapingEnvironment()
    {
        // Function with let/const where environment doesn't escape (no closures)
        // should benefit from slot caching across calls
        var engine = new Engine();
        var result = engine.Evaluate(@"
            function add(a, b) {
                let result = a + b;
                return result;
            }
            add(1, 2) + add(3, 4) + add(5, 6);
        ");
        result.AsNumber().Should().Be(21d);
    }

    [Test]
    public void ShouldAllowSlotCachingWhenClosureDoesNotReferenceSlotVars()
    {
        // Closure exists but doesn't reference any outer slot variables —
        // environment can still be cached for reuse
        var engine = new Engine();
        var result = engine.Evaluate(@"
            function process(x, y) {
                var helper = function() { return 42; };
                return x + y + helper();
            }
            process(1, 2) + process(3, 4);
        ");
        result.AsNumber().Should().Be(94d);
    }

    [Test]
    public void ShouldPreventSlotCachingWhenClosureReferencesSlotVar()
    {
        // Closure references outer slot variable — environment must escape
        var engine = new Engine();
        var result = engine.Evaluate(@"
            function makeAdder(x) {
                return function(y) { return x + y; };
            }
            var add5 = makeAdder(5);
            var add10 = makeAdder(10);
            add5(3) + add10(3);
        ");
        result.AsNumber().Should().Be(21d);
    }

    [Test]
    public void ShouldHandleNestedClosureReferencingOuterSlotVar()
    {
        // Deeply nested closure references outer function's slot variable
        var engine = new Engine();
        var result = engine.Evaluate(@"
            function outer(x) {
                return function middle() {
                    return function inner() {
                        return x;
                    };
                };
            }
            outer(42)()();
        ");
        result.AsNumber().Should().Be(42d);
    }

    [Test]
    public void ShouldAllowSlotCachingWhenClosureOnlyUsesGlobals()
    {
        // Closure uses global variable, not any slot variables
        var engine = new Engine();
        engine.Evaluate("var globalVal = 100;");
        var result = engine.Evaluate(@"
            function compute(a, b) {
                var fn = function() { return globalVal; };
                return a + b + fn();
            }
            compute(1, 2) + compute(3, 4);
        ");
        result.AsNumber().Should().Be(210d);
    }

    [Test]
    public void ShouldHandleConciseArrowReturningArrowWithClosure()
    {
        // Concise arrow x => y => x * y — body IS a closure that references slot var x
        var engine = new Engine();
        var result = engine.Evaluate(@"
            var make = x => y => x * y;
            var double = make(2);
            var triple = make(3);
            double(5) + triple(5);
        ");
        result.AsNumber().Should().Be(25d);
    }

    private class Wrapper
    {
        public Testificate Test { get; set; }
    }

    private class Testificate
    {
        public string Name { get; set; }
        public Func<int, int, int> Init { get; set; }
    }

    private class TestObjectConverter : Jint.Runtime.Interop.ObjectConverter
    {
        public override bool TryConvert(Engine engine, object value, out JsValue result)
        {
            throw new NotImplementedException();
        }
    }

    private class TestTypeConverter : Jint.Runtime.Interop.ClrTypeConverter
    {
        public override object Convert(object value, Type type, IFormatProvider formatProvider)
        {
            throw new NotImplementedException();
        }

        public override bool TryConvert(object value, Type type, IFormatProvider formatProvider, out object converted)
        {
            throw new NotImplementedException();
        }
    }
}
