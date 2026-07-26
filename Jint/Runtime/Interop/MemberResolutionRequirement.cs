using Jint.Runtime.Interop.Reflection;

namespace Jint.Runtime.Interop;

/// <summary>
/// What a member resolution requires of the member it accepts. Resolution is filtered by this
/// (a member that cannot satisfy the requirement is skipped in favour of an indexer, an explicit
/// interface implementation, an extension method, ...), so it is part of the accessor cache key:
/// the accessor a read settles on is not necessarily the one a write settles on.
/// </summary>
[Flags]
internal enum MemberResolutionRequirement : byte
{
    /// <summary>
    /// The caller does not know yet whether it will read or write, so any resolved member answers.
    /// </summary>
    None = 0,

    /// <summary>
    /// Only a member that can be read answers.
    /// </summary>
    Readable = 1,

    /// <summary>
    /// Only a member that can be written answers.
    /// </summary>
    Writable = 2,
}

internal static class MemberResolutionRequirementExtensions
{
    /// <summary>
    /// Whether <paramref name="accessor"/> may answer for a resolution carrying this requirement.
    /// <see cref="MemberResolutionRequirement.None"/> is satisfied by every accessor.
    /// </summary>
    public static bool IsSatisfiedBy(this MemberResolutionRequirement requirement, ReflectionAccessor accessor)
    {
        if ((requirement & MemberResolutionRequirement.Readable) != MemberResolutionRequirement.None && !accessor.Readable)
        {
            return false;
        }

        if ((requirement & MemberResolutionRequirement.Writable) != MemberResolutionRequirement.None && !accessor.Writable)
        {
            return false;
        }

        return true;
    }
}
