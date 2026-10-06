namespace Jint.HtmlParser.Css.Media;

[Flags]
internal enum CssPointerCapabilities { Unknown = 0, None = 1, Coarse = 2, Fine = 4 }

// Snapshot supplied by the host; evaluation never calls out or retains a document/device service.
internal sealed record CssMediaEnvironment
{
    internal string Type { get; init; } = "screen";
    internal double Width { get; init; } = 1024;
    internal double Height { get; init; } = 768;
    internal double Resolution { get; init; } = 1;
    internal double InitialFontSize { get; init; } = 16;
    internal int Color { get; init; } = 8;
    internal int ColorIndex { get; init; }
    internal int Monochrome { get; init; }
    internal bool Grid { get; init; }
    internal string Pointer { get; init; } = "fine";
    internal string Hover { get; init; } = "hover";
    // MQ4 §7.3: all-input capabilities are independent of the primary input device.
    internal CssPointerCapabilities AnyPointer { get; init; } = CssPointerCapabilities.Fine;
    internal string AnyHover { get; init; } = "hover";
    internal string DisplayMode { get; init; } = "browser";
    internal string ColorScheme { get; init; } = "light";
    internal string ReducedMotion { get; init; } = "no-preference";
    internal string ReducedTransparency { get; init; } = "no-preference";
    internal string Contrast { get; init; } = "no-preference";
    internal string ForcedColors { get; init; } = "none";
    internal string ReducedData { get; init; } = "no-preference";
    internal string Scripting { get; init; } = "enabled";
}
