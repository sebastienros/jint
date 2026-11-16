#nullable enable

namespace Jint.Tests.Runtime;

/// <summary>
/// <c>String.raw</c> converts every substitution that is present with <c>ToString</c>
/// (https://tc39.es/ecma262/#sec-string.raw step 7.e.i), so an <c>undefined</c> one spells <c>"undefined"</c>; only a
/// substitution past the end of the argument list contributes nothing. Jint skipped <c>undefined</c> as if it were
/// absent (sebastienros/jint#4613). Every expected string is Node's answer to the same script.
/// </summary>
[Parallelizable(ParallelScope.All)]
public class StringRawSubstitutionTests
{
    public static TestCases<string, string> Cases => new()
    {
        { "String.raw`a${undefined}b`", "aundefinedb" },
        { "(function () { return String.raw`${undefined}${void 0}`; })()", "undefinedundefined" },
        { "String.raw({ raw: ['a', 'b'] }, undefined)", "aundefinedb" },
        { "String.raw({ raw: ['a', 'b', 'c'] }, undefined, null)", "aundefinedbnullc" },
        { "String.raw({ raw: { length: 3, 0: 'x', 1: 'y', 2: 'z' } }, void 0, 1)", "xundefinedy1z" },
        { "String.raw({ raw: ['a', 'b', 'c'] }, undefined)", "aundefinedbc" },
        { "String.raw({ raw: ['a', 'b'] })", "ab" },
    };

    [TestCaseSource(nameof(Cases))]
    public void ConvertsEveryPresentSubstitution(string script, string expected)
    {
        new Engine().Evaluate(script).ToString().Should().Be(expected);
    }
}
