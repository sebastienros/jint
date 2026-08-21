namespace Jint.Runtime;

internal sealed record ScriptRecord(
    Realm Realm,
    Script EcmaScriptCode,
    string? Location,
    ParsingConstraints ParsingConstraints,
    ParserOptions? ParserOptions) : IScriptOrModule;
