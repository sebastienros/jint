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
            executedScript.Logs.Should().ContainSingle();
            executedScript.Logs[0].Should().Be(expectedValue);
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

        sharedModules.ModulesParsed.Should().Be(1);
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

        runner.Logs.Should().ContainSingle();
        runner.Logs[0].Should().Be("json");
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

            _engine = new Engine(options => options.UseModules(this));
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
            resolved.Uri.Should().NotBeNull();
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

    [Fact]
    public void ModulesCanBeRegisteredFromAConfigurationCallback()
    {
        var engine = new Engine(options => options.Configure(e => e.Modules.Add("lib", "export const answer = 42;")));

        var ns = engine.Modules.Import("lib");

        ns.Get("answer").AsNumber().Should().Be(42);
    }

    /// <summary>
    /// A <c>file:</c> uri is the one case where the module's name is not the loader's key: it is reduced to
    /// <see cref="Uri.LocalPath"/>, which is exactly the rule a host preparing modules itself has to match.
    /// </summary>
    [Fact]
    public void LocationOfNamesTheBuiltModuleForAnAbsoluteFileUri()
    {
        var uri = new Uri("file:///lib/a.js");
        var resolved = new ResolvedSpecifier(new ModuleRequest("a.js", []), uri.ToString(), uri, SpecifierType.RelativeOrAbsolute);

        var location = ModuleFactory.LocationOf(resolved);

        location.Should().Be(uri.LocalPath).And.NotBe(resolved.Key);
        LocationOfShouldNameTheBuiltModule(resolved);
    }

    /// <summary>
    /// Every other scheme leaves the loader's key alone, uri or no uri - the uri is consulted for nothing but
    /// the "is this a file?" question.
    /// </summary>
    [Fact]
    public void LocationOfNamesTheBuiltModuleByItsKeyForANonFileUri()
    {
        var uri = new Uri("app:///x.js");
        var resolved = new ResolvedSpecifier(new ModuleRequest("x.js", []), uri.ToString(), uri, SpecifierType.RelativeOrAbsolute);

        ModuleFactory.LocationOf(resolved).Should().Be(resolved.Key);
        LocationOfShouldNameTheBuiltModule(resolved);

        // The key is taken verbatim rather than derived from the uri, so a loader whose key is not the uri's
        // own string still names the module by the key.
        var divergent = new ResolvedSpecifier(new ModuleRequest("x.js", []), "app:the-x-module", uri, SpecifierType.Bare);

        ModuleFactory.LocationOf(divergent).Should().Be("app:the-x-module");
        LocationOfShouldNameTheBuiltModule(divergent);
    }

    [Fact]
    public void LocationOfNamesTheBuiltModuleByItsKeyWhenTheLoaderReturnsNoUri()
    {
        var resolved = new ResolvedSpecifier(new ModuleRequest("____main____", []), "____main____", null, SpecifierType.Bare);

        ModuleFactory.LocationOf(resolved).Should().Be("____main____");
        LocationOfShouldNameTheBuiltModule(resolved);
    }

    private static void LocationOfShouldNameTheBuiltModule(ResolvedSpecifier resolved)
    {
        var engine = new Engine();
        var module = ModuleFactory.BuildSourceTextModule(engine, resolved, "export const value = 1;");

        module.Location.Should().Be(ModuleFactory.LocationOf(resolved));
    }

    /// <summary>
    /// The use case <see cref="ModuleFactory.LocationOf"/> is public for: a loader that prepares each module
    /// once and shares the prepared AST across engines has to name the module before any module exists, and
    /// the name has to be the one the engine would have derived - it is what the module's own relative
    /// imports are resolved against.
    /// </summary>
    [Fact]
    public void PreparedModulesNamedByLocationOfResolveTheirRelativeImports()
    {
        var loader = new SharedPreparedModuleLoader(new Dictionary<string, string>
        {
            ["file:///modules/main.js"] = "import { value } from './lib/dep.js'; export const result = value + '!';",
            ["file:///modules/lib/dep.js"] = "export const value = 'shared';",
        });

        foreach (var _ in Enumerable.Range(0, 3))
        {
            var engine = new Engine(options => options.UseModules(loader));

            var ns = engine.Modules.Import("file:///modules/main.js");

            ns.Get("result").AsString().Should().Be("shared!");
        }

        // One prepared AST per module, reused by all three engines.
        loader.PreparedLocations.Should().HaveCount(2);

        // The nested import was resolved against the very name PrepareModule was handed, and for a file: uri
        // that name is the reduced path rather than the url the source is keyed under.
        var mainLocation = loader.PreparedLocations[0];
        mainLocation.Should().NotBe("file:///modules/main.js");
        loader.Resolutions.Should().Contain(r => r.Specifier == "./lib/dep.js" && r.Referencing == mainLocation);
    }

    /// <summary>
    /// Prepares every module once and shares the prepared <see cref="AstModule"/> across engines, naming each
    /// one with <see cref="ModuleFactory.LocationOf"/> so it carries the identity the string-loading overloads
    /// of <see cref="ModuleFactory"/> would have produced. The same string is the cache key, so a module and
    /// its cache entry cannot drift apart.
    /// </summary>
    private sealed class SharedPreparedModuleLoader : IModuleLoader
    {
        private static readonly Uri Root = new("file:///modules/");

        private readonly IReadOnlyDictionary<string, string> _sources;
        private readonly Dictionary<string, Prepared<AstModule>> _prepared = new(StringComparer.Ordinal);
        private readonly Dictionary<string, Uri> _urisByLocation = new(StringComparer.Ordinal);
        private readonly List<(string? Referencing, string Specifier)> _resolutions = new();
        private readonly List<string> _preparedLocations = new();

        public SharedPreparedModuleLoader(IReadOnlyDictionary<string, string> sources)
        {
            _sources = sources;
        }

        public IReadOnlyList<(string? Referencing, string Specifier)> Resolutions => _resolutions;

        public IReadOnlyList<string> PreparedLocations => _preparedLocations;

        public ResolvedSpecifier Resolve(string? referencingModuleLocation, ModuleRequest moduleRequest)
        {
            _resolutions.Add((referencingModuleLocation, moduleRequest.Specifier));

            Uri uri;
            if (Uri.TryCreate(moduleRequest.Specifier, UriKind.Absolute, out var absolute))
            {
                uri = absolute;
            }
            else
            {
                // A module loaded from a file: uri knows itself by a path with no scheme left to resolve a
                // relative import against, so the loader reconstructs the origin - here from the uri it
                // handed out for that location.
                var origin = referencingModuleLocation is not null && _urisByLocation.TryGetValue(referencingModuleLocation, out var known)
                    ? known
                    : Root;
                uri = new Uri(origin, moduleRequest.Specifier);
            }

            var resolved = new ResolvedSpecifier(moduleRequest, uri.ToString(), uri, SpecifierType.RelativeOrAbsolute);
            _urisByLocation[ModuleFactory.LocationOf(resolved)] = uri;
            return resolved;
        }

        public Module LoadModule(Engine engine, ResolvedSpecifier resolved)
        {
            // Both the cache key and the prepared module's name are the location the engine derives itself.
            var location = ModuleFactory.LocationOf(resolved);
            if (!_prepared.TryGetValue(location, out var prepared))
            {
                prepared = Engine.PrepareModule(_sources[resolved.Key], location);
                _prepared[location] = prepared;
                _preparedLocations.Add(location);
            }

            return ModuleFactory.BuildSourceTextModule(engine, prepared);
        }
    }
}
