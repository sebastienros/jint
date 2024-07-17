namespace Jint.Tests.Runtime;

public class EvaluationContextTests
{
    [Fact]
    public void ShouldThrowJavaScriptException()
    {
        var mockedEngine = new Engine();

        Expression expression = new Identifier(NodeType.MemberExpression.ToString());

        Assert.Throws<Jint.Runtime.JavaScriptException>(() => AstExtensions.TryGetComputedPropertyKey(expression, mockedEngine));
    }
}
