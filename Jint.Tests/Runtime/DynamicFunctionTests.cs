namespace Jint.Tests.Runtime;

/// <summary>
/// Functions created by the Function constructor share a definition-level call environment
/// across their one-shot instances; every call must still observe fresh bindings, and
/// closure-capturing bodies must never share state.
/// </summary>
public class DynamicFunctionTests
{
    [Fact]
    public void RepeatedDynamicFunctionCallsGetFreshBindings()
    {
        var engine = new Engine(static options => options.Strict());
        engine.Execute("""
            var results = [];
            for (var k = 0; k < 5; k++) {
                results.push(new Function("var c = (typeof c === 'undefined') ? 1 : c + 1; return c;")());
            }
            """);

        Assert.Equal("1,1,1,1,1", engine.Evaluate("results.join(',')").AsString());
    }

    [Fact]
    public void ClosureCapturingDynamicFunctionsKeepIndependentEnvironments()
    {
        var engine = new Engine(static options => options.Strict());
        engine.Execute("""
            var counters = [];
            for (var k = 0; k < 3; k++) {
                counters.push(new Function("var x = 0; return function () { return ++x; };")());
            }
            var results = [counters[0](), counters[0](), counters[1](), counters[2]()];
            """);

        Assert.Equal("1,2,1,1", engine.Evaluate("results.join(',')").AsString());
    }

    [Fact]
    public void InterleavedInstancesOfTheSameSourceStayIndependent()
    {
        var engine = new Engine(static options => options.Strict());
        engine.Execute("""
            var src = "var n = seed; seed = seed + 1; return n;";
            var seed = 10;
            var f1 = new Function(src);
            var f2 = new Function(src);
            var results = [f1(), f2(), f1()];
            """);

        Assert.Equal("10,11,12", engine.Evaluate("results.join(',')").AsString());
    }
}
