using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_15_3_2 : EcmaTest
    {
        [Fact]
        [Trait("Category", "15.3.2")]
        public void WhenFunctionIsCalledAsPartOfANewExpressionItIsAConstructorItInitialisesTheNewlyCreatedObject()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.2/S15.3.2_A1.js", false);
        }


    }
}
