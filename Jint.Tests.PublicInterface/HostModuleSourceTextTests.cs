#nullable enable

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Jint.Runtime.Modules;

namespace Jint.Tests.PublicInterface;

/// <summary>
/// <see cref="Options.RetainFunctionSourceText"/> reaching a module a host <see cref="IModuleLoader"/>
/// supplied — both halves of what it buys, <c>Function.prototype.toString</c> and
/// <see cref="Engine.AdvancedOperations.TryGetSourceText"/>.
/// </summary>
/// <remarks>
/// These live in the public-interface suite because the loader is the thing under test: this project has no
/// <c>InternalsVisibleTo</c> grant, so the loader below reaches <see cref="ModuleFactory"/>,
/// <see cref="ResolvedSpecifier"/> and <see cref="Engine.Debugger"/> exactly as an embedder's would, and a
/// green assertion here is proof that an embedder gets the retention it asked the engine for.
/// </remarks>
public class HostModuleSourceTextTests
{
    private const string Source = "export function greet(name) { return 'hi ' + name; }";

    /// <summary>
    /// The smallest loader an embedder can write: a dictionary of sources, handed to
    /// <see cref="ModuleFactory.BuildSourceTextModule(Engine, ResolvedSpecifier, string, ModuleParsingOptions)"/>
    /// without any parsing options of its own — which is the case under test, since that is where the
    /// engine's own settings have to apply.
    /// </summary>
    private sealed class DictionaryModuleLoader : IModuleLoader
    {
        private readonly IReadOnlyDictionary<string, string> _modules;
        private readonly ModuleParsingOptions? _parsingOptions;

        public DictionaryModuleLoader(IReadOnlyDictionary<string, string> modules, ModuleParsingOptions? parsingOptions = null)
        {
            _modules = modules;
            _parsingOptions = parsingOptions;
        }

        public ResolvedSpecifier Resolve(string? referencingModuleLocation, ModuleRequest moduleRequest)
            => new(moduleRequest, moduleRequest.Specifier, Uri: null, SpecifierType.Bare);

        public ModuleRecord LoadModule(Engine engine, ResolvedSpecifier resolved)
            => ModuleFactory.BuildSourceTextModule(engine, resolved, _modules[resolved.Key], _parsingOptions);
    }

    private static Engine Build(bool retain, ModuleParsingOptions? loaderOptions = null)
        => new(options =>
        {
            options.RetainFunctionSourceText = retain;
            options.UseModules(new DictionaryModuleLoader(
                new Dictionary<string, string> { ["lib"] = Source },
                loaderOptions));
        });

    /// <summary>
    /// The engine's setting is what a loader that named no options gets, so a function the module exports
    /// prints the source an embedder switched the option on to see.
    /// </summary>
    [Test]
    public void AModuleAHostLoaderSuppliedFollowsTheEnginesRetentionSetting()
    {
        var engine = Build(retain: true);

        engine.SetValue("greet", engine.Modules.Import("lib").Get("greet"));
        engine.Evaluate("greet.toString()").AsString().Should().Be("function greet(name) { return 'hi ' + name; }");
    }

    /// <summary>
    /// The other half of the same switch: the module's <c>Program</c> answers
    /// <see cref="Engine.AdvancedOperations.TryGetSourceText"/>, which is what a debugger's
    /// <c>getScriptSource</c> resolves a script's text through.
    /// </summary>
    [Test]
    public void TheProgramOfSuchAModuleAnswersTryGetSourceText()
    {
        var engine = Build(retain: true);

        Program? loaded = null;
        engine.Debugger.BeforeEvaluate += (_, ast) => loaded = ast;

        engine.Modules.Import("lib");

        loaded.Should().NotBeNull();
        engine.Advanced.TryGetSourceText(loaded!, out var sourceText).Should().BeTrue();
        sourceText.Should().Be(Source);
    }

    /// <summary>
    /// The default is unchanged, and it is the whole reason retention is opt-in: nothing is kept, and
    /// <c>toString</c> answers the placeholder.
    /// </summary>
    [Test]
    public void WithoutTheOptionNothingIsRetained()
    {
        var engine = Build(retain: false);

        Program? loaded = null;
        engine.Debugger.BeforeEvaluate += (_, ast) => loaded = ast;

        engine.SetValue("greet", engine.Modules.Import("lib").Get("greet"));
        engine.Evaluate("greet.toString()").AsString().Should().Be("function greet() { [native code] }");

        loaded.Should().NotBeNull();
        engine.Advanced.TryGetSourceText(loaded!, out var sourceText).Should().BeFalse();
        sourceText.Should().BeNull();
    }

    /// <summary>
    /// The asynchronous loader path builds its record somewhere else entirely
    /// (<see cref="ModuleLoadCompletion"/>, not <see cref="ModuleFactory"/>), so it is asserted separately:
    /// which of the two a host implements must not decide what its modules retain.
    /// </summary>
    [Test]
    public void AnAsynchronousLoaderRetainsTheSameSourceText()
    {
        var engine = new Engine(options =>
        {
            options.RetainFunctionSourceText = true;
            options.UseModules(new TaskModuleLoader());
        });

        engine.SetValue("greet", engine.Modules.Import("lib").Get("greet"));
        engine.Evaluate("greet.toString()").AsString().Should().Be("function greet(name) { return 'hi ' + name; }");
    }

    /// <summary>
    /// The asynchronous sibling of <see cref="DictionaryModuleLoader"/>, answering with an already-completed
    /// task — the warm-cache shape, which settles inside the load call and so never leaves the engine thread.
    /// </summary>
    private sealed class TaskModuleLoader : AsyncModuleLoader
    {
        public override ResolvedSpecifier Resolve(string? referencingModuleLocation, ModuleRequest moduleRequest)
            => new(moduleRequest, moduleRequest.Specifier, Uri: null, SpecifierType.Bare);

        protected override Task<string> LoadModuleContentsAsync(
            Engine engine,
            ResolvedSpecifier resolved,
            CancellationToken cancellationToken)
            => Task.FromResult(Source);
    }

    /// <summary>
    /// A loader that names its own <see cref="ModuleParsingOptions"/> still decides for itself, in both
    /// directions — the engine's setting is the default for a loader that named none, not an override of one
    /// that did.
    /// </summary>
    [Test]
    public void ALoaderThatNamesItsOwnOptionsStillDecides()
    {
        var retainingLoaderOnAPlainEngine = Build(
            retain: false,
            loaderOptions: new ModuleParsingOptions { RetainFunctionSourceText = true });

        retainingLoaderOnAPlainEngine.SetValue("greet", retainingLoaderOnAPlainEngine.Modules.Import("lib").Get("greet"));
        retainingLoaderOnAPlainEngine.Evaluate("greet.toString()").AsString()
            .Should().Be("function greet(name) { return 'hi ' + name; }");

        var plainLoaderOnARetainingEngine = Build(
            retain: true,
            loaderOptions: new ModuleParsingOptions { RetainFunctionSourceText = false });

        plainLoaderOnARetainingEngine.SetValue("greet", plainLoaderOnARetainingEngine.Modules.Import("lib").Get("greet"));
        plainLoaderOnARetainingEngine.Evaluate("greet.toString()").AsString()
            .Should().Be("function greet() { [native code] }");
    }
}
