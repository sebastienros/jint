using System.Collections.Concurrent;
using Jint.Native;
using Jint.Native.Json;
using Jint.Runtime.Modules;

using Module = Jint.Runtime.Modules.Module;

#nullable enable

namespace Jint.Tests.PublicInterface;

public class ModuleLoaderTests
{
    [Fact]
    public void CustomModuleLoaderWithUriModuleLocations()
    {
        // Dummy module store which shows that different protocols can be
        // used for modules.
        var store = new ModuleStore(new Dictionary<string, string>()
        {
            ["https://example.com/someModule.js"] = "export const DEFAULT_VALUE = 'remote';",
            ["https://example.com/test.js"] = "import { DEFAULT_VALUE } from 'someModule.js'; export const value = DEFAULT_VALUE;",
            ["file:///someModule.js"] = "export const value = 'local';",
            ["proprietary-protocol:///someModule.js"] = "export const value = 'proprietary';",
        });
        var sharedModules = new CachedModuleLoader(store);

        var runA = RunModule("import { value } from 'https://example.com/test.js'; log(value);");
        var runB = RunModule("import { value } from 'someModule.js'; log(value);");
        var runC = RunModule("import { value } from 'proprietary-protocol:///someModule.js'; log(value);");

        ExpectLoggedValue(runA, "remote");
        ExpectLoggedValue(runB, "local");
        ExpectLoggedValue(runC, "proprietary");

        static void ExpectLoggedValue(ModuleScript executedScript, string expectedValue)
        {
            Assert.Single(executedScript.Logs);
            Assert.Equal(expectedValue, executedScript.Logs[0]);
        }

        ModuleScript RunModule(string code)
        {
            var result = new ModuleScript(code, sharedModules);
            result.Execute();
            return result;
        }
    }

    [Fact]
    public void CustomModuleLoaderWithCachingSupport()
    {
        // Different engines use the same module loader.
        // The module loader caches the parsed Module
        // which allows to re-use these for different engine runs.
        var store = new ModuleStore(new Dictionary<string, string>()
        {
            ["file:///localModule.js"] = "export const value = 'local';",
        });
        var sharedModules = new CachedModuleLoader(store);

        // Simulate the re-use by simply running the same main entry point 10 times.
        foreach (var _ in Enumerable.Range(0, 10))
        {
            var runner = new ModuleScript("import { value } from 'localModule.js'; log(value);", sharedModules);
            runner.Execute();
        }

        Assert.Equal(1, sharedModules.ModulesParsed);
    }

    [Fact]
    public void CustomModuleLoaderCanWorkWithJsonModules()
    {
        var store = new ModuleStore(new Dictionary<string, string>()
        {
            ["file:///config.json"] = "{ \"value\": \"json\" }",
        });
        var sharedModules = new CachedModuleLoader(store);

        var runner = new ModuleScript("import data from 'config.json' with { type: 'json' }; log(data.value);", sharedModules);
        runner.Execute();

        Assert.Single(runner.Logs);
        Assert.Equal("json", runner.Logs[0]);
    }

    /// <summary>
    /// A simple in-memory store for module sources. The keys
    /// must be absolute <see cref="Uri.ToString()"/> values.
    /// </summary>
    /// <remarks>
    /// This is just an example and not production ready code. The implementation
    /// is missing important path traversal checks and other edge cases.
    /// </remarks>
    private sealed class ModuleStore
    {
        private const string DefaultProtocol = "file:///./";
        private readonly IReadOnlyDictionary<string, string> _sourceCode;

        public ModuleStore(IReadOnlyDictionary<string, string> sourceCode)
        {
            _sourceCode = sourceCode;
        }

        public ResolvedSpecifier Resolve(string? referencingModuleLocation, ModuleRequest moduleRequest)
        {
            Uri uri = Resolve(referencingModuleLocation, moduleRequest.Specifier);
            return new ResolvedSpecifier(moduleRequest, uri.ToString(), uri, SpecifierType.Bare);
        }

        private Uri Resolve(string? referencingModuleLocation, string specifier)
        {
            if (Uri.TryCreate(specifier, UriKind.Absolute, out Uri? absoluteLocation))
                return absoluteLocation;

            if (!string.IsNullOrEmpty(referencingModuleLocation) && Uri.TryCreate(referencingModuleLocation, UriKind.Absolute, out Uri? baseUri))
            {
                if (Uri.TryCreate(baseUri, specifier, out Uri? relative))
                    return relative;
            }

            return new Uri(DefaultProtocol + specifier);
        }

        public string GetModuleSource(Uri uri)
        {
            if (!_sourceCode.TryGetValue(uri.ToString(), out var result))
                throw new InvalidOperationException($"Module not found: {uri}");
            return result;
        }
    }

