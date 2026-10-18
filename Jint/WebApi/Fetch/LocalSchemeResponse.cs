#if NET8_0_OR_GREATER
using System.Runtime.InteropServices;

namespace Jint.WebApi.Fetch;

/// <summary>
/// The response scheme fetch builds for a <c>blob:</c> or <c>data:</c> URL without a transport: a status, a
/// header list and the bytes — https://fetch.spec.whatwg.org/#local-scheme.
/// </summary>
/// <remarks>
/// Both <c>fetch</c> and <c>XMLHttpRequest</c> deliver it, so that the two arms build their responses once and
/// the two interfaces cannot disagree about what a URL answered. <see cref="Body"/> is
/// <see langword="null"/> for a response with no body at all, which is what a <c>HEAD</c> of a <c>data:</c>
/// URL gets; a body of zero bytes is a different thing and is an empty window instead.
/// </remarks>
[StructLayout(LayoutKind.Auto)]
internal readonly record struct LocalSchemeResponse(int Status, string StatusText, List<HeaderEntry> Headers, ReadOnlyMemory<byte>? Body);
#endif
