#if NET8_0_OR_GREATER
namespace Jint.WebApi;

/// <summary>
/// The well-formedness rule <see href="https://webidl.spec.whatwg.org/#quotaexceedederror">QuotaExceededError</see>
/// puts on the pair of numbers it carries, checked where a <b>host</b> supplies them.
/// </summary>
/// <remarks>
/// <para>
/// The interface's own constructor enforces the same three conditions with a <c>RangeError</c> — a negative
/// <c>quota</c>, a negative <c>requested</c>, and a <c>requested</c> less than the <c>quota</c> it exceeded —
/// and the specification restates the last one as a requirement on anything that throws one: "Specifications
/// that create or throw a QuotaExceededError must not provide a requested and quota that are both non-null and
/// where requested is less than quota." Jint's own throw sites satisfy it by construction; a host's
/// <see cref="StorageQuotaExceededException"/> or <see cref="CacheQuotaExceededException"/> is the one place
/// the numbers come from outside, so it is the one place worth checking.
/// </para>
/// <para>
/// It throws rather than silently dropping to <see langword="null"/>, because the mistake is the host's and a
/// script reading <c>requested &lt; quota</c> off an error is being told something that cannot be true. The
/// exception is raised from the exception's own constructor, so it surfaces at the provider's <c>throw</c>
/// statement rather than three layers away inside the engine.
/// </para>
/// </remarks>
internal static class QuotaExceededAmounts
{
    internal static void Validate(double quota, double requested)
    {
        if (!double.IsFinite(quota) || quota < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(quota),
                quota,
                "A QuotaExceededError's quota must be a finite, non-negative number.");
        }

        if (!double.IsFinite(requested) || requested < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(requested),
                requested,
                "A QuotaExceededError's requested must be a finite, non-negative number.");
        }

        if (requested < quota)
        {
            throw new ArgumentOutOfRangeException(
                nameof(requested),
                requested,
                $"A QuotaExceededError's requested must not be less than its quota ({quota}).");
        }
    }
}
#endif
