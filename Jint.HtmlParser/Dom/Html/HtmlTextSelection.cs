namespace Jint.HtmlParser;

internal enum HtmlSelectionDirection { None, Forward, Backward }
internal enum HtmlRangeTextMode { Select, Start, End, Preserve }
internal enum HtmlValueChangeOrigin { NonUser, User }

/// <summary>UTF-16 selection offsets for HTML text controls.</summary>
internal readonly record struct HtmlTextSelection(uint Start, uint End, HtmlSelectionDirection Direction);