    /// <summary>
    /// The main entry point for a module script. Allows
    /// to use a script as a main module.
    /// </summary>
    private sealed class ModuleScript : IModuleLoader
    {
        private const string MainSpecifier = "____main____";
        private readonly List<string> _logs = new();
        private readonly Engine _engine;
        private readonly string _main;
        private readonly IModuleLoader _modules;

        public ModuleScript(string main, IModuleLoader modules)
        {
            _main = main;
            _modules = modules;

            _engine = new Engine(options => options.EnableModules(this));
            _engine.SetValue("log", _logs.Add);
        }

        public IReadOnlyList<string> Logs => _logs;

        public void Execute()
        {
            _engine.Modules.Import(MainSpecifier);
        }

        ResolvedSpecifier IModuleLoader.Resolve(string? referencingModuleLocation, ModuleRequest moduleRequest)
        {
            if (moduleRequest.Specifier == MainSpecifier)
                return new ResolvedSpecifier(moduleRequest, MainSpecifier, null, SpecifierType.Bare);
            return _modules.Resolve(referencingModuleLocation, moduleRequest);
        }

        Module IModuleLoader.LoadModule(Engine engine, ResolvedSpecifier resolved)
        {
            if (resolved.ModuleRequest.Specifier == MainSpecifier)
                return ModuleFactory.BuildSourceTextModule(engine, Engine.PrepareModule(_main, MainSpecifier));
            return _modules.LoadModule(engine, resolved);
        }
    }

    /// <summary>
    /// <para>
    /// A simple <see cref="IModuleLoader"/> implementation which will
    /// re-use prepared <see cref="AstModule"/> or <see cref="JsValue"/> modules to
    /// produce <see cref="Jint.Runtime.Modules.Module"/>.
    /// </para>
    /// <para>
    /// The module source gets loaded from <see cref="ModuleStore"/>.
    /// </para>
    /// </summary>
    private sealed class CachedModuleLoader : IModuleLoader
    {
        private readonly ConcurrentDictionary<Uri, ParsedModule> _parsedModules = new();
        private readonly ModuleStore _store;
        #if NETCOREAPP1_0_OR_GREATER
        private readonly Func<Uri, ResolvedSpecifier, ParsedModule> _moduleParser;
        #endif
        private int _modulesParsed;

        public CachedModuleLoader(ModuleStore store)
        {
            _store = store;
            #if NETCOREAPP1_0_OR_GREATER
            _moduleParser = GetParsedModule;
            #endif
        }

        public int ModulesParsed => _modulesParsed;

        public ResolvedSpecifier Resolve(string? referencingModuleLocation, ModuleRequest moduleRequest)
        {
            return _store.Resolve(referencingModuleLocation, moduleRequest);
        }

        public Module LoadModule(Engine engine, ResolvedSpecifier resolved)
        {
            Assert.NotNull(resolved.Uri);
            #if NETCOREAPP1_0_OR_GREATER
            var parsedModule = _parsedModules.GetOrAdd(resolved.Uri, _moduleParser, resolved);
            #else
            var parsedModule = _parsedModules.GetOrAdd(resolved.Uri, _ => GetParsedModule(resolved.Uri, resolved));
            #endif
            return parsedModule.ToModule(engine);
        }

        private ParsedModule GetParsedModule(Uri uri, ResolvedSpecifier resolved)
        {
            var script = _store.GetModuleSource(resolved.Uri!);
            var result = resolved.ModuleRequest.IsJsonModule()
                ? ParsedModule.JsonModule(script, resolved.Uri!.ToString())
                : ParsedModule.TextModule(script, resolved.Uri!.ToString());
            Interlocked.Increment(ref _modulesParsed);
            return result;
        }

        private sealed class ParsedModule
        {
            private readonly Prepared<AstModule>? _textModule;
            private readonly (JsValue Json, string Location)? _jsonModule;

            private ParsedModule(in Prepared<AstModule> textModule)
            {
                _textModule = textModule;
            }

            private ParsedModule(JsValue json, string location)
            {
                _jsonModule = (json, location);
            }

            public static ParsedModule TextModule(string script, string location)
                => new(Engine.PrepareModule(script, location));

            public static ParsedModule JsonModule(string json, string location)
                => new(ParseJson(json), location);

            private static JsValue ParseJson(string json)
            {
                var engine = new Engine();
                var parser = new JsonParser(engine);
                return parser.Parse(json);
            }

            public Module ToModule(Engine engine)
            {
                if (_jsonModule is not null)
                    return ModuleFactory.BuildJsonModule(engine, _jsonModule.Value.Json, _jsonModule.Value.Location);
                if (_textModule is not null)
                    return ModuleFactory.BuildSourceTextModule(engine, _textModule.Value);
                throw new InvalidOperationException("Unexpected state - no module type available");
            }
        }
    }
}
