namespace Jint.HtmlParser;

/// <summary>HTML elements a document records once created in, or adopted into, it.</summary>
[Flags]
internal enum DocumentElementKinds : byte
{
    None = 0,
    SelectedContent = 1,
    Base = 2,
}
