using System.Diagnostics.CodeAnalysis;
using Jint.Native;
using Jint.Native.Object;
using Jint.Native.Promise;
using Jint.Runtime;
using Jint.Runtime.Interpreter;
using Jint.Runtime.Modules;
using Module = Jint.Runtime.Modules.Module;

namespace Jint;

public partial class Engine
{
    public ModuleOperations Modules { get; internal set; } = null!;

    /// <summary>
    /// https://tc39.es/ecma262/#sec-getactivescriptormodule
    /// </summary>
    internal IScriptOrModule? GetActiveScriptOrModule()
    {
        return _executionContexts?.GetActiveScriptOrModule();
    }

    // Cache key for the per-engine module map. Per the import-attributes spec,
    // two requests for the same resolved specifier with different attributes
    // are distinct module records. Import phase (defer/source) does NOT create
    // distinct records — defer and evaluation share the same underlying module.
    internal readonly record struct ModuleCacheKey(string Key, ModuleImportAttribute[] Attributes)
    {
        public static ModuleCacheKey From(ResolvedSpecifier resolved)
            => new(resolved.Key, resolved.ModuleRequest.Attributes ?? []);

        public bool Equals(ModuleCacheKey other)
        {
            if (!string.Equals(Key, other.Key, StringComparison.Ordinal))
            {
                return false;
            }
            var a = Attributes;
            var b = other.Attributes;
            if (a.Length != b.Length)
            {
                return false;
            }
            for (var i = 0; i < a.Length; i++)
            {
                if (Array.IndexOf(b, a[i]) < 0)
                {
                    return false;
                }
            }
            return true;
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (StringComparer.Ordinal.GetHashCode(Key) * 397) ^ Attributes.Length;
            }
        }
    }

    public class ModuleOperations
    {
        private readonly Engine _engine;
        private readonly Dictionary<ModuleCacheKey, Module> _modules = new();
        private readonly Dictionary<string, ModuleBuilder> _builders = new(StringComparer.Ordinal);

        /// <summary>
        /// Resolved key to the registration name it was filed under, for the registrations whose two names
        /// differ. Built lazily by <see cref="IndexBuilderKeys"/>; null while no registration needed an entry,
        /// which is every engine that registers under names its loader leaves alone.
        /// </summary>
        private Dictionary<string, string>? _builderKeys;

        /// <summary>
        /// Registration names already put through the loader, including the ones it refused, so neither is
        /// resolved twice.
        /// </summary>
        private HashSet<string>? _indexedBuilders;

        private int _buildersVersion;
        private int _indexedBuildersVersion;

        public ModuleOperations(Engine engine, IModuleLoader moduleLoader)
        {
            ModuleLoader = moduleLoader;
            _engine = engine;
        }

        internal IModuleLoader ModuleLoader { get; }

        internal Module Load(string? referencingModuleLocation, ModuleRequest request)
        {
            var moduleResolution = ModuleLoader.Resolve(referencingModuleLocation, request);
            var cacheKey = ModuleCacheKey.From(moduleResolution);

            if (_modules.TryGetValue(cacheKey, out var module))
            {
                return module;
            }

            if (TryGetBuilder(moduleResolution, out var builderSpecifier, out var moduleBuilder))
            {
                module = LoadFromBuilder(builderSpecifier, moduleBuilder, moduleResolution, cacheKey);
            }
            else
            {
                module = LoadFromModuleLoader(moduleResolution, cacheKey);
            }

            if (module is SourceTextModule sourceTextModule)
            {
                _engine.Debugger.OnBeforeEvaluate(sourceTextModule._source);
            }

            return module;
        }

        /// <summary>
        /// Finds the builder registered for a module. <see cref="Add(string,ModuleBuilder)"/> files one under
        /// whatever name the host passed it, which is not necessarily the key the module loader resolves that
        /// name to - any loader that canonicalizes urls spells <c>http://host</c> as <c>http://host/</c> - and
        /// the resolved key is what the engine identifies a module by everywhere else. Consulting only the
        /// resolved key silently replaced the source the host supplied with a load from disk or the network.
        /// </summary>
        /// <remarks>
        /// The registration is therefore put through the loader once and indexed under the key it resolves to,
        /// so a builder has exactly one identity. Matching the raw <see cref="ModuleRequest.Specifier"/>
        /// instead would not work: that text is the import as written, which for a relative specifier is
        /// relative to the <em>referencing</em> module and so names no module at all. Two files importing
        /// <c>'./shared.js'</c> from different directories would both match one registration, and since
        /// <see cref="LoadFromBuilder"/> consumes it, whichever the graph walk reached first would win and the
        /// other would fail to resolve.
        /// </remarks>
        private bool TryGetBuilder(
            ResolvedSpecifier moduleResolution,
            [NotNullWhen(true)] out string? specifier,
            [NotNullWhen(true)] out ModuleBuilder? moduleBuilder)
        {
            var key = moduleResolution.Key;
            if (_builders.TryGetValue(key, out moduleBuilder))
            {
                // Registered under the name the loader resolves to, so there is nothing to index.
                specifier = key;
                return true;
            }

            IndexBuilderKeys();

            if (_builderKeys is not null
                && _builderKeys.TryGetValue(key, out var registered)
                && _builders.TryGetValue(registered, out moduleBuilder))
            {
                specifier = registered;
                return true;
            }

            specifier = null;
            moduleBuilder = null;
            return false;
        }

        /// <summary>
        /// Resolves each registration that has not been resolved yet and indexes it under the resulting key.
        /// Lazy rather than done in <see cref="Add(string,ModuleBuilder)"/> so that registering a module stays
        /// independent of the loader: <c>Add</c> keeps working before <c>EnableModules</c>, and cannot start
        /// throwing for a specifier the loader refuses.
        /// </summary>
        private void IndexBuilderKeys()
        {
            if (_builders.Count == 0 || _indexedBuildersVersion == _buildersVersion)
            {
                return;
            }

            _indexedBuildersVersion = _buildersVersion;

            List<string>? pending = null;
            foreach (var specifier in _builders.Keys)
            {
                if (_indexedBuilders?.Contains(specifier) != true)
                {
                    (pending ??= []).Add(specifier);
                }
            }

            if (pending is null)
            {
                return;
            }

            foreach (var specifier in pending)
            {
                // Marked before resolving, so a specifier the loader refuses is attempted once rather than on
                // every load.
                (_indexedBuilders ??= new HashSet<string>(StringComparer.Ordinal)).Add(specifier);

                string resolvedKey;
                try
                {
                    // A registration is a top-level name, so it is resolved the way Modules.Import resolves
                    // one: against no referrer.
                    resolvedKey = ModuleLoader.Resolve(referencingModuleLocation: null, new ModuleRequest(specifier, [])).Key;
                }
#pragma warning disable CA1031 // a loader may signal "I will not resolve that" with any exception it likes
                catch
#pragma warning restore CA1031
                {
                    // A loader is free to reject a specifier outright - DefaultModuleLoader throws for a
                    // directory import, an unauthorized path or an invalid specifier. Such a registration is
                    // simply left unindexed and stays reachable only under its own name, which is exactly as
                    // reachable as it was before this index existed. Letting the exception out would instead
                    // fail an unrelated import that merely happened to run the indexing pass.
                    continue;
                }

                if (!string.Equals(resolvedKey, specifier, StringComparison.Ordinal))
                {
                    // Last registration wins, matching Add's own behaviour once a builder has been consumed.
                    (_builderKeys ??= new Dictionary<string, string>(StringComparer.Ordinal))[resolvedKey] = specifier;
                }
            }
        }

        private BuilderModule LoadFromBuilder(string specifier, ModuleBuilder moduleBuilder, ResolvedSpecifier moduleResolution, ModuleCacheKey cacheKey)
        {
            // The module is named by the key it resolved to rather than by the name it was registered under.
            // Those differ exactly when the loader canonicalized, and the location is what the module's own
            // relative imports are resolved against - so keeping the registration name would leave a builder
            // module reached through the index resolving its nested imports against a name the loader never
            // produced.
            var parsedModule = moduleBuilder.Parse(moduleResolution.Key);
            var hasTopLevelAwait = HoistingScope.HasTopLevelAwait(parsedModule.Program!);
            var module = new BuilderModule(_engine, _engine.Realm, in parsedModule, location: parsedModule.Program!.Location.SourceFile, async: hasTopLevelAwait);
            _modules[cacheKey] = module;
            moduleBuilder.BindExportedValues(module);
            _builders.Remove(specifier);
            return module;
        }

        private Module LoadFromModuleLoader(ResolvedSpecifier moduleResolution, ModuleCacheKey cacheKey)
        {
            var module = ModuleLoader.LoadModule(_engine, moduleResolution);
            _modules[cacheKey] = module;
            return module;
        }

        /// <summary>
        /// Registers a module built from <paramref name="code"/> under <paramref name="specifier"/>.
        /// </summary>
        /// <inheritdoc cref="Add(string,ModuleBuilder)" path="/remarks"/>
        public void Add(string specifier, string code)
        {
            var moduleBuilder = new ModuleBuilder(_engine, specifier);
            moduleBuilder.AddSource(code);
            Add(specifier, moduleBuilder);
        }

        /// <summary>
        /// Registers a module assembled by <paramref name="buildModule"/> under <paramref name="specifier"/>.
        /// </summary>
        /// <inheritdoc cref="Add(string,ModuleBuilder)" path="/remarks"/>
        public void Add(string specifier, Action<ModuleBuilder> buildModule)
        {
            var moduleBuilder = new ModuleBuilder(_engine, specifier);
            buildModule(moduleBuilder);
            Add(specifier, moduleBuilder);
        }

        /// <summary>
        /// Registers <paramref name="moduleBuilder"/> under <paramref name="specifier"/>.
        /// </summary>
        /// <remarks>
        /// <para>
        /// A registration is identified by <paramref name="specifier"/> <em>as the module loader resolves it</em>,
        /// not by the string itself, because that resolved key is what the engine identifies every module by. So
        /// a module registered as <c>http://localhost</c> against a loader that canonicalizes urls is also found
        /// by <c>import 'http://localhost/'</c>, and a module registered as <c>./config.js</c> is found by the
        /// imports that resolve to the same file - not by every import that merely spells <c>'./config.js'</c>,
        /// which for a relative specifier means a different file in each importing directory.
        /// </para>
        /// <para>
        /// Resolution happens on first use rather than here, so registering a module neither requires
        /// <see cref="OptionsExtensions.EnableModules(Options,string,bool)"/> nor fails for a specifier the
        /// loader would reject. A registration the loader refuses to resolve stays reachable only under the
        /// exact string it was registered with.
        /// </para>
        /// <para>
        /// A registration is consumed the first time it is loaded; the resulting module is cached, and the same
        /// specifier may be registered again afterwards.
        /// </para>
        /// </remarks>
        public void Add(string specifier, ModuleBuilder moduleBuilder)
        {
            _builders.Add(specifier, moduleBuilder);
            _buildersVersion++;
        }

        public ObjectInstance Import(string specifier)
        {
            return Import(specifier, referencingModuleLocation: null);
        }

        internal ObjectInstance Import(string specifier, string? referencingModuleLocation)
        {
            return Import(new ModuleRequest(specifier, []), referencingModuleLocation);
        }

        internal ObjectInstance Import(ModuleRequest request, string? referencingModuleLocation)
        {
            var moduleResolution = ModuleLoader.Resolve(referencingModuleLocation, request);

            if (!_modules.TryGetValue(ModuleCacheKey.From(moduleResolution), out var module))
            {
                module = Load(referencingModuleLocation, request);
            }

            if (module is not CyclicModule cyclicModule)
            {
                LinkModule(request.Specifier, module);
                EvaluateModule(request.Specifier, module);
            }
            else if (cyclicModule.Status == ModuleStatus.Unlinked)
            {
                LinkModule(request.Specifier, cyclicModule);

                if (cyclicModule.Status == ModuleStatus.Linked)
                {
                    _engine.ExecuteWithConstraints(true, () => EvaluateModule(request.Specifier, cyclicModule));
                }

                if (cyclicModule.Status != ModuleStatus.Evaluated)
                {
                    Throw.NotSupportedException($"Error while evaluating module: Module is in an invalid state: '{cyclicModule.Status}'");
                }
            }
            else if (cyclicModule.Status == ModuleStatus.Evaluated)
            {
                // The module has already been evaluated - either as its own entry point or as a
                // dependency of some other graph. https://tc39.es/ecma262/#sec-ContinueDynamicImport
                // evaluates it again regardless, and https://tc39.es/ecma262/#sec-moduleevaluation
                // makes that a no-op that hands back the already settled promise, with one exception:
                // a module whose evaluation threw replays its recorded [[EvaluationError]]. Skipping
                // the call returned a namespace over bindings the failed evaluation never initialized.
                _engine.ExecuteWithConstraints(true, () => EvaluateModule(request.Specifier, cyclicModule));
            }

            _engine.RunAvailableContinuations();

            return Module.GetModuleNamespace(module);
        }

        private static void LinkModule(string specifier, Module module)
        {
            module.Link();
        }

        private JsValue EvaluateModule(string specifier, Module module)
        {
            // Brackets the host entry the same way ExecuteWithConstraints does: Import reaches this
            // outside that method for a non-cyclic module, so it is an entry in its own right.
            _engine._hostEntryDepth++;
            JsValue evaluationResult;
            try
            {
                evaluationResult = module.Evaluate();
            }
            finally
            {
                _engine._hostEntryDepth--;
            }

            // This should instead be returned and resolved in ImportModule(specifier) only so Host.ImportModuleDynamically can use this promise
            if (evaluationResult is not JsPromise promise)
            {
                Throw.InvalidOperationException($"Error while evaluating module: Module evaluation did not return a promise: {evaluationResult.Type}");
                return null;
            }

            // For async modules (top-level await), drive the event loop until the module's top-level
            // capability promise settles. A .NET Task awaited at the top level (task interop) completes
            // on a ThreadPool thread and enqueues its resolve job only after a delay, so we must poll
            // rather than spin — a tight loop finishes in microseconds while the queue is still empty
            // and gives up before the Task ever completes (issue #2663). Bounded by PromiseTimeout so a
            // genuinely never-settling top-level await cannot block Import forever.
            _engine.DrainEventLoopUntilSettled(promise, _engine.Options.Constraints.PromiseTimeout);

            if (promise.State == PromiseState.Rejected)
            {
                var location = module is CyclicModule cyclicModuleRecord
                    ? cyclicModuleRecord.AbnormalCompletionLocation
                    : SourceLocation.From(new Position(), new Position());

                var node = AstExtensions.CreateLocationNode(in location);
                Throw.JavaScriptException(_engine, promise.Value, node.Location);
            }
            else if (promise.State != PromiseState.Fulfilled)
            {
                Throw.InvalidOperationException($"Error while evaluating module: Module evaluation did not return a fulfilled promise: {promise.State}");
            }

            return evaluationResult;
        }
    }
}
