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
        public void CanExportNamedListRenamed()
        {
            _engine.DefineModule(@"const value1 = 1; const value2 = 2; export { value1 as renamed1, value2 as renamed2 }", "my-module");
            var ns = _engine.ImportModule("my-module");

            Assert.Equal(1, ns.Get("renamed1").AsInteger());
            Assert.Equal(2, ns.Get("renamed2").AsInteger());
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

        [Fact]
        public void CanImportNamed()
        {
            _engine.DefineModule(@"export const value = 'exported value';", "imported-module");
            _engine.DefineModule(@"import { value } from 'imported-module'; export const exported = value;", "my-module");
            var ns = _engine.ImportModule("my-module");

            Assert.Equal("exported value", ns.Get("exported").AsString());
        }
    }
}
