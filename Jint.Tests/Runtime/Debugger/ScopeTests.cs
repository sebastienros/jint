using Jint.Native;
using Jint.Runtime.Debugger;

namespace Jint.Tests.Runtime.Debugger;

public class ScopeTests
{
    private static JsValue AssertOnlyScopeContains(DebugScopes scopes, string name, DebugScopeType scopeType)
    {
        var containingScope = scopes.Should().ContainSingle(s => s.ScopeType == scopeType && s.BindingNames.Contains(name)).Which;
        scopes.Should().NotContain(s => s != containingScope && s.BindingNames.Contains(name));

        return containingScope.GetBindingValue(name);
    }

    private static void AssertScope(DebugScope actual, DebugScopeType expectedType, params string[] expectedBindingNames)
    {
        actual.ScopeType.Should().Be(expectedType);
        // Global scope will have a number of intrinsic bindings that are outside the scope [no pun] of these tests
        if (actual.ScopeType != DebugScopeType.Global)
        {
            actual.BindingNames.Should().HaveCount(expectedBindingNames.Length);
        }
        foreach (string expectedName in expectedBindingNames)
        {
            actual.BindingNames.Should().Contain(expectedName);
        }
    }

    [Fact]
    public void AllowsInspectionOfUninitializedGlobalBindings()
    {
        string script = @"
                debugger;
                const globalConstant = 'test';
                let globalLet = 'test';
            ";

        TestHelpers.TestAtBreak(script, info =>
        {
            // Uninitialized global block scoped ("script scoped") bindings return null (and, just as importantly, don't throw):
            info.CurrentScopeChain[0].GetBindingValue("globalConstant").Should().BeNull();
            info.CurrentScopeChain[0].GetBindingValue("globalLet").Should().BeNull();
        });
    }

    [Fact]
    public void AllowsInspectionOfUninitializedBlockBindings()
    {
        string script = @"
                function test()
                {
                    debugger;
                    const globalConstant = 'test';
                    let globalLet = 'test';
                }
                test();
            ";

        TestHelpers.TestAtBreak(script, info =>
        {
            // Uninitialized block scoped bindings return null (and, just as importantly, don't throw):
            info.CurrentScopeChain[0].GetBindingValue("globalConstant").Should().BeNull();
            info.CurrentScopeChain[0].GetBindingValue("globalLet").Should().BeNull();
        });
    }

    [Fact]
    public void ScriptScopeIncludesGlobalConst()
    {
        string script = @"
                const globalConstant = 'test';
                debugger;
            ";

        TestHelpers.TestAtBreak(script, info =>
        {
            var value = AssertOnlyScopeContains(info.CurrentScopeChain, "globalConstant", DebugScopeType.Script);
            value.AsString().Should().Be("test");
        });
    }

    [Fact]
    public void ScriptScopeIncludesGlobalLet()
    {
        string script = @"
                let globalLet = 'test';
                debugger;";

        TestHelpers.TestAtBreak(script, info =>
        {
            var value = AssertOnlyScopeContains(info.CurrentScopeChain, "globalLet", DebugScopeType.Script);
            value.AsString().Should().Be("test");
        });
    }

    [Fact]
    public void GlobalScopeIncludesGlobalVar()
    {
        string script = @"
                var globalVar = 'test';
                debugger;";

        TestHelpers.TestAtBreak(script, info =>
        {
            var value = AssertOnlyScopeContains(info.CurrentScopeChain, "globalVar", DebugScopeType.Global);
            value.AsString().Should().Be("test");
        });
    }

    [Fact]
    public void TopLevelBlockScopeIsIdentified()
    {
        string script = @"
                function test()
                {
                    const localConst = 'test';
                    debugger;
                }
                test();";

        TestHelpers.TestAtBreak(script, info =>
        {
            info.CurrentScopeChain.Should().HaveCount(3);
            info.CurrentScopeChain[0].ScopeType.Should().Be(DebugScopeType.Block);
            info.CurrentScopeChain[0].IsTopLevel.Should().BeTrue();
        });
    }

