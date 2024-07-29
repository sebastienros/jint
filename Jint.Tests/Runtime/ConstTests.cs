namespace Jint.Tests.Runtime;

public class ConstTests
{
    private readonly Engine _engine;

    public ConstTests()
    {
        _engine = new Engine()
            .SetValue("log", new Action<object>(Console.WriteLine))
            .SetValue("assert", new Action<bool>(Assert.True))
            .SetValue("equal", new Action<object, object>(Assert.Equal));
    }

    [Fact]
    public void ConstInsideIife()
    {
        _engine.Execute(@"
                (function(){
                    const testVariable = 'test';
                    function render() {
                        log(testVariable);
                    }
                    render();
                })();
            ");
    }

    [Fact]
    public void ConstDestructuring()
    {
        _engine.Execute(@"
                let obj = {};
                for (var i = 0; i < 1; i++) {
                    const { subElement } = obj;
                }
            ");
    }

    [Fact]
    public void DestructuringWithFunctionArgReferenceInStrictMode()
    {
        _engine.Execute(@"
                'use strict';
                function tst(a) {
                    let [let1, let2, let3] = a;
                    const [const1, const2, const3] = a;
                    var [var1, var2, var3] = a;
                    
                    equal(1, var1);
                    equal(2, var2);
                    equal(undefined, var3);
                    equal(1, const1);
                    equal(2, const2);
                    equal(undefined, const3);
                    equal(1, let1);
                    equal(2, let2);
                    equal(undefined, let3);
                }

                tst([1,2])
            ");
    }
}