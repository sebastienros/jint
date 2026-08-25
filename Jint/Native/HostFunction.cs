using System.Runtime.CompilerServices;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.Native;

/// <summary>
/// Base class for a host-defined callable — a function object whose body is a method on a class the host
/// writes, rather than a closure it hands over. Deriving from it gives an object script sees as an ordinary
/// built-in function: <c>typeof</c> reports <c>"function"</c>, it inherits from <c>Function.prototype</c> (so
/// <c>call</c>, <c>apply</c> and <c>bind</c> work and <c>instanceof Function</c> is <see langword="true"/>),
/// it carries own <c>name</c> and <c>length</c> properties with the attributes the specification gives a
/// built-in, and it can be passed anywhere a callback is expected.
/// </summary>
/// <remarks>
/// <para>
/// A subclass supplies exactly one member — <see cref="Invoke"/> — and this class derives the rest. It is the
/// class-shaped sibling of <see cref="Jint.Runtime.Interop.ClrFunction"/>, which is sealed and takes the body
/// as a delegate: reach for that when the body is a lambda, and for this one when the callable has state,
/// wants a name a stack trace can show, or is one member of a family that shares a base. It is the callable
/// sibling of <see cref="Jint.Native.Object.ArrayLikeObject"/> and
/// <see cref="Jint.Native.Object.NamedPropertyObject"/>, and follows the same shape: few abstract members,
/// everything derived from them sealed.
/// </para>
/// <para>
/// <b><see cref="Function.Function.Call"/> is sealed.</b> A host declares its body once, in
/// <see cref="Invoke"/>, and the base owns what surrounds it — which is what lets the surrounding behaviour
/// (today, routing an escaping CLR exception through <see cref="Options.InteropOptions.ExceptionHandler"/>
/// exactly as <c>ClrFunction</c> does, so the two spellings of "a host function threw" are indistinguishable
/// from script) be a property of the class rather than something every subclass has to reproduce.
/// </para>
/// <para>
/// <b>It is not a constructor.</b> <c>new hostFunction()</c> raises a <c>TypeError</c>, which is what the
/// specification says for a built-in function that has no <c>[[Construct]]</c>. A host that wants
/// <c>new</c> derives from <see cref="Constructor"/> instead, which is the same deal for the other half of
/// the callable surface: supply <see cref="Constructor.Construct"/> and the base supplies the rest.
/// </para>
/// <para>
/// <b>The <c>arguments</c> array is borrowed, never owned.</b> The engine pools the array it passes to
/// <see cref="Invoke"/> and may hand the same instance to the next call, so a subclass that needs the values
/// after it returns must copy them. Reading them during the call is always safe.
/// </para>
/// <para>
/// <b>Realm.</b> The function belongs to the engine's principal realm, whatever else that engine has been
/// asked to create in the meantime — the same rule <c>ClrFunction</c> follows, so a host function built after
/// a <c>ShadowRealm</c> still inherits from the <c>Function.prototype</c> the surrounding script can reach.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// file sealed class CounterFunction : HostFunction
/// {
///     private int _count;
///
///     public CounterFunction(Engine engine) : base(engine, "next", length: 1)
///     {
///     }
///
///     protected override JsValue Invoke(JsValue thisObject, JsValue[] arguments)
///     {
///         var step = arguments.Length == 0 ? 1 : (int) TypeConverter.ToNumber(arguments[0]);
///         return new JsNumber(_count += step);
///     }
/// }
///
/// engine.SetValue("next", new CounterFunction(engine));
/// </code>
/// </example>
public abstract class HostFunction : Function.Function
{
    private readonly bool _bubbleExceptions;

    /// <summary>
    /// Creates the function object and installs its own <c>name</c> and <c>length</c> properties, each
    /// <c>{ writable: false, enumerable: false, configurable: true }</c> — the attributes
    /// <see href="https://tc39.es/ecma262/#sec-built-in-function-objects">§10.3</see> gives a built-in.
    /// </summary>
    /// <param name="engine">The engine the function belongs to. A <see cref="JsValue"/> is never shared
    /// between engines, so this is the engine it may be installed on and no other.</param>
    /// <param name="name">The value of the function's own <c>name</c> property. May be empty for an
    /// anonymous function, but not <see langword="null"/>.</param>
    /// <param name="length">The value of the function's own <c>length</c> property — the number of
    /// arguments script should expect to pass, which never limits how many it may pass.</param>
    protected HostFunction(Engine engine, string name, int length = 0)
        : base(Validated(engine, name), engine.Realm, JsString.CachedCreate(name))
    {
        if (length < 0)
        {
            Throw.ArgumentOutOfRangeException(nameof(length), "A function's length cannot be negative.");
        }

        // The engine's ORIGINAL intrinsics, not the running realm's: a host function built after a
        // ShadowRealm exists must still be a Function of the realm the surrounding script can reach.
        _prototype = engine._originalIntrinsics.Function.PrototypeObject;

        _length = new PropertyDescriptor(JsNumber.Create(length), PropertyFlag.OnlyConfigurable);

        _bubbleExceptions = ClrExceptionsBubble(engine);
    }

    /// <summary>
    /// The function's body. Called every time script invokes the function, however it does so — directly, as
    /// a callback, through <c>call</c>/<c>apply</c>/<c>bind</c>, or from host code through
    /// <see cref="Engine.Call(JsValue, JsValue[])"/>.
    /// </summary>
    /// <param name="thisObject">The <c>this</c> value of the call. <see cref="JsValue.Undefined"/> for a
    /// plain call, since a host function is a built-in and therefore strict.</param>
    /// <param name="arguments">The arguments, which may be empty and never <see langword="null"/>. Use
    /// <c>arguments.At(i)</c> to read a position that may not have been passed. The array is pooled by the
    /// engine — copy anything that must outlive the call.</param>
    /// <returns>The call's result. Return <see cref="JsValue.Undefined"/> rather than
    /// <see langword="null"/> for a function that produces nothing.</returns>
    protected abstract JsValue Invoke(JsValue thisObject, JsValue[] arguments);

    /// <summary>
    /// Sealed: <see cref="Invoke"/> is the hook. See the class remarks for why.
    /// </summary>
    protected internal sealed override JsValue Call(JsValue thisObject, JsCallArguments arguments)
        => _bubbleExceptions ? Invoke(thisObject, arguments) : CallGuarded(thisObject, arguments);

    [MethodImpl(MethodImplOptions.NoInlining)]
    private JsValue CallGuarded(JsValue thisObject, JsCallArguments arguments)
    {
        try
        {
            return Invoke(thisObject, arguments);
        }
        catch (Exception e) when (e is not JavaScriptException && !Throw.MustPropagateHostException(e))
        {
            return TranslateClrException(_engine, e);
        }
    }

    /// <summary>
    /// Validates the constructor's arguments before the base call, which cannot be preceded by statements.
    /// Returns <paramref name="engine"/> so it can stand in the first argument position: the arguments after
    /// it are evaluated later, so <c>engine.Realm</c> and <paramref name="name"/> are both known good by then.
    /// </summary>
    private static Engine Validated(Engine engine, string name)
    {
        if (engine is null)
        {
            Throw.ArgumentNullException(nameof(engine));
        }

        if (name is null)
        {
            Throw.ArgumentNullException(nameof(name));
        }

        return engine;
    }
}
