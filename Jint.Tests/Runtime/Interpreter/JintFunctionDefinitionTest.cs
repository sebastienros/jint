using Jint.Runtime.Interpreter;

namespace Jint.Tests.Runtime.Interpreter;

public class JintFunctionDefinitionTest
{
    [Theory]
    [InlineData("function f(_ = probeParams = function() { return 42; }) { }", true)]
    [InlineData("function* g(_ = probeParams = function() { return 42; }) { }", true)]
    [InlineData("function x(t = {}) {}", false)]
    [InlineData("function x(e, t = {}) {}", false)]
    [InlineData("function x([t, e]) { }", false)]
    public void ShouldDetectParameterExpression(string functionCode, bool hasExpressions)
    {
        var parser = new Parser();
        var script = parser.ParseScript(functionCode);
        var function = (IFunction) script.Body.First();

        var state = JintFunctionDefinition.BuildState(function);
        state.HasParameterExpressions.Should().Be(hasExpressions);
    }

    [Theory]
    [InlineData("function g() { }", false)]
    [InlineData("function* g() { }", false)]
    [InlineData("async function g() { }", false)]
    [InlineData("() => { }", false)]
    [InlineData("async () => { }", false)]
    [InlineData("function g(a) { }", false)]
    [InlineData("function* g(a) { }", false)]
    [InlineData("async function g(a) { }", false)]
    [InlineData("(a) => { }", false)]
    [InlineData("async (a) => { }", false)]
    [InlineData("function g(a) { _ = arguments[0] }", false)]
    [InlineData("function* g(a) { _ = arguments[0] }", true)]
    [InlineData("async function g(a) { _ = arguments[0] }", true)]
    [InlineData("(a) => { _ = arguments[0] }", false)]
    [InlineData("async (a) => { _ = arguments[0] }", true)]
    public void ShouldIndicateArgumentsOwnershipIfNeeded(string functionCode, bool requiresOwnership)
    {
        var parser = new Parser();
        var script = parser.ParseScript(functionCode);
        Node statement = script.Body.First();
        var function = (IFunction) (
            statement is ExpressionStatement expr
                ? expr.Expression
                : statement
        );

        var state = JintFunctionDefinition.BuildState(function);
        state.RequiresInputArgumentsOwnership.Should().Be(requiresOwnership);
    }
}
