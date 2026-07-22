using Jint.Native;

namespace Jint.Tests.Runtime;

/// <summary>
/// Guards the completion-value semantics around the function-body elision optimization:
/// Normal-completion values must stay observable at script/eval/module top level while
/// function bodies only surface Return/Throw completions.
/// </summary>
public class CompletionValueTests
{
    private readonly Engine _engine = new();

    [Fact]
    public void ScriptTopLevelReturnsLastExpressionValue()
    {
        _engine.Evaluate("1; 2;").AsNumber().Should().Be(2d);
        _engine.Evaluate("var x = 1; x + 2;").AsNumber().Should().Be(3d);
        _engine.Evaluate("var y = 0.25; y + 0.25;").AsNumber().Should().Be(0.5);
    }

    [Fact]
    public void EvalReturnsLastExpressionValue()
    {
        _engine.Evaluate("eval('1;2;3')").AsNumber().Should().Be(3d);
        _engine.Evaluate("eval('var a = 1;')").Should().BeUndefined();
    }

    [Fact]
    public void DirectEvalInsideFunctionReturnsLastExpressionValue()
    {
        _engine.Evaluate("function f() { return eval('1;2;3'); } f()").AsNumber().Should().Be(3d);
        _engine.Evaluate("function g() { var t = 2; return eval('t; 42'); } g()").AsNumber().Should().Be(42d);
    }

    [Fact]
    public void IndirectEvalReturnsLastExpressionValue()
    {
        _engine.Evaluate("function f() { var e = eval; return e('5;6;7'); } f()").AsNumber().Should().Be(7d);
    }

    [Fact]
    public void FunctionWithoutReturnGivesUndefined()
    {
        _engine.Evaluate("function f() { 1; 2; 3; } f()").Should().BeUndefined();
        _engine.Evaluate("(function () { 42; })()").Should().BeUndefined();
    }

    [Fact]
    public void FunctionReturnValueFlows()
    {
        _engine.Evaluate("function f() { var s = 0; for (var i = 0; i < 3; i++) { s += 0.25; } return s; } f()").AsNumber().Should().Be(0.75);
    }

    [Fact]
    public void TopLevelLoopProducesLastIterationValue()
    {
        _engine.Evaluate("for (var i = 0; i < 5; i++) { i; }").AsNumber().Should().Be(4d);
        _engine.Evaluate("var s = 0; for (var i = 0; i < 5; i++) { s += 0.5; }").AsNumber().Should().Be(2.5);
    }

    [Fact]
    public void TopLevelBlockAndIfProduceValues()
    {
        _engine.Evaluate("{ 1; 2; }").AsNumber().Should().Be(2d);
        _engine.Evaluate("if (true) { 10; } else { 20; }").AsNumber().Should().Be(10d);
        _engine.Evaluate("switch (1) { case 1: 5; }").AsNumber().Should().Be(5d);
    }

    [Fact]
    public void GeneratorAndAsyncResultsFlow()
    {
        _engine.Evaluate("function* g() { 41; yield 1; 43; } g().next().value").AsNumber().Should().Be(1d);
        _engine.Evaluate("async function a() { 1; return 11; } a()").UnwrapIfPromise().AsNumber().Should().Be(11d);
    }

    [Fact]
    public void ClassStaticBlockValuesAreNotObservable()
    {
        _engine.Evaluate("class C { static x; static { 123; C.x = 9; } } C.x").AsNumber().Should().Be(9d);
    }

    [Fact]
    public void EvalSwitchAndTryProduceValues()
    {
        _engine.Evaluate("eval('try { 3; } catch (e) { 4; }')").AsNumber().Should().Be(3d);
        _engine.Evaluate("eval('try { throw 1; } catch (e) { 4; }')").AsNumber().Should().Be(4d);
        _engine.Evaluate("eval('switch (2) { case 2: 8; }')").AsNumber().Should().Be(8d);
    }
}
