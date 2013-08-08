using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_8_12_8 : EcmaTest
    {
        [Fact]
        [Trait("Category", "8.12.8")]
        public void ThisShouldGenerateATypeerrorCauseWeOverloadTostringMethodSoItReturnNonPrimitiveValueSeeEcmaReferenceAtHttpBugzillaMozillaOrgShowBugCgiId167325()
        {
			RunTest(@"TestCases/ch08/8.12/8.12.8/S8.12.8_A1.js", false);
        }

        [Fact]
        [Trait("Category", "8.12.8")]
        public void ThisShouldGenerateNoTypeerrorCauseWeOverloadTostringMethodSoItReturnNonPrimitiveValueButWeOverloadedValueofMethodTooSeeEcmaReferenceAtHttpBugzillaMozillaOrgShowBugCgiId167325()
        {
			RunTest(@"TestCases/ch08/8.12/8.12.8/S8.12.8_A2.js", false);
        }

        [Fact]
        [Trait("Category", "8.12.8")]
        public void WeOverloadValueofMethodSoItReturnNonPrimitiveValueThusDefaultvalueMustReturnObjectTostringValue()
        {
			RunTest(@"TestCases/ch08/8.12/8.12.8/S8.12.8_A3.js", false);
        }

        [Fact]
        [Trait("Category", "8.12.8")]
        public void WeOverloadValueofMethodSoItReturnNonPrimitiveValueAndTostringMethodSoItReturnNonPrimitiveValueTooThusDefaultvalueMustGenerateTypeerrorError()
        {
			RunTest(@"TestCases/ch08/8.12/8.12.8/S8.12.8_A4.js", false);
        }


    }
}
