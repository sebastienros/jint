namespace Jint.Runtime;

internal interface IScriptOrModule
{
    public string? Location { get; }
    public ParsingConstraints ParsingConstraints { get; }
    public ParserOptions? ParserOptions { get; }

    /// <summary>
    /// The abstract syntax tree this script or module was parsed into, which is the identity a host maps a
    /// running position back to a script by. Null for a module that has no source of its own — a synthetic
    /// or builder module.
    /// </summary>
    public Program? Program { get; }
}