    [Fact]
    public void NonTopLevelBlockScopeIsIdentified()
    {
        string script = @"
                function test()
                {
                    {
                        const localConst = 'test';
                        debugger;
                    }
                }
                test();";

        TestHelpers.TestAtBreak(script, info =>
        {
            // We only have 3 scopes, because the function top level block scope is empty.
            info.CurrentScopeChain.Should().HaveCount(3);
            info.CurrentScopeChain[0].ScopeType.Should().Be(DebugScopeType.Block);
            info.CurrentScopeChain[0].IsTopLevel.Should().BeFalse();
        });
    }

    [Fact]
    public void BlockScopeIncludesLocalConst()
    {
        string script = @"
                function test()
                {
                    {
                        const localConst = 'test';
                        debugger;
                    }
                }
                test();";

        TestHelpers.TestAtBreak(script, info =>
        {
            var value = AssertOnlyScopeContains(info.CurrentScopeChain, "localConst", DebugScopeType.Block);
            value.AsString().Should().Be("test");
        });
    }
    [Fact]
    public void BlockScopeIncludesLocalLet()
    {
        string script = @"
                function test()
                {
                    {
                        let localLet = 'test';
                        debugger;
                    }
                }
                test();";

        TestHelpers.TestAtBreak(script, info =>
        {
            var value = AssertOnlyScopeContains(info.CurrentScopeChain, "localLet", DebugScopeType.Block);
            value.AsString().Should().Be("test");
        });
    }

    [Fact]
    public void LocalScopeIncludesLocalVar()
    {
        string script = @"
                function test()
                {
                    var localVar = 'test';
                    debugger;
                }
                test();";

        TestHelpers.TestAtBreak(script, info =>
        {
            AssertOnlyScopeContains(info.CurrentScopeChain, "localVar", DebugScopeType.Local);
        });
    }

    [Fact]
    public void LocalScopeIncludesBlockVar()
    {
        string script = @"
                function test()
                {
                    debugger;
                    {
                        var localVar = 'test';
                    }
                }
                test();";

        TestHelpers.TestAtBreak(script, info =>
        {
            AssertOnlyScopeContains(info.CurrentScopeChain, "localVar", DebugScopeType.Local);
        });
    }

    [Fact]
    public void BlockScopedConstIsVisibleInsideBlock()
    {
        string script = @"
            'dummy statement';
            {
                const blockConst = 'block';
                debugger; // const isn't initialized until declaration
            }";

        TestHelpers.TestAtBreak(script, info =>
        {
            AssertOnlyScopeContains(info.CurrentScopeChain, "blockConst", DebugScopeType.Block);
        });
    }

    [Fact]
    public void BlockScopedLetIsVisibleInsideBlock()
    {
        string script = @"
            'dummy statement';
            {
                let blockLet = 'block';
                debugger; // let isn't initialized until declaration
            }";

        TestHelpers.TestAtBreak(script, info =>
        {
            AssertOnlyScopeContains(info.CurrentScopeChain, "blockLet", DebugScopeType.Block);
        });
    }

    [Fact]
    public void HasCorrectScopeChainForFunction()
    {
        string script = @"
            function add(a, b)
            {
                debugger;
                return a + b;
            }
            const x = 1;
            const y = 2;
            const z = add(x, y);";

        TestHelpers.TestAtBreak(script, info =>
        {
            info.CurrentScopeChain.Should().SatisfyRespectively(
                scope => AssertScope(scope, DebugScopeType.Local, "arguments", "a", "b"),
                scope => AssertScope(scope, DebugScopeType.Script, "x", "y", "z"),
                scope => AssertScope(scope, DebugScopeType.Global, "add"));
        });
    }

