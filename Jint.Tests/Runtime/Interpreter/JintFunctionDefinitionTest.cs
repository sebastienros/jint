using Jint.Runtime.Interpreter;

namespace Jint.Tests.Runtime.Interpreter;

public class JintFunctionDefinitionTest
{
    [TestCase("function f(_ = probeParams = function() { return 42; }) { }", true)]
    [TestCase("function* g(_ = probeParams = function() { return 42; }) { }", true)]
    [TestCase("function x(t = {}) {}", false)]
    [TestCase("function x(e, t = {}) {}", false)]
    [TestCase("function x([t, e]) { }", false)]
    public void ShouldDetectParameterExpression(string functionCode, bool hasExpressions)
    {
        var parser = new Parser();
        var script = parser.ParseScript(functionCode);
        var function = (IFunction) script.Body.First();

        var state = JintFunctionDefinition.BuildState(function);
        state.HasParameterExpressions.Should().Be(hasExpressions);
    }

    [TestCase("function g() { }", false)]
    [TestCase("function* g() { }", false)]
    [TestCase("async function g() { }", false)]
    [TestCase("() => { }", false)]
    [TestCase("async () => { }", false)]
    [TestCase("function g(a) { }", false)]
    [TestCase("function* g(a) { }", false)]
    [TestCase("async function g(a) { }", false)]
    [TestCase("(a) => { }", false)]
    [TestCase("async (a) => { }", false)]
    [TestCase("function g(a) { _ = arguments[0] }", false)]
    [TestCase("function* g(a) { _ = arguments[0] }", true)]
    [TestCase("async function g(a) { _ = arguments[0] }", true)]
    [TestCase("(a) => { _ = arguments[0] }", false)]
    [TestCase("async (a) => { _ = arguments[0] }", true)]
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
