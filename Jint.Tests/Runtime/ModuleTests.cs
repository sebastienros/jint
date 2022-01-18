using Jint.Runtime;
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
            _engine.DefineModule(@"export * from 'module1';", "module2");
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

        [Fact]
        public void CanImportRenamed()
        {
            _engine.DefineModule(@"export const value = 'exported value';", "imported-module");
            _engine.DefineModule(@"import { value as renamed } from 'imported-module'; export const exported = renamed;", "my-module");
            var ns = _engine.ImportModule("my-module");

            Assert.Equal("exported value", ns.Get("exported").AsString());
        }

        [Fact]
        public void CanImportDefault()
        {
            _engine.DefineModule(@"export default 'exported value';", "imported-module");
            _engine.DefineModule(@"import imported from 'imported-module'; export const exported = imported;", "my-module");
            var ns = _engine.ImportModule("my-module");

            Assert.Equal("exported value", ns.Get("exported").AsString());
        }

        [Fact]
        public void CanImportAll()
        {
            _engine.DefineModule(@"export const value = 'exported value';", "imported-module");
            _engine.DefineModule(@"import * as imported from 'imported-module'; export const exported = imported.value;", "my-module");
            var ns = _engine.ImportModule("my-module");

            Assert.Equal("exported value", ns.Get("exported").AsString());
        }

        [Fact]
        public void CanThrow()
        {
            _engine.DefineModule(@"throw new Error('imported successfully');", "my-module");

            var exc = Assert.Throws<JavaScriptException>(() => _engine.ImportModule("my-module"));
            Assert.Equal("imported successfully", exc.Message);
            Assert.Equal("my-module", exc.Location.Source);
        }

        [Fact]
        public void CanPropagateThrowThroughImport()
        {
            _engine.DefineModule(@"throw new Error('imported successfully');", "imported-module");
            _engine.DefineModule(@"import 'imported-module';", "my-module");

            var exc = Assert.Throws<JavaScriptException>(() => _engine.ImportModule("my-module"));
            Assert.Equal("imported successfully", exc.Message);
            Assert.Equal("imported-module", exc.Location.Source);
        }
    }
}
