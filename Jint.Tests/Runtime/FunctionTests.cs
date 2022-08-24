using Jint.Native;
using Jint.Native.Array;
using Jint.Native.Function;
using Jint.Runtime;
using Jint.Runtime.Interop;

namespace Jint.Tests.Runtime
{
    public class FunctionTests
    {
        [Fact]
        public void BindCombinesBoundArgumentsToCallArgumentsCorrectly()
        {
            var e = new Engine();
            e.Evaluate("var testFunc = function (a, b, c) { return a + ', ' + b + ', ' + c + ', ' + JSON.stringify(arguments); }");

            Assert.Equal("a, 1, a, {\"0\":\"a\",\"1\":1,\"2\":\"a\"}", e.Evaluate("testFunc('a', 1, 'a');").AsString());
            Assert.Equal("a, 1, a, {\"0\":\"a\",\"1\":1,\"2\":\"a\"}", e.Evaluate("testFunc.bind('anything')('a', 1, 'a');").AsString());
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
            const string script = @"
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
            engine.Execute(script);

            var obj = engine.Evaluate("var obj = {}; execute(obj); return obj;").AsObject();

            Assert.Equal("ayende", obj.Get("Name").AsString());
        }

        [Fact]
        public void ObjectCoercibleForCallable()
        {
            const string script = @"
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
                .SetValue("testFunction", new ClrFunctionInstance(engine, "testFunction", (thisValue, args) =>
                {
                    return engine.Invoke(thisValue, "then", new[] { Undefined.Instance, args.At(0) });
                }))
                .SetValue("assertEqual", new Action<object, object>((a, b) => Assert.Equal(b, a)))
                .Execute(script);
        }

        [Fact]
        public void AnonymousLambdaShouldHaveNameDefined()
        {
            var engine = new Engine();
            Assert.True(engine.Evaluate("(()=>{}).hasOwnProperty('name')").AsBoolean());
        }

        [Fact]
        public void CanInvokeConstructorsFromEngine()
        {
            var engine = new Engine();

            engine.Evaluate("class TestClass { constructor(a, b) { this.a = a; this.b = b; }}");
            engine.Evaluate("function TestFunction(a, b) { this.a = a; this.b = b; }");

            var instanceFromClass = engine.Construct("TestClass", "abc", 123).AsObject();
            Assert.Equal("abc", instanceFromClass.Get("a"));
            Assert.Equal(123, instanceFromClass.Get("b"));

            var instanceFromFunction = engine.Construct("TestFunction", "abc", 123).AsObject();
            Assert.Equal("abc", instanceFromFunction.Get("a"));
            Assert.Equal(123, instanceFromFunction.Get("b"));

            var arrayInstance = (ArrayInstance) engine.Construct("Array", "abc", 123).AsObject();
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

            ev(null, new JsValue[0]);
            Assert.Equal(10, engine.Evaluate("a"));

            ev(null, new JsValue[] { 20 });
            Assert.Equal(30, engine.Evaluate("a"));
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

            ev(null, new JsValue[0]);
            Assert.Equal(10, engine.Evaluate("a"));

            ev(null, new JsValue[] { 20 });
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

            Assert.Equal(true, ev(JsValue.Undefined, new JsValue[] { "test" }));
            Assert.Equal(true, ev(JsValue.Undefined, new JsValue[] { 5 }));
            Assert.Equal(false, ev(JsValue.Undefined, new JsValue[] { false }));
            Assert.Equal(false, ev(JsValue.Undefined, new JsValue[] { 0}));
            Assert.Equal(false, ev(JsValue.Undefined, new JsValue[] { JsValue.Undefined }));
        }

        [Fact]
        public void FunctionsShouldResolveToSameReference()
        {
            var engine = new Engine();
            engine.SetValue("equal", new Action<object, object>(Assert.Equal));
            engine.Execute(@"
                function testFn() {}
                equal(testFn, testFn);
            ");
        }
        
        [Fact]
        public void CanInvokeCallForFunctionInstance()
        {
            var engine = new Engine();

            engine.Evaluate(@"
                (function () {
                    function foo(a = 123) { return a; }
                    foo()
                })")
                .As<FunctionInstance>().Call();

            var result = engine.Evaluate(@"
                (function () {
                    class Foo { test() { return 123 } }
                    let f = new Foo()
                    return f.test()
                })")
                .As<FunctionInstance>().Call();

            Assert.True(result.IsInteger());
            Assert.Equal(123, result.AsInteger());
        }

        [Fact]
        public void CanInvokeFunctionViaEngineInstance()
        {
            var engine = new Engine();

            var function = engine.Evaluate("function bar(a) { return a; }; bar;");

            Assert.Equal(123, engine.Call(function, 123));
            Assert.Equal(123, engine.Call("bar", 123));
        }

        [Fact]
        public void CanInvokeFunctionViaEngineInstanceWithCustomThisObj()
        {
            var engine = new Engine();

            var function = engine.Evaluate("function baz() { return this; }; baz;");

            Assert.Equal("I'm this!", TypeConverter.ToString(engine.Call(function, "I'm this!", Arguments.Empty)));
            Assert.Equal("I'm this!", TypeConverter.ToString(function.Call("I'm this!", Arguments.Empty)));
        }
    }
}
