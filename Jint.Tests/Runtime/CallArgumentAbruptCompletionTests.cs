#nullable enable

namespace Jint.Tests.Runtime;

public class CallArgumentAbruptCompletionTests
{
    [Test]
    public void NestedNativeCallPropagatesUriErrorToCatch()
    {
        var result = new Engine().Evaluate(
            """
            try {
                JSON.parse(decodeURIComponent("74px 0px -25%"));
                "unexpected";
            } catch (error) {
                error.name;
            }
            """);

        result.AsString().Should().Be("URIError");
    }

    [TestCase("decodeURIComponent(value)", "\"%\"", "URIError")]
    [TestCase("String.fromCodePoint(value)", "-1", "RangeError")]
    public void GenericArgumentEvaluationStopsBeforeCallingTheNativeFunction(
        string expression,
        string input,
        string expectedError)
    {
        var result = new Engine().Evaluate(
            $$"""
            const values = [];
            let laterArguments = 0;
            function invoke(value) {
                try {
                    values.push({{expression}}, laterArguments++, "last");
                    return "unexpected";
                } catch (error) {
                    return error.name;
                }
            }
            const error = invoke({{input}});
            error + "|" + laterArguments + "|" + values.length;
            """);

        result.AsString().Should().Be(expectedError + "|0|0");
    }

    [Test]
    public void WarmFastCallDoesNotInvokeTheNativeFunctionAfterAnArgumentThrows()
    {
        var result = new Engine().Evaluate(
            """
            const values = [];
            function append(value) {
                return values.push(decodeURIComponent(value));
            }
            append("first");
            append("second");
            let caught = "";
            try {
                append("%");
            } catch (error) {
                caught = error.name;
            }
            caught + "|" + values.join(",");
            """);

        result.AsString().Should().Be("URIError|first,second");
    }

    [Test]
    public void WarmRegisterCallDoesNotInvokeTheScriptFunctionAfterAnArgumentThrows()
    {
        var result = new Engine().Evaluate(
            """
            let calls = 0;
            function target(value) {
                calls++;
            }
            function invoke(value) {
                return target(decodeURIComponent(value));
            }
            invoke("first");
            invoke("second");
            let caught = "";
            try {
                invoke("%");
            } catch (error) {
                caught = error.name;
            }
            caught + "|" + calls;
            """);

        result.AsString().Should().Be("URIError|2");
    }

    [Test]
    public void SpreadArgumentEvaluationStopsBeforeCallingTheNativeFunction()
    {
        var result = new Engine().Evaluate(
            """
            const values = [];
            let laterArguments = 0;
            function invoke(value) {
                try {
                    values.push(...[decodeURIComponent(value), laterArguments++]);
                    return "unexpected";
                } catch (error) {
                    return error.name;
                }
            }
            const error = invoke("%");
            error + "|" + laterArguments + "|" + values.length;
            """);

        result.AsString().Should().Be("URIError|0|0");
    }
}
