using Xunit;

namespace Jint.Tests.Runtime
{
    public class ModuleTests
    {
        private readonly Engine _engine;

        public ModuleTests()
        {
            _engine = new Engine();
        }

        [Fact]
        public void CanExportNamed()
        {
            _engine.DefineModule(@"export const value = 'exported value';", "my-module");
            var ns = _engine.ImportModule("my-module");

            Assert.Equal("exported value", ns.Get("value").AsString());
        }

        [Fact]
        public void CanExportDefault()
        {
            _engine.DefineModule(@"export default 'exported value';", "my-module");
            var ns = _engine.ImportModule("my-module");

            Assert.Equal("exported value", ns.Get("default").AsString());
        }

        [Fact]
        public void CanExportAll()
        {
            _engine.DefineModule(@"export const value = 'exported value';", "module1");
            _engine.DefineModule(@"export * from 'module 1';", "module2");
            var ns = _engine.ImportModule("module2");

            Assert.Equal("exported value", ns.Get("value").AsString());
        }
    }
}
