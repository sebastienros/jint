using System.Diagnostics.CodeAnalysis;
using System.Runtime.ExceptionServices;
using System.Threading;
using Jint.Native;
using Jint.Native.Error;
using Jint.Native.Object;
using Jint.Native.Promise;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;
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
            return string.Equals(Key, other.Key, StringComparison.Ordinal)
                   && ModuleRequest.AttributesEqual(Attributes, other.Attributes);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (StringComparer.Ordinal.GetHashCode(Key) * 397) ^ Attributes.Length;
            }
        }
    }

    /// <summary>
    /// Keys the <c>[[LoadedModules]]</c> list of a referrer that is not itself a module — a script, or the
    /// host calling <see cref="ModuleOperations.Import(string)"/>. Such a referrer has nowhere to keep the
    /// list, so the engine keeps it, and the location is part of the key because two scripts at different
    /// locations may resolve one specifier to two different modules.
    /// </summary>
    internal readonly record struct ScriptLoadedModuleKey(string? ReferrerLocation, ModuleRequest Request)
    {
        public bool Equals(ScriptLoadedModuleKey other)
            => string.Equals(ReferrerLocation, other.ReferrerLocation, StringComparison.Ordinal)
               && LoadedModuleRequestComparer.Instance.Equals(Request, other.Request);

        public override int GetHashCode()
        {
            unchecked
            {
                var locationHash = ReferrerLocation is null ? 0 : StringComparer.Ordinal.GetHashCode(ReferrerLocation);
                return (locationHash * 397) ^ LoadedModuleRequestComparer.Instance.GetHashCode(Request);
            }
        }
    }

    public class ModuleOperations
    {
        private readonly Engine _engine;
        private readonly Dictionary<ModuleCacheKey, Module> _modules = new();
        private readonly Dictionary<string, ModuleBuilder> _builders = new(StringComparer.Ordinal);
        private readonly Dictionary<ScriptLoadedModuleKey, Module> _scriptLoadedModules = new();

        /// <summary>
        /// The loads an asynchronous loader has started but not finished, keyed by resolved specifier. Two
        /// referrers importing the same file — the ordinary diamond — must not become two fetches, so the
        /// second one attaches to the first load's completion instead of asking the loader again.
        /// </summary>
        private Dictionary<ModuleCacheKey, ModuleLoadCompletion>? _pendingLoads;

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

        /// <summary>
        /// The most recent exception a module-load failure was reduced from, kept alongside the error value
        /// it was reduced to. The load pipeline hands failures around as <see cref="JsValue"/>s — a promise
        /// rejection can carry nothing else — but the blocking import paths turn the rejection back into a
        /// throw, and rebuilding a <see cref="JavaScriptException"/> from the value alone loses the original
        /// location (a syntax error's file/line/column), the .NET stack and <see cref="Exception.Data"/>.
        /// Matched strictly by the error value's reference identity, so a stale entry can never fire for an
        /// unrelated failure; it is simply overwritten by the next one.
        /// </summary>
        private JsValue? _lastLoadFailureError;
        private ExceptionDispatchInfo? _lastLoadFailure;

        private object _moduleErrorPolicyToken = new();
        private bool _moduleErrorPolicyInitialized;
        private bool _moduleErrorPolicyExposeDetails;
        private Options.ClrExceptionErrorDecoratorDelegate? _moduleErrorPolicyDecorator;

        // --- Module graph limit accounting ---

        /// <summary>Cumulative count of distinct module records registered in this engine.</summary>
        private int _registeredModuleCount;

        /// <summary>Cumulative total UTF-8 source bytes of all registered modules.</summary>
        private long _totalModuleSourceBytes;

        internal ModuleOperations(Engine engine, IModuleLoader moduleLoader)
        {
            ModuleLoader = moduleLoader;
            AsyncModuleLoader = moduleLoader as IAsyncModuleLoader;
            _engine = engine;
        }

        internal IModuleLoader ModuleLoader { get; }

        /// <summary>
        /// The registered loader's asynchronous face, or null when it only loads synchronously. Non-null is
        /// what switches the engine onto the asynchronous load path.
        /// </summary>
        internal IAsyncModuleLoader? AsyncModuleLoader { get; }

        internal Engine Engine => _engine;

        /// <summary>
        /// Module records this engine has loaded, for
        /// <see cref="AdvancedOperations.GetMemoryReport(int)"/>. Deliberately the module map and not the
        /// builder registrations: a registration that nothing imported has produced no module and retains
        /// only what the host handed it.
        /// </summary>
        internal int RegisteredModuleCount => _modules.Count;

        /// <summary>
        /// Loads an asynchronous loader has started and not yet finished, for
        /// <see cref="AdvancedOperations.GetMemoryReport(int)"/>.
        /// </summary>
        internal int PendingModuleLoadCount => _pendingLoads?.Count ?? 0;

        // --- Guarded resolution and registration ---

        /// <summary>
        /// Resolves a module request through the loader, enforcing policy and consuming one resolution hop.
        /// All import-driven resolution call sites must go through this method so policy cannot be bypassed.
        /// </summary>
        private ResolvedSpecifier GuardedResolve(
            string? referrerLocation,
            ModuleRequest request,
            ModuleLoadBudget budget)
        {
            budget.ConsumeResolutionHop(request.Specifier);

            var resolved = ModuleLoader.Resolve(referrerLocation, request);

            var policy = _engine.Options.Modules.LoadPolicy;
            if (policy is not null && !policy.AllowLoad(referrerLocation, request, resolved))
            {
                Throw.ModuleResolutionException("Module load denied by policy", request.Specifier, referrerLocation);
            }

            return resolved;
        }

        /// <summary>
        /// Resolves for registration indexing. Applies policy but does NOT consume a resolution hop, so
        /// accumulated registrations do not exhaust a per-operation budget on pooled engines.
        /// </summary>
        private ResolvedSpecifier? GuardedResolveForIndex(string specifier)
        {
            var resolved = ModuleLoader.Resolve(referencingModuleLocation: null, new ModuleRequest(specifier, []));

            var policy = _engine.Options.Modules.LoadPolicy;
            if (policy is not null && !policy.AllowLoad(null, new ModuleRequest(specifier, []), resolved))
            {
                return null; // Silently skip indexing for denied specifiers.
            }

            return resolved;
        }

        /// <summary>
        /// Checks module count and source byte limits, then registers the module.
        /// </summary>
        internal void EnsureModuleRegistrationAllowed(
            ModuleCacheKey cacheKey,
            long sourceByteCount)
        {
            var opts = _engine.Options.Modules;

            if (opts.MaxModuleCount != int.MaxValue && _registeredModuleCount >= opts.MaxModuleCount)
            {
                throw new ModuleGraphLimitException(
                    $"Module count limit of {opts.MaxModuleCount} exceeded while registering module '{cacheKey.Key}'.");
            }

            if (sourceByteCount < 0)
            {
                Throw.InvalidOperationException("A module source byte count cannot be negative.");
            }

            if (opts.MaxTotalModuleSourceBytes != long.MaxValue
                && sourceByteCount > opts.MaxTotalModuleSourceBytes - _totalModuleSourceBytes)
            {
                throw new ModuleGraphLimitException(
                    $"Module total source byte limit of {opts.MaxTotalModuleSourceBytes} exceeded while registering module '{cacheKey.Key}'.");
            }
        }

        internal void RegisterModuleWithAccounting(ModuleCacheKey cacheKey, Module module, long sourceByteCount)
        {
            if (_modules.TryGetValue(cacheKey, out var existing))
            {
                if (!ReferenceEquals(existing, module))
                {
                    Throw.InvalidOperationException(
                        $"Module '{cacheKey.Key}' is already registered with a different module record.");
                }

                return;
            }

            EnsureModuleRegistrationAllowed(cacheKey, sourceByteCount);
            var sourceBytesReserved = false;
            _registeredModuleCount++;
            try
            {
                _totalModuleSourceBytes = checked(_totalModuleSourceBytes + sourceByteCount);
                sourceBytesReserved = true;
                // Reserve the budget before the debugger callback inside RegisterModule can re-enter the
                // engine. A nested load must observe this module's pending charge rather than over-commit the
                // limit, and a throwing callback rolls the reservation back with the failed registration.
                RegisterModule(cacheKey, module);
            }
            catch
            {
                _registeredModuleCount--;
                if (sourceBytesReserved)
                {
                    _totalModuleSourceBytes -= sourceByteCount;
                }
                throw;
            }
        }

        internal Module Load(string? referencingModuleLocation, ModuleRequest request)
        {
            var moduleResolution = GuardedResolve(
                referencingModuleLocation,
                request,
                new ModuleLoadBudget(_engine.Options.Modules));
            var cacheKey = ModuleCacheKey.From(moduleResolution);

            if (_modules.TryGetValue(cacheKey, out var module))
            {
                return module;
            }

            if (TryGetBuilder(moduleResolution, out var builderSpecifier, out var moduleBuilder))
            {
                module = LoadFromBuilder(builderSpecifier, moduleBuilder, cacheKey);
            }
            else
            {
                module = LoadFromModuleLoader(moduleResolution, cacheKey);
            }

            return module;
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-HostLoadImportedModule
        /// </summary>
        /// <remarks>
        /// Resolution is synchronous even for an asynchronous loader — the spec's HostLoadImportedModule maps
        /// a referrer/specifier pair to a module record, and only the fetching of an unseen one is allowed to
        /// take time. Everything already known is therefore finished inline: an entry in the referrer's
        /// <c>[[LoadedModules]]</c>, a module already in this engine's registry, or a
        /// <see cref="Add(string,string)"/>ed builder.
        /// </remarks>
        internal void LoadImportedModule(IScriptOrModule? referrer, ModuleRequest request, ModuleLoadPayload payload)
            => LoadImportedModule(referrer, referrer?.Location, request, payload);

        internal void LoadImportedModule(IScriptOrModule? referrer, string? referrerLocation, ModuleRequest request, ModuleLoadPayload payload)
        {
            var previous = _engine._parsingConstraintsOverride;
            var active = _engine.GetActiveParsingConstraints();
            _engine._parsingConstraintsOverride = referrer is null
                ? active
                : active.Combine(referrer.ParsingConstraints);
            try
            {
                LoadImportedModuleCore(referrer, referrerLocation, request, payload);
            }
            finally
            {
                _engine._parsingConstraintsOverride = previous;
            }
        }

        private void LoadImportedModuleCore(IScriptOrModule? referrer, string? referrerLocation, ModuleRequest request, ModuleLoadPayload payload)
        {
            if (TryGetLoadedModule(referrer, referrerLocation, request, out var loaded))
            {
                Finish(loaded, error: null);
                return;
            }

            ResolvedSpecifier moduleResolution;
            try
            {
                moduleResolution = GuardedResolve(referrerLocation, request, payload.Budget);
            }
            catch (JavaScriptException ex)
            {
                var error = HasModuleErrorPolicyApplied(ex.Error)
                    ? ex.Error
                    : CreateLoadError(ex);
                Finish(module: null, error, ex);
                return;
            }
            catch (Exception ex) when (_engine._eventLoop.IsRunningJob
                                       && !ModuleLoadCompletion.MustPropagateLoaderException(_engine, ex))
            {
                // A loader signals refusal with whatever exception it likes — DefaultModuleLoader throws
                // ModuleResolutionException, a sibling of JavaScriptException that the catch above does not
                // see. On a synchronous stack it propagates as it always has, but on a queued event-loop turn
                // — a static import discovered inside an asynchronously fetched module — there is no caller
                // left: escaping would erupt out of ProcessTasks with the graph state's pending count never
                // decremented, stranding the import promise forever. The refusal becomes the load's failure,
                // and the original exception travels along for the blocking importer to rethrow.
                Finish(module: null, CreateLoadError(ex), ex);
                return;
            }

            var cacheKey = ModuleCacheKey.From(moduleResolution);

            if (_modules.TryGetValue(cacheKey, out var module))
            {
                Finish(module, error: null);
                return;
            }

            // An in-flight fetch outranks a builder registered for the same key while it was airborne: the
            // importers already waiting on the fetch and this one must agree on which record the key denotes,
            // and consulting the builder here would hand this importer a second live module for the key.
            if (_pendingLoads is not null && _pendingLoads.TryGetValue(cacheKey, out var inFlight))
            {
                inFlight.AddWaiter(referrer, referrerLocation, request, payload);
                return;
            }

            if (TryGetBuilder(moduleResolution, out var builderSpecifier, out var moduleBuilder))
            {
                // Parsing the registered source can throw. On a synchronous stack the exception propagates as
                // it always has - the original JavaScriptException, message and location intact, out of
                // Modules.Import. But this branch also runs from inside a ModuleLoadCompletion job - an
                // asynchronously fetched module statically importing a builder module - where no caller is
                // left to catch anything: escaping there erupts out of ProcessTasks and strands the graph
                // load forever, so the error becomes the load's failure instead.
                Module? builderModule = null;
                JsValue? builderError = null;
                Exception? builderException = null;
                try
                {
                    builderModule = LoadFromBuilder(builderSpecifier, moduleBuilder, cacheKey);
                }
                catch (JavaScriptException ex) when (_engine._eventLoop.IsRunningJob)
                {
                    builderError = HasModuleErrorPolicyApplied(ex.Error)
                        ? ex.Error
                        : CreateLoadError(ex);
                    builderException = ex;
                }
                catch (Exception ex) when (_engine._eventLoop.IsRunningJob && !ModuleLoadCompletion.MustPropagate(ex))
                {
                    builderError = CreateLoadError(ex);
                    builderException = ex;
                }

                Finish(builderModule, builderError, builderException);
                return;
            }

            if (AsyncModuleLoader is not null)
            {
                EnsureModuleRegistrationAllowed(cacheKey, sourceByteCount: 0);
                var completion = new ModuleLoadCompletion(this, moduleResolution, cacheKey);
                completion.AddWaiter(referrer, referrerLocation, request, payload);
                (_pendingLoads ??= new Dictionary<ModuleCacheKey, ModuleLoadCompletion>())[cacheKey] = completion;

                try
                {
                    // The window lets a loader whose answer is already at hand settle on this very stack, the
                    // way a synchronous loader's answer arrives — see ModuleLoadCompletion.Settle.
                    completion.OpenInlineSettleWindow();
                    AsyncModuleLoader.LoadModuleAsync(_engine, moduleResolution, completion);
                }
                catch (Exception ex) when (!completion.IsCompleted)
                {
                    // A loader that throws instead of reporting through the completion still has to end up as
                    // a rejection rather than an exception on whatever thread happened to be evaluating. The
                    // filter is what keeps an exception thrown *after* an inline settle propagating: SetError
                    // on a settled completion is a no-op, and Build deliberately rethrows non-module failures.
                    if (ModuleLoadCompletion.MustPropagateLoaderException(_engine, ex))
                    {
                        // A constraint that becomes a rejection no longer bounds anything: the same cancelled
                        // engine with a synchronous loader surfaces ExecutionCanceledException, and a host's
                        // shutdown handling is written against exactly that. The unsettled completion must
                        // not stay registered, or a later import of the specifier would wait on it forever.
                        RemovePendingLoad(cacheKey);
                        throw;
                    }

                    completion.SetError(ex);
                }
                finally
                {
                    completion.CloseInlineSettleWindow();
                }

                return;
            }

            Module? loadedModule = null;
            JsValue? loadError = null;
            Exception? loadException = null;
            try
            {
                loadedModule = LoadFromModuleLoader(moduleResolution, cacheKey);
            }
            catch (JavaScriptException ex)
            {
                loadError = HasModuleErrorPolicyApplied(ex.Error)
                    ? ex.Error
                    : CreateLoadError(ex);
                loadException = ex;
            }

            // Dispatched outside the try: the spec hands FinishLoadingImportedModule one completion, once, and
            // a try around the dispatch itself would re-finish the payload if user code reached through
            // Continue ever threw after a successful load.
            Finish(loadedModule, loadError, loadException);

            void Finish(Module? module, JsValue? error, Exception? exception = null)
            {
                if (error is not null
                    && exception is JavaScriptException javaScriptException
                    && ReferenceEquals(javaScriptException.Error, error)
                    && HasModuleErrorPolicyApplied(error))
                {
                    RememberLoadFailure(error, exception);
                }

                _engine._host.FinishLoadingImportedModule(referrer, referrerLocation, request, payload, module, error);
            }
        }

        /// <summary>
        /// Looks a request up in the referrer's <c>[[LoadedModules]]</c>.
        /// </summary>
        internal bool TryGetLoadedModule(IScriptOrModule? referrer, ModuleRequest request, out Module module)
            => TryGetLoadedModule(referrer, referrer?.Location, request, out module);

        internal bool TryGetLoadedModule(IScriptOrModule? referrer, string? referrerLocation, ModuleRequest request, out Module module)
        {
            if (referrer is Module referrerModule)
            {
                return referrerModule.TryGetLoadedModule(request, out module);
            }

            return _scriptLoadedModules.TryGetValue(new ScriptLoadedModuleKey(referrerLocation, request), out module!);
        }

        /// <summary>
        /// Step 1 of <see href="https://tc39.es/ecma262/#sec-FinishLoadingImportedModule">FinishLoadingImportedModule</see>.
        /// </summary>
        internal void RecordLoadedModule(IScriptOrModule? referrer, string? referrerLocation, ModuleRequest request, Module module)
        {
            if (referrer is Module referrerModule)
            {
                referrerModule.RecordLoadedModule(request, module);
                return;
            }

            var key = new ScriptLoadedModuleKey(referrerLocation, request);
            if (_scriptLoadedModules.TryGetValue(key, out var existing))
            {
                if (!ReferenceEquals(existing, module))
                {
                    Throw.InvalidOperationException(
                        $"Error while loading module: the module loader returned two different modules for specifier '{request.Specifier}'. HostLoadImportedModule must be consistent for a given referrer and specifier.");
                }

                return;
            }

            _scriptLoadedModules[key] = module;
        }

        /// <summary>
        /// Registers a module the engine has just obtained, under the resolved specifier every referrer that
        /// resolves to it will look it up by.
        /// </summary>
        internal void RegisterModule(ModuleCacheKey cacheKey, Module module)
        {
            // The debugger callback runs before the registry commit so that a callback that throws leaves no
            // half-registered record behind — the failed load can then be retried cleanly.
            if (module is SourceTextModule sourceTextModule)
            {
                _engine.Debugger.OnBeforeEvaluate(sourceTextModule._source);
            }

            _modules[cacheKey] = module;
        }

        /// <summary>
        /// Whether the registry already holds a module for <paramref name="cacheKey"/>. Consulted by an
        /// asynchronous load's deferred build, which must not overwrite a record that was registered while
        /// its fetch was in flight.
        /// </summary>
        internal bool TryGetRegisteredModule(ModuleCacheKey cacheKey, [NotNullWhen(true)] out Module? module)
            => _modules.TryGetValue(cacheKey, out module);

        internal void RememberLoadFailure(JsValue error, Exception exception)
        {
            _lastLoadFailureError = error;
            _lastLoadFailure = ExceptionDispatchInfo.Capture(exception);
        }

        private ObjectInstance CreateLoadError(Exception exception)
        {
            var message = _engine.Options.Modules.ExposeDetailedLoadErrors
                ? exception.Message
                : "Could not load module.";
            return Throw.CreateClrError(_engine, exception, message, moduleErrorPolicyApplied: true);
        }

        internal object GetModuleErrorPolicyToken()
        {
            var exposeDetails = _engine.Options.Modules.ExposeDetailedLoadErrors;
            var decorator = _engine.Options.Interop.ClrExceptionErrorDecorator;
            if (!_moduleErrorPolicyInitialized
                || exposeDetails != _moduleErrorPolicyExposeDetails
                || !ReferenceEquals(decorator, _moduleErrorPolicyDecorator))
            {
                _moduleErrorPolicyToken = new object();
                _moduleErrorPolicyExposeDetails = exposeDetails;
                _moduleErrorPolicyDecorator = decorator;
                _moduleErrorPolicyInitialized = true;
            }

            return _moduleErrorPolicyToken;
        }

        internal bool HasModuleErrorPolicyApplied(JsValue error)
            => error is ErrorInstance errorInstance
               && errorInstance.HasModuleErrorPolicyToken(GetModuleErrorPolicyToken());

        /// <summary>
        /// Rethrows the original exception <paramref name="error"/> was reduced from, stack intact, when it
        /// is still at hand; otherwise returns and the caller raises what it can build from the value alone.
        /// </summary>
        internal void RethrowLoadFailure(JsValue error)
        {
            if (ReferenceEquals(_lastLoadFailureError, error))
            {
                var failure = _lastLoadFailure!;
                _lastLoadFailureError = null;
                _lastLoadFailure = null;
                failure.Throw();
            }
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

            // Stamped before the loop rather than after, so a loader whose Resolve re-enters the engine sees
            // the pass as already running and cannot start a second one over the same registrations. The
            // trade-off is that such a re-entrant call consults the index before this pass has finished
            // filling it and may miss a still-unindexed registration.
            var previousIndexedBuildersVersion = _indexedBuildersVersion;
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

            string? indexingSpecifier = null;
            try
            {
                foreach (var specifier in pending)
                {
                    indexingSpecifier = specifier;

                    // Marked before resolving, so a specifier the loader refuses is attempted once rather than
                    // on every load.
                    (_indexedBuilders ??= new HashSet<string>(StringComparer.Ordinal)).Add(specifier);

                    string? resolvedKey;
                    try
                    {
                        // A registration is a top-level name, so it is resolved the way Modules.Import resolves
                        // one: against no referrer. Uses GuardedResolveForIndex so policy applies but no
                        // resolution-hop budget is consumed — registration indexing is not per-import work.
                        var resolved = GuardedResolveForIndex(specifier);
                        resolvedKey = resolved?.Key;
                    }
#pragma warning disable CA1031 // a loader may signal "I will not resolve that" with any exception it likes
                    catch (Exception ex) when (ex is not OperationCanceledException
                                               && !ModuleLoadCompletion.MustPropagateLoaderException(_engine, ex))
#pragma warning restore CA1031
                    {
                        // A loader is free to reject a specifier outright - DefaultModuleLoader throws for a
                        // directory import, an unauthorized path or an invalid specifier. Such a registration is
                        // simply left unindexed, leaving it exactly as reachable as it was before this index
                        // existed, and the attempt is not repeated. Letting the exception out would instead fail
                        // an unrelated import that merely happened to run the indexing pass. Cancellation is
                        // different: it is the host calling off the whole operation, not the loader refusing one
                        // name, so it propagates.
                        continue;
                    }

                    if (resolvedKey is null)
                    {
                        // A loader compiled without nullable annotations can hand back a null key instead of
                        // throwing; treat it as the refusal it is rather than failing the triggering import.
                        continue;
                    }

                    if (string.Equals(resolvedKey, specifier, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    string? collidingRegistration = null;
                    if (_builders.ContainsKey(resolvedKey))
                    {
                        collidingRegistration = resolvedKey;
                    }
                    else if (_builderKeys is not null
                        && _builderKeys.TryGetValue(resolvedKey, out var other)
                        && !string.Equals(other, specifier, StringComparison.Ordinal)
                        && _builders.ContainsKey(other))
                    {
                        collidingRegistration = other;
                    }

                    if (collidingRegistration is not null)
                    {
                        // Two live registrations sharing one resolved identity: whichever lost would be silently
                        // unreachable under every name - its own raw name resolves to the shared key too - which
                        // is the exact failure this index exists to eliminate. So it fails as loudly as
                        // registering the same spelling twice fails in Add.
                        Throw.InvalidOperationException(
                            $"Module '{specifier}' resolves to '{resolvedKey}', which already identifies the registration '{collidingRegistration}'. Two registrations must not resolve to the same specifier.");
                    }

                    (_builderKeys ??= new Dictionary<string, string>(StringComparer.Ordinal))[resolvedKey] = specifier;
                    indexingSpecifier = null;
                }
            }
            catch
            {
                if (indexingSpecifier is not null)
                {
                    _indexedBuilders?.Remove(indexingSpecifier);
                }

                _indexedBuildersVersion = previousIndexedBuildersVersion;
                throw;
            }
        }

        internal void RemovePendingLoad(ModuleCacheKey cacheKey) => _pendingLoads?.Remove(cacheKey);

        /// <summary>
        /// Forgets every load an asynchronous loader has not finished. Called when the engine ends an
        /// evaluation cycle (<see cref="AdvancedOperations.RestoreGlobalSnapshot"/>): the completion of such a
        /// load is already fenced off by the event loop's generation and will be discarded, so leaving the
        /// entry behind would let the next cycle attach to a load that can never finish.
        /// </summary>
        internal void DiscardPendingLoads()
        {
            _pendingLoads?.Clear();

            // The failure memo goes with them. It is only ever consulted by the blocking importer of the very
            // failure that filled it, and that importer has long returned by the time a cycle ends — so what
            // survives here is never read again, and on a pooled engine it would pin one error object, and
            // the whole exception it was captured from, for the rest of the engine's life.
            _lastLoadFailureError = null;
            _lastLoadFailure = null;
        }

        private BuilderModule LoadFromBuilder(string specifier, ModuleBuilder moduleBuilder, ModuleCacheKey cacheKey)
        {
            // The module is named by the key it resolved to - which is what the cache key carries - rather than
            // by the name it was registered under. Those differ exactly when the loader canonicalized, and the
            // location is what the module's own relative imports are resolved against, so keeping the
            // registration name would leave a builder module reached through the index resolving its nested
            // imports against a name the loader never produced. A module supplied pre-compiled keeps its
            // prepare-time name instead; see ModuleBuilder.AddModule.
            var sourceByteCount = moduleBuilder.GetSourceByteCount();
            EnsureModuleRegistrationAllowed(cacheKey, sourceByteCount);
            var parsedModule = moduleBuilder.Parse(cacheKey.Key);
            var hasTopLevelAwait = HoistingScope.HasTopLevelAwait(parsedModule.Program!);
            var module = new BuilderModule(_engine, _engine.Realm, in parsedModule, location: parsedModule.Program!.Location.SourceFile, async: hasTopLevelAwait);
            RegisterModuleWithAccounting(cacheKey, module, sourceByteCount);
            moduleBuilder.BindExportedValues(module);
            _builders.Remove(specifier);
            return module;
        }

        private Module LoadFromModuleLoader(ResolvedSpecifier moduleResolution, ModuleCacheKey cacheKey)
        {
            EnsureModuleRegistrationAllowed(cacheKey, sourceByteCount: 0);
            var module = ModuleLoader.LoadModule(_engine, moduleResolution);
            var sourceBytes = module.SourceByteLength;
            RegisterModuleWithAccounting(cacheKey, module, sourceBytes);
            return module;
        }

        /// <summary>
        /// Registers a module built from <paramref name="code"/> under <paramref name="specifier"/>.
        /// </summary>
        /// <inheritdoc cref="Add(string,ModuleBuilder)" path="/remarks"/>
        public void Add(string specifier, string code)
        {
            using var ownership = _engine.EnterHostCall();
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
            using var ownership = _engine.EnterHostCall();
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
        /// loader would reject. The first import after a registration puts every not-yet-resolved registration
        /// through <see cref="IModuleLoader.Resolve"/>, so a loader observes resolve calls for names no script
        /// has imported. Each registration is resolved at most once: a resolution that fails - whether the
        /// loader refused the name deliberately or failed transiently - is not retried, and the registration
        /// stays unindexed. That makes it no <em>less</em> reachable than before this index existed, since
        /// importing the name puts the same string through the same loader and surfaces the same refusal. Two
        /// registrations must not resolve to the same key; the import that discovers such a pair throws
        /// <see cref="InvalidOperationException"/>, because whichever registration lost would be silently
        /// unreachable under every name.
        /// </para>
        /// <para>
        /// A registration is consumed the first time it is loaded and the resulting module is cached under its
        /// resolved key. The freed name may be registered again, but the cache - which is never evicted - keeps
        /// answering every later import that resolves to that key, so the new registration is only consulted by
        /// a request that misses the cache: one carrying different import attributes.
        /// </para>
        /// </remarks>
        public void Add(string specifier, ModuleBuilder moduleBuilder)
        {
            using var ownership = _engine.EnterHostCall();
            _builders.Add(specifier, moduleBuilder);
            _buildersVersion++;
        }

        /// <summary>
        /// Imports a module and returns its namespace, blocking until the whole graph has loaded, linked and
        /// evaluated.
        /// </summary>
        /// <remarks>
        /// With an <see cref="IAsyncModuleLoader"/> the calling thread is what drives the engine's event loop
        /// while the loads are in flight, so this deadlocks if the loader's own completions need that same
        /// thread — a Unity main thread delivering web requests through a coroutine, for instance. Use
        /// <see cref="ImportAsync(string,CancellationToken)"/> or <see cref="StartImport(string)"/> there.
        /// </remarks>
        public ObjectInstance Import(string specifier)
        {
            using var ownership = _engine.EnterHostCall();
            return Import(specifier, referencingModuleLocation: null);
        }

        internal ObjectInstance Import(string specifier, string? referencingModuleLocation)
        {
            return Import(new ModuleRequest(specifier, []), referencingModuleLocation);
        }

        internal ObjectInstance Import(ModuleRequest request, string? referencingModuleLocation)
            => _engine.ExecuteWithMemoryAccounting(
                () => ImportCore(request, referencingModuleLocation));

        private ObjectInstance ImportCore(ModuleRequest request, string? referencingModuleLocation)
        {
            var budget = new ModuleLoadBudget(_engine.Options.Modules);
            var module = LoadRootModule(request, referencingModuleLocation, budget);

            // The specification's load phase. Everything the module imports has to be present before it can
            // be linked, and with an asynchronous loader "present" is not something the engine can arrange by
            // itself: the load promise settles on some later turn of the event loop, so the caller of a
            // synchronous Import is the thread that has to run those turns. Only a module still in
            // [[Status]] new can have anything left to load — every later status is only reachable once its
            // transitive dependencies are present — so the warm re-import of an already-evaluated graph, the
            // Import-per-request embedding shape, skips the walk and its allocations entirely. A module
            // record with no dependencies of its own (anything that is not a CyclicModule) has nothing to
            // load either way.
            if (module is CyclicModule { Status: ModuleStatus.New })
            {
                RunLoadPhaseBlocking(module, budget);
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
            else if (cyclicModule.Status is ModuleStatus.Linked or ModuleStatus.EvaluatingAsync or ModuleStatus.Evaluated)
            {
                // The three states https://tc39.es/ecma262/#sec-moduleevaluation step 2 accepts as input, and
                // https://tc39.es/ecma262/#sec-ContinueDynamicImport hands the record to Evaluate() for every
                // one of them. A host import is the host's spelling of a dynamic import, so it does too.
                //
                // Linked is what some other operation on the graph left behind: an `import defer` elsewhere
                // loads and links its target without evaluating it, so importing that target for real is what
                // has to evaluate it. EvaluatingAsync is a graph still suspended on a top-level await, started
                // by an earlier StartImport; Evaluate() hands back its pending [[TopLevelCapability]] promise
                // and EvaluateModule waits that out. Both used to fall past this chain and be returned as a
                // namespace over uninitialized bindings - a read threw "Cannot access 'v' before
                // initialization" for the first and silently observed a half-run module for the second.
                //
                // Evaluated re-enters deliberately: ModuleEvaluation makes the call a no-op that hands back
                // the already settled promise, with one exception - a module whose evaluation threw replays
                // its recorded [[EvaluationError]]. Skipping the call returned a namespace over bindings the
                // failed evaluation never initialized.
                _engine.ExecuteWithConstraints(true, () => EvaluateModule(request.Specifier, cyclicModule));
            }

            // Linking and Evaluating are deliberately not handled: the module is being worked on further up
            // this very stack, by a host callback or a loader that re-entered Import, and the spec's own
            // answer to a self-import mid-evaluation is the pending top-level promise - which this thread
            // cannot wait for without deadlocking against itself. The namespace is handed back as it stands,
            // which is what a circular import observes anyway.

            _engine.RunAvailableContinuations();

            return Module.GetModuleNamespace(module);
        }

        /// <summary>
        /// Starts an import and returns at once, without blocking and without needing a thread of its own. The
        /// engine only makes progress on it when it is given turns, so the host drives it by calling
        /// <see cref="AdvancedOperations.ProcessTasks"/> — from a game loop or a UI message pump, say — until
        /// the returned operation reports <see cref="ModuleImportOperation.IsCompleted"/>.
        /// </summary>
        /// <remarks>
        /// This is the same pipeline a dynamic <c>import()</c> inside script goes through, and
        /// <see cref="ModuleImportOperation.Promise"/> is the promise it settles into.
        /// </remarks>
        public ModuleImportOperation StartImport(string specifier) => StartImport(specifier, referencingModuleLocation: null);

        /// <inheritdoc cref="StartImport(string)" />
        public ModuleImportOperation StartImport(string specifier, string? referencingModuleLocation)
        {
            using var ownership = _engine.EnterHostCall();
            return _engine.ExecuteWithConstraints(
                strict: true,
                () => StartImportCore(specifier, referencingModuleLocation));
        }

        private ModuleImportOperation StartImportCore(string specifier, string? referencingModuleLocation)
        {
            var request = new ModuleRequest(specifier, []);
            var capability = PromiseConstructor.NewPromiseCapability(_engine, _engine.Realm.Intrinsics.Promise);
            var payload = new DynamicImportPayload(
                _engine,
                request,
                capability,
                new ModuleLoadBudget(_engine.Options.Modules));

            try
            {
                _engine._host.LoadImportedModule(referrer: null, referencingModuleLocation, request, payload);
            }
            catch (JavaScriptException ex)
            {
                capability.Reject(HasModuleErrorPolicyApplied(ex.Error)
                    ? ex.Error
                    : CreateLoadError(ex));
            }
            catch (Exception ex) when (!ModuleLoadCompletion.MustPropagateLoaderException(_engine, ex))
            {
                // Resolution refusals arrive as ModuleResolutionException — not JavaScriptException — and the
                // same failure delivered through the loader's asynchronous settle would arrive as the
                // operation's Error. One failure must not have two delivery channels depending on where it
                // happened, and host code written to the poll-then-GetResult pattern must not crash at the
                // start call.
                capability.Reject(CreateLoadError(ex));
            }
            var operation = new ModuleImportOperation(_engine, capability.PromiseInstance);

            // Reactions rather than polling, so that the operation is also what marks the rejection handled and
            // a failed import does not read as an unhandled promise rejection to a host watching for those.
            var onFulfilled = new ClrFunction(_engine, "", (_, args) =>
            {
                operation.Fulfil(args.At(0));
                return JsValue.Undefined;
            }, 1, PropertyFlag.Configurable);

            var onRejected = new ClrFunction(_engine, "", (_, args) =>
            {
                operation.Fail(args.At(0));
                return JsValue.Undefined;
            }, 1, PropertyFlag.Configurable);

            PromiseOperations.PerformPromiseThen(_engine, (JsPromise) capability.PromiseInstance, onFulfilled, onRejected, resultCapability: null!);

            return operation;
        }

        /// <summary>
        /// Imports a module and returns its namespace, without blocking a thread while the module graph loads
        /// or while a top-level <c>await</c> is outstanding. The natural entry point for an
        /// <see cref="IAsyncModuleLoader"/>.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The engine is single-threaded and this method does not change that: continuations run one at a time,
        /// on whichever thread resumes the await. A host with a thread affinity — a game loop, a UI thread —
        /// wants <see cref="StartImport(string)"/> and its own pump instead, so that every turn runs where the
        /// host needs it to.
        /// </para>
        /// <para>
        /// <b>Where failures arrive.</b> Everything the import itself does — resolving and loading the graph,
        /// linking it, evaluating it, an execution constraint tripping — is reported through the returned
        /// <see cref="Task{TResult}"/> and never thrown out of this call, so a <c>catch</c> around the
        /// <c>await</c> sees all of it. Only the <see cref="InvalidOperationException"/> refusing the call
        /// because the engine is already in use arrives synchronously; it means the import never started, so
        /// there is nothing for a task to describe.
        /// </para>
        /// </remarks>
        /// <exception cref="PromiseRejectedException">The module failed to load or its evaluation threw.</exception>
        /// <exception cref="InvalidOperationException">This engine is already in use.</exception>
        public Task<ObjectInstance> ImportAsync(string specifier, CancellationToken cancellationToken = default)
            => ImportAsync(specifier, referencingModuleLocation: null, cancellationToken);

        /// <inheritdoc cref="ImportAsync(string,CancellationToken)" />
        public Task<ObjectInstance> ImportAsync(string specifier, string? referencingModuleLocation, CancellationToken cancellationToken = default)
        {
            // Taken here rather than inside the async body so that the admission failure is reported to the
            // caller synchronously, exactly as EvaluateAsync and its siblings report it; the body owns the
            // release, and every other way the import can fail belongs to the returned task.
            var owner = _engine.ReserveAsyncHostOperation();
            return ImportOnReservationAsync(specifier, referencingModuleLocation, owner, cancellationToken);
        }

        private async Task<ObjectInstance> ImportOnReservationAsync(
            string specifier,
            string? referencingModuleLocation,
            object owner,
            CancellationToken cancellationToken)
        {
            try
            {
                Task<JsValue> task;
                using (_engine.EnterHostCall(owner))
                {
                    var promise = StartImport(specifier, referencingModuleLocation).Promise;
                    task = _engine.UnwrapResultAsync(promise, owner, cancellationToken);
                }

                return (ObjectInstance) await task.ConfigureAwait(false);
            }
            finally
            {
                _engine.ReleaseAsyncHostOperation(owner);
            }
        }

        /// <summary>
        /// Loads the root of a module graph: a <c>HostLoadImportedModule</c> call like any other, so with an
        /// asynchronous loader it may only finish on a later turn of the event loop.
        /// </summary>
        private Module LoadRootModule(
            ModuleRequest request,
            string? referencingModuleLocation,
            ModuleLoadBudget budget)
        {
            var payload = new RootModuleLoadPayload(budget);
            _engine._host.LoadImportedModule(referrer: null, referencingModuleLocation, request, payload);

            if (!payload.IsCompleted)
            {
                ThrowIfBlockedInsideJob(request.Specifier);

                var timeout = _engine.Options.Constraints.PromiseTimeout;
                if (!_engine.DrainEventLoopUntil(static state => ((RootModuleLoadPayload) state).IsCompleted, payload, completedEvent: null, timeout))
                {
                    Throw.TimeoutException($"Timeout of {timeout} reached while loading module '{request.Specifier}'. An asynchronous module loader did not finish the load in time.");
                }
            }

            if (payload.Error is not null)
            {
                RethrowLoadFailure(payload.Error);
                Throw.JavaScriptException(_engine, payload.Error, in AstExtensions.DefaultLocation);
            }

            return payload.Module!;
        }

        /// <summary>
        /// Fails a blocking import that cannot possibly make progress. Inside an event-loop job the
        /// re-entrancy guard makes queued work unrunnable from a nested drain, so a load still in flight here
        /// would sit out the whole <c>PromiseTimeout</c> and then report the loader as too slow — ten seconds
        /// of stall and a misleading message for a structurally impossible wait. Failing now, with the actual
        /// reason, is what a UI or game-loop host can act on.
        /// </summary>
        private void ThrowIfBlockedInsideJob(string specifier)
        {
            if (_engine._eventLoop.IsRunningJob)
            {
                Throw.InvalidOperationException(
                    $"The asynchronous load of module '{specifier}' cannot be driven to completion from inside an event-loop job: queued work cannot run while another job is running, so the blocking import would only time out. Import the module with Engine.Modules.StartImport or ImportAsync before entering the job, or settle the load inside IAsyncModuleLoader.LoadModuleAsync so it completes synchronously.");
            }
        }

        /// <summary>
        /// Runs <see cref="Module.LoadRequestedModules"/> and, for an asynchronous loader, drives the event
        /// loop until the graph is in. Throws whatever the load failed with.
        /// </summary>
        private void RunLoadPhaseBlocking(Module module, ModuleLoadBudget budget)
        {
            var loadResult = module is CyclicModule cyclicModule
                ? cyclicModule.LoadRequestedModulesWithBudget(budget)
                : module.LoadRequestedModules();
            if (loadResult is not JsPromise loadPromise)
            {
                return;
            }

            if (loadPromise.State == PromiseState.Pending)
            {
                ThrowIfBlockedInsideJob(module.Location ?? "(null)");
                _engine.DrainEventLoopUntilSettled(loadPromise, _engine.Options.Constraints.PromiseTimeout);
            }

            switch (loadPromise.State)
            {
                case PromiseState.Rejected:
                    RethrowLoadFailure(loadPromise.Value);
                    Throw.JavaScriptException(_engine, loadPromise.Value, in AstExtensions.DefaultLocation);
                    break;
                case PromiseState.Pending:
                    Throw.TimeoutException($"Timeout of {_engine.Options.Constraints.PromiseTimeout} reached while loading the module graph. An asynchronous module loader did not finish the load in time.");
                    break;
            }
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
