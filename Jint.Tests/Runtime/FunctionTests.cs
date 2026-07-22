using System.Text.RegularExpressions;
using Jint.Native;
using Jint.Native.Function;
using Jint.Runtime;
using Jint.Runtime.Interop;

namespace Jint.Tests.Runtime;

public class FunctionTests
{
    private readonly Engine _engine;

    public FunctionTests()
    {
        _engine = new Engine()
            .SetValue("equal", new Action<object, object>(static (expected, actual) =>
                    actual.Should().BeEquivalentTo(expected, static options => options.WithStrictOrdering())));
    }

    [Fact]
    public void ApplyRejectsArrayLikeArgumentsThatCannotBeMaterialized()
    {
        var engine = new Engine();
        var length = ClrLimits.MaxArrayLength + 1UL;

        var exception = Invoking(() => engine.Evaluate($"(function(){{}}).apply(null, {{ length: {length} }});")).Should().ThrowExactly<JavaScriptException>().Which;

        exception.Error.InstanceofOperator(engine.Intrinsics.RangeError).Should().BeTrue();
    }

    [Fact]
    public void FunctionPrototypeApplyThrowsRangeErrorFromTheFunctionsRealm()
    {
        var engine = new Engine();
        var otherGlobal = engine._host.CreateRealm().GlobalObject;
        otherGlobal.Set("global", otherGlobal);
        engine.SetValue("other", otherGlobal);
        var length = ClrLimits.MaxArrayLength + 1UL;

        var script = @"
            var threw = false;
            try {
                other.Function.prototype.apply.call(function () {}, null, { length: LENGTH });
            } catch (e) {
                threw = (e instanceof other.RangeError) && !(e instanceof RangeError);
            }
            threw
        ".Replace("LENGTH", length.ToString());

        ((JsBoolean) engine.Evaluate(script))._value.Should().BeTrue();
    }

    [Fact]
    public void FunctionPrototypeApplyThrowsTypeErrorFromTheFunctionsRealm()
    {
        // Regression for the source-gen [JsFunction]/ICallable cast precondition: when
        // `other.Function.prototype.apply` is invoked from the main realm with a non-callable
        // 'this', the TypeError must come from `other`'s realm (the realm that defined apply),
        // not from whatever realm is executing at the time of the call. ECMA-262 + test262
        // built-ins/Function/prototype/apply/this-not-callable-realm.
        var engine = new Engine();
        var otherGlobal = engine._host.CreateRealm().GlobalObject;
        otherGlobal.Set("global", otherGlobal);
        engine.SetValue("other", otherGlobal);

        var script = @"
            var threw = false;
            try {
                other.Function.prototype.apply.call({}, undefined);
            } catch (e) {
                threw = (e instanceof other.TypeError) && !(e instanceof TypeError);
            }
            threw
        ";

        ((JsBoolean) engine.Evaluate(script))._value.Should().BeTrue();
    }

    [Fact]
    public void BindCombinesBoundArgumentsToCallArgumentsCorrectly()
    {
        _engine.Evaluate("var testFunc = function (a, b, c) { return a + ', ' + b + ', ' + c + ', ' + JSON.stringify(arguments); }");

        _engine.Evaluate("testFunc('a', 1, 'a');").AsString().Should().Be("a, 1, a, {\"0\":\"a\",\"1\":1,\"2\":\"a\"}");
        _engine.Evaluate("testFunc.bind('anything')('a', 1, 'a');").AsString().Should().Be("a, 1, a, {\"0\":\"a\",\"1\":1,\"2\":\"a\"}");
    }

