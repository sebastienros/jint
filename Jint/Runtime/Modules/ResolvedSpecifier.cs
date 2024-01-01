namespace Jint.Runtime.Modules;

public record ResolvedSpecifier(ModuleRequest ModuleRequest, string Key, Uri? Uri, SpecifierType Type);
