using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Modules;

#nullable enable

namespace Jint.Tests.PublicInterface;

/// <summary>
/// Source phase imports (<see href="https://tc39.es/proposal-source-phase-imports/">stage 3</see>) let script
/// reach a module's <c>[[ModuleSource]]</c> without linking or evaluating it. No ECMA-262 module kind has one,
/// so the slot is filled entirely by the host: a loader overriding <see cref="ModuleLoader.GetModuleSource"/>
/// is the whole extension point, WebAssembly being the motivating case. These pin both ways script reaches
/// that object, and what happens for the modules that have none.
/// </summary>
public class HostModuleSourcePhaseTests
{
    private sealed class SourceProvidingModuleLoader : ModuleLoader
    {
        private const string WithSource = "has-source";

        private readonly Dictionary<string, string> _contents = new(StringComparer.Ordinal)
        {
            [WithSource] = "export const x = 1;",
            ["plain"] = "export const y = 2;",
            ["main"] = "",
        };

        /// <summary>The object handed back as the <c>[[ModuleSource]]</c>, so a test can assert identity.</summary>
        internal ObjectInstance? Provided { get; private set; }

        internal void AddModule(string specifier, string contents) => _contents[specifier] = contents;

        public override ResolvedSpecifier Resolve(string? referencingModuleLocation, ModuleRequest moduleRequest)
            => new ResolvedSpecifier(moduleRequest, moduleRequest.Specifier, Uri: null, SpecifierType.Bare);

        protected override string LoadModuleContents(Engine engine, ResolvedSpecifier resolved)
            => _contents[resolved.ModuleRequest.Specifier];

        protected override ObjectInstance? GetModuleSource(Engine engine, ResolvedSpecifier resolved)
        {
            if (!string.Equals(resolved.ModuleRequest.Specifier, WithSource, StringComparison.Ordinal))
            {
                return null;
            }

            var source = new JsObject(engine);
            source.Set("kind", "host-source");
            Provided = source;
            return source;
        }
    }

    private static Engine CreateEngine(SourceProvidingModuleLoader loader)
        => new Engine(options => options.EnableModules(loader));

    [Fact]
    public void DynamicImportSourceResolvesWithTheHostSuppliedModuleSource()
    {
        // ContinueDynamicImport step 3: for the source phase the promise settles with module.[[ModuleSource]]
        // and the module is never linked or evaluated.
        var loader = new SourceProvidingModuleLoader();
        loader.AddModule("main", """
            globalThis.captured = null;
            globalThis.settled = null;
            import.source('has-source').then(
                s => { globalThis.settled = 'fulfilled'; globalThis.captured = s; },
                e => { globalThis.settled = 'rejected'; globalThis.captured = e; });
            """);

        var engine = CreateEngine(loader);
        engine.Modules.Import("main");

        engine.Evaluate("globalThis.settled").AsString().Should().Be("fulfilled");
        engine.Evaluate("globalThis.captured.kind").AsString().Should().Be("host-source");
        engine.Evaluate("globalThis.captured").Should().BeSameAs(loader.Provided);
    }

    [Fact]
    public void StaticImportSourceBindsTheHostSuppliedModuleSource()
    {
        // InitializeEnvironment step 7.c: `import source x from` binds x to importedModule.[[ModuleSource]].
        var loader = new SourceProvidingModuleLoader();
        loader.AddModule("main", """
            import source x from 'has-source';
            globalThis.captured = x;
            """);

        var engine = CreateEngine(loader);
        engine.Modules.Import("main");

        engine.Evaluate("globalThis.captured.kind").AsString().Should().Be("host-source");
        engine.Evaluate("globalThis.captured").Should().BeSameAs(loader.Provided);
    }

    [Fact]
    public void DynamicImportSourceOfAModuleWithoutASourceRejectsWithSyntaxError()
    {
        // ContinueDynamicImport step 3.b: "If moduleSource is empty ... a newly created SyntaxError object".
        var loader = new SourceProvidingModuleLoader();
        loader.AddModule("main", """
            globalThis.errorName = null;
            import.source('plain').then(
                () => { globalThis.errorName = 'fulfilled'; },
                e => { globalThis.errorName = e.constructor.name; });
            """);

        var engine = CreateEngine(loader);
        engine.Modules.Import("main");

        engine.Evaluate("globalThis.errorName").AsString().Should().Be("SyntaxError");
    }

    [Fact]
    public void StaticImportSourceOfAModuleWithoutASourceThrowsSyntaxError()
    {
        // InitializeEnvironment step 7.c.ii: "If moduleSourceObject is empty, throw a SyntaxError exception."
        var loader = new SourceProvidingModuleLoader();
        loader.AddModule("main", "import source x from 'plain';");

        var engine = CreateEngine(loader);

        var ex = Assert.Throws<JavaScriptException>(() => engine.Modules.Import("main"));
        ex.Error.Get("name").AsString().Should().Be("SyntaxError");
    }

    [Fact]
    public void SourcePhaseImportDoesNotEvaluateTheModule()
    {
        // The source phase stops before Link and Evaluate, so the module body never runs.
        var loader = new SourceProvidingModuleLoader();
        loader.AddModule("has-source", "globalThis.evaluated = true; export const x = 1;");
        loader.AddModule("main", """
            import source x from 'has-source';
            globalThis.captured = x;
            globalThis.wasEvaluated = globalThis.evaluated === true;
            """);

        var engine = CreateEngine(loader);
        engine.Modules.Import("main");

        engine.Evaluate("globalThis.wasEvaluated").AsBoolean().Should().BeFalse();
        engine.Evaluate("globalThis.captured").Should().BeSameAs(loader.Provided);
    }
}
