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
        Assert.Equal(2d, _engine.Evaluate("1; 2;").AsNumber());
        Assert.Equal(3d, _engine.Evaluate("var x = 1; x + 2;").AsNumber());
        Assert.Equal(0.5, _engine.Evaluate("var y = 0.25; y + 0.25;").AsNumber());
    }

    [Fact]
    public void EvalReturnsLastExpressionValue()
    {
        Assert.Equal(3d, _engine.Evaluate("eval('1;2;3')").AsNumber());
        Assert.Equal(JsValue.Undefined, _engine.Evaluate("eval('var a = 1;')"));
    }

    [Fact]
    public void DirectEvalInsideFunctionReturnsLastExpressionValue()
    {
        Assert.Equal(3d, _engine.Evaluate("function f() { return eval('1;2;3'); } f()").AsNumber());
        Assert.Equal(42d, _engine.Evaluate("function g() { var t = 2; return eval('t; 42'); } g()").AsNumber());
    }

    [Fact]
    public void IndirectEvalReturnsLastExpressionValue()
    {
        Assert.Equal(7d, _engine.Evaluate("function f() { var e = eval; return e('5;6;7'); } f()").AsNumber());
    }

    [Fact]
    public void FunctionWithoutReturnGivesUndefined()
    {
        Assert.Equal(JsValue.Undefined, _engine.Evaluate("function f() { 1; 2; 3; } f()"));
        Assert.Equal(JsValue.Undefined, _engine.Evaluate("(function () { 42; })()"));
    }

    [Fact]
    public void FunctionReturnValueFlows()
    {
        Assert.Equal(0.75, _engine.Evaluate("function f() { var s = 0; for (var i = 0; i < 3; i++) { s += 0.25; } return s; } f()").AsNumber());
    }

    [Fact]
    public void TopLevelLoopProducesLastIterationValue()
    {
        Assert.Equal(4d, _engine.Evaluate("for (var i = 0; i < 5; i++) { i; }").AsNumber());
        Assert.Equal(2.5, _engine.Evaluate("var s = 0; for (var i = 0; i < 5; i++) { s += 0.5; }").AsNumber());
    }

    [Fact]
    public void TopLevelBlockAndIfProduceValues()
    {
        Assert.Equal(2d, _engine.Evaluate("{ 1; 2; }").AsNumber());
        Assert.Equal(10d, _engine.Evaluate("if (true) { 10; } else { 20; }").AsNumber());
        Assert.Equal(5d, _engine.Evaluate("switch (1) { case 1: 5; }").AsNumber());
    }

    [Fact]
    public void GeneratorAndAsyncResultsFlow()
    {
        Assert.Equal(1d, _engine.Evaluate("function* g() { 41; yield 1; 43; } g().next().value").AsNumber());
        Assert.Equal(11d, _engine.Evaluate("async function a() { 1; return 11; } a()").UnwrapIfPromise().AsNumber());
    }

    [Fact]
    public void ClassStaticBlockValuesAreNotObservable()
    {
        Assert.Equal(9d, _engine.Evaluate("class C { static x; static { 123; C.x = 9; } } C.x").AsNumber());
    }

    [Fact]
    public void EvalSwitchAndTryProduceValues()
    {
        Assert.Equal(3d, _engine.Evaluate("eval('try { 3; } catch (e) { 4; }')").AsNumber());
        Assert.Equal(4d, _engine.Evaluate("eval('try { throw 1; } catch (e) { 4; }')").AsNumber());
        Assert.Equal(8d, _engine.Evaluate("eval('switch (2) { case 2: 8; }')").AsNumber());
    }
}
