using Jint.Native;
using Jint.Runtime.Debugger;
using System.Linq;
using Xunit;

namespace Jint.Tests.Runtime.Debugger
{
    public class ScopeTests
    {
        private JsValue AssertOnlyScopeContains(string name, DebugScopes scopes, DebugScopeType type)
        {
            var containingScope = Assert.Single(scopes, s => s.ScopeType == type && s.ContainsKey(name));
            foreach (var scope in scopes)
            {
                if (scope == containingScope)
                {
                    continue;
                }
                Assert.DoesNotContain(scope, g => g.Key == name);
            }

            return containingScope[name];
        }

        private void AssertNoScopeContains(string name, DebugScopes scopes)
        {
            foreach (var scope in scopes)
            {
                Assert.DoesNotContain(scope, g => g.Key == name);
            }
        }

        [Fact]
        public void GlobalScopeIncludesGlobalConst()
        {
            string script = @"
                const globalConstant = 'test';
                debugger;
            ";

            TestHelpers.TestAtBreak(script, info =>
            {
                var value = AssertOnlyScopeContains("globalConstant", info.CurrentScopeChain, DebugScopeType.Global);
                Assert.Equal("test", value.AsString());
            });
        }

        [Fact]
        public void GlobalScopeIncludesGlobalLet()
        {
            string script = @"
                let globalLet = 'test';
                debugger;";

            TestHelpers.TestAtBreak(script, info =>
            {
                var value = AssertOnlyScopeContains("globalLet", info.CurrentScopeChain, DebugScopeType.Global);
                Assert.Equal("test", value.AsString());
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
                var value = AssertOnlyScopeContains("globalVar", info.CurrentScopeChain, DebugScopeType.Global);
                Assert.Equal("test", value.AsString());
            });
        }

        [Fact]
        public void LocalScopeIncludesTopLevelLocalConst()
        {
            string script = @"
                function test()
                {
                    // const and let at the top level of a function are collapsed into the Local scope
                    const localConst = 'test';
                    debugger;
                }
                test();";

            TestHelpers.TestAtBreak(script, info =>
            {
                var value = AssertOnlyScopeContains("localConst", info.CurrentScopeChain, DebugScopeType.Local);
                Assert.Equal("test", value.AsString());
            });
        }

        [Fact]
        public void LocalScopeIncludesTopLevelLocalLet()
        {
            string script = @"
                function test()
                {
                    // const and let at the top level of a function are collapsed into the Local scope
                    let localLet = 'test';
                    debugger;
                }
                test();";

            TestHelpers.TestAtBreak(script, info =>
            {
                var value = AssertOnlyScopeContains("localLet", info.CurrentScopeChain, DebugScopeType.Local);
                Assert.Equal("test", value.AsString());
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
                var value = AssertOnlyScopeContains("localConst", info.CurrentScopeChain, DebugScopeType.Block);
                Assert.Equal("test", value.AsString());
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
                var value = AssertOnlyScopeContains("localLet", info.CurrentScopeChain, DebugScopeType.Block);
                Assert.Equal("test", value.AsString());
            });
        }

        [Fact]
        public void OnlyLocalsIncludeLocalVar()
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
                var value = AssertOnlyScopeContains("localVar", info.CurrentScopeChain, DebugScopeType.Local);
            });
        }

        [Fact]
        public void BlockScopedConstIsVisibleInsideBlock()
        {
            string script = @"
            'dummy statement';
            {
                debugger; // const is initialized (as undefined) at beginning of block
                const blockConst = 'block';
            }";

            TestHelpers.TestAtBreak(script, info =>
            {
                var value = AssertOnlyScopeContains("blockConst", info.CurrentScopeChain, DebugScopeType.Block);
                Assert.Equal(JsUndefined.Undefined, value);
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
                AssertOnlyScopeContains("blockLet", info.CurrentScopeChain, DebugScopeType.Block);
            });
        }
    }
}
