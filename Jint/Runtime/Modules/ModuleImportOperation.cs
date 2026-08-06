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
    internal ModuleImportOperation(JsValue promise)
    {
        Promise = promise;
    }

    /// <summary>
    /// The promise the import settles into — the same value a dynamic <c>import()</c> in script would produce.
    /// Useful for handing the import to script; a host tracking it from .NET wants <see cref="IsCompleted"/>.
    /// </summary>
    public JsValue Promise { get; }

    /// <summary>
    /// Whether the import has finished, successfully or not. Becomes true during a turn of the event loop, so
    /// it is only ever worth re-reading after the engine has been given one.
    /// </summary>
    public bool IsCompleted { get; private set; }

    /// <summary>Whether the import finished by failing.</summary>
    public bool IsFaulted { get; private set; }

    /// <summary>The imported module's namespace once the import has succeeded, otherwise null.</summary>
    public ObjectInstance? Namespace { get; private set; }

    /// <summary>
    /// The error the import failed with once it has failed, otherwise null. A loading failure reported by the
    /// module loader and a module body that threw both arrive here.
    /// </summary>
    public JsValue? Error { get; private set; }

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
            throw new PromiseRejectedException(Error!);
        }

        return Namespace!;
    }

    internal void Fulfil(JsValue value)
    {
        if (IsCompleted)
        {
            return;
        }

        IsCompleted = true;
        Namespace = value as ObjectInstance;
    }

    internal void Fail(JsValue error)
    {
        if (IsCompleted)
        {
            return;
        }

        IsCompleted = true;
        IsFaulted = true;
        Error = error;
    }
}
