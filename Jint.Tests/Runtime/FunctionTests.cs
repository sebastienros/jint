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
            .SetValue("equal", new Action<object, object>(Assert.Equal));
    }

    [Fact]
    public void ApplyRejectsArrayLikeArgumentsThatCannotBeMaterialized()
    {
        var engine = new Engine();
        var length = ClrLimits.MaxArrayLength + 1UL;

        var exception = Assert.Throws<JavaScriptException>(
            () => engine.Evaluate($"(function(){{}}).apply(null, {{ length: {length} }});"));

        Assert.True(exception.Error.InstanceofOperator(engine.Intrinsics.RangeError));
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

        Assert.True(((JsBoolean) engine.Evaluate(script))._value);
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

        Assert.True(((JsBoolean) engine.Evaluate(script))._value);
    }

    [Fact]
    public void BindCombinesBoundArgumentsToCallArgumentsCorrectly()
    {
        _engine.Evaluate("var testFunc = function (a, b, c) { return a + ', ' + b + ', ' + c + ', ' + JSON.stringify(arguments); }");

        Assert.Equal("a, 1, a, {\"0\":\"a\",\"1\":1,\"2\":\"a\"}", _engine.Evaluate("testFunc('a', 1, 'a');").AsString());
        Assert.Equal("a, 1, a, {\"0\":\"a\",\"1\":1,\"2\":\"a\"}", _engine.Evaluate("testFunc.bind('anything')('a', 1, 'a');").AsString());
    }

    [Fact]
    public void ArrowFunctionShouldBeExtensible()
    {
        new Engine()
            .SetValue("assert", new Action<bool>(Assert.True))
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

        Assert.Equal("ayende", obj.Get("Name").AsString());
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
            .SetValue("assertEqual", new Action<object, object>((a, b) => Assert.Equal(b, a)))
            .Execute(Script);
    }

    [Fact]
    public void AnonymousLambdaShouldHaveNameDefined()
    {
        Assert.True(_engine.Evaluate("(()=>{}).hasOwnProperty('name')").AsBoolean());
    }

    [Fact]
    public void CanInvokeConstructorsFromEngine()
    {
        _engine.Evaluate("class TestClass { constructor(a, b) { this.a = a; this.b = b; }}");
        _engine.Evaluate("function TestFunction(a, b) { this.a = a; this.b = b; }");

        var instanceFromClass = _engine.Construct("TestClass", "abc", 123).AsObject();
        Assert.Equal("abc", instanceFromClass.Get("a"));
        Assert.Equal(123, instanceFromClass.Get("b"));

        var instanceFromFunction = _engine.Construct("TestFunction", "abc", 123).AsObject();
        Assert.Equal("abc", instanceFromFunction.Get("a"));
        Assert.Equal(123, instanceFromFunction.Get("b"));

        var arrayInstance = (JsArray) _engine.Construct("Array", "abc", 123).AsObject();
        Assert.Equal((uint) 2, arrayInstance.Length);
        Assert.Equal("abc", arrayInstance[0]);
        Assert.Equal(123, arrayInstance[1]);
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

        Assert.Equal(5, engine.Evaluate("a"));

        ev(null, []);
        Assert.Equal(10, engine.Evaluate("a"));

        ev(null, [20]);
        Assert.Equal(30, engine.Evaluate("a"));
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
        Assert.Equal(42, result.AsInteger());
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

        Assert.Equal(5, engine.Evaluate("a"));

        ev(null, []);
        Assert.Equal(10, engine.Evaluate("a"));

        ev(null, [20]);
        Assert.Equal(30, engine.Evaluate("a"));
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

        Assert.Equal(true, ev(JsValue.Undefined, ["test"]));
        Assert.Equal(true, ev(JsValue.Undefined, [5]));
        Assert.Equal(false, ev(JsValue.Undefined, [false]));
        Assert.Equal(false, ev(JsValue.Undefined, [0]));
        Assert.Equal(false, ev(JsValue.Undefined, [JsValue.Undefined]));
    }

    [Fact]
    public void FunctionsShouldResolveToSameReference()
    {
        _engine.SetValue("equal", new Action<object, object>(Assert.Equal));
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

        Assert.True(result.IsInteger());
        Assert.Equal(123, result.AsInteger());
    }

    [Fact]
    public void CanInvokeCallForFunctionInstanceWithDifferingArgCounts()
    {
        _engine.Execute("function foo() { return Array.from(arguments).join(','); }");
        var function = _engine.GetValue("foo");
        Assert.Equal("", function.Call());
        Assert.Equal("1", function.Call(1));
        Assert.Equal("1,2", function.Call(1, 2));
        Assert.Equal("1,2,3", function.Call(1, 2, 3));
        Assert.Equal("1,2,3,4", function.Call(1, 2, 3, 4));
    }

    [Fact]
    public void CanInvokeFunctionViaEngineInstance()
    {
        var function = _engine.Evaluate("function bar(a) { return a; }; bar;");

        Assert.Equal(123, _engine.Call(function, 123));
        Assert.Equal(123, _engine.Call("bar", 123));
    }

    [Fact]
    public void CanInvokeFunctionViaEngineInstanceWithCustomThisObj()
    {
        var function = _engine.Evaluate("function baz() { return this; }; baz;");

        Assert.Equal("I'm this!", TypeConverter.ToString(_engine.Call(function, "I'm this!", Arguments.Empty)));
        Assert.Equal("I'm this!", TypeConverter.ToString(function.Call("I'm this!", Arguments.Empty)));
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

        Assert.Equal(values[0], found1);
        Assert.Equal(values[1], found2);
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
    public void ToStringShouldProduceStandardsCompliantResultByDefault(string code, string expectedResult)
    {
        code = Regex.Replace(code, @"\r\n?", "\n", RegexOptions.CultureInvariant); // normalize line endings to '\n'
        expectedResult = Regex.Replace(expectedResult, @"\r\n?", "\n", RegexOptions.CultureInvariant); // normalize line endings to '\n'

        var returnValue = new Engine().Evaluate(code);

        var actualResults = Assert.IsType<JsArray>(returnValue);
        Assert.Equal(2u, actualResults.Length);

        var actualResult1 = Assert.IsType<JsString>(actualResults[0]).ToString();
        Assert.Equal(expectedResult, actualResult1);

        var actualResult2 = Assert.IsType<JsString>(actualResults[1]).ToString();
        Assert.Same(actualResult1, actualResult2);
    }
}
