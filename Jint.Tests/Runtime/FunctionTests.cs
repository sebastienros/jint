using System;
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
    }
}