    [Fact]
    public void ArrowFunctionShouldBeExtensible()
    {
        new Engine()
            .SetValue("assert", new Action<bool>(static value => value.Should().BeTrue()))
            .Execute(@"
                    var a = () => null
                    Object.defineProperty(a, 'hello', { enumerable: true, get: () => 'world' })
                    assert(a.hello === 'world')

                    a.foo = 'bar';
                    assert(a.foo === 'bar');
                ");
    }

    [Fact]
    public void BlockScopeFunctionShouldWork()
    {
        const string Script = @"
function execute(doc, args){
    var i = doc;
    {
        function doSomething() {
            return 'ayende';
        }

        i.Name = doSomething();
    }
}
";

        var engine = new Engine(options =>
        {
            options.Strict();
        });
        engine.Execute(Script);

        var obj = engine.Evaluate("var obj = {}; execute(obj); return obj;").AsObject();

        obj.Get("Name").AsString().Should().Be("ayende");
    }

    [Fact]
    public void ObjectCoercibleForCallable()
    {
        const string Script = @"
var booleanCount = 0;
Boolean.prototype.then = function() {
  booleanCount += 1;
};
function test() {
    this.then();    
}
testFunction.call(true);
assertEqual(booleanCount, 1);
";
        var engine = new Engine();
        engine
            .SetValue("testFunction", new ClrFunction(engine, "testFunction", (thisValue, args) =>
            {
                return engine.Invoke(thisValue, "then", [JsValue.Undefined, args.At(0)]);
            }))
            .SetValue("assertEqual", new Action<object, object>((a, b) => a.Should().Be(b)))
            .Execute(Script);
    }

    [Fact]
    public void AnonymousLambdaShouldHaveNameDefined()
    {
        _engine.Evaluate("(()=>{}).hasOwnProperty('name')").AsBoolean().Should().BeTrue();
    }

    [Fact]
    public void CanInvokeConstructorsFromEngine()
    {
        _engine.Evaluate("class TestClass { constructor(a, b) { this.a = a; this.b = b; }}");
        _engine.Evaluate("function TestFunction(a, b) { this.a = a; this.b = b; }");

        var instanceFromClass = _engine.Construct("TestClass", "abc", 123).AsObject();
        instanceFromClass.Get("a").Should().Be("abc");
        instanceFromClass.Get("b").Should().Be(123);

        var instanceFromFunction = _engine.Construct("TestFunction", "abc", 123).AsObject();
        instanceFromFunction.Get("a").Should().Be("abc");
        instanceFromFunction.Get("b").Should().Be(123);

        var arrayInstance = (JsArray) _engine.Construct("Array", "abc", 123).AsObject();
        arrayInstance.Length.Should().Be((uint) 2);
        arrayInstance[0].Should().Be("abc");
        arrayInstance[1].Should().Be(123);
    }

    [Fact]
    public void FunctionInstancesCanBePassedToHost()
    {
        var engine = new Engine();
        Func<JsValue, JsValue[], JsValue> ev = null;

        void addListener(Func<JsValue, JsValue[], JsValue> callback)
        {
            ev = callback;
        }

        engine.SetValue("addListener", new Action<Func<JsValue, JsValue[], JsValue>>(addListener));

        engine.Execute(@"
                var a = 5;

                (function() {
                    var acc = 10;
                    addListener(function (val) {
                        a = (val || 0) + acc;
                    });
                })();
");

        engine.Evaluate("a").Should().Be(5);

        ev(null, []);
        engine.Evaluate("a").Should().Be(10);

        ev(null, [20]);
        engine.Evaluate("a").Should().Be(30);
    }

    [Fact]
    public void BoundFunctionCanBeUsedAsPropertyGetter()
    {
        var result = new Engine().Evaluate("""
            const holder = {
                x: 42,
                getter() { return this.x; }
            };
            const target = {};
            Object.defineProperty(target, "prop", { get: holder.getter.bind(holder) });
            target.prop;
            """);
        result.AsInteger().Should().Be(42);
    }

    [Fact]
    public void BoundFunctionsCanBePassedToHost()
    {
        var engine = new Engine();
        Func<JsValue, JsValue[], JsValue> ev = null;

        void addListener(Func<JsValue, JsValue[], JsValue> callback)
        {
            ev = callback;
        }

        engine.SetValue("addListener", new Action<Func<JsValue, JsValue[], JsValue>>(addListener));

        engine.Execute(@"
                var a = 5;

                (function() {
                    addListener(function (acc, val) {
                        a = (val || 0) + acc;
                    }.bind(null, 10));
                })();
            ");

        engine.Evaluate("a").Should().Be(5);

        ev(null, []);
        engine.Evaluate("a").Should().Be(10);

        ev(null, [20]);
        engine.Evaluate("a").Should().Be(30);
    }

    [Fact]
    public void ConstructorsCanBePassedToHost()
    {
        var engine = new Engine();
        Func<JsValue, JsValue[], JsValue> ev = null;

        void addListener(Func<JsValue, JsValue[], JsValue> callback)
        {
            ev = callback;
        }

        engine.SetValue("addListener", new Action<Func<JsValue, JsValue[], JsValue>>(addListener));

        engine.Execute(@"addListener(Boolean)");

        ev(JsValue.Undefined, ["test"]).Should().BeTrue();
        ev(JsValue.Undefined, [5]).Should().BeTrue();
        ev(JsValue.Undefined, [false]).Should().BeFalse();
        ev(JsValue.Undefined, [0]).Should().BeFalse();
        ev(JsValue.Undefined, [JsValue.Undefined]).Should().BeFalse();
    }

    [Fact]
    public void FunctionsShouldResolveToSameReference()
    {
        _engine.SetValue("equal", new Action<object, object>(static (expected, actual) =>
                    actual.Should().BeEquivalentTo(expected, static options => options.WithStrictOrdering())));
        _engine.Execute(@"
                function testFn() {}
                equal(testFn, testFn);
            ");
    }

    [Fact]
    public void CanInvokeCallForFunctionInstance()
    {
        _engine.Evaluate(@"
                (function () {
                    function foo(a = 123) { return a; }
                    foo()
                })")
            .As<Function>().Call();

        var result = _engine.Evaluate(@"
                (function () {
                    class Foo { test() { return 123 } }
                    let f = new Foo()
                    return f.test()
                })")
            .As<Function>().Call();

        result.IsInteger().Should().BeTrue();
        result.AsInteger().Should().Be(123);
    }

    [Fact]
    public void CanInvokeCallForFunctionInstanceWithDifferingArgCounts()
    {
        _engine.Execute("function foo() { return Array.from(arguments).join(','); }");
        var function = _engine.GetValue("foo");
        function.Call().Should().Be("");
        function.Call(1).Should().Be("1");
        function.Call(1, 2).Should().Be("1,2");
        function.Call(1, 2, 3).Should().Be("1,2,3");
        function.Call(1, 2, 3, 4).Should().Be("1,2,3,4");
    }

    [Fact]
    public void CanInvokeFunctionViaEngineInstance()
    {
        var function = _engine.Evaluate("function bar(a) { return a; }; bar;");

        _engine.Call(function, 123).Should().Be(123);
        _engine.Call("bar", 123).Should().Be(123);
    }

    [Fact]
    public void CanInvokeFunctionViaEngineInstanceWithCustomThisObj()
    {
        var function = _engine.Evaluate("function baz() { return this; }; baz;");

        TypeConverter.ToString(_engine.Call(function, "I'm this!", Arguments.Empty)).Should().Be("I'm this!");
        TypeConverter.ToString(function.Call("I'm this!", Arguments.Empty)).Should().Be("I'm this!");
    }

    [Fact]
    public void ArrowFunction()
    {
        const string Script = @"var f = (function() { return z => arguments[0]; }(5)); equal(5, f(6));";
        _engine.Execute(Script);
    }

    [Fact]
    public void MultipleCallsShouldNotCacheFunctionEnvironment()
    {
        var engine = new Engine();
        engine.Evaluate(
            """
            function findInArray(arr, predicate) {
                for (let i = 0; i<arr.length; i++) {
                    if (predicate(arr[i]) === true) {
                        return arr[i];
                    }
                }
            }
            function findIt(array, kind) {           
                let found = findInArray(array, function sub(x) {
                    return x.kind == kind;
                });
                return found;
            };
            """);
        var findIt = (ScriptFunction) engine.GetValue("findIt");
        var interop = (Func<JsValue, JsValue[], JsValue>) findIt.ToObject()!;

        var values = new List<object>
        {
            new { kind = 'a' },
            new { kind = 'b' }
        };

        var found1 = interop(
            JsValue.Undefined,
            [
                JsValue.FromObject(engine, values),
                JsValue.FromObject(engine, "a")
            ])
            .ToObject();

        var found2 = interop(
            JsValue.Undefined,
            [
                JsValue.FromObject(engine, values),
                JsValue.FromObject(engine, "b")
            ])
            .ToObject();

        found1.Should().Be(values[0]);
        found2.Should().Be(values[1]);
    }

    [Theory]
    [InlineData(
        """
        /* before */ function fn() {
          return 0;
        } /* after */
        [fn.toString(), fn.toString()]
        """,
        """
        function fn() {
          return 0;
        }
        """)]
    [InlineData(
        """
        /* before */ async function* g() {
          yield 0;
        } /* after */
        [g.toString(), g.toString()]
        """,
        """
        async function* g() {
          yield 0;
        }
        """)]
    [InlineData(
        """
        /* before */ var fn = async function f() {
          return 0;
        } /* after */ ;
        [fn.toString(), fn.toString()]
        """,
        """
        async function f() {
          return 0;
        }
        """)]
    [InlineData(
        """
        /* before */ let g = function*() {
          yield 0;
        }; /* after */
        [g.toString(), g.toString()]
        """,
        """
        function*() {
          yield 0;
        }
        """)]
    [InlineData(
        """
        /* before */ var fn = () => {
          return 0;
        } /* after */
        [fn.toString(), fn.toString()]
        """,
        """
        () => {
          return 0;
        }
        """)]
    [InlineData(
        """
        /* before */ const o = {
          m () {
            return 0;
          }
        } /* after */
        const m = o.m;
        [m.toString(), m.toString()]
        """,
        """
        m () {
            return 0;
          }
        """)]
    [InlineData(
        """
        /* before */ const o = {
          *g( ) {
            yield 0;
          }
        } /* after */
        const g = o.g;
        [g.toString(), g.toString()]
        """,
        """
        *g( ) {
            yield 0;
          }
        """)]
    [InlineData(
        """
        /* before */ const o = {
          get p() {
            return 0;
          }
        } /* after */
        const getter = Object.getOwnPropertyDescriptor(o, "p").get;
        [getter.toString(), getter.toString()]
        """,
        """
        get p() {
            return 0;
          }
        """)]
    [InlineData(
        """
        /* before */ const o = {
          set p(_) {
          }
        } /* after */
        const setter = Object.getOwnPropertyDescriptor(o, "p").set;
        [setter.toString(), setter.toString()]
        """,
        """
        set p(_) {
          }
        """)]
    [InlineData(
        """
        /* before */ class A extends Object.prototype.constructor {
          constructor() { super(); }
        } /* after */
        const ctor = A.prototype.constructor;
        [ctor.toString(), ctor.toString()]
        """,
        """
        class A extends Object.prototype.constructor {
          constructor() { super(); }
        }
        """)]
    [InlineData(
        """
        /* before */ class A {
          m() {
            return 0;
          }
        } /* after */
        const m = A.prototype.m;
        [m.toString(), m.toString()]
        """,
        """
        m() {
            return 0;
          }
        """)]
    [InlineData(
        """
        /* before */ class A {
          * g( ) {
            yield 0;
          }
        } /* after */
        const g = A.prototype.g;
        [g.toString(), g.toString()]
        """,
        """
        * g( ) {
            yield 0;
          }
        """)]
    [InlineData(
        """
        /* before */ class A {
          get p() {
            return 0;
          }
        } /* after */
        const getter = Object.getOwnPropertyDescriptor(A.prototype, "p").get;
        [getter.toString(), getter.toString()]
        """,
        """
        get p() {
            return 0;
          }
        """)]
    [InlineData(
        """
        /* before */ class A {
          set p(_) {
          }
        } /* after */
        const setter = Object.getOwnPropertyDescriptor(A.prototype, "p").set;
        [setter.toString(), setter.toString()]
        """,
        """
        set p(_) {
          }
        """)]
    [InlineData(
        """
        /* before */ class A {
          static m() {
            return 0;
          }
        } /* after */
        const m = A.m;
        [m.toString(), m.toString()]
        """,
        """
        m() {
            return 0;
          }
        """)]
    [InlineData(
        """
        /* before */ class A {
          static async * g() {
            yield 0;
          }
        } /* after */
        const g = A.g;
        [g.toString(), g.toString()]
        """,
        """
        async * g() {
            yield 0;
          }
        """)]
    [InlineData(
        """
        /* before */ class A {
          static get p() {
            return 0;
          }
        } /* after */
        const getter = Object.getOwnPropertyDescriptor(A, "p").get;
        [getter.toString(), getter.toString()]
        """,
        """
        get p() {
            return 0;
          }
        """)]
    [InlineData(
        """
        /* before */ class A {
          static set p(_) {
          }
        } /* after */
        const setter = Object.getOwnPropertyDescriptor(A, "p").set;
        [setter.toString(), setter.toString()]
        """,
        """
        set p(_) {
          }
        """)]
    [InlineData(
        """
        var fn = /* before */Function()/* after */;
        [fn.toString(), fn.toString()]
        """,
        """
        function anonymous(
        ) {

        }
        """)]
    [InlineData(
        """
        var Generator = (function*() {}).constructor;
        var fn = /* before */Generator()/* after */;
        [fn.toString(), fn.toString()]
        """,
        """
        function* anonymous(
        ) {

        }
        """)]
    [InlineData(
        """
        var AsyncFunction = (async function() {}).constructor;
        var fn = /* before */AsyncFunction()/* after */;
        [fn.toString(), fn.toString()]
        """,
        """
        async function anonymous(
        ) {

        }
        """)]
    [InlineData(
        """
        var AsyncGenerator = (async function*() {}).constructor;
        var fn = /* before */AsyncGenerator()/* after */;
        [fn.toString(), fn.toString()]
        """,
        """
        async function* anonymous(
        ) {

        }
        """)]
    [InlineData(
        """
        var p1 = "a //", p2 = "/*a */ b, c";
        var fn = /* before */Function(p1, p2, "return Promise.resolve(a+b+c) /* d */ //")/* after */;
        [fn.toString(), fn.toString()]
        """,
        """
        function anonymous(a //,/*a */ b, c
        ) {
        return Promise.resolve(a+b+c) /* d */ //
        }
        """)]
    [InlineData(
        """
        var Generator = (function*() {}).constructor;
        var p1 = "a //", p2 = "/*a */ b, c";
        var fn = /* before */Generator(p1, p2, "return Promise.resolve(a+b+c) /* d */ //")/* after */;
        [fn.toString(), fn.toString()]
        """,
        """
        function* anonymous(a //,/*a */ b, c
        ) {
        return Promise.resolve(a+b+c) /* d */ //
        }
        """)]
    [InlineData(
        """
        var AsyncFunction = (async function() {}).constructor;
        var p1 = "a //", p2 = "/*a */ b, c";
        var fn = /* before */AsyncFunction(p1, p2, "return Promise.resolve(a+b+c) /* d */ //")/* after */; 
        [fn.toString(), fn.toString()]
        """,
        """
        async function anonymous(a //,/*a */ b, c
        ) {
        return Promise.resolve(a+b+c) /* d */ //
        }
        """)]
    [InlineData(
        """
        var AsyncGenerator = (async function*() {}).constructor;
        var p1 = "a //", p2 = "/*a */ b, c";
        var fn = /* before */AsyncGenerator(p1, p2, "return Promise.resolve(a+b+c) /* d */ //")/* after */; 
        [fn.toString(), fn.toString()]
        """,
        """
        async function* anonymous(a //,/*a */ b, c
        ) {
        return Promise.resolve(a+b+c) /* d */ //
        }
        """)]
    [InlineData(
        """
        eval('function fn() {\n}');
        [fn.toString(), fn.toString()]
        """,
        """
        function fn() {
        }
        """)]
    [InlineData(
        """
        const fn = new ShadowRealm().evaluate('(\n)=>0');
        [fn.toString(), fn.toString()]
        """,
        """
        (
        )=>0
        """)]
    [InlineData(
        """
        const fn = (0, eval)(
          new ShadowRealm().evaluate('(\n)=>0')
        );
        [fn.toString(), fn.toString()]
        """,
        """
        (
        )=>0
        """)]
    public void ToStringShouldProduceStandardsCompliantResultWhenSourceTextRetained(string code, string expectedResult)
    {
        code = Regex.Replace(code, @"\r\n?", "\n", RegexOptions.CultureInvariant); // normalize line endings to '\n'
        expectedResult = Regex.Replace(expectedResult, @"\r\n?", "\n", RegexOptions.CultureInvariant); // normalize line endings to '\n'

        var returnValue = new Engine(options => options.RetainFunctionSourceText()).Evaluate(code);

        var actualResults = returnValue.Should().BeOfType<JsArray>().Which;
        actualResults.Length.Should().Be(2u);

        var actualResult1 = actualResults[0].Should().BeOfType<JsString>().Which.ToString();
        actualResult1.Should().Be(expectedResult);

        var actualResult2 = actualResults[1].Should().BeOfType<JsString>().Which.ToString();
        actualResult2.Should().BeSameAs(actualResult1);
    }

    [Fact]
    public void ToStringReturnsNativeCodePlaceholderByDefault()
    {
        // By default the source text is not retained (to save memory), so toString() returns the
        // native-code placeholder rather than the original source. See issue #2560.
        var engine = new Engine();

        engine.Evaluate("function fn() { return 0; } fn.toString();").AsString().Should().Be("function fn() { [native code] }");
        engine.Evaluate("var foo = function () {}; foo.toString();").AsString().Should().Be("function foo() { [native code] }");
        engine.Evaluate("(() => 0).toString();").AsString().Should().Be("function () { [native code] }");
    }

    [Fact]
    public void ToStringReturnsSourceTextWhenRetainedViaParsingOptions()
    {
        // Opt in to source-text retention through parsing options (covers the prepared-script path too).
        var engine = new Engine();
        var parsingOptions = new ScriptParsingOptions { RetainFunctionSourceText = true };

        engine.Evaluate("function fn() { return 0; } fn.toString();", parsingOptions).AsString().Should().Be("function fn() { return 0; }");

        var prepared = Engine.PrepareScript(
            "function fn() { return 42; } fn.toString();",
            options: new ScriptPreparationOptions { ParsingOptions = parsingOptions });
        new Engine().Evaluate(prepared).AsString().Should().Be("function fn() { return 42; }");
    }

    [Fact]
    public void PreparedScriptDoesNotRetainSourceTextByDefault()
    {
        // A prepared script must not retain its source by default: toString() falls back to native code.
        var prepared = Engine.PrepareScript("function fn() { return 0; } fn.toString();");
        new Engine().Evaluate(prepared).AsString().Should().Be("function fn() { [native code] }");
    }

    [Fact]
    public void ClosureInParameterDefaultKeepsItsCapturedEnvironment()
    {
        // The environment-escape analysis must also scan parameter default expressions: a closure created
        // there captures the call's environment chain, so the environment must not be reused by the next
        // call. Previously only the function BODY was scanned, so the escaped closure observed the next
        // call's rebound values (g1() returned 2).
        var engine = new Engine();

        var result = engine.Evaluate("""
            function f(a, get = function() { return a; }) { return get; }
            var g1 = f(1);
            var g2 = f(2);
            [g1(), g2()].join(',');
            """);
        result.AsString().Should().Be("1,2");

        // Arrow variant + repeated calls so the reuse cache (had it been populated) would be exercised.
        result = engine.Evaluate("""
            const getters = [];
            function h(x, get = () => x) { getters.push(get); }
            for (var i = 0; i < 3; i++) { h(i); }
            getters.map(g => g()).join(',');
            """);
        result.AsString().Should().Be("0,1,2");
    }

    [Fact]
    public void EscapedArrowCapturingThisKeepsItsEnvironment()
    {
        // The slot-aware escape analysis must treat `this`/`new.target` inside a closure as environment
        // references: the call's FunctionEnvironment carries the this-binding, so reusing it for the next
        // call would rebind `this` under the escaped arrow (h1().name returned 'o2').
        var engine = new Engine();

        var result = engine.Evaluate("""
            function f(a) { var g = function() { return 1; }; var h = () => this; return h; }
            var h1 = f.call({ name: 'o1' }, 1);
            var h2 = f.call({ name: 'o2' }, 2);
            [h1().name, h2().name].join(',');
            """);
        result.AsString().Should().Be("o1,o2");

        // new.target variant: constructing must not rebind the escaped arrow's captured new.target.
        result = engine.Evaluate("""
            function F(a) { var g = function() { return 1; }; var h = () => new.target; return h; }
            var h1 = F(1);
            new F(2);
            '' + h1();
            """);
        result.AsString().Should().Be("undefined");
    }
}
