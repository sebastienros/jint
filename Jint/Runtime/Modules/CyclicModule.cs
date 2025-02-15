#nullable disable

using Jint.Native;
using Jint.Native.Promise;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;

namespace Jint.Runtime.Modules;

#pragma warning disable CS0649 // never assigned to, waiting for new functionalities in spec

internal sealed record ResolvedBinding(Module Module, string BindingName)
{
    internal static ResolvedBinding Ambiguous => new(null, "ambiguous");
}

/// <summary>
/// https://tc39.es/ecma262/#sec-cyclic-module-records
/// </summary>
public abstract class CyclicModule : Module
{
    private Completion? _evalError;
    private int _dfsIndex;
    private int _dfsAncestorIndex;
    internal HashSet<ModuleRequest> _requestedModules;
    private CyclicModule _cycleRoot;
    protected bool _hasTLA;
    private bool _asyncEvaluation;
    private PromiseCapability _topLevelCapability;
    private readonly List<CyclicModule> _asyncParentModules;
    private int _asyncEvalOrder;
    private int _pendingAsyncDependencies;

    internal JsValue _evalResult;
    private SourceLocation _abnormalCompletionLocation;

    internal CyclicModule(Engine engine, Realm realm, string location, bool async) : base(engine, realm, location)
    {
    }

    internal ModuleStatus Status { get; private set; }

    internal ref readonly SourceLocation AbnormalCompletionLocation => ref _abnormalCompletionLocation;

