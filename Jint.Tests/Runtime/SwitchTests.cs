namespace Jint.Tests.Runtime;

public class JintFailureTest
{
    [Fact]
    public void ShouldHandleCaseBlockLexicalScopeCorrectly()
    {
        var engine = new Engine();
        engine.SetValue("switchVal", 1);
        engine.SetValue("getCoffee", new Func<string>(() => "coffee"));

        engine.Execute(@"
        function myFunc() {
            switch(switchVal) {
                case 0:
                    const text = getCoffee();
                    return text;
                    break;
                case 1:
                    const line = getCoffee();
                    return line;
                    break;
            }
        }
        ");

        Assert.Equal("coffee", engine.Evaluate("myFunc()"));
    }

    [Fact]
    public void UnlabeledBreakShouldBeConsumedByLabeledSwitch()
    {
        // An unlabeled `break` inside a labeled switch must be absorbed by the switch, letting
        // execution continue after it. Previously the switch only absorbed a break whose target
        // matched its own label, so the unlabeled break escaped and the function fell off its end.
        var engine = new Engine();
        var result = engine.Evaluate(@"
            (function () {
                function f(e) {
                    var o = 0;
                    e: switch (e) {
                        case 1: o = 8; break;
                        default: o = 29;
                    }
                    return o;
                }
                return f(1);
            })();");

        Assert.Equal(8, result);
    }

    [Fact]
    public void LabeledBreakStillEscapesLabeledSwitch()
    {
        var engine = new Engine();
        var result = engine.Evaluate(@"
            (function () {
                function f(e) {
                    var o = 0;
                    e: switch (e) {
                        case 1: o = 8; break e;
                        default: o = 29;
                    }
                    o = 100;
                    return o;
                }
                return f(1);
            })();");

        Assert.Equal(100, result);
    }

    [Fact]
    public void UnlabeledBreakInNestedSwitchStaysInnermost()
    {
        var engine = new Engine();
        var result = engine.Evaluate(@"
            (function () {
                function f(x) {
                    var o = 0;
                    outer: switch (x) {
                        default:
                            switch (x) { case 5: o = 1; break; }
                            o = 2;
                    }
                    return o;
                }
                return f(5);
            })();");

        Assert.Equal(2, result);
    }
}
