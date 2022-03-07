using System;
using Esprima.Ast;
using System.Collections.Generic;
using Esprima;
using Jint.Native;
using Jint.Native.Promise;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;

namespace Jint.Runtime.Modules;

#pragma warning disable CS0649 // never assigned to, waiting for new functionalities in spec

internal sealed record ResolvedBinding(ModuleRecord Module, string BindingName)
{
    internal static ResolvedBinding Ambiguous => new(null, "ambiguous");
}

/// <summary>
/// https://tc39.es/ecma262/#sec-cyclic-module-records
/// </summary>
public abstract class CyclicModuleRecord : ModuleRecord
{
    private Completion? _evalError;
    private int _dfsIndex;
    private int _dfsAncestorIndex;
    protected HashSet<string> _requestedModules;
    private CyclicModuleRecord _cycleRoot;
    protected bool _hasTLA;
    private bool _asyncEvaluation;
    private PromiseCapability _topLevelCapability;
    private List<CyclicModuleRecord> _asyncParentModules;
    private int _asyncEvalOrder;
    private int _pendingAsyncDependencies;

    internal JsValue _evalResult;

    internal CyclicModuleRecord(Engine engine, Realm realm, Module source, string location, bool async) : base(engine, realm, location)
    {
    }

    internal ModuleStatus Status { get; private set; }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-moduledeclarationlinking
    /// </summary>
    public override void Link()
    {
        if (Status == ModuleStatus.Linking || Status == ModuleStatus.Evaluating)
        {
            ExceptionHelper.ThrowInvalidOperationException("Error while linking module: Module is already either linking or evaluating");
        }

        var stack = new Stack<CyclicModuleRecord>();

        try
        {
            InnerModuleLinking(stack, 0);
        }
        catch
        {
            foreach (var m in stack)
            {
                m.Status = ModuleStatus.Unlinked;
                m._environment = null;
                m._dfsIndex = -1;
                m._dfsAncestorIndex = -1;
            }

            Status = ModuleStatus.Unlinked;
            throw;
        }

        if (Status != ModuleStatus.Linked && Status != ModuleStatus.Unlinked)
        {
            ExceptionHelper.ThrowInvalidOperationException("Error while linking module: Module is neither linked or unlinked");
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

        var stack = new Stack<CyclicModuleRecord>();
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

            capability.Reject.Call(Undefined, new[] { result.Value });
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
    private int InnerModuleLinking(Stack<CyclicModuleRecord> stack, int index)
    {
        if (Status is
            ModuleStatus.Linking or
            ModuleStatus.Linked or
            ModuleStatus.EvaluatingAsync or
            ModuleStatus.Evaluating)
        {
            return index;
        }

        if (Status != ModuleStatus.Unlinked)
        {
            ExceptionHelper.ThrowInvalidOperationException("Error while linking module: Module in an invalid state");
        }

        Status = ModuleStatus.Linking;
        _dfsIndex = index;
        _dfsAncestorIndex = index;
        index++;
        stack.Push(this);

        var requestedModules = _requestedModules;

        foreach (var moduleSpecifier in requestedModules)
        {
            var requiredModule = _engine._host.ResolveImportedModule(this, moduleSpecifier);

            if (requiredModule is not CyclicModuleRecord requiredCyclicModule)
            {
                continue;
            }

            //TODO: Should we link only when a module is requested? https://tc39.es/ecma262/#sec-example-cyclic-module-record-graphs Should we support retry?
            if (requiredCyclicModule.Status == ModuleStatus.Unlinked)
            {
                index = requiredCyclicModule.InnerModuleLinking(stack, index);
            }

            if (requiredCyclicModule.Status != ModuleStatus.Linking &&
                requiredCyclicModule.Status != ModuleStatus.Linked &&
                requiredCyclicModule.Status != ModuleStatus.Evaluated)
            {
                ExceptionHelper.ThrowInvalidOperationException($"Error while linking module: Required module is in an invalid state: {requiredCyclicModule.Status}");
            }

            if (requiredCyclicModule.Status == ModuleStatus.Linking && !stack.Contains(requiredCyclicModule))
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

        if (_dfsIndex > _dfsAncestorIndex)
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
    private Completion InnerModuleEvaluation(Stack<CyclicModuleRecord> stack, int index, ref int asyncEvalOrder)
    {
        if (Status is ModuleStatus.EvaluatingAsync or ModuleStatus.Evaluated)
        {
            if (_evalError is null)
            {
                return new Completion(CompletionType.Normal, index, null, default);
            }

            return _evalError.Value;
        }

        if (Status == ModuleStatus.Evaluating)
        {
            return new Completion(CompletionType.Normal, index, null, default);
        }

        if (Status != ModuleStatus.Linked)
        {
            ExceptionHelper.ThrowInvalidOperationException("Error while evaluating module: Module is in an invalid state");
        }


        Status = ModuleStatus.Evaluating;
        _dfsIndex = index;
        _dfsAncestorIndex = index;
        _pendingAsyncDependencies = 0;
        index++;
        stack.Push(this);

        foreach (var moduleSpecifier in _requestedModules)
        {
            var requiredModule = _engine._host.ResolveImportedModule(this, moduleSpecifier);

            if (requiredModule is not CyclicModuleRecord requiredCyclicModule)
            {
                ExceptionHelper.ThrowNotImplementedException($"Resolving modules of type {requiredModule.GetType()} is not implemented");
                continue;
            }

            var result = requiredCyclicModule.InnerModuleEvaluation(stack, index, ref asyncEvalOrder);
            if (result.Type != CompletionType.Normal)
            {
                return result;
            }

            index = TypeConverter.ToInt32(result.Value);

            // TODO: Validate this behavior: https://tc39.es/ecma262/#sec-example-cyclic-module-record-graphs
            if (requiredCyclicModule.Status == ModuleStatus.Linked)
            {
                var evaluationResult = requiredCyclicModule.Evaluate();
                if (evaluationResult == null)
                {
                    ExceptionHelper.ThrowInvalidOperationException($"Error while evaluating module: Module evaluation did not return a promise");
                }
                else if (evaluationResult is not PromiseInstance promise)
                {
                    ExceptionHelper.ThrowInvalidOperationException($"Error while evaluating module: Module evaluation did not return a promise: {evaluationResult.Type}");
                }
                else if (promise.State == PromiseState.Rejected)
                {
                    ExceptionHelper.ThrowJavaScriptException(_engine, promise.Value,
                        new Completion(CompletionType.Throw, promise.Value, null, new Location(new Position(), new Position(), moduleSpecifier)));
                }
                else if (promise.State != PromiseState.Fulfilled)
                {
                    ExceptionHelper.ThrowInvalidOperationException($"Error while evaluating module: Module evaluation did not return a fulfilled promise: {promise.State}");
                }
            }

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
                if (requiredCyclicModule.Status != ModuleStatus.EvaluatingAsync && requiredCyclicModule.Status != ModuleStatus.Evaluated)
                {
                    ExceptionHelper.ThrowInvalidOperationException("Error while evaluating module: Module is in an invalid state");
                }
            }

            if (requiredCyclicModule._asyncEvaluation)
            {
                _pendingAsyncDependencies++;
                requiredCyclicModule._asyncParentModules.Add(this);
            }
        }

        Completion completion;

        if (_pendingAsyncDependencies > 0 || _hasTLA)
        {
            if (_asyncEvaluation)
            {
                ExceptionHelper.ThrowInvalidOperationException("Error while evaluating module: Module is in an invalid state");
            }

            _asyncEvaluation = true;
            _asyncEvalOrder = asyncEvalOrder++;
            if (_pendingAsyncDependencies == 0)
            {
                completion = ExecuteAsync();
            }
            else
            {
                completion = Execute();
            }
        }
        else
        {
            completion = Execute();
        }

        if (StackReferenceCount(stack) != 1)
        {
            ExceptionHelper.ThrowInvalidOperationException("Error while evaluating module: Module is in an invalid state");
        }

        if (_dfsAncestorIndex > _dfsIndex)
        {
            ExceptionHelper.ThrowInvalidOperationException("Error while evaluating module: Module is in an invalid state");
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

                done = requiredModule == this;
                requiredModule._cycleRoot = this;
            }
        }

        return completion;
    }

    private int StackReferenceCount(Stack<CyclicModuleRecord> stack)
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
    private Completion ExecuteAsync()
    {
        if (Status != ModuleStatus.Evaluating && Status != ModuleStatus.EvaluatingAsync || !_hasTLA)
        {
            ExceptionHelper.ThrowInvalidOperationException("Error while evaluating module: Module is in an invalid state");
        }

        var capability = PromiseConstructor.NewPromiseCapability(_engine, _realm.Intrinsics.Promise);

        var onFullfilled = new ClrFunctionInstance(_engine, "fulfilled", AsyncModuleExecutionFulfilled, 1, PropertyFlag.Configurable);
        var onRejected = new ClrFunctionInstance(_engine, "rejected", AsyncModuleExecutionRejected, 1, PropertyFlag.Configurable);

        PromiseOperations.PerformPromiseThen(_engine, (PromiseInstance) capability.PromiseInstance, onFullfilled, onRejected, null);

        return Execute(capability);
    }


    /// <summary>
    /// https://tc39.es/ecma262/#sec-async-module-execution-fulfilled
    /// </summary>
    private JsValue AsyncModuleExecutionFulfilled(JsValue thisObj, JsValue[] arguments)
    {
        var module = (CyclicModuleRecord) arguments.At(0);
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

        var execList = new List<CyclicModuleRecord>();
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
                m.ExecuteAsync();
            }
            else
            {
                var result = m.Execute();
                if (result.Type != CompletionType.Normal)
                {
                    AsyncModuleExecutionRejected(Undefined, new[] { m, result.Value });
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
    private JsValue AsyncModuleExecutionRejected(JsValue thisObj, JsValue[] arguments)
    {
        var module = (SourceTextModuleRecord) arguments.At(0);
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

        module._evalError = new Completion(CompletionType.Throw, error, null, default);
        module.Status = ModuleStatus.Evaluated;

        var asyncParentModules = module._asyncParentModules;
        for (var i = 0; i < asyncParentModules.Count; i++)
        {
            var m = asyncParentModules[i];
            AsyncModuleExecutionRejected(thisObj, new[] { m, error });
        }

        if (module._topLevelCapability is not null)
        {
            if (module._cycleRoot is null)
            {
                ExceptionHelper.ThrowInvalidOperationException("Error while evaluating module: Module is in an invalid state");
            }

            module._topLevelCapability.Reject.Call(Undefined, new[] { error });
        }

        return Undefined;
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-gather-available-ancestors
    /// </summary>
    private void GatherAvailableAncestors(List<CyclicModuleRecord> execList)
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
    internal abstract Completion Execute(PromiseCapability capability = null);
}