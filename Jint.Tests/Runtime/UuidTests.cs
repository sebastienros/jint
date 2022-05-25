using Jint.Tests.Runtime.Domain;
using System;
using Jint.Runtime.Interop;
using Xunit;

namespace Jint.Tests.Runtime
{
    public class UuidTests : IDisposable
    {
        private readonly Engine _engine;

        public UuidTests()
        {;
            _engine = new Engine(options => options.AllowOperatorOverloading());
            _engine.SetValue("copy", new Func<Guid, Guid>(v => new Guid(v.ToByteArray())));
            _engine.SetValue("Uuid", TypeReference.CreateTypeReference<JsUuid>(_engine));
        }

        void IDisposable.Dispose()
        {
        }

        private object RunTest(string source)
        {
            return _engine.Evaluate(source).ToObject();
        }

        [Fact]
        public void Empty()
        {
            Assert.Equal(JsUuid.Empty, RunTest($"Uuid.parse('{Guid.Empty}')"));
            Assert.Equal(JsUuid.Empty, RunTest($"Uuid.Empty"));
        }

        [Fact]
        public void Random()
        {
            var actual = RunTest($"new Uuid()");
            var jsUuid = Assert.IsType<JsUuid>(actual);
            Assert.Equal(JsUuid.Empty, jsUuid);
        }

        [Fact]
        public void Copy()
        {
            _engine.Evaluate("const g = new Uuid();");
            Assert.Equal(_engine.Evaluate("copy(g).toString()").AsString(), _engine.Evaluate("g.toString()").AsString());
        }
    }
}
