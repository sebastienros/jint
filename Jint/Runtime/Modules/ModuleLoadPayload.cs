using System.Runtime.InteropServices;
using Jint.Native;
using Jint.Native.Promise;

namespace Jint.Runtime.Modules;

/// <summary>
/// The <c>payload</c> argument threaded through
/// <see href="https://tc39.es/ecma262/#sec-HostLoadImportedModule">HostLoadImportedModule</see> and handed
/// back to <see href="https://tc39.es/ecma262/#sec-FinishLoadingImportedModule">FinishLoadingImportedModule</see>.
/// </summary>
/// <remarks>
/// The spec's FinishLoadingImportedModule branches on whether the payload is a GraphLoadingState Record
/// (continue the load phase) or a PromiseCapability Record (continue a dynamic import); this models the
/// branch as a virtual call instead, so a third kind of load — the engine's own
/// <c>Engine.ModuleOperations.Import</c>, which needs the module record itself rather than a namespace — is
/// a third subclass rather than a third arm of an <c>if</c>.
/// <para>
/// <see cref="Continue"/> always runs on the engine thread. A host that finishes a load from a background
/// thread reaches it through <see cref="ModuleLoadCompletion"/>, which marshals onto the event loop.
/// </para>
/// </remarks>
internal abstract class ModuleLoadPayload
{
    internal abstract ModuleLoadBudget Budget { get; }

    /// <summary>
    /// Consumes the completion of one <c>HostLoadImportedModule</c> call: exactly one of
    /// <paramref name="module"/> (a normal completion) and <paramref name="error"/> (a throw completion) is
    /// non-null.
    /// </summary>
    internal abstract void Continue(ModuleRecord? module, JsValue? error);
}

internal sealed class ModuleLoadBudget
{
    private int _resolutionHopsRemaining;

    internal ModuleLoadBudget(Options.ModuleOptions options)
    {
        MaximumGraphDepth = options.MaxModuleGraphDepth;
        MaximumResolutionHops = options.MaxModuleResolutionHops;
        _resolutionHopsRemaining = MaximumResolutionHops;
    }

    internal int MaximumGraphDepth { get; }

    private int MaximumResolutionHops { get; }

    internal void ConsumeResolutionHop(string specifier)
    {
        if (_resolutionHopsRemaining == int.MaxValue)
        {
            return;
        }

        if (_resolutionHopsRemaining == 0)
        {
            throw new ModuleGraphLimitException(
                $"Module resolution hop limit of {MaximumResolutionHops} exceeded while resolving '{specifier}'.");
        }

        _resolutionHopsRemaining--;
    }
}

/// <summary>
/// https://tc39.es/ecma262/#graphloadingstate-record
/// </summary>
internal sealed class GraphLoadingState
{
    private readonly Dictionary<ModuleRecord, int> _nodeIndexes = new(ReferenceComparer<ModuleRecord>.Instance);
    private readonly HashSet<CyclicModuleRecord> _expanded = new(ReferenceComparer<CyclicModuleRecord>.Instance);
    private readonly Queue<PendingGraphModule> _modulesToProcess = new();
    private bool _isProcessingModules;

    internal GraphLoadingState(PromiseCapability promiseCapability, ModuleRecord root, ModuleLoadBudget budget)
    {
        PromiseCapability = promiseCapability;
        Budget = budget;
        AddNode(root);
    }

    internal PromiseCapability PromiseCapability { get; }

    internal ModuleLoadBudget Budget { get; }

    /// <summary>[[IsLoading]]</summary>
    internal bool IsLoading { get; set; } = true;

    /// <summary>[[PendingModulesCount]] — starts at one, for the root of the load itself.</summary>
    internal int PendingModulesCount { get; set; } = 1;

    /// <summary>
    /// Modules reached by this graph load, in discovery order. Identity lookup uses a dictionary with
    /// <see cref="ReferenceComparer{T}"/> because <see cref="JsValue.GetHashCode"/> hashes only the value type.
    /// </summary>
    internal List<ModuleRecord> Nodes { get; } = [];

    internal List<CyclicModuleRecord> Expanded { get; } = [];

    internal List<ModuleGraphEdge> Edges { get; } = [];

    internal void RecordEdge(CyclicModuleRecord parent, ModuleRecord child)
    {
        Edges.Add(new ModuleGraphEdge(parent, child));
        AddNode(child);
    }

    internal bool TryExpand(CyclicModuleRecord module)
    {
        if (!_expanded.Add(module))
        {
            return false;
        }

        Expanded.Add(module);
        return true;
    }

    internal void Enqueue(ModuleRecord module, int depth)
        => _modulesToProcess.Enqueue(new PendingGraphModule(module, depth));