    [Fact]
    public void HasCorrectScopeChainForNestedFunction()
    {
        string script = @"
            function add(a, b)
            {
                function power(a)
                {
                    debugger;
                    return a * a;
                }
                return power(a) + b;
            }
            const x = 1;
            const y = 2;
            const z = add(x, y);";

        TestHelpers.TestAtBreak(script, info =>
        {
            info.CurrentScopeChain.Should().SatisfyRespectively(
                scope => AssertScope(scope, DebugScopeType.Local, "arguments", "a"),
                // a, arguments shadowed by local - but still exist in this scope
                scope => AssertScope(scope, DebugScopeType.Closure, "a", "arguments", "b", "power"),
                scope => AssertScope(scope, DebugScopeType.Script, "x", "y", "z"),
                scope => AssertScope(scope, DebugScopeType.Global, "add"));
        });
    }

    [Fact]
    public void HasCorrectScopeChainForBlock()
    {
        string script = @"
            function add(a, b)
            {
                if (a > 0)
                {
                    const y = b / a;
                    debugger;
                }
                return a + b;
            }
            const x = 1;
            const y = 2;
            const z = add(x, y);";

        TestHelpers.TestAtBreak(script, info =>
        {
            info.CurrentScopeChain.Should().SatisfyRespectively(
                scope => AssertScope(scope, DebugScopeType.Block, "y"),
                scope => AssertScope(scope, DebugScopeType.Local, "arguments", "a", "b"),
                scope => AssertScope(scope, DebugScopeType.Script, "x", "y", "z"), // y is shadowed, but still in the scope
                scope => AssertScope(scope, DebugScopeType.Global, "add"));
        });
    }

    [Fact]
    public void HasCorrectScopeChainForModule()
    {
        string imported = @"
            function add(a, b)
            {
                debugger;
                return a + b;
            }
            
            export { add };";

        string main = @"
            import { add } from 'imported-module';
            const x = 1;
            const y = 2;
            add(x, y);";
        TestHelpers.TestAtBreak(engine =>
            {
                engine.Modules.Add("imported-module", imported);
                engine.Modules.Add("main", main);
                engine.Modules.Import("main");
            },
            info =>
            {
                info.CurrentScopeChain.Should().SatisfyRespectively(
                    scope => AssertScope(scope, DebugScopeType.Local, "arguments", "a", "b"),
                    scope => AssertScope(scope, DebugScopeType.Module, "add"),
                    scope => AssertScope(scope, DebugScopeType.Global));
            });
    }

    [Fact]
    public void HasCorrectScopeChainForNestedBlock()
    {
        string script = @"
            function add(a, b)
            {
                if (a > 0)
                {
                    const y = b / a;
                    if (y > 0)
                    {
                        const x = b / y;
                        debugger;
                    }
                }
                return a + b;
            }
            const x = 1;
            const y = 2;
            const z = add(x, y);";

        TestHelpers.TestAtBreak(script, info =>
        {
            info.CurrentScopeChain.Should().SatisfyRespectively(
                scope => AssertScope(scope, DebugScopeType.Block, "x"),
                scope => AssertScope(scope, DebugScopeType.Block, "y"),
                scope => AssertScope(scope, DebugScopeType.Local, "arguments", "a", "b"),
                scope => AssertScope(scope, DebugScopeType.Script, "x", "y", "z"), // x, y are shadowed, but still in the scope
                scope => AssertScope(scope, DebugScopeType.Global, "add"));
        });
    }

    [Fact]
    public void HasCorrectScopeChainForCatch()
    {
        string script = @"
            function func()
            {
                let a = 1;
                try
                {
                    throw new Error('test');
                }
                catch (error)
                {
                    debugger;
                }
            }
            func();";

        TestHelpers.TestAtBreak(script, info =>
        {
            info.CurrentScopeChain.Should().SatisfyRespectively(
                scope => AssertScope(scope, DebugScopeType.Catch, "error"),
                scope => AssertScope(scope, DebugScopeType.Block, "a"),
                scope => AssertScope(scope, DebugScopeType.Local, "arguments"),
                scope => AssertScope(scope, DebugScopeType.Global, "func"));
        });
    }

