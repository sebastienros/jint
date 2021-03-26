using Jint.Native;
using Jint.Runtime.Debugger;
using Xunit;

namespace Jint.Tests.Runtime.Debugger
{
    public class ScopeTests
    {
        private JsValue AssertOnlyScopeContains(string name, DebugScopes scopes, DebugScopeType type)
        {
            var entry = Assert.Single(scopes[type], g => g.Key == name);
            foreach (var scope in scopes)
            {
                if (scope.ScopeType == type)
                {
                    continue;
                }
                Assert.DoesNotContain(scope, g => g.Key == name);
            }

            return entry.Value;
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
                var value = AssertOnlyScopeContains("globalConstant", info.Scopes, DebugScopeType.Global);
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
                var value = AssertOnlyScopeContains("globalLet", info.Scopes, DebugScopeType.Global);
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
                var value = AssertOnlyScopeContains("globalVar", info.Scopes, DebugScopeType.Global);
                Assert.Equal("test", value.AsString());
            });
        }

        [Fact]
        public void BlockScopeIncludesLocalConst()
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
                var value = AssertOnlyScopeContains("localConst", info.Scopes, DebugScopeType.Block);
                Assert.Equal("test", value.AsString());
            });
        }

        [Fact]
        public void BlockScopeIncludesLocalLet()
        {
            string script = @"
                function test()
                {
                    let localLet = 'test';
                    debugger;
                }
                test();";

            TestHelpers.TestAtBreak(script, info =>
            {
                var value = AssertOnlyScopeContains("localLet", info.Scopes, DebugScopeType.Block);
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
                var value = AssertOnlyScopeContains("localVar", info.Scopes, DebugScopeType.Local);
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
                var value = AssertOnlyScopeContains("blockConst", info.Scopes, DebugScopeType.Block);
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
                AssertOnlyScopeContains("blockLet", info.Scopes, DebugScopeType.Block);

                Assert.Single(info.Scopes[DebugScopeType.Block], v => v.Key == "blockLet" && v.Value.AsString() == "block");
            });
        }
    }
}
