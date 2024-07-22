using Jint.Runtime;

namespace Jint;

internal static class AcornimaExtensions
{
    public static Script ParseScriptGuarded(this Parser parser, Realm realm, string code, string? source = null, bool strict = false)
    {
        try
        {
            return parser.ParseScript(code, source, strict);
        }
        catch (ParseErrorException e)
        {
            ExceptionHelper.ThrowSyntaxError(realm, e.Message, ToLocation(e, source));
            return default;
        }
    }

    public static Module ParseModuleGuarded(this Parser parser, Engine engine, string code, string? source = null)
    {
        try
        {
            return parser.ParseModule(code, source);
        }
        catch (ParseErrorException ex)
        {
            ExceptionHelper.ThrowSyntaxError(engine.Realm, $"Error while loading module: error in module '{source}': {ex.Error}", ToLocation(ex, source));
            return default;
        }
        catch (Exception)
        {
            ExceptionHelper.ThrowJavaScriptException(engine, $"Could not load module {source}", AstExtensions.DefaultLocation);
            return default;
        }
    }

    private static SourceLocation ToLocation(ParseErrorException ex, string? source)
    {
        return SourceLocation.From(Position.From(ex.LineNumber, ex.Column), Position.From(ex.LineNumber, ex.Column), source);
    }
}
