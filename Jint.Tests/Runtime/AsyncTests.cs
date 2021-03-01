using Jint.Native;
using System;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace Jint.Tests.Runtime
{
    /// <summary>
    /// Setup new async tests here, and call echoAsync to return a result that matches the input, asynchroneosly.
    /// Debug by setting a breakpoint in EchoAsync and run the test.
    /// When the breakpoint hits, check the call stack and see where the Async execution path was swithed over to the synchroneos code path.
    /// </summary>
    public class AsyncTests
    {
        private static async Task<object> EchoAsync(object input)
        {
            await Task.Delay(1).ConfigureAwait(true);
            return input;
        }

        private static async Task<JsValue> RunTest(string expression)
        {
            var engine = new Engine();
            engine.SetValue("echoAsync", new Func<object, Task<object>>(obj => EchoAsync(obj)));
            return (await engine.ExecuteAsync(expression)).GetCompletionValue();
        }

        [Fact]
        public async void GetPropertyIsAwaited()
        {
            var code = @"
                var obj = {};
                Object.defineProperty(obj, 'value', {
                    get: function() { return echoAsync(42) },
                });
                return obj.value;
            ";

            var result = await RunTest(code);

            Assert.Equal(42, result);
        }

        [Fact]
        public async void SpreadParametersAreAwaited()
        {
            var code = @"
                function addSpread(...values) {
                    var result = 0;
                    for (var i=0; i<values.length; i++) {
                        result+=values[i];
                    }
                    return result;
                }
                return addSpread(...[echoAsync(10), echoAsync(20), echoAsync(30)]);
            ";

            var result = await RunTest(code);

            Assert.Equal(10 + 20 + 30, result);
        }

        [Fact]
        public async void FunctionArrayParameterAreAwaited()
        {
            var code = @"
                function addArray(values) {
                    var result = 0;
                    for (var i=0; i<values.length; i++) {
                        result+=values[i];
                    }
                    return result;
                }
                return addArray([echoAsync(10), echoAsync(20), echoAsync(30)]);
            ";

            var result = await RunTest(code);

            Assert.Equal(10 + 20 + 30, result);
        }

        [Fact]
        public async void FunctionParametersAreAwaited()
        {
            var code = @"
                function addValues(a, b, c) {
                    return a + b + c;
                }
                return addValues(echoAsync(10), echoAsync(20), echoAsync(30));
            ";

            var result = await RunTest(code);

            Assert.Equal(10 + 20 + 30, result);
        }

        [Fact]
        public async void BinaryExpressionsAwaits()
        {
            Assert.True((await RunTest("42 == echoAsync(42)")).AsBoolean());
            Assert.True((await RunTest("echoAsync(42) == 42")).AsBoolean());
            Assert.True((await RunTest("echoAsync(42) == echoAsync(42)")).AsBoolean());
        }

        [Fact]
        public async void AssignmentExpressionsAwaits()
        {
            Assert.Equal(42, await RunTest("var x = echoAsync(42); return x;"));
            Assert.Equal(52, await RunTest("var x = echoAsync(42) + 10; return x;"));
            Assert.Equal(62, await RunTest("var x = 20 + echoAsync(42); return x;"));
        }

        [Fact]
        public async void AssignmentThisExpressionsAwaits()
        {
            Assert.Equal(42, await RunTest("this.x = echoAsync(42); return this.x;"));
            Assert.Equal(52, await RunTest("this.x = echoAsync(42) + 10; return this.x;"));
            Assert.Equal(62, await RunTest("this.x = 20 + echoAsync(42); return this.x;"));
        }

        [Fact]
        public async void ObjectInitializerAwaits()
        {
            Assert.Equal(42, await RunTest("var x = { value: echoAsync(42) }; return x.value;"));
            Assert.Equal(52, await RunTest("var x = { value: echoAsync(42) + 10 }; return x.value;"));
            Assert.Equal(62, await RunTest("var x = { value: 20 + echoAsync(42) }; return x.value;"));
        }

        [Fact]
        public async void IfConditionAwaits()
        {
            Assert.Equal(42, await RunTest("if (echoAsync(42)==42) return 42;"));
            Assert.Equal(42, await RunTest("if (42 == echoAsync(42)) return 42;"));
            Assert.Equal(52, await RunTest("if (52 == echoAsync(42) + 10) return 52;"));
        }

        [Fact]
        public async void IfElseBodyStatementAwaits()
        {
            Assert.Equal(42, await RunTest("if (true) return echoAsync(42); else return null;"));
            Assert.Equal(42, await RunTest("if (false) return null; else return echoAsync(42);"));
            Assert.Equal(42, await RunTest("if (true) { return echoAsync(42); } else {return null; }"));
            Assert.Equal(42, await RunTest("if (false) { return null } else { return echoAsync(42); }"));
        }

        [Fact]
        public async void ForStatementAwaits()
        {
            Assert.Equal(10, await RunTest("for (var i=0; i<echoAsync(10); i++) ; return i;"));
            Assert.Equal(20, await RunTest("for (var i=0; i<10+echoAsync(10); i++) ; return i;"));
            Assert.Equal(30, await RunTest("for (var i=0; i<echoAsync(10)+20; i++) ; return i;"));
        }

        [Fact]
        public async void ArrayIndexAwaits()
        {
            Assert.Equal(20, await RunTest("var x = [echoAsync(10), echoAsync(20), echoAsync(30)][1]; return x;"));
        }

        [Fact]
        public async void ObjectPropertyNameIndexerAwaits()
        {
            Assert.Equal(42, await RunTest("var x = { value: 42 }; return x[echoAsync('value')];"));
        }

        [Fact]
        public async void DoWhileConditionAwaits()
        {
            Assert.Equal(42, await RunTest("var result = null; do {{ result = echoAsync(42); }} while (false) ; result;"));
        }

        [Fact]
        public async void FunctionBodyAwaits()
        {
            Assert.Equal(42, await RunTest("function foo() {{ return echoAsync(42); }} foo();"));
        }

        [Fact]
        public async void SwitchCondtionAwaits()
        {
            Assert.Equal(42, await RunTest("switch (echoAsync('a')) { case 'a': return 42; default: return null; };"));
        }

        [Fact]
        public async void SwitchCaseAwaits()
        {
            Assert.Equal(42, await RunTest("switch ('a') { case 'a': return echoAsync(42); default: return null; };"));
        }

        [Fact]
        public async void WhileConditionAwaits()
        {
            Assert.Equal(42, await RunTest("index = 0; result = 0; while (echoAsync(index++) < 42) { result++}; return result;"));
        }

        [Fact]
        public async void WhileBodyAwaits()
        {
            Assert.Equal(42, await RunTest("index = 0; result = 0; while (index++ < 42) { result = echoAsync(index) }; return result;"));
        }
    }
}