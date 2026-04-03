using Jint.Runtime;

namespace Jint;

internal static class AcornimaExtensions
{
    public static Script ParseScriptGuarded(this Parser parser, Realm realm, string code, Position sourceOffset = default, string? source = null, bool strict = false)
    {
        var padding = CreateSourceOffsetPadding(sourceOffset);

        try
        {
            return padding.Length > 0
                ? parser.ParseScript(padding + code, padding.Length, code.Length, source, strict)
                : parser.ParseScript(code, source, strict);
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
    internal static string CreateSourceOffsetPadding(Position sourceOffset)
    {
        var lineOffset = sourceOffset.Line > 0 ? sourceOffset.Line - 1 : 0;
        var columnOffset = sourceOffset.Column > 0 ? sourceOffset.Column : 0;
        return new string('\n', lineOffset) + new string(' ', columnOffset);
    }

    public static Module ParseModuleGuarded(this Parser parser, Engine engine, string code, string? source = null)
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
        catch (Exception)
        {
            Throw.JavaScriptException(engine, $"Could not load module {source}", AstExtensions.DefaultLocation);
            return default;
        }
    }

    private static SourceLocation ToLocation(ParseErrorException ex, string? source)
    {
        return SourceLocation.From(Position.From(ex.LineNumber, ex.Column), Position.From(ex.LineNumber, ex.Column), source);
    }
}
