using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_15_3_1 : EcmaTest
    {
        [Fact]
        [Trait("Category", "15.3.1")]
        public void TheFunctionCallFunctionIsEquivalentToTheObjectCreationExpressionNewFunctionWithTheSameArguments()
        {
			RunTest(@"TestCases/ch15/15.3/S15.3.1_A1_T1.js", false);
        }


    }
}
