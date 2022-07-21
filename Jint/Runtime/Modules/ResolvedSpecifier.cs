namespace Jint.Runtime.Modules;

public record ResolvedSpecifier(string Specifier, string Key, Uri? Uri, SpecifierType Type);
