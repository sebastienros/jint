using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
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
/// registering a type after an engine has already resolved one of its members invalidates that cache, so a
/// late <c>RegisterAll()</c> is merely late rather than silently ineffective.
/// </para>
/// <para>
/// Registering the same type twice replaces its previous entry. Registering is safe from several threads;
/// resolving is lock-free.
/// </para>
/// <para>
/// <b>What the registry does not decide.</b> A registered member is consulted only when the engine's
/// interop configuration still matches the one the generator assumed. A host that installed a
/// <see cref="TypeResolver.MemberFilter"/>, a <see cref="TypeResolver.MemberNameCreator"/>, a
/// <see cref="TypeResolver.MemberNameComparer"/>, or binding flags that no longer report public instance
/// members keeps the reflection path for every type, annotated or not, because those four steer which
/// members exist and under which names and the generated lanes do not yet run through them.
/// </para>
/// </remarks>
public static class JsAccessibleRegistry
{
    private static readonly ConcurrentDictionary<Type, GeneratedTypeMembers> _byType = new();

    /// <summary>
    /// Bumped by every <see cref="Register"/> call. <see cref="TypeResolver"/> compares the value it last
    /// saw against this one and drops its accessor cache when they differ, so a member resolved before its
    /// type was registered does not keep answering from the cache afterwards.
    /// </summary>
    private static int _generation;

    internal static int Generation => Volatile.Read(ref _generation);

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

    internal static bool TryGetAccessor(
        Type type,
        string memberName,
        [NotNullWhen(true)] out ReflectionAccessor? accessor)
    {
        if (_byType.TryGetValue(type, out var members))
        {
            return members.TryGet(memberName, out accessor);
        }

        accessor = null;
        return false;
    }

    /// <summary>
    /// The registered members of one type, keyed the way <see cref="TypeResolver"/>'s default name comparer
    /// matches them so <c>player.name</c> and <c>player.Name</c> both find <c>Name</c>.
    /// </summary>
    internal sealed class GeneratedTypeMembers
    {
        private readonly Dictionary<string, ReflectionAccessor> _members;

        internal GeneratedTypeMembers(Dictionary<string, ReflectionAccessor> members)
        {
            _members = members;
        }

        internal bool TryGet(string memberName, [NotNullWhen(true)] out ReflectionAccessor? accessor)
            => _members.TryGetValue(memberName, out accessor);
    }
}
