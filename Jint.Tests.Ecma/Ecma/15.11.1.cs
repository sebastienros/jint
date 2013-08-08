using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_15_11_1 : EcmaTest
    {
        [Fact]
        [Trait("Category", "15.11.1")]
        public void TheFunctionCallErrorIsEquivalentToTheObjectCreationExpressionNewErrorWithTheSameArguments()
        {
			RunTest(@"TestCases/ch15/15.11/15.11.1/S15.11.1_A1_T1.js", false);
        }


    }
}
