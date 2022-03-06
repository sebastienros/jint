#if(NET6_0_OR_GREATER)
using System.IO;
using System.Reflection;
using Jint.Runtime.Modules;
#endif
using System;
using Esprima;
using Jint.Native;
using Jint.Runtime;
using Xunit;

namespace Jint.Tests.Runtime;

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
    public void ShouldPropagateParseError()
    {
        _engine.AddModule("imported", @"export const invalid;");
        _engine.AddModule("my-module", @"import { invalid } from 'imported';");

        var exc = Assert.Throws<JavaScriptException>(() => _engine.ImportModule("my-module"));
        Assert.Equal("Error while loading module: error in module 'imported': Line 1: Missing initializer in const declaration", exc.Message);
    }

    [Fact]
    public void ShouldPropagateLinkError()
    {
        _engine.AddModule("imported", @"export invalid;");
        _engine.AddModule("my-module", @"import { value } from 'imported';");

        var exc = Assert.Throws<JavaScriptException>(() => _engine.ImportModule("my-module"));
        Assert.Equal("Error while loading module: error in module 'imported': Line 1: Unexpected identifier", exc.Message);
        Assert.Equal("my-module", exc.Location.Source);
    }

    [Fact]
    public void ShouldPropagateExecuteError()
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

    /* ECMAScript 2020 "export * as ns from"
    [Fact]
    public void ShouldAllowNamedStarExport()
    {
        _engine.AddModule("imported-module", builder => builder.ExportValue("value1", 5));
        _engine.AddModule("my-module", "export * as ns from 'imported-module';");
        var ns = _engine.ImportModule("my-module");

        Assert.Equal(5, ns.Get("ns").Get("value1").AsNumber());
    }
    */

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

    [Fact(Skip = "TODO re-enable in module fix branch")]
    public void ShouldImportOnlyOnce()
    {
        var called = 0;
        _engine.AddModule("imported-module", builder => builder.ExportFunction("count", args => called++));
        _engine.AddModule("my-module", @"import { count } from 'imported-module'; count();");
        _engine.ImportModule("my-module");
        _engine.ImportModule("my-module");

        Assert.Equal(1, called);
    }

    [Fact]
    public void ShouldAllowSelfImport()
    {
        _engine.AddModule("my-globals", @"export const globals = { counter: 0 };");
        _engine.AddModule("my-module", @"
import { globals } from 'my-globals';
export const count = ++globals.counter;
");
        var ns= _engine.ImportModule("my-module");

        Assert.Equal(1, ns.Get("count").AsInteger());
    }

    [Fact]
    public void ShouldAllowCyclicImport()
    {
        _engine.AddModule("module2", @"import { x1 } from 'module1'; export const x2 = 2;");
        _engine.AddModule("module1", @"import { x2 } from 'module2'; export const x1 = 1;");

        var ns1 = _engine.ImportModule("module1");
        var ns2 = _engine.ImportModule("module2");

        Assert.Equal(1, ns1.Get("x1").AsInteger());
        Assert.Equal(2, ns2.Get("x2").AsInteger());
    }

#if(NET6_0_OR_GREATER)

    [Fact(Skip = "TODO re-enable in module fix branch")]
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