    [Fact]
    public void HasCorrectScopeChainForWith()
    {
        string script = @"
            const obj = { a: 2, b: 4 };
            with (obj)
            {
                const x = a;
                debugger;
            };";

        TestHelpers.TestAtBreak(script, info =>
        {
            info.CurrentScopeChain.Should().SatisfyRespectively(
                scope => AssertScope(scope, DebugScopeType.Block, "x"),
                scope => AssertScope(scope, DebugScopeType.With, "a", "b"),
                scope => AssertScope(scope, DebugScopeType.Script, "obj"),
                scope => AssertScope(scope, DebugScopeType.Global));
        });
    }

    [Fact]
    public void ScopeChainIncludesNonEmptyScopes()
    {
        string script = @"
            const x = 2;
            if (x > 0)
            {
                const y = x;
                if (x > 1)
                {
                    const z = x;
                    debugger;
                }
            }";

        TestHelpers.TestAtBreak(script, info =>
        {
            info.CurrentScopeChain.Should().SatisfyRespectively(
                scope => AssertScope(scope, DebugScopeType.Block, "z"),
                scope => AssertScope(scope, DebugScopeType.Block, "y"),
                scope => AssertScope(scope, DebugScopeType.Script, "x"),
                scope => AssertScope(scope, DebugScopeType.Global));
        });
    }

    [Fact]
    public void ScopeChainExcludesEmptyScopes()
    {
        string script = @"
            const x = 2;
            if (x > 0)
            {
                if (x > 1)
                {
                    const z = x;
                    debugger;
                }
            }";

        TestHelpers.TestAtBreak(script, info =>
        {
            info.CurrentScopeChain.Should().SatisfyRespectively(
                scope => AssertScope(scope, DebugScopeType.Block, "z"),
                scope => AssertScope(scope, DebugScopeType.Script, "x"),
                scope => AssertScope(scope, DebugScopeType.Global));
        });
    }

    [Fact]
    public void ResolvesScopeChainsUpTheCallStack()
    {
        string script = @"
            const x = 1;
            function foo(a, c)
            {
                debugger;
            }
            
            function bar(b)
            {
                foo(b, 2);
            }

            bar(x);";

        TestHelpers.TestAtBreak(script, info =>
        {
            info.CallStack.Should().SatisfyRespectively(
                frame => frame.ScopeChain.Should().SatisfyRespectively(
                    // in foo()
                    scope => AssertScope(scope, DebugScopeType.Local, "arguments", "a", "c"),
                    scope => AssertScope(scope, DebugScopeType.Script, "x"),
                    scope => AssertScope(scope, DebugScopeType.Global, "foo", "bar")
                ),
                frame => frame.ScopeChain.Should().SatisfyRespectively(
                    // in bar()
                    scope => AssertScope(scope, DebugScopeType.Local, "arguments", "b"),
                    scope => AssertScope(scope, DebugScopeType.Script, "x"),
                    scope => AssertScope(scope, DebugScopeType.Global, "foo", "bar")
                ),
                frame => frame.ScopeChain.Should().SatisfyRespectively(
                    // in global
                    scope => AssertScope(scope, DebugScopeType.Script, "x"),
                    scope => AssertScope(scope, DebugScopeType.Global, "foo", "bar")
                )
            );
        });
    }

    [Fact]
    public void InspectsModuleScopedBindings()
    {
        string main = @"const x = 1; debugger;";
        TestHelpers.TestAtBreak(engine =>
            {
                engine.Modules.Add("main", main);
                engine.Modules.Import("main");
            },
            info =>
            {
                // No need for imports - main module is module scoped too, duh.
                var value = AssertOnlyScopeContains(info.CurrentScopeChain, "x", DebugScopeType.Module);
                value.AsInteger().Should().Be(1);
            });
    }
}