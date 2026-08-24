using System.Diagnostics.CodeAnalysis;
using Jint.Native;
using Jint.Runtime.Interop.Reflection;

namespace Jint.Runtime.Interop;

/// <summary>
/// Collects the typed member lanes of one <see cref="JsAccessibleAttribute"/>-annotated type. Instances are
/// handed to generated code by <see cref="JsAccessibleRegistry.Register"/> and are not useful outside it.
/// </summary>
/// <remarks>
/// The four delegates <see cref="AddMember"/> takes are the four lanes the engine already builds at run time
/// for every CLR property and field it reaches (see <c>CompiledMemberAccessor</c>), in the same shapes and
/// with the same contracts, which is what makes a generated member behave identically to a reflected one.
/// The generator emits them as ordinary <see langword="static"/> methods, so each one costs a cached
/// method-group delegate and no closure.
/// </remarks>
public sealed class JsAccessibleTypeBuilder
{
    private readonly Type _type;
    private readonly Dictionary<string, ReflectionAccessor> _members = new(TypeResolver.DefaultNameComparer);

    internal JsAccessibleTypeBuilder(Type type)
    {
        _type = type;
    }

    /// <summary>
    /// Declares one readable and/or writable member — a property or a field.
    /// </summary>
    /// <param name="name">The member's CLR name, matched the way the engine matches reflected member names.</param>
    /// <param name="memberType">The member's declared type. Steers the conversion the engine applies to a written value.</param>
    /// <param name="read">
    /// Produces the member's value as a <see cref="JsValue"/> without boxing. Supply it only for the member
    /// types the engine's own typed read lane covers (<see cref="int"/>, <see cref="long"/>,
    /// <see cref="double"/>, <see cref="bool"/>, <see cref="string"/>, and <see cref="JsValue"/> or a
    /// subtype); <see langword="null"/> otherwise, and <paramref name="readBoxed"/> answers instead.
    /// </param>
    /// <param name="readBoxed">
    /// Reads the member into a boxed CLR value, leaving the engine's conversion untouched.
    /// <see langword="null"/> for a write-only member, which is then not readable.
    /// </param>
    /// <param name="write">
    /// Writes an exactly-typed <see cref="JsValue"/> straight to the member, returning <see langword="false"/>
    /// — having written nothing — for a value that is not exactly the expected JavaScript type, so the engine
    /// runs its full conversion instead. Supply it only for a member typed <see cref="int"/>,
    /// <see cref="long"/>, <see cref="double"/>, <see cref="bool"/>, <see cref="string"/> or exactly
    /// <see cref="JsValue"/>; <see langword="null"/> otherwise.
    /// </param>
    /// <param name="writeBoxed">
    /// Writes an already-converted CLR value whose runtime type the member accepts.
    /// <see langword="null"/> for a read-only member, which is then not writable.
    /// </param>
    public void AddMember(
        string name,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicFields)] Type memberType,
        Func<object, JsValue>? read,
        Func<object, object?>? readBoxed,
        Func<object, JsValue, bool>? write,
        Action<object, object?>? writeBoxed)
    {
        if (name is null)
        {
            Throw.ArgumentNullException(nameof(name));
        }

        if (memberType is null)
        {
            Throw.ArgumentNullException(nameof(memberType));
        }

        _members[name] = new GeneratedMemberAccessor(memberType, read, readBoxed, write, writeBoxed);
    }

    /// <summary>
    /// Declares one method. <paramref name="invoke"/> receives the resolved CLR receiver and the call's
    /// arguments and returns the result already converted to a <see cref="JsValue"/>.
    /// </summary>
    /// <param name="name">The method's CLR name.</param>
    /// <param name="length">
    /// The arity reported as the JavaScript function's <c>length</c>, i.e. the method's parameter count.
    /// </param>
    /// <param name="invoke">
    /// Invokes the method. The engine has already unwrapped the receiver and verified that it is an instance
    /// of the registered type, so the first argument can be cast to it unconditionally.
    /// </param>
    public void AddMethod(string name, int length, Func<Engine, object, JsValue[], JsValue> invoke)
    {
        if (name is null)
        {
            Throw.ArgumentNullException(nameof(name));
        }

        if (invoke is null)
        {
            Throw.ArgumentNullException(nameof(invoke));
        }

        _members[name] = new GeneratedMethodAccessor(_type, name, length, invoke);
    }

    internal JsAccessibleRegistry.GeneratedTypeMembers Build() => new(_members);
}
