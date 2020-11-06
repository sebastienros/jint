using Jint.Tests.Runtime.Domain;
using System;
using Xunit;

namespace Jint.Tests.Runtime
{
    public class UuidTests : IDisposable
    {
        private readonly Engine _engine;

        public UuidTests()
        {
            _engine = new Engine(o => o.AddObjectConverter(new UuidConverter()))
                .SetValue("copy", new Func<Guid, Guid>(v => new Guid(v.ToByteArray())))
                ;
            UuidConstructor.CreateUuidConstructor(_engine);
        }

        void IDisposable.Dispose()
        {
        }

        private object RunTest(string source)
        {
            return _engine.Execute(source).GetCompletionValue().ToObject();
        }

        [Fact]
        public void Empty()
        {
            Assert.Equal(Guid.Empty, RunTest($"Uuid.parse('{Guid.Empty}')"));
            Assert.Equal(Guid.Empty, RunTest($"Uuid.Empty"));
        }

        [Fact]
        public void Random()
        {
            var actual = RunTest($"new Uuid()");
            Assert.NotEqual(Guid.Empty, actual);
            Assert.IsType<Guid>(actual);
        }

        [Fact]
        public void Copy()
        {
            var actual = (bool)RunTest($"const g = new Uuid(); copy(g).toString() === g.toString()");
            Assert.True(actual);
        }
    }
}
