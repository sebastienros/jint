using Jint.Runtime;

namespace Jint.Tests.Runtime;

/// <summary>
/// CreateDynamicFunction assembles <c>function anonymous(&lt;params&gt;\n) {\n&lt;body&gt;\n}</c> and then
/// parses the parameter string and the body string on their own before parsing that assembly, "to
/// ensure that each is valid alone" (https://tc39.es/ecma262/#sec-createdynamicfunction, steps 17-24).
/// Jint used to parse the assembled source only, so an argument string that reached across the
/// boundaries the assembly inserted — an unterminated comment or template swallowing the closing
/// parenthesis, a parameter string closing the list itself, a body string closing the function and
/// continuing with statements of its own — produced a function instead of a SyntaxError.
/// </summary>
public class DynamicFunctionSourceTests
{
    // The five cases from test262's staging/sm/Function/invalid-parameter-list.js. Each pair is
    // otherwise syntactically valid: assembled without the line feed the spec inserts after the
    // parameters, every one of them parses.
    [Theory]
    [InlineData("/*", "*/) {")]
    [InlineData("//", ") {")]
    [InlineData("a = `", "` ) {")]
    [InlineData(") { var x = function (", "} ")]
    [InlineData("x = function (", "}) {")]
    public void ParameterStringMayNotReachPastTheParameterList(string parameters, string body)
    {
        var engine = new Engine();
        engine.SetValue("p", parameters);
        engine.SetValue("b", body);

        Invoking(() => engine.Evaluate("new Function(p, b)")).Should().ThrowExactly<JavaScriptException>()
            .Which.Error.Get("name").AsString().Should().Be("SyntaxError");
    }

    [Theory]
    [InlineData("} function g() {")]
    [InlineData("} ; {")]
    [InlineData("} , {")]
    public void BodyStringMayNotCloseTheFunctionAndContinue(string body)
    {
        var engine = new Engine();
        engine.SetValue("b", body);

        Invoking(() => engine.Evaluate("new Function('a', b)")).Should().ThrowExactly<JavaScriptException>()
            .Which.Error.Get("name").AsString().Should().Be("SyntaxError");
    }

    [Fact]
    public void RejectionSurvivesTheCompilationCache()
    {
        // The compilation cache promotes a source on its second sighting, so a rejected source must
        // stay rejected rather than being answered from a poisoned entry.
        var engine = new Engine();
        for (var i = 0; i < 3; i++)
        {
            Invoking(() => engine.Evaluate("new Function('/*', '*/) {')")).Should().ThrowExactly<JavaScriptException>();
        }
    }

    [Theory]
    // Parameter strings that legitimately contain the characters the check keys on: a comment, a
    // nested parenthesis, a nested function body, a template, a destructuring pattern, a trailing
    // comma and an embedded line feed. Every one of them must still compile to a working function.
    [InlineData("a /* comment */, b", "return a + b", "1, 2")]
    [InlineData("a = (1 + 2)", "return a", "")]
    [InlineData("a = function () { return 3; }", "return a()", "")]
    [InlineData("a = `x${22}`", "return a.length", "")]
    [InlineData("{ a, b }", "return a + b", "{ a: 1, b: 2 }")]
    [InlineData("a,", "return a", "3")]
    [InlineData("a\n, b", "return a + b", "1, 2")]
    [InlineData("...a", "return a.length", "0, 0, 0")]
    public void ValidArgumentStringsStillCompile(string parameters, string body, string callArguments)
    {
        var engine = new Engine();
        engine.SetValue("p", parameters);
        engine.SetValue("b", body);

        engine.Evaluate($"new Function(p, b)({callArguments})").AsNumber().Should().Be(3);
    }

    [Theory]
    [InlineData("Object.getPrototypeOf(function* () {}).constructor", "yield a")]
    [InlineData("Object.getPrototypeOf(async function () {}).constructor", "return a")]
    [InlineData("Object.getPrototypeOf(async function* () {}).constructor", "yield a")]
    public void TheCheckAccountsForEachFunctionKindsPrefix(string constructorExpression, string body)
    {
        var engine = new Engine();
        engine.SetValue("b", body);

        // A valid parameter string is accepted for every kind: the expected body offset is derived
        // from that kind's own source prefix, which is longer than "function anonymous(".
        engine.Evaluate($"typeof new ({constructorExpression})('a', b)").AsString().Should().Be("function");

        Invoking(() => engine.Evaluate($"new ({constructorExpression})('/*', '*/) {{')"))
            .Should().ThrowExactly<JavaScriptException>()
            .Which.Error.Get("name").AsString().Should().Be("SyntaxError");
    }
}