    /// <summary>
    /// https://tc39.es/ecma262/#sec-moduledeclarationlinking
    /// </summary>
    public override void Link()
    {
        if (Status is ModuleStatus.Linking or ModuleStatus.Evaluating)
        {
            ExceptionHelper.ThrowInvalidOperationException("Error while linking module: Module is already either linking or evaluating");
        }

        var stack = new Stack<CyclicModule>();

        try
        {
            InnerModuleLinking(stack, 0);
        }
        catch
        {
            foreach (var m in stack)
            {
                m._environment = null;

                if (m.Status != ModuleStatus.Linking)
                {
                    ExceptionHelper.ThrowInvalidOperationException("Error while linking module: Module should be linking after abrupt completion");
                }

                m.Status = ModuleStatus.Unlinked;
                m._dfsIndex = -1;
                m._dfsAncestorIndex = -1;
            }

            if (Status != ModuleStatus.Unlinked)
            {
                ExceptionHelper.ThrowInvalidOperationException("Error while processing abrupt completion of module link: Module should be unlinked after cleanup");
            }

            throw;
        }

        if (Status is not (ModuleStatus.Linked or ModuleStatus.EvaluatingAsync or ModuleStatus.Evaluated))
        {
            ExceptionHelper.ThrowInvalidOperationException("Error while linking module: Module is neither linked, evaluating-async or evaluated");
        }

        if (stack.Count > 0)
        {
            ExceptionHelper.ThrowInvalidOperationException("Error while linking module: One or more modules were not linked");
        }
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-moduleevaluation
    /// </summary>
    public override JsValue Evaluate()
    {
        var module = this;

        if (module.Status != ModuleStatus.Linked &&
            module.Status != ModuleStatus.EvaluatingAsync &&
            module.Status != ModuleStatus.Evaluated)
        {
            ExceptionHelper.ThrowInvalidOperationException("Error while evaluating module: Module is in an invalid state");
        }

        if (module.Status is ModuleStatus.EvaluatingAsync or ModuleStatus.Evaluated)
        {
            module = module._cycleRoot;
        }

        if (module._topLevelCapability is not null)
        {
            return module._topLevelCapability.PromiseInstance;
        }

        var stack = new Stack<CyclicModule>();
        var capability = PromiseConstructor.NewPromiseCapability(_engine, _realm.Intrinsics.Promise);
        var asyncEvalOrder = 0;
        module._topLevelCapability = capability;

        var result = module.InnerModuleEvaluation(stack, 0, ref asyncEvalOrder);

        if (result.Type != CompletionType.Normal)
        {
            foreach (var m in stack)
            {
                m.Status = ModuleStatus.Evaluated;
                m._evalError = result;
            }

            _abnormalCompletionLocation = result.Location;
            capability.Reject.Call(Undefined, result.Value);
        }
        else
        {
            if (module.Status != ModuleStatus.EvaluatingAsync && module.Status != ModuleStatus.Evaluated)
            {
                ExceptionHelper.ThrowInvalidOperationException("Error while evaluating module: Module is in an invalid state");
            }

            if (module._evalError is not null)
            {
                ExceptionHelper.ThrowInvalidOperationException("Error while evaluating module: Module is in an invalid state");
            }

            if (!module._asyncEvaluation)
            {
                if (module.Status != ModuleStatus.Evaluated)
                {
                    ExceptionHelper.ThrowInvalidOperationException("Error while evaluating module: Module is in an invalid state");
                }

                capability.Resolve.Call(Undefined, Array.Empty<JsValue>());
            }

            if (stack.Count > 0)
            {
                ExceptionHelper.ThrowInvalidOperationException("Error while evaluating module: Module is in an invalid state");
            }
        }

        return capability.PromiseInstance;
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-InnerModuleLinking
    /// </summary>
    protected internal override int InnerModuleLinking(Stack<CyclicModule> stack, int index)
    {
        if (Status is
            ModuleStatus.Linking or
            ModuleStatus.Linked or
            ModuleStatus.EvaluatingAsync or
            ModuleStatus.Evaluated)
        {
            return index;
        }

        if (Status != ModuleStatus.Unlinked)
        {
            ExceptionHelper.ThrowInvalidOperationException($"Error while linking module: Module in an invalid state: {Status}");
        }

        Status = ModuleStatus.Linking;
        _dfsIndex = index;
        _dfsAncestorIndex = index;
        index++;
        stack.Push(this);

        foreach (var request in _requestedModules)
        {
            var requiredModule = _engine._host.GetImportedModule(this, request);

            index = requiredModule.InnerModuleLinking(stack, index);

            if (requiredModule is not CyclicModule requiredCyclicModule)
            {
                continue;
            }

            if (requiredCyclicModule.Status is not (
                ModuleStatus.Linking or
                ModuleStatus.Linked or
                ModuleStatus.EvaluatingAsync or
                ModuleStatus.Evaluated))
            {
                ExceptionHelper.ThrowInvalidOperationException($"Error while linking module: Required module is in an invalid state: {requiredCyclicModule.Status}");
            }

            if ((requiredCyclicModule.Status == ModuleStatus.Linking) == !stack.Contains(requiredCyclicModule))
            {
                ExceptionHelper.ThrowInvalidOperationException($"Error while linking module: Required module is in an invalid state: {requiredCyclicModule.Status}");
            }

            if (requiredCyclicModule.Status == ModuleStatus.Linking)
            {
                _dfsAncestorIndex = Math.Min(_dfsAncestorIndex, requiredCyclicModule._dfsAncestorIndex);
            }
        }

        InitializeEnvironment();

        if (StackReferenceCount(stack) != 1)
        {
            ExceptionHelper.ThrowInvalidOperationException("Error while linking module: Recursive dependency detected");
        }

        if (_dfsAncestorIndex > _dfsIndex)
        {
            ExceptionHelper.ThrowInvalidOperationException("Error while linking module: Recursive dependency detected");
        }

        if (_dfsIndex == _dfsAncestorIndex)
        {
            while (true)
            {
                var requiredModule = stack.Pop();
                requiredModule.Status = ModuleStatus.Linked;
                if (requiredModule == this)
                {
                    break;
                }
            }
        }

        return index;
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-innermoduleevaluation
    /// </summary>
    protected internal override Completion InnerModuleEvaluation(Stack<CyclicModule> stack, int index, ref int asyncEvalOrder)
    {
        if (Status is ModuleStatus.EvaluatingAsync or ModuleStatus.Evaluated)
        {
            if (_evalError is null)
            {
                return new Completion(CompletionType.Normal, index, default);
            }

            return _evalError.Value;
        }

        if (Status == ModuleStatus.Evaluating)
        {
            return new Completion(CompletionType.Normal, index, default);
        }

        if (Status != ModuleStatus.Linked)
        {
            ExceptionHelper.ThrowInvalidOperationException($"Error while evaluating module: Module is in an invalid state: {Status}");
        }

        Status = ModuleStatus.Evaluating;
        _dfsIndex = index;
        _dfsAncestorIndex = index;
        _pendingAsyncDependencies = 0;
        index++;
        stack.Push(this);

        foreach (var required in _requestedModules)
        {
            var requiredModule = _engine._host.GetImportedModule(this, required);

            var result = requiredModule.InnerModuleEvaluation(stack, index, ref asyncEvalOrder);
            if (result.Type != CompletionType.Normal)
            {
                return result;
            }

            index = TypeConverter.ToInt32(result.Value);

            if (requiredModule is CyclicModule requiredCyclicModule)
            {
                if (requiredCyclicModule.Status != ModuleStatus.Evaluating &&
                    requiredCyclicModule.Status != ModuleStatus.EvaluatingAsync &&
                    requiredCyclicModule.Status != ModuleStatus.Evaluated)
                {
                    ExceptionHelper.ThrowInvalidOperationException($"Error while evaluating module: Module is in an invalid state: {requiredCyclicModule.Status}");
                }

                if (requiredCyclicModule.Status == ModuleStatus.Evaluating && !stack.Contains(requiredCyclicModule))
                {
                    ExceptionHelper.ThrowInvalidOperationException($"Error while evaluating module: Module is in an invalid state: {requiredCyclicModule.Status}");
                }

                if (requiredCyclicModule.Status == ModuleStatus.Evaluating)
                {
                    _dfsAncestorIndex = Math.Min(_dfsAncestorIndex, requiredCyclicModule._dfsAncestorIndex);
                }
                else
                {
                    requiredCyclicModule = requiredCyclicModule._cycleRoot;
                    if (requiredCyclicModule.Status is not (ModuleStatus.EvaluatingAsync or ModuleStatus.Evaluated))
                    {
                        ExceptionHelper.ThrowInvalidOperationException("Error while evaluating module: Module is in an invalid state");
                    }

                    if (requiredCyclicModule._evalError != null)
                    {
                        return requiredCyclicModule._evalError.Value;
                    }
                }

                if (requiredCyclicModule._asyncEvaluation)
                {
                    _pendingAsyncDependencies++;
                    requiredCyclicModule._asyncParentModules.Add(this);
                }
            }
        }

        Completion completion;

        if (_pendingAsyncDependencies > 0 || _hasTLA)
        {
            if (_asyncEvaluation)
            {
                ExceptionHelper.ThrowInvalidOperationException("Error while evaluating module: Module is in an invalid state (async evaluation is true)");
            }

            _asyncEvaluation = true;
            _asyncEvalOrder = asyncEvalOrder++;
            if (_pendingAsyncDependencies == 0)
            {
                completion = ExecuteAsyncModule();
            }
            else
            {
                // This is not in the specifications, but it's unclear whether 16.2.1.5.2.1.13 "Otherwise" should mean "Else" for 12 or "In other cases"..
                completion = ExecuteModule();
            }
        }
        else
        {
            completion = ExecuteModule();
        }

        if (StackReferenceCount(stack) != 1)
        {
            ExceptionHelper.ThrowInvalidOperationException("Error while evaluating module: Module is in an invalid state (not found exactly once in stack)");
        }

        if (_dfsAncestorIndex > _dfsIndex)
        {
            ExceptionHelper.ThrowInvalidOperationException("Error while evaluating module: Module is in an invalid state (mismatch DFS ancestor index)");
        }

        if (_dfsIndex == _dfsAncestorIndex)
        {
            var done = false;
            while (!done)
            {
                var requiredModule = stack.Pop();
                if (!requiredModule._asyncEvaluation)
                {
                    requiredModule.Status = ModuleStatus.Evaluated;
                }
                else
                {
                    requiredModule.Status = ModuleStatus.EvaluatingAsync;
                }

                done = ReferenceEquals(requiredModule, this);
                requiredModule._cycleRoot = this;
            }
        }

        return completion;
    }

    private int StackReferenceCount(Stack<CyclicModule> stack)
    {
        var count = 0;
        foreach (var item in stack)
        {
            if (ReferenceEquals(item, this))
            {
                count++;
            }
        }

        return count;
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-execute-async-module
    /// </summary>
    private Completion ExecuteAsyncModule()
    {
        if (Status != ModuleStatus.Evaluating && Status != ModuleStatus.EvaluatingAsync || !_hasTLA)
        {
            ExceptionHelper.ThrowInvalidOperationException("Error while evaluating module: Module is in an invalid state");
        }

        var capability = PromiseConstructor.NewPromiseCapability(_engine, _realm.Intrinsics.Promise);

        var onFullfilled = new ClrFunction(_engine, "fulfilled", AsyncModuleExecutionFulfilled, 1, PropertyFlag.Configurable);
        var onRejected = new ClrFunction(_engine, "rejected", AsyncModuleExecutionRejected, 1, PropertyFlag.Configurable);

        PromiseOperations.PerformPromiseThen(_engine, (JsPromise) capability.PromiseInstance, onFullfilled, onRejected, null);

        return ExecuteModule(capability);
    }


    /// <summary>
    /// https://tc39.es/ecma262/#sec-async-module-execution-fulfilled
    /// </summary>
    private static JsValue AsyncModuleExecutionFulfilled(JsValue thisObject, JsCallArguments arguments)
    {
        var module = (CyclicModule) arguments.At(0);
        if (module.Status == ModuleStatus.Evaluated)
        {
            if (module._evalError is not null)
            {
                ExceptionHelper.ThrowInvalidOperationException("Error while evaluating module: Module is in an invalid state");
            }

            return Undefined;
        }

        if (module.Status != ModuleStatus.EvaluatingAsync ||
            !module._asyncEvaluation ||
            module._evalError is not null)
        {
            ExceptionHelper.ThrowInvalidOperationException("Error while evaluating module: Module is in an invalid state");
        }

        if (module._topLevelCapability is not null)
        {
            if (module._cycleRoot is null)
            {
                ExceptionHelper.ThrowInvalidOperationException("Error while evaluating module: Module is in an invalid state");
            }

            module._topLevelCapability.Resolve.Call(Undefined, Array.Empty<JsValue>());
        }

        var execList = new List<CyclicModule>();
        module.GatherAvailableAncestors(execList);
        execList.Sort((x, y) => x._asyncEvalOrder - y._asyncEvalOrder);

        for (var i = 0; i < execList.Count; i++)
        {
            var m = execList[i];
            if (m.Status == ModuleStatus.Evaluated && m._evalError is null)
            {
                ExceptionHelper.ThrowInvalidOperationException("Error while evaluating module: Module is in an invalid state");
            }
            else if (m._hasTLA)
            {
                m.ExecuteAsyncModule();
            }
            else
            {
                var result = m.ExecuteModule();
                if (result.Type != CompletionType.Normal)
                {
                    AsyncModuleExecutionRejected(Undefined, [m, result.Value]);
                }
                else
                {
                    m.Status = ModuleStatus.Evaluated;
                    if (m._topLevelCapability is not null)
                    {
                        if (m._cycleRoot is null)
                        {
                            ExceptionHelper.ThrowInvalidOperationException("Error while evaluating module: Module is in an invalid state");
                        }

                        m._topLevelCapability.Resolve.Call(Undefined, Array.Empty<JsValue>());
                    }
                }
            }
        }

        return Undefined;
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-async-module-execution-rejected
    /// </summary>
    private static JsValue AsyncModuleExecutionRejected(JsValue thisObject, JsCallArguments arguments)
    {
        var module = (SourceTextModule) arguments.At(0);
        var error = arguments.At(1);

        if (module.Status == ModuleStatus.Evaluated)
        {
            if (module._evalError is null)
            {
                ExceptionHelper.ThrowInvalidOperationException("Error while evaluating module: Module is in an invalid state");
            }

            return Undefined;
        }

        if (module.Status != ModuleStatus.EvaluatingAsync ||
            !module._asyncEvaluation ||
            module._evalError is not null)
        {
            ExceptionHelper.ThrowInvalidOperationException("Error while evaluating module: Module is in an invalid state");
        }

        module._evalError = new Completion(CompletionType.Throw, error, default);
        module.Status = ModuleStatus.Evaluated;

        var asyncParentModules = module._asyncParentModules;
        for (var i = 0; i < asyncParentModules.Count; i++)
        {
            var m = asyncParentModules[i];
            AsyncModuleExecutionRejected(thisObject, [m, error]);
        }

        if (module._topLevelCapability is not null)
        {
            if (module._cycleRoot is null)
            {
                ExceptionHelper.ThrowInvalidOperationException("Error while evaluating module: Module is in an invalid state");
            }

            module._topLevelCapability.Reject.Call(Undefined, error);
        }

        return Undefined;
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-gather-available-ancestors
    /// </summary>
    private void GatherAvailableAncestors(List<CyclicModule> execList)
    {
        foreach (var m in _asyncParentModules)
        {
            if (!execList.Contains(m) && m._cycleRoot._evalError is null)
            {
                if (m.Status != ModuleStatus.EvaluatingAsync ||
                    m._evalError is not null ||
                    !m._asyncEvaluation ||
                    m._pendingAsyncDependencies <= 0)
                {
                    ExceptionHelper.ThrowInvalidOperationException("Error while evaluating module: Module is in an invalid state");
                }

                if (--m._pendingAsyncDependencies == 0)
                {
                    execList.Add(m);
                    if (!m._hasTLA)
                    {
                        m.GatherAvailableAncestors(execList);
                    }
                }
            }
        }
    }

    /// <summary>
    /// https://tc39.es/ecma262/#table-cyclic-module-methods
    /// </summary>
    protected abstract void InitializeEnvironment();

    internal abstract Completion ExecuteModule(PromiseCapability capability = null);
}
