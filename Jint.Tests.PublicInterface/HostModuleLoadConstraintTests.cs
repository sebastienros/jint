#nullable enable

using System;
using Jint.Runtime;
using Jint.Runtime.Modules;

using Module = Jint.Runtime.Modules.Module;

namespace Jint.Tests.PublicInterface;

/// <summary>
/// A module load can fail in two very different ways, and the module pipeline deliberately treats them
/// differently. A load that failed — the file is missing, the loader refused the specifier, the source did
/// not parse — becomes a rejection of the importing promise, because there is often no caller left to throw
/// to. A failure that exists to <em>bound</em> execution must not: a constraint that turns into a rejection
/// no longer bounds anything, because script catches it and carries on, in a loop if it likes.
///
/// <para>
/// The loads that hit this are the ones where the loader itself re-enters the engine — a resolve hook or a
/// virtual file system implemented in script, which is a shape hosts really do use.
/// </para>
/// </summary>
public class HostModuleLoadConstraintTests
{
    private const string DeepRecursion = """
        globalThis.deep = function deep(n) { return 1 + deep(n + 1); };
        """;

    /// <summary>A loader that asks script for the module source, the JS-implemented virtual file system.</summary>
    private sealed class ScriptSourcedModuleLoader : IAsyncModuleLoader
    {
        private readonly Action<Engine>? _beforeSource;

        public ScriptSourcedModuleLoader(Action<Engine>? beforeSource = null) => _beforeSource = beforeSource;

        public ResolvedSpecifier Resolve(string? referencingModuleLocation, ModuleRequest moduleRequest)
            => new(moduleRequest, moduleRequest.Specifier, Uri: null, SpecifierType.Bare);

        public Module LoadModule(Engine engine, ResolvedSpecifier resolved)
            => throw new InvalidOperationException("The engine must not take the synchronous path for an IAsyncModuleLoader.");

        public void LoadModuleAsync(Engine engine, ResolvedSpecifier resolved, ModuleLoadCompletion completion)
        {
            _beforeSource?.Invoke(engine);
            completion.SetSource("export const v = 1;");
        }
    }

    /// <summary>A loader whose resolve hook is a script function — an import map written in JavaScript.</summary>
    private sealed class ScriptResolvingModuleLoader : ModuleLoader
    {
        public Engine Engine { get; set; } = null!;

        public override ResolvedSpecifier Resolve(string? referencingModuleLocation, ModuleRequest moduleRequest)
        {
            var key = Engine.Invoke("resolveModule", moduleRequest.Specifier).AsString();
            return new(moduleRequest, key, Uri: null, SpecifierType.Bare);
        }

        protected override string LoadModuleContents(Engine engine, ResolvedSpecifier resolved) => "export const v = 1;";
    }

    [Fact]
    public void ARecursionLimitReachedInsideAnAsynchronousLoaderIsNotFlattenedIntoARejection()
    {
        var engine = new Engine(options =>
        {
            options.Constraints.MaxRecursionDepth = 20;
            options.UseModules(new ScriptSourcedModuleLoader(e => e.Invoke("deep", 0)));
        });

        engine.Execute(DeepRecursion + "globalThis.caught = 'none';");

        Invoking(() => engine.Execute("import('m').catch(e => { globalThis.caught = String(e); });"))
            .Should().Throw<RecursionDepthOverflowException>("a recursion limit that script can catch bounds nothing");

        engine.Evaluate("globalThis.caught").AsString().Should().Be("none");
    }

    [Fact]
    public void ARecursionLimitReachedInsideResolveIsNotFlattenedIntoAFailedOperation()
    {
        var loader = new ScriptResolvingModuleLoader();
        var engine = new Engine(options =>
        {
            options.Constraints.MaxRecursionDepth = 20;
            options.UseModules(loader);
        });
        loader.Engine = engine;

        engine.Execute(DeepRecursion + "globalThis.resolveModule = function (s) { return deep(0); };");

        Invoking(() => engine.Modules.StartImport("m")).Should().Throw<RecursionDepthOverflowException>();
    }

    [Fact]
    public void AnOrdinaryLoaderFailureStillBecomesARejection()
    {
        var engine = new Engine(options =>
        {
            options.Constraints.MaxRecursionDepth = 20;
            options.UseModules(new ScriptSourcedModuleLoader(_ => throw new InvalidOperationException("the asset pipeline is down")));
        });

        var operation = engine.Modules.StartImport("m");
        engine.Advanced.ProcessTasks();

        operation.IsCompleted.Should().BeTrue();
        operation.IsFaulted.Should().BeTrue();
        operation.Error!.Get("message").AsString().Should().Be("Could not load module.");
        var rejection = Invoking(() => operation.GetResult()).Should().Throw<PromiseRejectedException>().Which;
        JintException.TryGetClrException(rejection, out var clrException).Should().BeTrue();
        clrException.Should().BeOfType<InvalidOperationException>().Which.Message.Should().Be("the asset pipeline is down");
    }
}
