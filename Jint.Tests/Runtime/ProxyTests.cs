using Jint.Native;
using Jint.Native.Array;
using Jint.Native.Function;
using Jint.Runtime;
using Jint.Runtime.Interop;

namespace Jint.Tests.Runtime
{
    public class ProxyTests
    {
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
        public void ProxyToStringUseTarget()
        {
            var engine = new Engine().Execute(@"
                const targetWithToString = {toString: () => 'target'}
            ");
            Assert.Equal("target", engine.Evaluate("new Proxy(targetWithToString, {}).toString()").AsString());
            Assert.Equal("target", engine.Evaluate("`${new Proxy(targetWithToString, {})}`").AsString());
        }

        [Fact]
        public void ProxyToStringUseHandler()
        {
            var engine = new Engine().Execute(@"
                const handler = { get: (target, prop, receiver) => prop === 'toString' ? () => 'handler' : Reflect.get(target, prop, receiver) }
                const targetWithToString = {toString: () => 'target'}
            ");

            Assert.Equal("handler", engine.Evaluate("new Proxy({}, handler).toString()").AsString());
            Assert.Equal("handler", engine.Evaluate("new Proxy(targetWithToString, handler).toString()").AsString());
            Assert.Equal("handler", engine.Evaluate("`${new Proxy({}, handler)}`").AsString());
            Assert.Equal("handler", engine.Evaluate("`${new Proxy(targetWithToString, handler)}`").AsString());
        }
    }
}
