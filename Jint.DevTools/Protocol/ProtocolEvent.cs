using System.Runtime.InteropServices;

namespace Jint.DevTools.Protocol;

/// <summary>
/// One event, ready to send: its qualified method name and its already-serialized parameters.
/// </summary>
/// <remarks>
/// The parameters arrive as JSON rather than as an object because that is where the type is still known.
/// A generated <c>&lt;Domain&gt;Events</c> factory serializes through the source-generated context, and the
/// session then splices the fragment into the envelope; carrying the object instead would mean serializing
/// it later through a type the session does not know, which is exactly the reflection this package has none
/// of.
/// </remarks>
[StructLayout(LayoutKind.Auto)]
internal readonly record struct ProtocolEvent(string Method, string ParametersJson);
