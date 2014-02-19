using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_13_2_3 : EcmaTest
    {
        [Fact]
        [Trait("Category", "13.2.3")]
        public void CheckThatAllPoisoningUseTheThrowtypeerrorFunctionObject()
        {
			RunTest(@"TestCases/ch13/13.2/S13.2.3_A1.js", false);
        }


    }
}
