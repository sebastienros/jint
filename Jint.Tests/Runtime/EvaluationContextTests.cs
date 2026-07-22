namespace Jint.Tests.Runtime;

public class EvaluationContextTests
{
    [Fact]
    public void ShouldThrowJavaScriptException()
    {
        var mockedEngine = new Engine();

        Expression expression = new Identifier(NodeType.MemberExpression.ToString());

        Invoking(() => AstExtensions.TryGetComputedPropertyKey(expression, mockedEngine)).Should().ThrowExactly<Jint.Runtime.JavaScriptException>();
    }
}
