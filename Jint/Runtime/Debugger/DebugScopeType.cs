namespace Jint.Runtime.Debugger;

/// <summary>
/// Variable scope type.
/// These mirror <see href="https://chromedevtools.github.io/devtools-protocol/tot/Debugger/#type-Scope">Chrome DevTools Protocol</see>.
/// </summary>
public enum DebugScopeType
{
    /// <summary>
    /// Global scope bindings.
    /// </summary>
    /// <remarks>
    /// A scope chain will only include one scope of this type.
    /// </remarks>
    Global,

    /// <summary>
    /// Block scope bindings (let/const) defined at top level.
    /// </summary>
    /// <remarks>
    /// A scope chain will only include one scope of this type.
    /// </remarks>
    Script,

    /// <summary>
    /// Function local bindings.
    /// </summary>
    /// <remarks>
    /// Function scoped variables.
    /// Note that variables in outer functions are in <see cref="Closure"/> scopes.
    /// A scope chain will only include one scope of this type.
    /// </remarks>
    Local,

    /// <summary>
    /// Block scoped bindings.
    /// </summary>
    /// <remarks>
    /// This scope is not used for block scoped variables (let/const) declared at the top level of the <see cref="Global">global</see> scope.
    /// Unlike Chromium V8, it *is* used as a separate scope for block scoped variables declared at the top level of a function.
    /// A scope chain may include more than one scope of this type.
    /// </remarks>
    Block,

    /// <summary>
    /// Catch scope bindings.
    /// </summary>
    /// <remarks>
    /// This scope only includes the argument of a <c>catch</c> clause in a <c>try/catch</c> statement.
    /// A scope chain may include more than one scope of this type.
    /// </remarks>
    Catch,

    /// <summary>
    /// Function scope bindings in outer functions.
    /// </summary>
    /// <remarks>
    /// Unlike Chromium V8, which will optimize variables out that aren't referenced from the inner scope,
    /// Jint includes local variables from the outer function in this scope.
    /// A scope chain may include more than one scope of this type.
    /// </remarks>
    Closure,

    /// <summary>
    /// With scope bindings.
    /// </summary>
    /// <remarks>
    /// Includes the bindings created from properties of object used as argument to a <c>with</c> statement.
    /// A scope chain may include more than one scope of this type.
    /// </remarks>
    With,

    /// <summary>
    /// Eval scope bindings.
    /// </summary>
    /// <remarks>Variables declared in an evaluated string. Not implemented.</remarks>
    Eval,

    /// <summary>
    /// Module scope bindings.
    /// </summary>
    Module,

    /// <summary>
    /// WebAssembly expression stack bindings.
    /// </summary>
    /// <remarks>Not currently implemented by Jint.</remarks>
    WasmExpressionStack
}
