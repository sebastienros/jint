using System.Diagnostics.CodeAnalysis;

namespace Jint.Profiling;

/// <summary>
/// Whose code a profiled frame is running: the script's, the engine's, or the host's. The one
/// classification a native profiler cannot make, since at the CLR level all three are Jint frames.
/// </summary>
/// <remarks>
/// <para>
/// The values are the category indices the Firefox Profiler document declares in <c>meta.categories</c>,
/// so a frame's category is written out as-is.
/// </para>
/// <para>
/// This type is in a preview area, declared to the compiler as <c>JINT0002</c>; see
/// <see cref="JintDiagnosticIds"/>. New members may be added, so treat an unrecognized value as "some other
/// kind of code" rather than switching exhaustively without a default arm.
/// </para>
/// </remarks>
[Experimental(JintDiagnosticIds.PreviewDiagnostic)]
public enum ProfileFrameCategory
{
    /// <summary>
    /// Nothing else fits. Index 0 is the profiler format's default category and must stay the grey one.
    /// </summary>
    Other = 0,

    /// <summary>
    /// A function the script itself defines, and the top-level program frame every sample is rooted at.
    /// </summary>
    Script = 1,

    /// <summary>
    /// A built-in: a function whose body is Jint's own code, so its time is the engine's rather than the
    /// script's or the host's.
    /// </summary>
    BuiltIn = 2,

    /// <summary>
    /// A callable whose body is the host's code — a registered delegate, a CLR method or property accessor
    /// reached through interop, or a host-derived function class. Seeing this apart from
    /// <see cref="Script"/> is half the diagnosis for an embedder.
    /// </summary>
    HostInterop = 3,
}