    internal bool TryBeginProcessing()
    {
        if (_isProcessingModules)
        {
            return false;
        }

        _isProcessingModules = true;
        return true;
    }

    internal bool TryDequeue(out PendingGraphModule module)
    {
        if (_modulesToProcess.Count == 0)
        {
            module = default;
            return false;
        }

        module = _modulesToProcess.Dequeue();
        return true;
    }

    internal void EndProcessing() => _isProcessingModules = false;

    private void AddNode(ModuleRecord module)
    {
        if (_nodeIndexes.TryAdd(module, Nodes.Count))
        {
            Nodes.Add(module);
        }
    }

    /// <summary>
    /// Steps 3-5 of <see href="https://tc39.es/ecma262/#sec-InnerModuleLoading">InnerModuleLoading</see>:
    /// account for one settled request and, when the last one settles, promote every visited module out of
    /// <see cref="ModuleStatus.New"/> and resolve the load promise.
    /// </summary>
    internal void SettleOnePendingModule()
    {
        if (PendingModulesCount < 1)
        {
            Throw.InvalidOperationException("Error while loading module: pending module count underflow");
        }

        PendingModulesCount--;
        if (PendingModulesCount != 0)
        {
            return;
        }

        IsLoading = false;
        ValidateMaximumDepth();
        foreach (var loaded in Expanded)
        {
            loaded.OnGraphLoaded();
        }

        PromiseCapability.Resolve(JsValue.Undefined);
    }

    private void ValidateMaximumDepth()
    {
        var maximumDepth = Budget.MaximumGraphDepth;
        if (maximumDepth == int.MaxValue || Nodes.Count == 0)
        {
            return;
        }

        var count = Nodes.Count;
        var adjacency = new List<int>[count];
        var reverseAdjacency = new List<int>[count];
        for (var i = 0; i < count; i++)
        {
            adjacency[i] = [];
            reverseAdjacency[i] = [];
        }

        foreach (var edge in Edges)
        {
            var from = _nodeIndexes[edge.Parent];
            var to = _nodeIndexes[edge.Child];
            if (!adjacency[from].Contains(to))
            {
                adjacency[from].Add(to);
                reverseAdjacency[to].Add(from);
            }
        }

        var visited = new bool[count];
        var order = new List<int>(count);
        for (var start = 0; start < count; start++)
        {
            if (visited[start])
            {
                continue;
            }

            var traversal = new Stack<SccTraversalFrame>();
            visited[start] = true;
            traversal.Push(new SccTraversalFrame(start, 0));
            while (traversal.Count > 0)
            {
                var frame = traversal.Pop();
                var children = adjacency[frame.Node];
                if (frame.NextChild < children.Count)
                {
                    traversal.Push(new SccTraversalFrame(frame.Node, frame.NextChild + 1));
                    var child = children[frame.NextChild];
                    if (!visited[child])
                    {
                        visited[child] = true;
                        traversal.Push(new SccTraversalFrame(child, 0));
                    }
                }
                else
                {
                    order.Add(frame.Node);
                }
            }
        }

        var componentCount = 0;
        var components = new int[count];
        for (var i = 0; i < components.Length; i++)
        {
            components[i] = -1;
        }

        var members = new Stack<int>();
        for (var i = order.Count - 1; i >= 0; i--)
        {
            var start = order[i];
            if (components[start] >= 0)
            {
                continue;
            }

            components[start] = componentCount;
            members.Push(start);
            while (members.Count > 0)
            {
                var node = members.Pop();
                foreach (var parent in reverseAdjacency[node])
                {
                    if (components[parent] < 0)
                    {
                        components[parent] = componentCount;
                        members.Push(parent);
                    }
                }
            }

            componentCount++;
        }

        var componentSizes = new int[componentCount];
        var componentEdges = new List<int>[componentCount];
        var indegrees = new int[componentCount];
        for (var i = 0; i < componentCount; i++)
        {
            componentEdges[i] = [];
        }

        for (var node = 0; node < count; node++)
        {
            var component = components[node];
            componentSizes[component]++;
            foreach (var child in adjacency[node])
            {
                var childComponent = components[child];
                if (component != childComponent && !componentEdges[component].Contains(childComponent))
                {
                    componentEdges[component].Add(childComponent);
                    indegrees[childComponent]++;
                }
            }
        }

        var distances = new int[componentCount];
        distances[components[0]] = componentSizes[components[0]];
        var ready = new Queue<int>();
        for (var i = 0; i < componentCount; i++)
        {
            if (indegrees[i] == 0)
            {
                ready.Enqueue(i);
            }
        }

        while (ready.Count > 0)
        {
            var component = ready.Dequeue();
            foreach (var child in componentEdges[component])
            {
                if (distances[component] > 0)
                {
                    distances[child] = Math.Max(
                        distances[child],
                        checked(distances[component] + componentSizes[child]));
                }

                indegrees[child]--;
                if (indegrees[child] == 0)
                {
                    ready.Enqueue(child);
                }
            }
        }

        var actualDepth = 0;
        for (var i = 0; i < distances.Length; i++)
        {
            actualDepth = Math.Max(actualDepth, distances[i]);
        }
        if (actualDepth > maximumDepth)
        {
            throw new ModuleGraphLimitException(
                $"Module graph depth limit of {maximumDepth} exceeded; the graph requires depth {actualDepth}.");
        }
    }
}

