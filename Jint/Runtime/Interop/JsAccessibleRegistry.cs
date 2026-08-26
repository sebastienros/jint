using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using Jint.Runtime.Interop.Reflection;

namespace Jint.Runtime.Interop;

/// <summary>
/// Where the code emitted for a <see cref="JsAccessibleAttribute"/>-annotated type hands its typed member
/// lanes to the engine. The generator emits one <c>RegisterAll()</c> per assembly that calls
/// <see cref="Register"/> once per annotated type; a host calls that entry point itself, so registration is
/// an ordinary, observable, testable statement rather than something an assembly load did.
/// </summary>
/// <remarks>
/// <para>
/// The registry is process-wide, which is what a <see cref="JsAccessibleAttribute"/> already implies: the
/// members of a type do not vary by engine, and the lanes registered here close over nothing but the target
/// instance handed to them. It composes with <see cref="TypeResolver"/>'s own process-wide accessor cache —
/// registering a type after an engine has already resolved one of its members invalidates that type's
/// entries in that cache, so a late <c>RegisterAll()</c> is merely late rather than silently ineffective,
/// and costs the types it did not name nothing.
/// </para>
/// <para>
/// Registering the same type twice replaces its previous entry. Registering is safe from several threads;
/// resolving is lock-free.
/// </para>
/// <para>
/// <b>What the registry does not decide.</b> Whether a registered member is reachable at all, and under
/// which name. <see cref="TypeResolver.MemberFilter"/>, <see cref="TypeResolver.MemberNameCreator"/>,
/// <see cref="TypeResolver.MemberNameComparer"/> and the reported binding flags apply to a generated
/// member exactly as they do to a reflected one, so a member a host hid stays hidden and a member it
/// renamed answers only to the new name.
/// </para>
/// </remarks>
public static class JsAccessibleRegistry
{
    private static readonly ConcurrentDictionary<Type, GeneratedTypeMembers> _byType = new();

    /// <summary>
    /// Bumped by every <see cref="Register"/> call. <see cref="TypeResolver"/> compares the value it last
    /// saw against this one and, when they differ, drops the accessor cache entries of the <b>registered
    /// types</b>, so a member resolved before its type was registered does not keep answering from the cache
    /// afterwards.
    /// </summary>
    /// <remarks>
    /// It is a counter and not a log of what changed, so the resolver cannot ask which type moved and asks
    /// <see cref="IsRegistered"/> about each cached entry instead. That over-invalidates by the types
    /// registered earlier and under-invalidates by nothing, where dropping the whole cache made one
    /// registration cost every unrelated engine in the process everything it had resolved (#3368).
    /// </remarks>
    private static int _generation;

    internal static int Generation => Volatile.Read(ref _generation);

    /// <summary>
    /// Whether <paramref name="type"/> has been registered, and therefore whether a cached accessor for it
    /// may have been resolved before its registration. Every consult of this registry is keyed on the exact
    /// target type — <see cref="TryGetMember"/>, and <see cref="TryGetAccessorForDeclaredMember"/>, which
    /// additionally requires the reflected member to be declared on that very type — so no other type's
    /// resolution can have changed.
    /// </summary>
    internal static bool IsRegistered(Type type) => _byType.ContainsKey(type);

    /// <summary>
    /// Declares the typed member lanes of one <see cref="JsAccessibleAttribute"/>-annotated type. Called by
    /// generated code; a host calls the generated <c>RegisterAll()</c> instead of calling this directly.
    /// </summary>
    /// <param name="type">The annotated type. Members are resolved for this exact type, never for a subclass.</param>
    /// <param name="members">Receives the builder the generated code adds each member to.</param>
    public static void Register(Type type, Action<JsAccessibleTypeBuilder> members)
    {
        if (type is null)
        {
            Throw.ArgumentNullException(nameof(type));
        }

        if (members is null)
        {
            Throw.ArgumentNullException(nameof(members));
        }

        var builder = new JsAccessibleTypeBuilder(type);
        members(builder);

        _byType[type] = builder.Build();
        Interlocked.Increment(ref _generation);
    }

    /// <summary>
    /// Whether anything has been registered at all. The whole lane is skipped when nothing has, so a host
    /// that never calls a generated <c>RegisterAll()</c> pays a single field read per member resolution.
    /// </summary>
    internal static bool IsEmpty => _byType.IsEmpty;

    /// <summary>
    /// The name-keyed lookup, matched the way <see cref="TypeResolver"/>'s default name comparer matches a
    /// reflected member. Only equivalent to the reflected selection while the host has changed nothing that
    /// steers it, which is why <see cref="TypeResolver"/> and not this method decides when to ask.
    /// </summary>
    internal static bool TryGetMember(Type type, string memberName, out GeneratedMember member)
    {
        if (_byType.TryGetValue(type, out var members))
        {
            return members.TryGet(memberName, out member);
        }

        member = default;
        return false;
    }

    /// <summary>
    /// The generated lane for a member the reflected selection already chose, or <see langword="false"/> if
    /// that member is not one of this type's registered ones. Everything registered is a public instance
    /// property, field or method declared on <paramref name="type"/> itself, so anything else — an inherited
    /// or static member, an extension method, a member whose name merely reads the same — is not it.
    /// </summary>
    internal static bool TryGetAccessorForDeclaredMember(
        Type type,
        MemberInfo reflected,
        [NotNullWhen(true)] out ReflectionAccessor? accessor)
    {
        accessor = null;

        if (!_byType.TryGetValue(type, out var members)
            || !members.TryGet(reflected.Name, out var member)
            // the name-keyed lookup is deliberately fuzzy, so the entry it returned still has to be the
            // member being asked about rather than one whose name only differs in the first character
            || !string.Equals(member.Name, reflected.Name, StringComparison.Ordinal)
            || reflected.DeclaringType != type)
        {
            return false;
        }

        var matches = reflected switch
        {
            PropertyInfo property => member.Accessor is GeneratedMemberAccessor && !IsStatic(property),
            FieldInfo field => member.Accessor is GeneratedMemberAccessor && !field.IsStatic,
            MethodInfo method => member.Accessor is GeneratedMethodAccessor generated
                                 && !method.IsStatic
                                 && method.GetParameters().Length == generated.Length,
            _ => false,
        };

        if (!matches)
        {
            return false;
        }

        accessor = member.Accessor;
        return true;
    }

    private static bool IsStatic(PropertyInfo property) => (property.GetMethod ?? property.SetMethod)?.IsStatic ?? false;

    /// <summary>
    /// One registered member. The CLR name is carried beside the accessor because a containment question is
    /// asked about the member, not about the name the script used to reach it.
    /// </summary>
    [StructLayout(LayoutKind.Auto)]
    internal readonly record struct GeneratedMember(string Name, ReflectionAccessor Accessor);

    /// <summary>
    /// The registered members of one type, keyed the way <see cref="TypeResolver"/>'s default name comparer
    /// matches them so <c>player.name</c> and <c>player.Name</c> both find <c>Name</c>.
    /// </summary>
    internal sealed class GeneratedTypeMembers
    {
        private readonly Dictionary<string, GeneratedMember> _members;

        internal GeneratedTypeMembers(Dictionary<string, GeneratedMember> members)
        {
            _members = members;
        }

        internal bool TryGet(string memberName, out GeneratedMember member)
            => _members.TryGetValue(memberName, out member);
    }
}
