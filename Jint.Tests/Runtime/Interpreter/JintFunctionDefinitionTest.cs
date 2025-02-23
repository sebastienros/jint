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
}
