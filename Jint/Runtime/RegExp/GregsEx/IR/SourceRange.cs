#nullable enable
namespace Jint.Runtime.RegExp.GregsEx.IR;

internal readonly record struct SourceRange(int Offset, int Length, RegexOffsetUnit Unit);
