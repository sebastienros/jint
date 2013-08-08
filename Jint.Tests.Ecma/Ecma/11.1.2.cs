using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_11_1_2 : EcmaTest
    {
        [Fact]
        [Trait("Category", "11.1.2")]
        public void TheResultOfEvaluatingAnIdentifierIsAlwaysAValueOfTypeReference()
        {
			RunTest(@"TestCases/ch11/11.1/11.1.2/S11.1.2_A1_T1.js", false);
        }

        [Fact]
        [Trait("Category", "11.1.2")]
        public void TheResultOfEvaluatingAnIdentifierIsAlwaysAValueOfTypeReference2()
        {
			RunTest(@"TestCases/ch11/11.1/11.1.2/S11.1.2_A1_T2.js", false);
        }


    }
}
