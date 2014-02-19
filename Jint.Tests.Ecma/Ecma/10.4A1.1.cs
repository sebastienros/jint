using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_10_4A1_1 : EcmaTest
    {
        [Fact]
        [Trait("Category", "10.4A1.1")]
        public void EveryFunctionCallEntersANewExecutionContext()
        {
			RunTest(@"TestCases/ch10/10.4/S10.4A1.1_T2.js", false);
        }


    }
}
