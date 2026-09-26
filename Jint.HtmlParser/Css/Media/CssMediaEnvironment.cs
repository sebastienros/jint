namespace Jint.HtmlParser.Css.Media;

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
    internal string ColorScheme { get; init; } = "light";
    internal string ReducedMotion { get; init; } = "no-preference";
    internal string ReducedTransparency { get; init; } = "no-preference";
    internal string Contrast { get; init; } = "no-preference";
    internal string ForcedColors { get; init; } = "none";
    internal string ReducedData { get; init; } = "no-preference";
    internal string Scripting { get; init; } = "enabled";
}
