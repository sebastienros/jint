using System;
using Jint.Native;
using Jint.Runtime;
using Jint.Runtime.Interop;
using Xunit;

namespace Jint.Tests.Runtime
{
    public class FunctionTests
    {
        [Fact]
        public void BindCombinesBoundArgumentsToCallArgumentsCorrectly()
        {
            var e = new Engine();
            e.Execute("var testFunc = function (a, b, c) { return a + ', ' + b + ', ' + c + ', ' + JSON.stringify(arguments); }");

            Assert.Equal("a, 1, a, {\"0\":\"a\",\"1\":1,\"2\":\"a\"}", e.Execute("testFunc('a', 1, 'a');").GetCompletionValue().AsString());
            Assert.Equal("a, 1, a, {\"0\":\"a\",\"1\":1,\"2\":\"a\"}", e.Execute("testFunc.bind('anything')('a', 1, 'a');").GetCompletionValue().AsString());
        }

        [Fact]
        public void ProxyCanBeRevokedWithoutContext()
        {
            new Engine()
                .Execute(@"
                    var revocable = Proxy.revocable({}, {});
                    var revoke = revocable.revoke;
                    revoke.call(null);
                ");
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

            var obj = engine.Execute("var obj = {}; execute(obj); return obj;").GetCompletionValue().AsObject();

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
    }
}