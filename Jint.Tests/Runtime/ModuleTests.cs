using System;
using System.IO;
using System.Reflection;
using Jint.Native;
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
        public void ShouldExportNamed()
        {
            _engine.DefineModule(@"export const value = 'exported value';", "my-module");
            var ns = _engine.ImportModule("my-module");

            Assert.Equal("exported value", ns.Get("value").AsString());
        }

        [Fact]
        public void ShouldExportNamedListRenamed()
        {
            _engine.DefineModule(@"const value1 = 1; const value2 = 2; export { value1 as renamed1, value2 as renamed2 }", "my-module");
            var ns = _engine.ImportModule("my-module");

            Assert.Equal(1, ns.Get("renamed1").AsInteger());
            Assert.Equal(2, ns.Get("renamed2").AsInteger());
        }

        [Fact]
        public void ShouldExportDefault()
        {
            _engine.DefineModule(@"export default 'exported value';", "my-module");
            var ns = _engine.ImportModule("my-module");

            Assert.Equal("exported value", ns.Get("default").AsString());
        }

        [Fact]
        public void ShouldExportAll()
        {
            _engine.DefineModule(@"export const value = 'exported value';", "module1");
            _engine.DefineModule(@"export * from 'module1';", "module2");
            var ns = _engine.ImportModule("module2");

            Assert.Equal("exported value", ns.Get("value").AsString());
        }

        [Fact]
        public void ShouldImportNamed()
        {
            _engine.DefineModule(@"export const value = 'exported value';", "imported-module");
            _engine.DefineModule(@"import { value } from 'imported-module'; export const exported = value;", "my-module");
            var ns = _engine.ImportModule("my-module");

            Assert.Equal("exported value", ns.Get("exported").AsString());
        }

        [Fact]
        public void ShouldImportRenamed()
        {
            _engine.DefineModule(@"export const value = 'exported value';", "imported-module");
            _engine.DefineModule(@"import { value as renamed } from 'imported-module'; export const exported = renamed;", "my-module");
            var ns = _engine.ImportModule("my-module");

            Assert.Equal("exported value", ns.Get("exported").AsString());
        }

        [Fact]
        public void ShouldImportDefault()
        {
            _engine.DefineModule(@"export default 'exported value';", "imported-module");
            _engine.DefineModule(@"import imported from 'imported-module'; export const exported = imported;", "my-module");
            var ns = _engine.ImportModule("my-module");

            Assert.Equal("exported value", ns.Get("exported").AsString());
        }

        [Fact]
        public void ShouldImportAll()
        {
            _engine.DefineModule(@"export const value = 'exported value';", "imported-module");
            _engine.DefineModule(@"import * as imported from 'imported-module'; export const exported = imported.value;", "my-module");
            var ns = _engine.ImportModule("my-module");

            Assert.Equal("exported value", ns.Get("exported").AsString());
        }

        [Fact]
        public void ShouldPropagateThrowStatementOnCSharpImport()
        {
            _engine.DefineModule(@"throw new Error('imported successfully');", "my-module");

            var exc = Assert.Throws<JavaScriptException>(() => _engine.ImportModule("my-module"));
            Assert.Equal("imported successfully", exc.Message);
            Assert.Equal("my-module", exc.Location.Source);
        }

        [Fact]
        public void ShouldPropagateThrowStatementThroughJavaScriptImport()
        {
            _engine.DefineModule(@"throw new Error('imported successfully');", "imported-module");
            _engine.DefineModule(@"import 'imported-module';", "my-module");

            var exc = Assert.Throws<JavaScriptException>(() => _engine.ImportModule("my-module"));
            Assert.Equal("imported successfully", exc.Message);
            Assert.Equal("imported-module", exc.Location.Source);
        }

        [Fact]
        public void ShouldDefineModuleFromJsValue()
        {
            _engine.DefineModule("value", JsString.Create("hello world"), "my-module");
            var ns = _engine.ImportModule("my-module");

            Assert.Equal("hello world", ns.Get("value").AsString());
        }

        [Fact]
        public void ShouldDefineModuleFromClrInstance()
        {
            _engine.DefineModule("value", new ImportedClass { Value = "instance value" }, "imported-module");
            _engine.DefineModule(@"import { value } from 'imported-module'; export const exported = value.value;", "my-module");
            var ns = _engine.ImportModule("my-module");

            Assert.Equal("instance value", ns.Get("exported").AsString());
        }

        [Fact]
        public void ShouldDefineModuleFromClrType()
        {
            _engine.DefineModule<ImportedClass>("imported-module");
            _engine.DefineModule(@"import { ImportedClass } from 'imported-module'; export const exported = new ImportedClass().value;", "my-module");
            var ns = _engine.ImportModule("my-module");

            Assert.Equal("hello world", ns.Get("exported").AsString());
        }

        [Fact]
        public void ShouldAllowExportMultipleImports()
        {
            _engine.DefineModule("value1", JsNumber.Create(1), "@mine/import1");
            _engine.DefineModule("value2", JsNumber.Create(2), "@mine/import2");
            _engine.DefineModule("export * from '@mine/import1'; export * from '@mine/import2'", "@mine");
            _engine.DefineModule(@"import { value1, value2 } from '@mine'; export const result = `${value1} ${value2}`", "app");
            var ns = _engine.ImportModule("app");

            Assert.Equal("1 2", ns.Get("result").AsString());
        }

        private class ImportedClass
        {
            public string Value { get; set; } = "hello world";
        }

#if(NET6_0_OR_GREATER)

        [Fact]
        public void CanLoadModuleImportsFromFiles()
        {
            var engine = new Engine(options => options.EnableModules(GetBasePath()));
            engine.DefineModule("import { User } from './modules/user'; export const user = new User('John', 'Doe');", "my-module");
            var ns = engine.ImportModule("my-module");

            Assert.Equal("John Doe", ns["user"].AsObject()["name"].AsString());
        }

        [Fact]
        public void CanImportFromFile()
        {
            var engine = new Engine(options => options.EnableModules(GetBasePath()));
            var ns = engine.ImportModule("./modules/format-name");

            Assert.Equal("John Doe", ns["formatName"].AsFunctionInstance().Call(JsString.Create("John"), JsString.Create("Doe")).AsString());
        }

        private static string GetBasePath()
        {
            var assemblyPath = new Uri(typeof(ModuleTests).GetTypeInfo().Assembly.Location).LocalPath;
            var assemblyDirectory = new FileInfo(assemblyPath).Directory;
            var basePath = Path.Combine(assemblyDirectory.Parent.Parent.Parent.FullName, "Runtime", "Scripts");
            return basePath;
        }

#endif
    }
}
