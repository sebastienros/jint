#if(NET6_0_OR_GREATER)
using System.IO;
using System.Reflection;
#endif
using System;
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
            _engine.AddModule("my-module", @"export const value = 'exported value';");
            var ns = _engine.ImportModule("my-module");

            Assert.Equal("exported value", ns.Get("value").AsString());
        }

        [Fact]
        public void ShouldExportNamedListRenamed()
        {
            _engine.AddModule("my-module", @"const value1 = 1; const value2 = 2; export { value1 as renamed1, value2 as renamed2 }");
            var ns = _engine.ImportModule("my-module");

            Assert.Equal(1, ns.Get("renamed1").AsInteger());
            Assert.Equal(2, ns.Get("renamed2").AsInteger());
        }

        [Fact]
        public void ShouldExportDefault()
        {
            _engine.AddModule("my-module", @"export default 'exported value';");
            var ns = _engine.ImportModule("my-module");

            Assert.Equal("exported value", ns.Get("default").AsString());
        }

        [Fact]
        public void ShouldExportAll()
        {
            _engine.AddModule("module1", @"export const value = 'exported value';");
            _engine.AddModule("module2", @"export * from 'module1';");
            var ns = _engine.ImportModule("module2");

            Assert.Equal("exported value", ns.Get("value").AsString());
        }

        [Fact]
        public void ShouldImportNamed()
        {
            _engine.AddModule("imported-module", @"export const value = 'exported value';");
            _engine.AddModule("my-module", @"import { value } from 'imported-module'; export const exported = value;");
            var ns = _engine.ImportModule("my-module");

            Assert.Equal("exported value", ns.Get("exported").AsString());
        }

        [Fact]
        public void ShouldImportRenamed()
        {
            _engine.AddModule("imported-module", @"export const value = 'exported value';");
            _engine.AddModule("my-module", @"import { value as renamed } from 'imported-module'; export const exported = renamed;");
            var ns = _engine.ImportModule("my-module");

            Assert.Equal("exported value", ns.Get("exported").AsString());
        }

        [Fact]
        public void ShouldImportDefault()
        {
            _engine.AddModule("imported-module", @"export default 'exported value';");
            _engine.AddModule("my-module", @"import imported from 'imported-module'; export const exported = imported;");
            var ns = _engine.ImportModule("my-module");

            Assert.Equal("exported value", ns.Get("exported").AsString());
        }

        [Fact]
        public void ShouldImportAll()
        {
            _engine.AddModule("imported-module", @"export const value = 'exported value';");
            _engine.AddModule("my-module", @"import * as imported from 'imported-module'; export const exported = imported.value;");
            var ns = _engine.ImportModule("my-module");

            Assert.Equal("exported value", ns.Get("exported").AsString());
        }

        [Fact]
        public void ShouldPropagateThrowStatementOnCSharpImport()
        {
            _engine.AddModule("my-module", @"throw new Error('imported successfully');");

            var exc = Assert.Throws<JavaScriptException>(() => _engine.ImportModule("my-module"));
            Assert.Equal("imported successfully", exc.Message);
            Assert.Equal("my-module", exc.Location.Source);
        }

        [Fact]
        public void ShouldPropagateThrowStatementThroughJavaScriptImport()
        {
            _engine.AddModule("imported-module", @"throw new Error('imported successfully');");
            _engine.AddModule("my-module", @"import 'imported-module';");

            var exc = Assert.Throws<JavaScriptException>(() => _engine.ImportModule("my-module"));
            Assert.Equal("imported successfully", exc.Message);
            Assert.Equal("imported-module", exc.Location.Source);
        }

        [Fact]
        public void ShouldAddModuleFromJsValue()
        {
            _engine.AddModule("my-module", builder => builder.ExportValue("value", JsString.Create("hello world")));
            var ns = _engine.ImportModule("my-module");

            Assert.Equal("hello world", ns.Get("value").AsString());
        }

        [Fact]
        public void ShouldAddModuleFromClrInstance()
        {
            _engine.AddModule("imported-module", builder => builder.ExportObject("value", new ImportedClass { Value = "instance value" }));
            _engine.AddModule("my-module", @"import { value } from 'imported-module'; export const exported = value.value;");
            var ns = _engine.ImportModule("my-module");

            Assert.Equal("instance value", ns.Get("exported").AsString());
        }

        [Fact]
        public void ShouldAllowInvokeUserDefinedClass()
        {
            _engine.AddModule("user", "export class UserDefined { constructor(v) { this._v = v; } hello(c) { return `hello ${this._v}${c}`; } }");
            var ctor = _engine.ImportModule("user").Get("UserDefined");
            var instance = _engine.Construct(ctor, JsString.Create("world"));
            var result = instance.GetMethod("hello").Call(instance, JsString.Create("!"));

            Assert.Equal("hello world!", result);
        }

        [Fact]
        public void ShouldAddModuleFromClrType()
        {
            _engine.AddModule("imported-module", builder => builder.ExportType<ImportedClass>());
            _engine.AddModule("my-module", @"import { ImportedClass } from 'imported-module'; export const exported = new ImportedClass().value;");
            var ns = _engine.ImportModule("my-module");

            Assert.Equal("hello world", ns.Get("exported").AsString());
        }

        private class ImportedClass
        {
            public string Value { get; set; } = "hello world";
        }

        [Fact]
        public void ShouldAllowExportMultipleImports()
        {
            _engine.AddModule("@mine/import1", builder => builder.ExportValue("value1", JsNumber.Create(1)));
            _engine.AddModule("@mine/import2", builder => builder.ExportValue("value2", JsNumber.Create(2)));
            _engine.AddModule("@mine", "export * from '@mine/import1'; export * from '@mine/import2'");
            _engine.AddModule("app", @"import { value1, value2 } from '@mine'; export const result = `${value1} ${value2}`");
            var ns = _engine.ImportModule("app");

            Assert.Equal("1 2", ns.Get("result").AsString());
        }

        [Fact]
        public void ShouldAllowChaining()
        {
            _engine.AddModule("dependent-module", "export const dependency = 1;");
            _engine.AddModule("my-module", builder => builder
                .AddSource("import { dependency } from 'dependent-module';")
                .AddSource("export const output = dependency + 1;")
                .ExportValue("num", JsNumber.Create(-1))
            );
            var ns = _engine.ImportModule("my-module");

            Assert.Equal(2, ns.Get("output").AsInteger());
            Assert.Equal(-1, ns.Get("num").AsInteger());
        }

        [Fact]
        public void ShouldAllowLoadingMoreThanOnce()
        {
            var called = 0;
            _engine.AddModule("imported-module", builder => builder.ExportFunction("count", args => called++));
            _engine.AddModule("my-module", @"import { count } from 'imported-module'; count();");
            _engine.ImportModule("my-module");
            _engine.ImportModule("my-module");

            Assert.Equal(called, 1);
        }

#if(NET6_0_OR_GREATER)

        [Fact]
        public void CanLoadModuleImportsFromFiles()
        {
            var engine = new Engine(options => options.EnableModules(GetBasePath()));
            engine.AddModule("my-module", "import { User } from './modules/user.js'; export const user = new User('John', 'Doe');");
            var ns = engine.ImportModule("my-module");

            Assert.Equal("John Doe", ns["user"].Get("name").AsString());
        }

        [Fact]
        public void CanImportFromFile()
        {
            var engine = new Engine(options => options.EnableModules(GetBasePath()));
            var ns = engine.ImportModule("./modules/format-name.js");
            var result = engine.Invoke(ns.Get("formatName"), "John", "Doe").AsString();

            Assert.Equal("John Doe", result);
        }

        private static string GetBasePath()
        {
            var assemblyPath = new Uri(typeof(ModuleTests).GetTypeInfo().Assembly.Location).LocalPath;
            var assemblyDirectory = new FileInfo(assemblyPath).Directory;
            return Path.Combine(
                assemblyDirectory?.Parent?.Parent?.Parent?.FullName ?? throw new NullReferenceException("Could not find tests base path"),
                "Runtime",
                "Scripts");
        }

#endif
    }
}
