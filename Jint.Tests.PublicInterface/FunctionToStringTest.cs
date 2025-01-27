using FluentAssertions;

namespace Jint.Tests.PublicInterface;

public class FunctionToStringTest
{
    [Fact]
    public void CanRegisterCustomFunctionToString()
    {
        const string Code = "var x = 1; var y = 3; function testFunction() { return 'Something'; }; testFunction.toString(); var z = x + y;";

        // we can rewrite back with AST to get custom formatting
        var engine = new Engine(options =>
            options.Host.FunctionToStringHandler = (function, node) => node.ToJavaScript(KnRJavaScriptTextFormatterOptions.Default)
        );

        engine.Evaluate(Code).AsString().Should().Be($"function testFunction() {{{Environment.NewLine}  return 'Something';{Environment.NewLine}}}");

        // or we can brute force the original input when we use node's location information
        engine = new Engine(options =>
            options.Host.FunctionToStringHandler = (function, node) => Code.Substring(node.Start, node.End - node.Start)
        );

        engine.Evaluate(Code).AsString().Should().Be("function testFunction() { return 'Something'; }");
    }
}
