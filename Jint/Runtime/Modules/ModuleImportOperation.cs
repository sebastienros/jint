using Jint.Native;
using Jint.Native.Object;

namespace Jint.Runtime.Modules;

/// <summary>
/// An import in progress, handed back by <c>Engine.Modules.StartImport</c>. The engine makes progress on it
/// only when it is given turns, so a host with a thread it must not block — a game loop, a UI thread — drives
/// it by calling <c>engine.Advanced.ProcessTasks()</c> and watching <see cref="IsCompleted"/>.
/// </summary>
/// <example>
/// <code>
/// // once
/// _import = engine.Modules.StartImport("./main.js");
///
/// // every frame
/// engine.Advanced.ProcessTasks();
/// if (_import.IsCompleted)
/// {
///     var ns = _import.GetResult();   // throws PromiseRejectedException if the load or evaluation failed
///     _import = null;
/// }
/// </code>
/// </example>
public sealed class ModuleImportOperation
{
    private readonly Engine _engine;

    /// <summary>
    /// The evaluation cycle the import was started in. Once the engine has moved past it, no turn of the
    /// event loop can finish this import; see <see cref="ObserveAbandonment"/>.
    /// </summary>
    private readonly int _generation;

    private bool _completed;
    private bool _faulted;
    private ObjectInstance? _namespace;
    private JsValue? _error;

    internal ModuleImportOperation(Engine engine, JsValue promise)
    {
        _engine = engine;
        _generation = engine.EventLoopGeneration;
        Promise = promise;
    }

    /// <summary>
    /// The promise the import settles into — the same value a dynamic <c>import()</c> in script would produce.
    /// Useful for handing the import to script; a host tracking it from .NET wants <see cref="IsCompleted"/>.
    /// </summary>
    /// <remarks>
    /// An import abandoned by <c>Engine.Advanced.RestoreGlobalSnapshot</c> (see <see cref="IsCompleted"/>)
    /// fails the operation but leaves this promise pending forever: settling it would run the ended cycle's
    /// reactions against the restored globals, which is the very thing the restore fenced off.
    /// </remarks>
    public JsValue Promise { get; }

    /// <summary>
    /// Whether the import has finished, successfully or not. Becomes true during a turn of the event loop, so
    /// it is only ever worth re-reading after the engine has been given one.
    /// </summary>
    /// <remarks>
    /// There is one way for an import to end without a turn: <c>Engine.Advanced.RestoreGlobalSnapshot</c> ends
    /// the evaluation cycle the import was started in, and a load fenced off that way can never settle into
    /// the engine again. Such an import is reported here as completed and <see cref="IsFaulted"/>, so a host
    /// polling this cannot poll forever. Starting the import again on the restored engine works and refetches.
    /// </remarks>
    public bool IsCompleted
    {
        get
        {
            ObserveAbandonment();
            return _completed;
        }
    }

    /// <summary>Whether the import finished by failing.</summary>
    public bool IsFaulted
    {
        get
        {
            ObserveAbandonment();
            return _faulted;
        }
    }

    /// <summary>The imported module's namespace once the import has succeeded, otherwise null.</summary>
    public ObjectInstance? Namespace
    {
        get
        {
            ObserveAbandonment();
            return _namespace;
        }
    }

    /// <summary>
    /// The error the import failed with once it has failed, otherwise null. A loading failure reported by the
    /// module loader and a module body that threw both arrive here.
    /// </summary>
    public JsValue? Error
    {
        get
        {
            ObserveAbandonment();
            return _error;
        }
    }

    /// <summary>
    /// The imported module's namespace.
    /// </summary>
    /// <exception cref="InvalidOperationException">The import has not finished yet.</exception>
    /// <exception cref="PromiseRejectedException">The import failed.</exception>
    public ObjectInstance GetResult()
    {
        if (!IsCompleted)
        {
            Throw.InvalidOperationException("The module import has not completed. Give the engine turns with engine.Advanced.ProcessTasks() until IsCompleted is true, or await Engine.Modules.ImportAsync instead.");
        }

        if (IsFaulted)
        {
            throw new PromiseRejectedException(_error!);
        }

        return _namespace!;
    }

    /// <summary>
    /// Fails an import the engine has fenced off. <c>Engine.Advanced.RestoreGlobalSnapshot</c> ends the
    /// evaluation cycle, and every completion registered in it is discarded at dequeue rather than run — so
    /// the reactions that would settle this operation are exactly the ones that can no longer fire. Nothing
    /// pushes that news: the operation is not a promise the engine tracks, and the documented contract is
    /// that the host polls. Deriving it from the engine's generation on read is what keeps that poll from
    /// being a poll forever, and it cannot miss an operation the way a registry of live ones could.
    /// </summary>
    private void ObserveAbandonment()
    {
        if (_completed || _engine.EventLoopGeneration == _generation)
        {
            return;
        }

        Fail(_engine.Realm.Intrinsics.Error.Construct(
            "The module import was abandoned: Engine.Advanced.RestoreGlobalSnapshot ended the evaluation cycle it was started in, so nothing it is waiting for can settle into this engine any more. Start the import again on the restored engine."));
    }

    internal void Fulfil(JsValue value)
    {
        if (_completed)
        {
            return;
        }

        _completed = true;
        _namespace = value as ObjectInstance;
    }

    internal void Fail(JsValue error)
    {
        if (_completed)
        {
            return;
        }

        _completed = true;
        _faulted = true;
        _error = error;
    }
}
