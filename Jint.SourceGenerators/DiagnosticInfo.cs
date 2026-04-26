using Microsoft.CodeAnalysis;

namespace Jint.SourceGenerators;

internal sealed record class DiagnosticInfo(
    string Id,
    string Title,
    string MessageFormat,
    string Category,
    DiagnosticSeverity Severity,
    LocationInfo? Location,
    EquatableArray<string> MessageArgs)
{
    public DiagnosticInfo(DiagnosticDescriptor descriptor, Location? location, params string[] messageArgs)
        : this(
            descriptor.Id,
            descriptor.Title.ToString(),
            descriptor.MessageFormat.ToString(),
            descriptor.Category,
            descriptor.DefaultSeverity,
            LocationInfo.From(location),
            messageArgs.ToEquatableArray())
    {
    }

    public Diagnostic ToDiagnostic()
    {
        var descriptor = new DiagnosticDescriptor(
            id: Id,
            title: Title,
            messageFormat: MessageFormat,
            category: Category,
            defaultSeverity: Severity,
            isEnabledByDefault: true);

        var args = new object[MessageArgs.Count];
        for (var i = 0; i < MessageArgs.Count; i++) args[i] = MessageArgs[i];

        return Diagnostic.Create(descriptor, Location?.ToLocation(), args);
    }
}

internal sealed record class LocationInfo(string FilePath, TextSpan TextSpan, LinePositionSpan LineSpan)
{
    public static LocationInfo? From(Location? location)
    {
        if (location is null || location.SourceTree is null) return null;
        return new LocationInfo(
            location.SourceTree.FilePath,
            new TextSpan(location.SourceSpan.Start, location.SourceSpan.Length),
            new LinePositionSpan(
                new LinePosition(location.GetLineSpan().StartLinePosition.Line, location.GetLineSpan().StartLinePosition.Character),
                new LinePosition(location.GetLineSpan().EndLinePosition.Line, location.GetLineSpan().EndLinePosition.Character)));
    }

    public Location ToLocation()
        => Location.Create(
            FilePath,
            new Microsoft.CodeAnalysis.Text.TextSpan(TextSpan.Start, TextSpan.Length),
            new Microsoft.CodeAnalysis.Text.LinePositionSpan(
                new Microsoft.CodeAnalysis.Text.LinePosition(LineSpan.Start.Line, LineSpan.Start.Character),
                new Microsoft.CodeAnalysis.Text.LinePosition(LineSpan.End.Line, LineSpan.End.Character)));
}

internal readonly record struct TextSpan(int Start, int Length);
internal readonly record struct LinePosition(int Line, int Character);
internal readonly record struct LinePositionSpan(LinePosition Start, LinePosition End);
