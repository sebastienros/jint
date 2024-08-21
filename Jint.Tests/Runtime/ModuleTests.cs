using Jint.Native;
using Jint.Runtime;
using Jint.Runtime.Modules;

using Module = Jint.Runtime.Modules.Module;

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
        _engine.Modules.Add("my-module", "export const value = 'exported value';");
        var ns = _engine.Modules.Import("my-module");

        Assert.Equal("exported value", ns.Get("value").AsString());
    }

    [Fact]
    public void ShouldExportNamedListRenamed()
    {
        _engine.Modules.Add("my-module", "const value1 = 1; const value2 = 2; export { value1 as renamed1, value2 as renamed2 }");
        var ns =  _engine.Modules.Import("my-module");

        Assert.Equal(1, ns.Get("renamed1").AsInteger());
        Assert.Equal(2, ns.Get("renamed2").AsInteger());
    }

    [Fact]
    public void ShouldExportDefault()
    {
        _engine.Modules.Add("my-module", "export default 'exported value';");
        var ns =  _engine.Modules.Import("my-module");

        Assert.Equal("exported value", ns.Get("default").AsString());
    }

    [Fact]
    public void ShouldExportDefaultFunctionWithoutName()
    {
        _engine.Modules.Add("module1", "export default function main() { return 1; }");
        _engine.Modules.Add("module2", "export default function () { return 1; }");
        var ns = _engine.Modules.Import("module1");

        var func = ns.Get("default");
        Assert.Equal(1, func.Call());

        ns = _engine.Modules.Import("module2");

        func = ns.Get("default");
        Assert.Equal(1, func.Call());
    }

    [Fact]
    public void ShouldExportAll()
    {
        _engine.Modules.Add("module1", "export const value = 'exported value';");
        _engine.Modules.Add("module2", "export * from 'module1';");
        var ns =  _engine.Modules.Import("module2");

        Assert.Equal("exported value", ns.Get("value").AsString());
    }

    [Fact]
    public void ShouldImportNamed()
    {
        _engine.Modules.Add("imported-module", "export const value = 'exported value';");
        _engine.Modules.Add("my-module", "import { value } from 'imported-module'; export const exported = value;");
        var ns =  _engine.Modules.Import("my-module");

        Assert.Equal("exported value", ns.Get("exported").AsString());
    }

    [Fact]
    public void ShouldImportRenamed()
    {
        _engine.Modules.Add("imported-module", "export const value = 'exported value';");
        _engine.Modules.Add("my-module", "import { value as renamed } from 'imported-module'; export const exported = renamed;");
        var ns =  _engine.Modules.Import("my-module");

        Assert.Equal("exported value", ns.Get("exported").AsString());
    }

    [Fact]
    public void ShouldImportDefault()
    {
        _engine.Modules.Add("imported-module", "export default 'exported value';");
        _engine.Modules.Add("my-module", "import imported from 'imported-module'; export const exported = imported;");
        var ns =  _engine.Modules.Import("my-module");

        Assert.Equal("exported value", ns.Get("exported").AsString());
    }

    [Fact]
    public void ShouldImportAll()
    {
        _engine.Modules.Add("imported-module", "export const value = 'exported value';");
        _engine.Modules.Add("my-module", "import * as imported from 'imported-module'; export const exported = imported.value;");
        var ns =  _engine.Modules.Import("my-module");

        Assert.Equal("exported value", ns.Get("exported").AsString());
    }

    [Fact]
    public void ShouldImportDynamically()
    {
        var received = false;
        _engine.Modules.Add("imported-module", builder => builder.ExportFunction("signal", () => received = true));
        _engine.Modules.Add("my-module", "import('imported-module').then(ns => { ns.signal(); });");

         _engine.Modules.Import("my-module");

        Assert.True(received);
    }

    [Fact]
    public void ShouldPropagateParseError()
    {
        _engine.Modules.Add("imported", "export const invalid;");
        _engine.Modules.Add("my-module", "import { invalid } from 'imported';");

        var exc = Assert.Throws<JavaScriptException>(() =>  _engine.Modules.Import("my-module"));
        Assert.Equal("Error while loading module: error in module 'imported': Missing initializer in const declaration (imported:1:21)", exc.Message);
        Assert.Equal("imported", exc.Location.SourceFile);
    }

    [Fact]
    public void ShouldPropagateLinkError()
    {
        _engine.Modules.Add("imported", "export invalid;");
        _engine.Modules.Add("my-module", "import { value } from 'imported';");

        var exc = Assert.Throws<JavaScriptException>(() =>  _engine.Modules.Import("my-module"));
        Assert.Equal("Error while loading module: error in module 'imported': Unexpected identifier 'invalid' (imported:1:8)", exc.Message);
        Assert.Equal("imported", exc.Location.SourceFile);
    }

    [Fact]
    public void ShouldPropagateExecuteError()
    {
        _engine.Modules.Add("my-module", "throw new Error('imported successfully');");

        var exc = Assert.Throws<JavaScriptException>(() =>  _engine.Modules.Import("my-module"));
        Assert.Equal("imported successfully", exc.Message);
        Assert.Equal("my-module", exc.Location.SourceFile);
    }

    [Fact]
    public void ShouldPropagateThrowStatementThroughJavaScriptImport()
    {
        _engine.Modules.Add("imported-module", "throw new Error('imported successfully');");
        _engine.Modules.Add("my-module", "import 'imported-module';");

        var exc = Assert.Throws<JavaScriptException>(() =>  _engine.Modules.Import("my-module"));
        Assert.Equal("imported successfully", exc.Message);
    }

    [Fact]
    public void ShouldAddModuleFromJsValue()
    {
        _engine.Modules.Add("my-module", builder => builder.ExportValue("value", JsString.Create("hello world")));
        var ns =  _engine.Modules.Import("my-module");

        Assert.Equal("hello world", ns.Get("value").AsString());
    }

    [Fact]
    public void ShouldAddModuleFromClrInstance()
    {
        _engine.Modules.Add("imported-module", builder => builder.ExportObject("value", new ImportedClass
        {
            Value = "instance value"
        }));
        _engine.Modules.Add("my-module", "import { value } from 'imported-module'; export const exported = value.value;");
        var ns =  _engine.Modules.Import("my-module");

        Assert.Equal("instance value", ns.Get("exported").AsString());
    }

    [Fact]
    public void ShouldAllowInvokeUserDefinedClass()
    {
        _engine.Modules.Add("user", "export class UserDefined { constructor(v) { this._v = v; } hello(c) { return `hello ${this._v}${c}`; } }");
        var ctor =  _engine.Modules.Import("user").Get("UserDefined");
        var instance = _engine.Construct(ctor, JsString.Create("world"));
        var result = instance.GetMethod("hello").Call(instance, JsString.Create("!"));

        Assert.Equal("hello world!", result);
    }

    [Fact]
    public void ShouldAddModuleFromClrType()
    {
        _engine.Modules.Add("imported-module", builder => builder.ExportType<ImportedClass>());
        _engine.Modules.Add("my-module", "import { ImportedClass } from 'imported-module'; export const exported = new ImportedClass().value;");
        var ns =  _engine.Modules.Import("my-module");

        Assert.Equal("hello world", ns.Get("exported").AsString());
    }

    [Fact]
    public void ShouldAddModuleFromClrFunction()
    {
        var received = new List<string>();
        _engine.Modules.Add("imported-module", builder => builder
            .ExportFunction("act_noargs", () => received.Add("act_noargs"))
            .ExportFunction("act_args", args => received.Add($"act_args:{args[0].AsString()}"))
            .ExportFunction("fn_noargs", () =>
            {
                received.Add("fn_noargs");
                return "ret";
            })
            .ExportFunction("fn_args", args =>
            {
                received.Add($"fn_args:{args[0].AsString()}");
                return "ret";
            })
        );
        _engine.Modules.Add("my-module", @"
import * as fns from 'imported-module';
export const result = [fns.act_noargs(), fns.act_args('ok'), fns.fn_noargs(), fns.fn_args('ok')];");
        var ns =  _engine.Modules.Import("my-module");

        Assert.Equal(new[]
        {
            "act_noargs",
            "act_args:ok",
            "fn_noargs",
            "fn_args:ok"
        }, received.ToArray());
        Assert.Equal(new[]
        {
            "undefined",
            "undefined",
            "ret",
            "ret"
        }, ns.Get("result").AsArray().Select(x => x.ToString()).ToArray());
    }

    private class ImportedClass
    {
        public string Value { get; set; } = "hello world";
    }

    [Fact]
    public void ShouldAllowExportMultipleImports()
    {
        _engine.Modules.Add("@mine/import1", builder => builder.ExportValue("value1", JsNumber.Create(1)));
        _engine.Modules.Add("@mine/import2", builder => builder.ExportValue("value2", JsNumber.Create(2)));
        _engine.Modules.Add("@mine", "export * from '@mine/import1'; export * from '@mine/import2'");
        _engine.Modules.Add("app", "import { value1, value2 } from '@mine'; export const result = `${value1} ${value2}`");
        var ns =  _engine.Modules.Import("app");

        Assert.Equal("1 2", ns.Get("result").AsString());
    }

    [Fact]
    public void ShouldAllowNamedStarExport()
    {
        _engine.Modules.Add("imported-module", builder => builder.ExportValue("value1", 5));
        _engine.Modules.Add("my-module", "export * as ns from 'imported-module';");
        var ns =  _engine.Modules.Import("my-module");

        Assert.Equal(5, ns.Get("ns").Get("value1").AsNumber());
    }

    [Fact]
    public void ShouldAllowChaining()
    {
        _engine.Modules.Add("dependent-module", "export const dependency = 1;");
        _engine.Modules.Add("my-module", builder => builder
            .AddSource("import { dependency } from 'dependent-module';")
            .AddSource("export const output = dependency + 1;")
            .ExportValue("num", JsNumber.Create(-1))
        );
        var ns =  _engine.Modules.Import("my-module");

        Assert.Equal(2, ns.Get("output").AsInteger());
        Assert.Equal(-1, ns.Get("num").AsInteger());
    }

    [Fact]
    public void ShouldImportOnlyOnce()
    {
        var called = 0;
        _engine.Modules.Add("imported-module", builder => builder.ExportFunction("count", args => called++));
        _engine.Modules.Add("my-module", "import { count } from 'imported-module'; count();");
         _engine.Modules.Import("my-module");
         _engine.Modules.Import("my-module");

        Assert.Equal(1, called);
    }

    [Fact]
    public void ShouldAllowSelfImport()
    {
        _engine.Modules.Add("my-globals", "export const globals = { counter: 0 };");
        _engine.Modules.Add("my-module", @"
import { globals } from 'my-globals';
import {} from 'my-module';
globals.counter++;
export const count = globals.counter;
");
        var ns =  _engine.Modules.Import("my-module");

        Assert.Equal(1, ns.Get("count").AsInteger());
    }

    [Fact]
    public void ShouldAllowCyclicImport()
    {
        // https://tc39.es/ecma262/#sec-example-cyclic-module-record-graphs

        _engine.Modules.Add("B", "import { a } from 'A'; export const b = 'b';");
        _engine.Modules.Add("A", "import { b } from 'B'; export const a = 'a';");

        var nsA =  _engine.Modules.Import("A");
        var nsB =  _engine.Modules.Import("B");

        Assert.Equal("a", nsA.Get("a").AsString());
        Assert.Equal("b", nsB.Get("b").AsString());
    }

    [Fact]
    public void ShouldSupportConstraints()
    {
        var engine = new Engine(opts => opts.TimeoutInterval(TimeSpan.FromTicks(1)));

        engine.Modules.Add("sleep", builder => builder.ExportFunction("sleep", () => Thread.Sleep(100)));
        engine.Modules.Add("my-module", "import { sleep } from 'sleep'; for(var i = 0; i < 100; i++) { sleep(); } export const result = 'ok';");
        Assert.Throws<TimeoutException>(() => engine.Modules.Import("my-module"));
    }

    [Fact]
    public void CanLoadModuleImportsFromFiles()
    {
        var engine = new Engine(options => options.EnableModules(GetBasePath()));
        engine.Modules.Add("my-module", "import { User } from './modules/user.js'; export const user = new User('John', 'Doe');");
        var ns = engine.Modules.Import("my-module");

        Assert.Equal("John Doe", ns["user"].Get("name").AsString());
    }

    [Fact]
    public void CanImportFromFile()
    {
        var engine = new Engine(options => options.EnableModules(GetBasePath()));
        var ns = engine.Modules.Import("./modules/format-name.js");
        var result = engine.Invoke(ns.Get("formatName"), "John", "Doe").AsString();

        Assert.Equal("John Doe", result);
    }

    [Fact]
    public void CanImportFromFileWithSpacesInPath()
    {
        var engine = new Engine(options => options.EnableModules(GetBasePath()));
        var ns = engine.Modules.Import("./dir with spaces/format name.js");
        var result = engine.Invoke(ns.Get("formatName"), "John", "Doe").AsString();

        Assert.Equal("John Doe", result);
    }

    [Fact]
    public void CanReuseModule()
    {
        const string Code = "export function formatName(firstName, lastName) {\r\n    return `${firstName} ${lastName}`;\r\n}";
        var module = Engine.PrepareModule(Code);
        for (var i = 0; i < 5; i++)
        {
            var engine = new Engine();
            engine.Modules.Add("__main__", x => x.AddModule(module));
            var ns = engine.Modules.Import("__main__");
            var result = engine.Invoke(ns.Get("formatName"), "John" + i, "Doe").AsString();
            Assert.Equal($"John{i} Doe", result);
        }
    }

    [Fact]
    public void EngineExecutePassesSourceForModuleResolving()
    {
        var moduleLoader = new EnforceRelativeModuleLoader(new Dictionary<string, string>()
        {
            ["file:///folder/my-module.js"] = "export const value = 'myModuleConst'"
        });
        var engine = new Engine(options => options.EnableModules(moduleLoader));
        var code = @"
(async () => {
    const { value } = await import('./my-module.js');
    log(value);
})();
";
        List<string> logStatements = new List<string>();
        engine.SetValue("log", logStatements.Add);

        engine.Execute(code, source: "file:///folder/main.js");
        engine.Advanced.ProcessTasks();

        Assert.Collection(
            logStatements,
            s => Assert.Equal("myModuleConst", s));
    }

    [Fact]
    public void EngineExecuteUsesScriptSourceForSource()
    {
        var moduleLoader = new EnforceRelativeModuleLoader(new Dictionary<string, string>()
        {
            ["file:///folder/my-module.js"] = "export const value = 'myModuleConst'"
        });
        var engine = new Engine(options => options.EnableModules(moduleLoader));
        var code = @"
(async () => {
    const { value } = await import('./my-module.js');
    log(value);
})();
";
        List<string> logStatements = new List<string>();
        engine.SetValue("log", logStatements.Add);

        var script = Engine.PrepareScript(code, source: "file:///folder/main.js");
        engine.Execute(script);
        engine.Advanced.ProcessTasks();

        Assert.Collection(
            logStatements,
            s => Assert.Equal("myModuleConst", s));
    }

    [Fact]
    public void EngineEvaluatePassesSourceForModuleResolving()
    {
        var moduleLoader = new EnforceRelativeModuleLoader(new Dictionary<string, string>()
        {
            ["file:///folder/my-module.js"] = "export const value = 'myModuleConst'"
        });
        var engine = new Engine(options => options.EnableModules(moduleLoader));
        var code = @"
(async () => {
    const { value } = await import('./my-module.js');
    log(value);
})();
";
        List<string> logStatements = new List<string>();
        engine.SetValue("log", logStatements.Add);

        engine.Evaluate(code, source: "file:///folder/main.js");
        engine.Advanced.ProcessTasks();

        Assert.Collection(
            logStatements,
            s => Assert.Equal("myModuleConst", s));
    }

    [Fact]
    public void EngineEvaluateUsesScriptSourceForSource()
    {
        var moduleLoader = new EnforceRelativeModuleLoader(new Dictionary<string, string>()
        {
            ["file:///folder/my-module.js"] = "export const value = 'myModuleConst'"
        });
        var engine = new Engine(options => options.EnableModules(moduleLoader));
        var code = @"
(async () => {
    const { value } = await import('./my-module.js');
    log(value);
})();
";
        List<string> logStatements = new List<string>();
        engine.SetValue("log", logStatements.Add);

        var script = Engine.PrepareScript(code, source: "file:///folder/main.js");
        engine.Evaluate(script);
        engine.Advanced.ProcessTasks();

        Assert.Collection(
            logStatements,
            s => Assert.Equal("myModuleConst", s));
    }

    private sealed class EnforceRelativeModuleLoader : IModuleLoader
    {
        private readonly IReadOnlyDictionary<string, string> _modules;

        public EnforceRelativeModuleLoader(IReadOnlyDictionary<string, string> modules)
        {
            _modules = modules;
        }

        public ResolvedSpecifier Resolve(string referencingModuleLocation, ModuleRequest moduleRequest)
        {
            Assert.False(string.IsNullOrEmpty(referencingModuleLocation), "Referencing module location is null or empty");
            var target = new Uri(new Uri(referencingModuleLocation, UriKind.Absolute), moduleRequest.Specifier);
            Assert.True(_modules.ContainsKey(target.ToString()), $"Resolve was called with unexpected module request, {moduleRequest.Specifier} relative to {referencingModuleLocation}");
            return new ResolvedSpecifier(moduleRequest, target.ToString(), target, SpecifierType.Bare);
        }

        public Module LoadModule(Engine engine, ResolvedSpecifier resolved)
        {
            Assert.NotNull(resolved.Uri);
            var source = resolved.Uri.ToString();
            Assert.True(_modules.TryGetValue(source, out var script), $"Resolved module does not exist: {source}");
            return ModuleFactory.BuildSourceTextModule(engine, Engine.PrepareModule(script, source));
        }
    }

    private static string GetBasePath()
    {
        var assemblyDirectory = new DirectoryInfo(AppDomain.CurrentDomain.RelativeSearchPath ?? AppDomain.CurrentDomain.BaseDirectory);

        var current = assemblyDirectory;
        var binDirectory = $"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}";
        while (current is not null)
        {
            if (current.FullName.Contains(binDirectory) || current.Name == "bin")
            {
                current = current.Parent;
                continue;
            }

            var testDirectory = current.GetDirectories("Jint.Tests").FirstOrDefault();
            if (testDirectory == null)
            {
                current = current.Parent;
                continue;
            }

            // found it
            current = testDirectory;
            break;
        }

        if (current is null)
        {
            throw new NullReferenceException($"Could not find tests base path, assemblyPath: {assemblyDirectory}");
        }

        return Path.Combine(current.FullName, "Runtime", "Scripts");
    }

    [Fact]
    public void ModuleBuilderWithCustomModuleLoaderLoadsModulesProperly()
    {
        var engine = new Engine(o => o.EnableModules(new LocationResolveOnlyModuleLoader((_, moduleRequest) =>
        {
            var result = moduleRequest.Specifier;
            if (moduleRequest.Specifier == "../library1/builder_module.js")
            {
                result = "library1/builder_module.js";
            }
            return result;
        })));

        var logStatements = new List<string>();
        engine.SetValue("log", logStatements.Add);
        engine.Modules.Add("library1/builder_module.js",
            builder => builder.AddSource("export const value = 'builder_module_const'; log('builder_module')"));
        engine.Modules.Add("library2/entry_point_module.js",
            builder => builder.AddSource("import * as m from '../library1/builder_module.js'; log('entry_point_module')"));

        engine.Modules.Import("library2/entry_point_module.js");

        Assert.Collection(
            logStatements,
            s => Assert.Equal("builder_module", s),
            s => Assert.Equal("entry_point_module", s));
    }

    [Fact]
    public void ModuleBuilderPassesReferencingModuleLocationToModuleLoader()
    {
        var engine = new Engine(o => o.EnableModules(new LocationResolveOnlyModuleLoader((referencingModuleLocation, moduleRequest) =>
        {
            var result = moduleRequest.Specifier;
            if (moduleRequest.Specifier == "../library1/builder_module.js")
            {
                Assert.Equal("library2/entry_point_module.js", referencingModuleLocation);
                result = "library1/builder_module.js";
            }
            return result;
        })));

        var logStatements = new List<string>();
        engine.SetValue("log", logStatements.Add);
        engine.Modules.Add("library1/builder_module.js",
            builder => builder.AddSource("export const value = 'builder_module_const'; log('builder_module')"));
        engine.Modules.Add("library2/entry_point_module.js",
            builder => builder.AddSource("import * as m from '../library1/builder_module.js'; log('entry_point_module')"));

        engine.Modules.Import("library2/entry_point_module.js");

        Assert.Collection(
            logStatements,
            s => Assert.Equal("builder_module", s),
            s => Assert.Equal("entry_point_module", s));
    }

    /// <summary>
    /// Custom <see cref="ModuleLoader"/> implementation which is only responsible to
    /// resolve the correct module location (see <see cref="Resolve"/>). Modules
    /// must be registered using <see cref="Jint.Engine.ModuleOperations"/> (e.g.
    /// by using <see cref="Engine.ModuleOperations.Add(string,string)"/>)
    /// </summary>
    private sealed class LocationResolveOnlyModuleLoader : ModuleLoader
    {
        public delegate string ResolveHandler(string referencingModuleLocation, ModuleRequest moduleRequest);

        private readonly ResolveHandler _resolveHandler;

        public LocationResolveOnlyModuleLoader(ResolveHandler resolveHandler)
        {
            _resolveHandler = resolveHandler ?? throw new ArgumentNullException(nameof(resolveHandler));
        }

        public override ResolvedSpecifier Resolve(string referencingModuleLocation, ModuleRequest moduleRequest)
        {
            return new ResolvedSpecifier(
                moduleRequest,
                Key: _resolveHandler(referencingModuleLocation, moduleRequest),
                Uri: null,
                SpecifierType.RelativeOrAbsolute
            );
        }
        protected override string LoadModuleContents(Engine engine, ResolvedSpecifier resolved)
            => throw new InvalidOperationException();
    }

    [Fact]
    public void EngineShouldTransmitSourceModuleForModuleLoader()
    {
        var engine = new Engine(o => o.EnableModules(new ModuleLoaderForEngineShouldTransmitSourceModuleForModuleLoaderTest()));

        var logs = new List<string>();
        engine.SetValue("log", logs.Add);

        engine.Modules.Import($"code/lib/module.js");

        Assert.Collection(logs,
            s => Assert.Equal("code/execute.js", s),
            s => Assert.Equal("code/lib/module.js", s));
    }
    public class ModuleLoaderForEngineShouldTransmitSourceModuleForModuleLoaderTest : ModuleLoader
    {
        public override ResolvedSpecifier Resolve(string referencingModuleLocation, ModuleRequest moduleRequest)
        {
            var moduleSpec = moduleRequest.Specifier;

            // to resolve this statement requires information about source module
            if (moduleSpec == "../execute.js")
            {
                Assert.True(!string.IsNullOrEmpty(referencingModuleLocation), "module loader cannot resolve referensing module - has no referencing module location");
                moduleSpec = $"code/execute.js";
            }

            return new ResolvedSpecifier(
                moduleRequest,
                moduleSpec,
                Uri: null,
                SpecifierType.RelativeOrAbsolute
            );
        }
        protected override string LoadModuleContents(Engine engine, ResolvedSpecifier resolved)
        {
            if (resolved.Key == $"code/lib/module.js")
                return $"import * as m from '../execute.js'; log('code/lib/module.js')";
            if (resolved.Key == $"code/execute.js")
            {
                return $"log('code/execute.js')";
            }

            throw new NotImplementedException(); // no need in this test
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CanStaticallyImportJsonModule(bool importViaLoader)
    {
        const string JsonModuleSpecifier = "./test.json";
        const string JsonModuleContent =
            """
            { "message": "hello" }
            """;

        const string MainModuleSpecifier = "./main.js";
        const string MainModuleCode =
            $$"""
            import json from "{{JsonModuleSpecifier}}" with { type: "json" };
            export const msg = json.message;
            """;

        var loaderModules = new Dictionary<string, Func<Engine, ResolvedSpecifier, Module>>();
        var engine = new Engine(o => o.EnableModules(new TestModuleLoader(loaderModules)));

        loaderModules.Add(JsonModuleSpecifier, (engine, resolved) => ModuleFactory.BuildJsonModule(engine, resolved, JsonModuleContent));
        if (importViaLoader)
        {
            loaderModules.Add(MainModuleSpecifier, (engine, resolved) => ModuleFactory.BuildSourceTextModule(engine, resolved, MainModuleCode));
        }
        else
        {
            engine.Modules.Add(MainModuleSpecifier, MainModuleCode);
        }

        var mainModule = engine.Modules.Import(MainModuleSpecifier);

        Assert.Equal("hello", mainModule.Get("msg").AsString());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CanDynamicallyImportJsonModule(bool importViaLoader)
    {
        const string JsonModuleSpecifier = "./test.json";
        const string JsonModuleContent =
            """
            { "message": "hello" }
            """;

        const string MainModuleSpecifier = "./main.js";
        const string MainModuleCode =
            $$"""
            const json = await import("{{JsonModuleSpecifier}}", { with: { type: "json" } });
            callback(json.default.message);
            """;

        var completionTcs = new TaskCompletionSource<JsValue>(TaskCreationOptions.RunContinuationsAsynchronously);

        var loaderModules = new Dictionary<string, Func<Engine, ResolvedSpecifier, Module>>();
        var engine = new Engine(o => o.EnableModules(new TestModuleLoader(loaderModules)))
            .SetValue("callback", new Action<JsValue>(value => completionTcs.SetResult(value)));

        loaderModules.Add(JsonModuleSpecifier, (engine, resolved) => ModuleFactory.BuildJsonModule(engine, resolved, JsonModuleContent));
        if (importViaLoader)
        {
            loaderModules.Add(MainModuleSpecifier, (engine, resolved) => ModuleFactory.BuildSourceTextModule(engine, resolved, MainModuleCode));
        }
        else
        {
            engine.Modules.Add(MainModuleSpecifier, MainModuleCode);
        }

        var mainModule = engine.Modules.Import(MainModuleSpecifier);

        Assert.Equal("hello", (await completionTcs.Task).AsString());
    }

    private sealed class TestModuleLoader : IModuleLoader
    {
        private readonly Dictionary<string, Func<Engine, ResolvedSpecifier, Module>> _moduleFactories;

        public TestModuleLoader(Dictionary<string, Func<Engine, ResolvedSpecifier, Module>> moduleFactories)
        {
            _moduleFactories = moduleFactories;
        }

        ResolvedSpecifier IModuleLoader.Resolve(string referencingModuleLocation, ModuleRequest moduleRequest)
        {
            return new ResolvedSpecifier(moduleRequest, moduleRequest.Specifier, Uri: null, SpecifierType.RelativeOrAbsolute);
        }

        Module IModuleLoader.LoadModule(Engine engine, ResolvedSpecifier resolved)
        {
            if (_moduleFactories.TryGetValue(resolved.ModuleRequest.Specifier, out var moduleFactory))
            {
                return moduleFactory(engine, resolved);
            }

            throw new ArgumentException(null, nameof(resolved));
        }
    }
}
