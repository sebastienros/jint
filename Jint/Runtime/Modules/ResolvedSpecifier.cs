#nullable enable

namespace Jint.Runtime.Modules;

public record ResolvedSpecifier(string Specifier, string Key, string? Path, SpecifierType Type);