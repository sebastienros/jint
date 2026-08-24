using System.Diagnostics.CodeAnalysis;
using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;

namespace Jint;

/// <summary>
/// The one implementation behind <see cref="Engine.SetValue(string, JsValue)"/> and
/// <see cref="Native.ShadowRealm.ShadowRealm.SetValue(string, JsValue)"/> and their overloads: the value
/// conversion, and the property installation onto a global object.
/// </summary>
/// <remarks>
/// <para>
/// The two public surfaces differ in exactly two things — which global object is written, and what is
/// returned for chaining — so everything else lives here once. That is not tidiness: the two used to
/// duplicate the bodies and drifted apart on the parts a compiler cannot compare, and the drift went the
/// wrong way. <c>ShadowRealm</c>'s <see cref="Delegate"/> overload carried
/// <c>[RequiresUnreferencedCode("User supplied delegate")]</c>, which warns about a registration that
/// reflects over nothing a trimmer can remove, while its reflective <c>object</c> overload — the actual
/// trimming hazard — carried no annotation at all, so a trimming host projecting a host object into a
/// shadow realm got no diagnostic and a member that reads as <c>undefined</c> at run time.
/// </para>
/// <para>
/// Each method here assumes the caller already holds the engine's host-call reservation
/// (<c>Engine.EnterHostCall</c>), which is also what makes it safe for the caller to have read the target
/// global object off its realm.
/// </para>
/// </remarks>
internal static class GlobalValueRegistration
{
    /// <summary>
    /// The message both <c>SetValue(string, object?)</c> overloads carry. It is a single constant so the
    /// two cannot say different things about the same hazard, and so a reader of either baseline sees the
    /// same sentence.
    /// </summary>
    internal const string RequiresUnreferencedCodeMessage = "The runtime type of obj is not known at the call site, so the members Jint resolves by reflection cannot be preserved by the trimmer, and a removed one reads as undefined rather than as an error. Prefer SetValue<T>(string, T), whose type parameter carries [DynamicallyAccessedMembers]; pass an already-built JsValue; or root the type yourself.";

    /// <summary>
    /// Installs an already-built value as an ordinary configurable/enumerable/writable data property.
    /// </summary>
    internal static void Register(ObjectInstance globalObject, string name, JsValue value)
    {
        globalObject.Set(name, value);
    }

    /// <summary>
    /// Installs a delegate as a non-enumerable function property, which is what ECMA-262 §17 gives a
    /// built-in global function — and the one registration whose property attributes differ from every
    /// other overload's.
    /// </summary>
    internal static void RegisterDelegate(Engine engine, ObjectInstance globalObject, string name, Delegate value)
    {
        globalObject.FastSetProperty(name, new PropertyDescriptor(new DelegateWrapper(engine, value), PropertyFlag.NonEnumerable));
    }

    /// <summary>
    /// Converts and installs a value whose type is not known at the call site.
    /// </summary>
    [RequiresUnreferencedCode(RequiresUnreferencedCodeMessage)]
    internal static void RegisterObject(Engine engine, ObjectInstance globalObject, string name, object? obj)
    {
        var value = obj is Type t
            ? TypeReference.CreateTypeReference(engine, t)
            : JsValue.FromObject(engine, obj);

        globalObject.Set(name, value);
    }

    /// <summary>
    /// Installs a CLR type as a constructor script can name.
    /// </summary>
    /// <remarks>
    /// The installation is written out rather than routed back through <see cref="RegisterTyped{T}"/>, and
    /// that is load-bearing. <c>Engine.SetValue(string, Type)</c> used to end in
    /// <c>SetValue(name, TypeReference.CreateTypeReference(this, type))</c>, where <c>TypeReference</c> is
    /// more specific than <c>JsValue</c> and so bound to the generic overload — whose
    /// <c>[DynamicallyAccessedMembers]</c> then preserved every public method of <c>TypeReference</c>,
    /// <see cref="TypeReference.CreateTypeReference(Engine, Type)"/> among them, which is an
    /// <c>IL2111</c> the method had to suppress. Going straight to the global object clears it.
    /// </remarks>
    internal static void RegisterType(Engine engine, ObjectInstance globalObject, string name, [DynamicallyAccessedMembers(InteropHelper.DefaultDynamicallyAccessedMemberTypes)] Type type)
    {
        globalObject.Set(name, TypeReference.CreateTypeReference(engine, type));
    }

    /// <summary>
    /// Converts and installs a value whose type <em>is</em> known at the call site, so the trimmer is told
    /// what to preserve rather than warned that it cannot know.
    /// </summary>
    internal static void RegisterTyped<[DynamicallyAccessedMembers(InteropHelper.DefaultDynamicallyAccessedMemberTypes)] T>(Engine engine, ObjectInstance globalObject, string name, T? obj)
    {
        if (obj is Type t)
        {
            RegisterType(engine, globalObject, name, t);
        }
        else
        {
            globalObject.Set(name, JsValue.FromObject(engine, obj));
        }
    }

    /// <summary>
    /// The array form of <see cref="RegisterTyped{T}"/>, so that the annotation lands on the
    /// <em>element</em> type.
    /// </summary>
    internal static void RegisterArray<[DynamicallyAccessedMembers(InteropHelper.DefaultDynamicallyAccessedMemberTypes)] T>(Engine engine, ObjectInstance globalObject, string name, T[]? obj)
    {
        globalObject.Set(name, JsValue.FromObject(engine, obj));
    }
}
