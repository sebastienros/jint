using Jint.Runtime;

namespace Jint;

internal static class AcornimaExtensions
{
    public static Script ParseScriptGuarded(this JintParser parser, Realm realm, string code, Position sourceOffset = default, string? source = null, bool strict = false)
    {
        var paddingLength = GetSourceOffsetPaddingLength(sourceOffset);
        parser.CheckSourceLength(paddingLength + code.Length);

        var padding = CreateSourceOffsetPadding(sourceOffset, paddingLength);
        var paddedCode = padding.Length > 0 ? padding + code : code;

        try
        {
            return parser.ParseScript(paddedCode, padding.Length, code.Length, source, strict);
        }
        catch (ParseErrorException e)
        {
            Throw.SyntaxError(realm, e.Message, ToLocation(e, source));
            return default;
        }
    }

    /// <summary>
    /// Creates a padding string of newlines and spaces to shift parsed source positions by the given offset.
    /// </summary>
    internal static long GetSourceOffsetPaddingLength(Position sourceOffset)
    {
        if (sourceOffset.Line > 0)
        {
            return (long) sourceOffset.Line - 1 + Math.Max(sourceOffset.Column, 0);
        }

        return 0;
    }

    internal static string CreateSourceOffsetPadding(Position sourceOffset, long paddingLength)
    {
        if (paddingLength == 0)
        {
            return string.Empty;
        }

        if (paddingLength > int.MaxValue)
        {
            throw new ParsingLimitException(ParsingLimitKind.SourceLength, int.MaxValue, paddingLength);
        }

        var lineOffset = sourceOffset.Line - 1;
        return new string('\n', lineOffset) + new string(' ', (int) paddingLength - lineOffset);
    }

    public static Module ParseModuleGuarded(this JintParser parser, Engine engine, string code, string? source = null)
    {
        try
        {
            return parser.ParseModule(code, source);
        }
        catch (ParseErrorException ex)
        {
            Throw.SyntaxError(engine.Realm, $"Error while loading module: error in module '{source}': {ex.Error}", ToLocation(ex, source));
            return default;
        }
        catch (Exception ex) when (ex is not ParsingLimitException)
        {
            Throw.JavaScriptException(engine, $"Could not load module {source}", in AstExtensions.DefaultLocation);
            return default;
        }
    }

    private static SourceLocation ToLocation(ParseErrorException ex, string? source)
    {
        return SourceLocation.From(Position.From(ex.LineNumber, ex.Column), Position.From(ex.LineNumber, ex.Column), source);
    }
}
