#if NET8_0_OR_GREATER
using System.Runtime.InteropServices;

namespace Jint.WebApi.Url.Parsing;

/// <summary>
/// Which of the four host forms a <see cref="UrlHost"/> holds.
/// <para>
/// https://url.spec.whatwg.org/#host-representation
/// </para>
/// </summary>
internal enum UrlHostKind
{
    /// <summary>An empty host, https://url.spec.whatwg.org/#empty-host — reachable for "file" and for a URL that is not special.</summary>
    Empty,

    /// <summary>A domain, https://url.spec.whatwg.org/#concept-domain — always the ASCII form the domain parser produced.</summary>
    Domain,

    /// <summary>An IPv4 address, https://url.spec.whatwg.org/#concept-ipv4-address.</summary>
    Ipv4,

    /// <summary>An IPv6 address, https://url.spec.whatwg.org/#concept-ipv6-address.</summary>
    Ipv6,

    /// <summary>An opaque host, https://url.spec.whatwg.org/#opaque-host — what a URL that is not special gets.</summary>
    Opaque,
}

/// <summary>
/// A parsed host, https://url.spec.whatwg.org/#concept-host.
/// </summary>
/// <remarks>
/// <para>
/// The spec's in-memory representation of an IPv4 address is a 32-bit integer and of an IPv6 address a list of
/// eight 16-bit pieces, but nothing outside the host parser ever reads those numbers: every consumer — the URL
/// serializer, the <c>host</c>/<c>hostname</c> getters, the origin — wants the host serializer's output. So the
/// canonical serialization is computed once, at parse time, and is all that is carried. <see cref="Kind"/> is
/// kept beside it because the spec branches on the host's *form* in places where the string alone would be
/// ambiguous: only an IPv6 host is bracketed, only an empty host is the empty string, and a domain is the one
/// form the "ends in a number" re-check applies to.
/// </para>
/// <para>
/// <see cref="Serialized"/> is exactly what https://url.spec.whatwg.org/#concept-host-serializer returns,
/// brackets included for IPv6, so serializing a host is a field read.
/// </para>
/// </remarks>
[StructLayout(LayoutKind.Auto)]
internal readonly record struct UrlHost(UrlHostKind Kind, string Serialized)
{
    /// <summary>The empty host, https://url.spec.whatwg.org/#empty-host.</summary>
    internal static readonly UrlHost Empty = new(UrlHostKind.Empty, string.Empty);

    /// <summary>
    /// A URL "cannot have a username/password/port" when its host is null or the empty string; this is the
    /// second half of that test, https://url.spec.whatwg.org/#cannot-have-a-username-password-port.
    /// </summary>
    internal bool IsEmpty => Serialized.Length == 0;

    public override string ToString() => Serialized;
}
#endif
