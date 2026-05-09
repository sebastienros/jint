namespace Jint;

/// <summary>
/// Serializable representation of a JavaScript source location stored under
/// <see cref="System.Exception.Data"/> with key <see cref="JintExceptionDataKeys.Location"/>
/// when Jint annotates a CLR exception that bubbled out of script execution.
/// </summary>
/// <remarks>
/// This wrapper exists because <see cref="System.Exception.Data"/> on .NET Framework rejects
/// values whose type is not marked <see cref="System.SerializableAttribute"/>, and Acornima's
/// <see cref="SourceLocation"/> is not. Use
/// <see cref="JintException.TryGetJavaScriptLocation(System.Exception?, out SourceLocation)"/>
/// for the typed read path; index <see cref="System.Exception.Data"/> directly only when the
/// helper is unavailable.
/// </remarks>
[Serializable]
public sealed class JintExceptionLocation
{
    public JintExceptionLocation(string? sourceFile, int startLine, int startColumn, int endLine, int endColumn)
    {
        SourceFile = sourceFile;
        StartLine = startLine;
        StartColumn = startColumn;
        EndLine = endLine;
        EndColumn = endColumn;
    }

    /// <summary>The source file the JavaScript was parsed from, or <see langword="null"/>.</summary>
    public string? SourceFile { get; }

    /// <summary>One-based line number of the first character of the source span.</summary>
    public int StartLine { get; }

    /// <summary>Zero-based column index of the first character of the source span.</summary>
    public int StartColumn { get; }

    /// <summary>One-based line number of the last character of the source span.</summary>
    public int EndLine { get; }

    /// <summary>Zero-based column index of the last character of the source span.</summary>
    public int EndColumn { get; }

    internal static JintExceptionLocation FromSourceLocation(in SourceLocation location)
    {
        return new JintExceptionLocation(
            location.SourceFile,
            location.Start.Line,
            location.Start.Column,
            location.End.Line,
            location.End.Column);
    }

    internal SourceLocation ToSourceLocation()
    {
        return SourceLocation.From(
            Position.From(StartLine, StartColumn),
            Position.From(EndLine, EndColumn),
            SourceFile);
    }
}