[StructLayout(LayoutKind.Auto)]
internal readonly record struct ModuleGraphEdge(CyclicModuleRecord Parent, ModuleRecord Child);

[StructLayout(LayoutKind.Auto)]
internal readonly record struct SccTraversalFrame(int Node, int NextChild);

[StructLayout(LayoutKind.Auto)]
internal readonly record struct PendingGraphModule(ModuleRecord Module, int Depth);

internal sealed class ReferenceComparer<T> : IEqualityComparer<T> where T : class
{
    internal static readonly ReferenceComparer<T> Instance = new();

    private ReferenceComparer()
    {
    }

    public bool Equals(T? x, T? y) => ReferenceEquals(x, y);

    public int GetHashCode(T obj) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
}

internal sealed class GraphModuleLoadPayload : ModuleLoadPayload
{
    private readonly GraphLoadingState _state;
    private readonly CyclicModuleRecord _parent;
    private readonly int _depth;

    internal GraphModuleLoadPayload(GraphLoadingState state, CyclicModuleRecord parent, int depth)
    {
        _state = state;
        _parent = parent;
        _depth = depth;
    }

    internal override ModuleLoadBudget Budget => _state.Budget;

    internal override void Continue(ModuleRecord? module, JsValue? error)
    {
        if (!_state.IsLoading)
        {
            return;
        }

        if (error is null)
        {
            _state.RecordEdge(_parent, module!);
            CyclicModuleRecord.InnerModuleLoading(_state, module!, _depth);
        }
        else
        {
            _state.IsLoading = false;
            _state.PromiseCapability.Reject(error);
        }
    }
}

/// <summary>
/// The dynamic-<c>import()</c> payload: a promise capability handed to
/// <see href="https://tc39.es/ecma262/#sec-ContinueDynamicImport">ContinueDynamicImport</see>, which owns the
/// rest of the pipeline — load the requested modules, link, evaluate, resolve with the namespace.
/// </summary>
internal sealed class DynamicImportPayload : ModuleLoadPayload
{
    private readonly Engine _engine;
    private readonly ModuleRequest _moduleRequest;

    internal DynamicImportPayload(
        Engine engine,
        ModuleRequest moduleRequest,
        PromiseCapability promiseCapability,
        ModuleLoadBudget? budget = null)
    {
        _engine = engine;
        _moduleRequest = moduleRequest;
        PromiseCapability = promiseCapability;
        Budget = budget ?? new ModuleLoadBudget(engine.Options.Modules);
    }

    internal PromiseCapability PromiseCapability { get; }

    internal override ModuleLoadBudget Budget { get; }

    internal override void Continue(ModuleRecord? module, JsValue? error)
    {
        if (error is not null)
        {
            PromiseCapability.Reject(error);
            return;
        }

        _engine._host.ContinueDynamicImport(module!, _moduleRequest, PromiseCapability, Budget);
    }
}

/// <summary>
/// The payload used when the engine itself needs the loaded module record rather than a namespace. The
/// synchronous <c>Engine.ModuleOperations.Import</c> and its asynchronous counterparts all start by loading
/// the root of the graph, which is a <c>HostLoadImportedModule</c> call like any other and may therefore
/// complete on a later event-loop turn.
/// </summary>
internal sealed class RootModuleLoadPayload : ModuleLoadPayload
{
    internal RootModuleLoadPayload(ModuleLoadBudget budget)
    {
        Budget = budget;
    }

    internal override ModuleLoadBudget Budget { get; }

    /// <summary>The loaded module, or null while the load is in flight or has failed.</summary>
    internal ModuleRecord? Module { get; private set; }

    /// <summary>The error the load failed with, or null.</summary>
    internal JsValue? Error { get; private set; }

    internal bool IsCompleted { get; private set; }

    internal override void Continue(ModuleRecord? module, JsValue? error)
    {
        IsCompleted = true;
        Module = module;
        Error = error;
    }
}
