using System.Diagnostics;
using Jint.Runtime;

namespace Jint.Constraints;

/// <summary>
/// The timestamp arithmetic the time-based constraints share, and — on the target frameworks that have
/// <c>TimeProvider</c> — the one place that decides what a host-supplied clock resolves to.
/// </summary>
/// <remarks>
/// <para>
/// A time-based constraint is checked on the interpreter's amortized lane, so its clock read is on a path
/// <c>AGENTS.md</c> says must not get measurably slower. <c>Resolve</c> is what keeps that promise:
/// <c>TimeProvider.System</c> — and no provider at all — fold to <see langword="null"/>, and a
/// <see langword="null"/> field is the constraint's signal to read <see cref="Stopwatch.GetTimestamp"/>
/// directly, exactly as it did before the seam existed. <c>TimeProvider</c>'s own base implementations of
/// <c>GetTimestamp()</c> and <c>TimestampFrequency</c> <em>are</em> <see cref="Stopwatch.GetTimestamp"/>
/// and <see cref="Stopwatch.Frequency"/>, so the fold changes no answer — it only removes the
/// indirection from the case nobody asked to move.
/// </para>
/// <para>
/// The provider half is <c>net8.0</c>-and-later. <c>TimeProvider</c> arrived in .NET 8, and Jint's
/// downlevel targets could only reach it through <c>Microsoft.Bcl.TimeProvider</c> — a second runtime
/// dependency on a package that has exactly one, in exchange for a seam whose consumer is a test. The
/// constraints therefore compile without any of it on <c>net472</c>, <c>netstandard2.0</c> and
/// <c>netstandard2.1</c>: no field, no branch, the same instructions they emitted before.
/// </para>
/// </remarks>
internal static class ConstraintClock
{
    /// <summary>
    /// Converts a budget to timestamp ticks on <paramref name="frequency"/>'s scale, clamped so that adding
    /// it to a timestamp cannot overflow. Without the clamp a large budget wraps <see cref="long"/> and
    /// lands the deadline in the <em>past</em>, failing the operation immediately — the opposite of what was
    /// asked for.
    /// </summary>
    internal static long ToTimestampTicks(TimeSpan budget, long frequency)
    {
        var ticks = budget.Ticks * ((double) frequency / TimeSpan.TicksPerSecond);
        return ticks >= long.MaxValue / 2.0 ? long.MaxValue / 2 : (long) ticks;
    }

#if NET8_0_OR_GREATER
    /// <summary>
    /// The provider a constraint should store, or <see langword="null"/> for "read
    /// <see cref="Stopwatch.GetTimestamp"/> directly".
    /// </summary>
    /// <exception cref="ArgumentException">
    /// <paramref name="provider"/> reports a non-positive <see cref="TimeProvider.TimestampFrequency"/>.
    /// Rejected here rather than divided by later: a zero frequency turns every interval into zero ticks, so
    /// the constraint would arm a deadline equal to its own start and fail the first check of every
    /// execution. A named error where the clock is supplied beats a timeout nobody can explain.
    /// </exception>
    internal static TimeProvider? Resolve(TimeProvider? provider)
    {
        if (provider is null || ReferenceEquals(provider, TimeProvider.System))
        {
            return null;
        }

        if (provider.TimestampFrequency <= 0)
        {
            Throw.ArgumentException(
                "A TimeProvider used by an execution constraint must report a positive TimestampFrequency.",
                nameof(provider));
        }

        return provider;
    }

    /// <summary>
    /// The tick frequency the timestamps of <paramref name="provider"/> are expressed in, for a provider
    /// already put through <see cref="Resolve"/>.
    /// </summary>
    internal static long FrequencyOf(TimeProvider? provider)
        => provider is null ? Stopwatch.Frequency : provider.TimestampFrequency;
#endif
}
