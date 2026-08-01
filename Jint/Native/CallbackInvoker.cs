using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Jint.Native.Function;
using Jint.Pooling;
using Jint.Runtime;
using Jint.Runtime.Interpreter;

namespace Jint.Native;

/// <summary>
/// The user callback a built-in invokes once per element, with the decision of HOW to invoke it
/// taken once — when the invoker is built, outside the loop — instead of per element.
/// </summary>
/// <remarks>
/// <para>
/// The array lane is exactly what these built-ins did before: one argument array, rented from the
/// pool (or allocated) once, its slots rewritten per element, invoked through
/// <see cref="ICallable.Call"/>. The register lane hands the same values over in locals through
/// <see cref="ScriptFunction.CallFromRegisters"/>, so an interpreted callback's fixed parameter
/// slots are filled straight from registers — no array stores on the way in, no reads back out,
/// and no array rented at all.
/// </para>
/// <para>
/// The eligibility question — is this a plain interpreted function whose instantiation is the
/// fixed-slot fast path — depends only on the callback, which does not change across elements, so
/// it is asked once. Every input to the answer is fixed for the callback's lifetime:
/// <see cref="JintFunctionDefinition.State"/> is computed once and cached on the immutable AST
/// node, <c>_isClassConstructor</c> is decided when a class is defined, and
/// <c>Engine._isDebugMode</c> is readonly. A callback that mutates the collection mid-iteration,
/// throws, or is a revoked proxy therefore cannot invalidate the verdict.
/// </para>
/// <para>
/// Most of these callbacks end in the collection itself — the <c>(value, index, target)</c> shape —
/// which is the same value on every call. Handing it to the factory as <c>lastArgument</c> keeps
/// the array lane's hoisted store: only the leading arguments are written per element, exactly as
/// before, and the register lane passes it from a field.
/// </para>
/// </remarks>
[StructLayout(LayoutKind.Auto)]
internal readonly struct CallbackInvoker
{
    private readonly ICallable _callable;

    // Non-null together, and only when the register lane took: the callee and the State its own
    // [[Call]] would have looked up anyway.
    private readonly ScriptFunction? _registerTarget;
    private readonly JintFunctionDefinition.State? _registerState;

    // Non-null exactly when Return() has something to give back — so a default instance, a
    // register-lane invoker and a caller-allocated one are all safe to Return().
    private readonly JsValueArrayPool? _pool;

    // Empty in the register lane, which never materializes an argument array.
    private readonly JsValue[] _arguments;

    private readonly JsValue _lastArgument;
    private readonly bool _hasLastArgument;
    private readonly int _argumentCount;

    private CallbackInvoker(Engine engine, ICallable callable, int argumentCount, JsValue? lastArgument, bool pooled)
    {
        Debug.Assert(argumentCount is > 0 and <= 4, "the register lane serves at most four arguments");

        _callable = callable;
        _argumentCount = argumentCount;
        _hasLastArgument = lastArgument is not null;
        _lastArgument = lastArgument ?? JsValue.Undefined;

        // Same gate ScriptFunction.Call applies before taking its own register-backed arm, asked
        // here once for the whole loop. Initialize() is what the callee's [[Call]] does first
        // anyway, and the State it returns lives on the AST node, so the reference stays valid.
        if (!engine._isDebugMode
            && callable is ScriptFunction { _isClassConstructor: false } scriptFunction
            && scriptFunction._functionDefinition!.Initialize() is { SupportsRegisterCall: true } state)
        {
            _registerTarget = scriptFunction;
            _registerState = state;
            _pool = null;
            _arguments = [];
            return;
        }

        _registerTarget = null;
        _registerState = null;

        if (pooled)
        {
            _pool = engine._jsValueArrayPool;
            // The pool's factories allocate exactly JsValue[n], so the fills below may bypass the
            // covariant array store type check.
            _arguments = _pool.RentArray(argumentCount);
        }
        else
        {
            _pool = null;
            _arguments = new JsValue[argumentCount];
        }

        if (_hasLastArgument)
        {
            Arguments.WriteNoTypeCheck(_arguments, argumentCount - 1, _lastArgument);
        }
    }

    /// <summary>
    /// An invoker whose argument array — if it needs one at all — comes from the engine's pool.
    /// The caller must <see cref="Return"/> it on the paths where it returns the array today.
    /// </summary>
    /// <remarks>
    /// <c>lastArgument</c> is the callback's final argument when it is the same value on every call
    /// (the collection, for the <c>(value, index, target)</c> shape), and <c>null</c> when every
    /// argument varies. When supplied it occupies the last of <c>argumentCount</c> positions, so
    /// the caller passes one fewer argument to <see cref="Call(JsValue, JsValue)"/> and its
    /// siblings.
    /// </remarks>
    public static CallbackInvoker Rent(Engine engine, ICallable callable, int argumentCount, JsValue? lastArgument = null)
        => new(engine, callable, argumentCount, lastArgument, pooled: true);

    /// <summary>
    /// An invoker that allocates its own argument array — and only if it turns out to need one —
    /// for callers holding it longer than a single built-in call, such as a sort comparer living
    /// for the whole sort. Nothing to <see cref="Return"/>.
    /// </summary>
    public static CallbackInvoker Create(Engine engine, ICallable callable, int argumentCount, JsValue? lastArgument = null)
        => new(engine, callable, argumentCount, lastArgument, pooled: false);

    /// <summary>
    /// Invokes the callback with one leading argument, plus the fixed last argument if one was
    /// supplied to the factory.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public JsValue Call(JsValue thisArgument, JsValue argument0)
    {
        Debug.Assert(_argumentCount == (_hasLastArgument ? 2 : 1), "leading arguments plus the fixed one must be the callback's arity");

        if (_registerTarget is not null)
        {
            return _registerTarget.CallFromRegisters(thisArgument, argument0, _lastArgument, JsValue.Undefined, JsValue.Undefined, _argumentCount, _registerState!);
        }

        var arguments = _arguments;
        Arguments.WriteNoTypeCheck(arguments, 0, argument0);
        return _callable.Call(thisArgument, arguments);
    }

    /// <summary>
    /// Invokes the callback with two leading arguments, plus the fixed last argument if one was
    /// supplied to the factory.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public JsValue Call(JsValue thisArgument, JsValue argument0, JsValue argument1)
    {
        Debug.Assert(_argumentCount == (_hasLastArgument ? 3 : 2), "leading arguments plus the fixed one must be the callback's arity");

        if (_registerTarget is not null)
        {
            return _registerTarget.CallFromRegisters(thisArgument, argument0, argument1, _lastArgument, JsValue.Undefined, _argumentCount, _registerState!);
        }

        var arguments = _arguments;
        Arguments.WriteNoTypeCheck(arguments, 0, argument0);
        Arguments.WriteNoTypeCheck(arguments, 1, argument1);
        return _callable.Call(thisArgument, arguments);
    }

    /// <summary>
    /// Invokes the callback with three leading arguments, plus the fixed last argument if one was
    /// supplied to the factory — the <c>reduce</c> shape.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public JsValue Call(JsValue thisArgument, JsValue argument0, JsValue argument1, JsValue argument2)
    {
        Debug.Assert(_argumentCount == (_hasLastArgument ? 4 : 3), "leading arguments plus the fixed one must be the callback's arity");

        if (_registerTarget is not null)
        {
            return _registerTarget.CallFromRegisters(thisArgument, argument0, argument1, argument2, _lastArgument, _argumentCount, _registerState!);
        }

        var arguments = _arguments;
        Arguments.WriteNoTypeCheck(arguments, 0, argument0);
        Arguments.WriteNoTypeCheck(arguments, 1, argument1);
        Arguments.WriteNoTypeCheck(arguments, 2, argument2);
        return _callable.Call(thisArgument, arguments);
    }

    /// <summary>
    /// Gives a pooled argument array back. A no-op for an invoker that rented none — the register
    /// lane, a <see cref="Create"/>d one, and a <c>default</c> instance — so callers keep whatever
    /// conditional structure they had around the rent.
    /// </summary>
    public void Return() => _pool?.ReturnArray(_arguments);
}
