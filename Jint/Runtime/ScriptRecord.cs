namespace Jint.Runtime;

internal sealed record ScriptRecord(Realm Realm, Script EcmaScriptCode, string? Location) : IScriptOrModule;
