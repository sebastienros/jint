using Jint.Runtime;

namespace Jint.Tests.Runtime;

/// <summary>
/// Plain assignment to slot-stored bindings takes a fast lane; these pin its parity with the
/// materialized AssignToIdentifier path: anonymous function/class naming, abrupt completions,
/// const/TDZ error ordering, and targets rewritten during right-hand side evaluation.
/// </summary>
public class PlainAssignmentTests
{
    [Test]
    public void AnonymousFunctionAndClassGetAssignedName()
    {
        var engine = new Engine(static options => options.Strict = true);
        var result = engine.Evaluate("""
            function f() {
                var g = function () {};
                var C = class {};
                var named = function realName() {};
                var arrow = () => {};
                return [g.name, C.name, named.name, arrow.name].join('|');
            }
            f();
            """).AsString();

        result.Should().Be("g|C|realName|arrow");
    }

    [Test]
    public void ValuePositionAndChainedAssignments()
    {
        var engine = new Engine(static options => options.Strict = true);
        var result = engine.Evaluate("""
            function f() {
                var a = 1, b, c;
                c = (b = a);
                return '' + b + c;
            }
            f();
            """).AsString();

        result.Should().Be("11");
    }

    [Test]
    public void ThrowingRightHandSideDoesNotAssign()
    {
        var engine = new Engine(static options => options.Strict = true);
        var result = engine.Evaluate("""
            function f() {
                var a = 'initial';
                try { a = (function () { throw new Error('x'); })(); } catch (e) { }
                return a;
            }
            f();
            """).AsString();

        result.Should().Be("initial");
    }

    [Test]
    public void RightHandSideRewritingTargetIsOverwritten()
    {
        var engine = new Engine(static options => options.Strict = true);
        var result = engine.Evaluate("function f() { var b = 0; b = (b = 5, 9); return b; } f();").AsNumber();

        result.Should().Be(9);
    }

    [Test]
    public void ConstTargetThrowsTypeErrorAfterEvaluatingRightHandSide()
    {
        var engine = new Engine(static options => options.Strict = true);

        var ex = Invoking(() => engine.Execute("""
            function f() {
                const c = 1;
                var order = [];
                try { c = (order.push('rhs'), 2); } finally { globalThis.order = order.join(','); }
            }
            f();
            """)).Should().ThrowExactly<JavaScriptException>().Which;
        ex.Error.InstanceofOperator(engine.Intrinsics.TypeError).Should().BeTrue();
        engine.Evaluate("globalThis.order").AsString().Should().Be("rhs");
    }

    [Test]
    public void AssignmentBeforeLetDeclarationThrowsReferenceError()
    {
        var engine = new Engine(static options => options.Strict = true);

        var ex = Invoking(() => engine.Execute("function f() { { x = 1; let x; } } f();")).Should().ThrowExactly<JavaScriptException>().Which;
        ex.Error.InstanceofOperator(engine.Intrinsics.ReferenceError).Should().BeTrue();
    }
}
