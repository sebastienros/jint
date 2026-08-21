namespace Jint.Runtime;

internal interface IScriptOrModule
{
    public string? Location { get; }
    public ParsingConstraints ParsingConstraints { get; }
    public ParserOptions? ParserOptions { get; }
}
