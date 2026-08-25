using Jint.Runtime;
using Jint.Runtime.Modules;

namespace Jint.NodeCompat;

/// <summary>
/// Wraps the engine's configured module loader so that <c>node:</c> specifiers resolve to Jint's builtin
/// modules and everything else is answered exactly as before.
/// </summary>
/// <remarks>
/// <para>
/// A decorator rather than a loader of its own, because a host that wants the builtins almost always also
/// wants a real loader underneath: <see cref="NodeStyleModuleLoader"/> reading a <c>node_modules</c> tree is
/// the whole point of the exercise. Nothing about the inner loader changes — its base path, its conditions,
/// its refusals — and a specifier that is not a builtin never reaches this class's own code at all.
/// </para>
/// <para>
/// <b>A host registration wins.</b> The engine consults <c>Engine.Modules.Add</c>'s registrations before it
/// asks a loader to load anything, so a module the host registered under <c>node:path</c> — or under
/// <c>path</c>, which resolves to the same key — is what an import of either spelling gets. That is the
/// non-clobbering posture the <c>process</c> shim and the web APIs take, arrived at here by leaving the
/// precedence the module system already has alone rather than by checking anything.
/// </para>
/// <para>
/// <b>An unknown <c>node:</c> specifier is deferred, not refused, at resolution time.</b> It resolves to
/// itself as a bare specifier, so a host that supplies its own <c>node:fs</c> is found; only when nothing is
/// registered under the name does the load fail, with a message naming the modules that do exist. Resolution
/// therefore stays a mapping rather than a veto, which is what <see cref="IModuleLoader.Resolve"/> documents
/// it to be.
/// </para>
/// </remarks>
internal sealed class NodeBuiltinModuleLoader : IModuleLoader
{
    private readonly IModuleLoader _inner;
    private readonly NodeBuiltinModuleConfiguration _configuration;

    private NodeBuiltinModuleLoader(IModuleLoader inner, NodeBuiltinModuleConfiguration configuration)
    {
        _inner = inner;
        _configuration = configuration;
    }

    /// <summary>
    /// Wraps <paramref name="inner"/>, keeping whichever faces it has.
    /// </summary>
    /// <remarks>
    /// An <see cref="IAsyncModuleLoader"/> has to stay one: the engine switches onto the asynchronous load
    /// path by testing the registered loader for that interface, and a decorator that dropped it would turn
    /// every fetch into a blocking one. Implementing it unconditionally is not an option either — a loader
    /// that is <em>not</em> asynchronous must not be driven as though it were, because that changes which
    /// failures become promise rejections rather than exceptions on the caller's thread.
    /// </remarks>
    internal static IModuleLoader Wrap(IModuleLoader inner, NodeBuiltinModuleConfiguration configuration)
    {
        var loader = new NodeBuiltinModuleLoader(inner, configuration);

        return inner is IAsyncModuleLoader asyncInner
            ? new AsyncLoader(loader, asyncInner)
            : loader;
    }

    /// <inheritdoc />
    public ResolvedSpecifier Resolve(string? referencingModuleLocation, ModuleRequest moduleRequest)
    {
        var specifier = moduleRequest.Specifier;
        if (!string.IsNullOrEmpty(specifier))
        {
            if (NodeBuiltinModules.TryCanonicalize(specifier, _configuration.AllowUnprefixedSpecifiers, out var canonical))
            {
                // Both spellings land on the one `node:` key, so `import 'path'` and `import 'node:path'`
                // denote one module record - which is also what makes a host registration under either name
                // claim both.
                return new ResolvedSpecifier(moduleRequest, canonical!, Uri: null, SpecifierType.Bare);
            }

            if (NodeBuiltinModules.IsNodeScheme(specifier))
            {
                return new ResolvedSpecifier(moduleRequest, specifier, Uri: null, SpecifierType.Bare);
            }
        }

        return _inner.Resolve(referencingModuleLocation, moduleRequest);
    }

