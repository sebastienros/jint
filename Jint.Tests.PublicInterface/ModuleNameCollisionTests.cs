using Jint.Runtime.Modules;

#nullable enable

namespace Jint.Tests.PublicInterface;

/// <summary>
/// The shape an embedder's file actually has: a module loader that caches Acornima's parsed
/// <see cref="Module"/> and hands Jint back a <see cref="ModuleRecord"/> — with no <c>using</c>
/// alias anywhere in the file.
/// </summary>
/// <remarks>
/// This file is the regression test for https://github.com/sebastienros/jint/issues/3311, and it
/// tests the compiler rather than the engine: while the record was called
/// <c>Jint.Runtime.Modules.Module</c>, the same source produced
/// <c>CS0104: 'Module' is an ambiguous reference between 'Acornima.Ast.Module' and
/// 'Jint.Runtime.Modules.Module'</c> on both the field and the <c>LoadModule</c> return type, and a
/// host had no way to write it but to alias one of the two. Nine files in this project — the only
/// suite without <c>InternalsVisibleTo</c>, and so the only one that sees what an integrator sees —
/// carried that alias. The assertions below merely keep the loader honest; the proof is that the
/// file compiles.
/// </remarks>
public class ModuleNameCollisionTests
{
    [Test]
    public void AHostCanNameBothModuleTypesWithoutAnAlias()
    {
        var loader = new PreparingModuleLoader();
        var engine = new Engine(options => options.UseModules(loader));

        var ns = engine.Modules.Import("main");

        ns.Get("value").AsNumber().Should().Be(42);
        loader.Last.Should().NotBeNull();
        loader.Last!.Location.Should().Be("main");
    }

    private sealed class PreparingModuleLoader : IModuleLoader
    {
        private readonly Dictionary<string, Prepared<Module>> _cache = new(StringComparer.Ordinal);

        public ModuleRecord? Last { get; private set; }

        public ResolvedSpecifier Resolve(string? referencingModuleLocation, ModuleRequest moduleRequest)
            => new ResolvedSpecifier(moduleRequest, moduleRequest.Specifier, null, SpecifierType.Bare);

        public ModuleRecord LoadModule(Engine engine, ResolvedSpecifier resolved)
        {
            if (!_cache.TryGetValue(resolved.Key, out var prepared))
            {
                prepared = Engine.PrepareModule("export const value = 42;", resolved.Key);
                _cache[resolved.Key] = prepared;
            }

            Last = ModuleFactory.BuildSourceTextModule(engine, prepared);
            return Last;
        }
    }
}
