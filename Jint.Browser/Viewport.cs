using System.Runtime.InteropServices;

namespace Jint.Browser;

/// <summary>
/// The size a page believes its window to be, and how many device pixels one CSS pixel spans.
/// </summary>
/// <remarks>
/// <para>
/// Nothing is laid out or rendered, so this is not a rendering surface: it is the set of numbers a script
/// can read — <c>innerWidth</c>, <c>innerHeight</c>, <c>devicePixelRatio</c>, <c>screen.width</c> — and the
/// set a <c>matchMedia</c> query is answered from. A page that branches on viewport size branches on this.
/// </para>
/// <para>
/// The default is 1280 × 720 at a ratio of 1, which is a desktop shape rather than a mobile one, because a
/// site that serves two layouts serves the desktop one to that.
/// </para>
/// </remarks>
/// <param name="Width">The viewport width in CSS pixels.</param>
/// <param name="Height">The viewport height in CSS pixels.</param>
/// <param name="DeviceScaleFactor">Device pixels per CSS pixel, reported as <c>devicePixelRatio</c>.</param>
[StructLayout(LayoutKind.Auto)]
public readonly record struct Viewport(int Width, int Height, double DeviceScaleFactor)
{
    /// <summary>1280 × 720 at a device pixel ratio of 1.</summary>
    public static Viewport Default { get; } = new(1280, 720, 1);

    /// <summary>A viewport at a device pixel ratio of 1.</summary>
    /// <param name="width">The viewport width in CSS pixels.</param>
    /// <param name="height">The viewport height in CSS pixels.</param>
    public Viewport(int width, int height) : this(width, height, 1)
    {
    }
}
