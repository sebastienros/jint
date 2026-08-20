#if NET8_0_OR_GREATER
using System.Globalization;

namespace Jint.WebApi.Url.Parsing;

/// <summary>
/// How closely the platform's IDNA implementation matches what
/// https://url.spec.whatwg.org/#concept-domain-to-ascii asks for.
/// </summary>
internal enum IdnaFidelity
{
    /// <summary>Non-transitional UTS-46 processing, which is what the URL Standard specifies.</summary>
    Full,

    /// <summary>
    /// Transitional processing: <c>faß.de</c> becomes <c>fass.de</c> rather than <c>xn--fa-hia.de</c>. This is
    /// what the Windows <c>IdnToAscii</c> path historically does.
    /// </summary>
    Transitional,

    /// <summary>
    /// No IDNA processing is available — globalization-invariant mode, or a platform whose ICU is missing. A
    /// non-ASCII domain then always fails to parse; an ASCII one is unaffected, because the spec never
    /// consults IDNA for it.
    /// </summary>
    Unavailable,
}

/// <summary>
/// Unicode ToASCII, https://url.spec.whatwg.org/#concept-domain-to-ascii, on top of
/// <see cref="IdnMapping"/>.
/// </summary>
/// <remarks>
/// <para>
/// The URL Standard's domain parser only reaches IDNA for a domain that is <b>not</b> an ASCII string: step 4
/// of https://url.spec.whatwg.org/#concept-domain-to-ascii returns an ASCII domain lowercased "regardless of
/// Unicode ToASCII's outcome, due to web compatibility". That is not an optimisation this class invented, it is
/// the algorithm — and it is why the divergences below are confined to genuinely internationalized domains, and
/// why every <c>xn--</c> label (necessarily ASCII) is returned untouched.
/// </para>
/// <para>
/// For the remaining non-ASCII domains the parameters the spec asks for are CheckHyphens=false, CheckBidi=true,
/// CheckJoiners=true, UseSTD3ASCIIRules=false, Transitional_Processing=false, VerifyDnsLength=false and
/// IgnoreInvalidPunycode=false. <see cref="IdnMapping"/> exposes two of those seven as properties; the rest are
/// whatever the platform does. The known divergences, all absorbed by the WPT exclusion list rather than by
/// reimplementing UTS-46:
/// </para>
/// <list type="bullet">
/// <item><description>
/// <b>VerifyDnsLength cannot be turned off.</b> A label longer than 63 bytes, or a domain longer than 253, is
/// rejected where the spec accepts it — URLs do not enforce DNS limits.
/// </description></item>
/// <item><description>
/// <b>Empty labels.</b> VerifyDnsLength also rejects an empty label, so <c>a..ü</c> fails where the spec
/// accepts it. A trailing dot is likewise treated as a DNS root label rather than an empty one.
/// </description></item>
/// <item><description>
/// <b>CheckHyphens is not disableable.</b> A non-ASCII domain with a label starting or ending in a hyphen, or
/// carrying "--" in positions 3 and 4, can be rejected where the spec accepts it.
/// </description></item>
/// <item><description>
/// <b>Transitional processing.</b> On the Windows <c>IdnToAscii</c> path, <c>ß</c> and <c>ς</c> map to
/// <c>ss</c> and <c>σ</c> instead of being carried through as themselves. <see cref="Fidelity"/> reports this.
/// </description></item>
/// <item><description>
/// <b>ICU version skew.</b> The Unicode version behind the IDNA mapping table is the platform's, so a code
/// point whose status changed between Unicode releases can map differently.
/// </description></item>
/// <item><description>
/// <b>Globalization-invariant mode.</b> IDNA is then unavailable altogether and every non-ASCII domain fails;
/// <see cref="Fidelity"/> reports <see cref="IdnaFidelity.Unavailable"/>.
/// </description></item>
/// </list>
/// <para>
/// This is the same posture the engine takes towards CLDR data in ECMA-402: use the BCL, name the gaps, and
/// keep the excluded conformance rows commented rather than shipping a second Unicode implementation.
/// </para>
/// </remarks>
internal static class Idna
{
    /// <summary>
    /// The domain the probe parses. <c>ß</c> is the classic transitional/non-transitional discriminator:
    /// non-transitional processing carries it into Punycode, transitional maps it to <c>ss</c> first.
    /// </summary>
    private const string ProbeDomain = "faß.de";
    private const string NonTransitionalProbeResult = "xn--fa-hia.de";

    /// <summary>
    /// The <see cref="IdnMapping"/> is per thread rather than a shared static because its documentation makes
    /// no thread-safety promise for instance members. It is created on the first non-ASCII domain a thread
    /// parses and then reused, so the cost is one small object per thread that ever needs IDNA at all.
    /// </summary>
    [ThreadStatic]
    private static IdnMapping? _mapping;

    private static readonly IdnaFidelity _fidelity = Probe();

    /// <summary>
    /// What the platform's IDNA implementation is capable of. Computed once, from one probe parse.
    /// </summary>
    internal static IdnaFidelity Fidelity => _fidelity;

    /// <summary>
    /// Unicode ToASCII for a domain that is not an ASCII string, https://url.spec.whatwg.org/#concept-domain-to-ascii
    /// with beStrict false. Returns <see langword="false"/> for the spec's failure value.
    /// </summary>
    internal static bool TryToAscii(string domain, out string result)
    {
        try
        {
            var mapping = _mapping ??= new IdnMapping { AllowUnassigned = true, UseStd3AsciiRules = false };
            result = mapping.GetAscii(domain);
            return true;
        }
        catch (ArgumentException)
        {
            // The spec's failure value: ToASCII recorded an error.
            result = string.Empty;
            return false;
        }
        catch (NotSupportedException)
        {
            // Globalization-invariant mode, or a platform without the ICU data. Reported by Fidelity.
            result = string.Empty;
            return false;
        }
    }

    /// <summary>
    /// Unicode ToUnicode: the inverse mapping, which turns every <c>xn--</c> label back into the characters it
    /// encodes. Returns <see langword="false"/> when the platform reports the domain as invalid, which is the
    /// failure value <c>url.domainToUnicode</c> reports as an empty string.
    /// </summary>
    /// <remarks>
    /// The URL Standard never needs this direction — a URL record stores the ASCII form — so it exists for
    /// <c>node:url</c>'s <c>domainToUnicode</c> and for the UNC host of <c>fileURLToPath</c>, which is the one
    /// place Node decodes a host back before handing it to the file system. The same platform divergences
    /// listed above apply.
    /// </remarks>
    internal static bool TryToUnicode(string domain, out string result)
    {
        try
        {
            var mapping = _mapping ??= new IdnMapping { AllowUnassigned = true, UseStd3AsciiRules = false };
            result = mapping.GetUnicode(domain);
            return true;
        }
        catch (ArgumentException)
        {
            result = string.Empty;
            return false;
        }
        catch (NotSupportedException)
        {
            // Globalization-invariant mode, or a platform without the ICU data. Reported by Fidelity.
            result = string.Empty;
            return false;
        }
    }

    private static IdnaFidelity Probe()
    {
        if (!TryToAscii(ProbeDomain, out var result))
        {
            return IdnaFidelity.Unavailable;
        }

        return string.Equals(result, NonTransitionalProbeResult, StringComparison.Ordinal)
            ? IdnaFidelity.Full
            : IdnaFidelity.Transitional;
    }
}
#endif
