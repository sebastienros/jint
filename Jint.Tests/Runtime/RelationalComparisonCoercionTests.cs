#nullable enable

namespace Jint.Tests.Runtime;

/// <summary>
/// <c>IsLessThan</c> converts each operand once, with <c>ToPrimitive(..., NUMBER)</c>, and compares two string
/// results as they are (https://tc39.es/ecma262/#sec-islessthan steps 1-3). When both primitives were strings, Jint
/// compared <c>ToString</c> of the original operands instead, which ran an object's <c>toString</c> a second time and
/// compared what that returned (sebastienros/jint#4613). Every expected string is Node's answer to the same script.
/// </summary>
public class RelationalComparisonCoercionTests
{
    private const string Logged = "var log = []; var o = { valueOf() { log.push('v'); return '7'; }, toString() { log.push('s'); return 'ab'; } }; ";

    public static TheoryData<string, string> Cases => new()
    {
        { Logged + "(o < '9') + ' ' + log.join()", "true v" },
        { Logged + "(o > '9') + ' ' + log.join()", "false v" },
        { Logged + "(o <= '9') + ' ' + log.join()", "true v" },
        { Logged + "(o >= '9') + ' ' + log.join()", "false v" },
        { Logged + "('9' > o) + ' ' + log.join()", "true v" },
        { "var a = { valueOf() { return 'b'; }, toString() { return 'a'; } }, b = { valueOf() { return 'a'; }, toString() { return 'b'; } }; String(a < b)", "false" },
        { "var l = []; var p = { valueOf() { l.push('p'); return 'a'; } }, q = { valueOf() { l.push('q'); return 'b'; } }; (p > q) + ' ' + l.join()", "false p,q" },
        { "var d = new Date(0); d.valueOf = function () { return 'x'; }; String(d < 'y')", "true" },
    };

    [Theory]
    [MemberData(nameof(Cases))]
    public void ComparesTheConvertedPrimitives(string script, string expected)
    {
        new Engine().Evaluate(script).ToString().Should().Be(expected);
    }
}
