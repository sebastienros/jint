using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime.Environments;
using Environment = Jint.Runtime.Environments.Environment;

namespace Jint.Runtime.Debugger;

/// <summary>
/// Scope information, bindings, and values for a single scope in the scope chain
/// </summary>
public sealed class DebugScope
{
    private readonly Environment _record;
    private string[]? _bindingNames;

    internal DebugScope(DebugScopeType type, Environment record, bool isTopLevel)
    {
        ScopeType = type;
        _record = record;
        BindingObject = record is ObjectEnvironment objEnv ? objEnv._bindingObject : null;
        IsTopLevel = isTopLevel;
    }

    /// <summary>
    /// The type of scope. Scope types are the same as defined by Chrome devtools protocol.
    /// </summary>
    public DebugScopeType ScopeType { get; }

    /// <summary>
    /// For <see cref="DebugScopeType.Block">block</see> scopes, indicates whether this scope is at the top level of a containing function.
    /// </summary>
    /// <remarks>
    /// Block scopes at the top level of a function are combined with Local scope in Chromium.
    /// This property facilitates implementing the same "flattening" in e.g. a UI. Because empty scopes are excluded in the scope chain,
    /// top level cannot be determined from the scope chain order alone.
    /// </remarks>
    public bool IsTopLevel { get; }

    /// <summary>
    /// Names of all bindings in the scope.
    /// </summary>
    public IReadOnlyList<string> BindingNames => _bindingNames ??= _record.GetAllBindingNames();

    /// <summary>
    /// Binding object for ObjectEnvironmentRecords - that is, Global scope and With scope. Null for other scopes.
    /// </summary>
    /// <remarks>
    /// This is mainly useful as an optimization for devtools, allowing the BindingObject to be serialized directly rather than
    /// building a new transient object in response to e.g. Runtime.getProperties.
    /// </remarks>
    public ObjectInstance? BindingObject { get; }

    /// <summary>
    /// Retrieves the value of a specific binding. Note that some bindings (e.g. uninitialized let/const) may return null.
    /// </summary>
    /// <param name="name">Binding name</param>
    /// <returns>Value of the binding</returns>
    public JsValue? GetBindingValue(string name)
    {
        _record.TryGetBinding(new Environment.BindingName(name), strict: false, out var result);
        return result;
    }

    /// <summary>
    /// Sets the value of an existing binding.
    /// </summary>
    /// <param name="name">Binding name</param>
    /// <param name="value">New value of the binding</param>
    public void SetBindingValue(string name, JsValue value)
    {
        _record.SetMutableBinding(name, value, strict: true);
    }
}