    /// <inheritdoc />
    public ModuleRecord LoadModule(Engine engine, ResolvedSpecifier resolved)
    {
        if (TryLoadBuiltin(engine, resolved, out var module))
        {
            return module!;
        }

        return _inner.LoadModule(engine, resolved);
    }

    /// <summary>
    /// Builds a builtin, or reports an unknown <c>node:</c> name. Answers <see langword="false"/> for
    /// everything else, which is what the inner loader is for.
    /// </summary>
    private bool TryLoadBuiltin(Engine engine, ResolvedSpecifier resolved, out ModuleRecord? module)
    {
        var key = resolved.Key;

        // The key is already canonical: Resolve is the only thing that produces one.
        if (NodeBuiltinModules.TryCanonicalize(key, allowUnprefixed: false, out var canonical))
        {
            module = Build(engine, canonical!);
            return true;
        }

        if (NodeBuiltinModules.IsNodeScheme(key))
        {
            Throw.ModuleResolutionException(
                $"Unknown Node Builtin Module: Jint provides {NodeBuiltinModules.AvailableNames}, and no module is registered under '{key}' with {nameof(Engine)}.{nameof(Engine.Modules)}.{nameof(Engine.ModuleOperations.Add)}(). Node modules that need platform resources - node:fs, node:buffer, node:crypto, node:os, node:child_process and the rest - are deliberately not provided.",
                resolved.ModuleRequest.Specifier,
                parent: null);
        }

        module = null;
        return false;
    }

    /// <summary>
    /// Assembles one builtin as a module with named exports plus a <c>default</c>, which is what
    /// <c>Engine.Modules.Add</c> produces for an exports-only registration - the same record type, built
    /// directly so that nothing has to be registered up front and a module nobody imports costs nothing.
    /// </summary>
    private BuilderModuleRecord Build(Engine engine, string canonicalName)
    {
        var builder = new ModuleBuilder(engine, canonicalName);

        var exports = NodeBuiltinModules.CreateExports(engine, _configuration, canonicalName);
        for (var i = 0; i < exports.Count; i++)
        {
            builder.ExportValue(exports[i].Key, exports[i].Value);
        }

        var parsed = builder.Parse(canonicalName);
        var module = new BuilderModuleRecord(engine, engine.Realm, in parsed, canonicalName, async: false);
        builder.BindExportedValues(module);
        return module;
    }

    /// <summary>
    /// The same decorator for an inner loader that is also an <see cref="IAsyncModuleLoader"/>.
    /// </summary>
    /// <remarks>
    /// A separate type rather than a flag, because the interface a loader implements is what the engine keys
    /// its whole load path on: it cannot be decided per call. A builtin settles the completion on the stack it
    /// was asked on, inside the window <c>ModuleLoadCompletion</c> opens for exactly that, so importing
    /// <c>node:path</c> from an engine with an asynchronous loader touches the event loop no more than it
    /// would with a synchronous one.
    /// </remarks>
    private sealed class AsyncLoader : IModuleLoader, IAsyncModuleLoader
    {
        private readonly NodeBuiltinModuleLoader _builtins;
        private readonly IAsyncModuleLoader _inner;

        public AsyncLoader(NodeBuiltinModuleLoader builtins, IAsyncModuleLoader inner)
        {
            _builtins = builtins;
            _inner = inner;
        }

        /// <inheritdoc />
        public ResolvedSpecifier Resolve(string? referencingModuleLocation, ModuleRequest moduleRequest)
            => _builtins.Resolve(referencingModuleLocation, moduleRequest);

        /// <inheritdoc />
        public ModuleRecord LoadModule(Engine engine, ResolvedSpecifier resolved) => _builtins.LoadModule(engine, resolved);

        /// <inheritdoc />
        public void LoadModuleAsync(Engine engine, ResolvedSpecifier resolved, ModuleLoadCompletion completion)
        {
            if (_builtins.TryLoadBuiltin(engine, resolved, out var module))
            {
                completion.SetModule(module!);
                return;
            }

            _inner.LoadModuleAsync(engine, resolved, completion);
        }
    }
}
