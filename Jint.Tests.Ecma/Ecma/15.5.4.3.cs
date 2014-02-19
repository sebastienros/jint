using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_15_5_4_3 : EcmaTest
    {
        [Fact]
        [Trait("Category", "15.5.4.3")]
        public void StringPrototypeValueofReturnsThisStringValue()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/S15.5.4.3_A1_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.3")]
        public void StringPrototypeValueofReturnsThisStringValue2()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/S15.5.4.3_A1_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.3")]
        public void StringPrototypeValueofReturnsThisStringValue3()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/S15.5.4.3_A1_T3.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.3")]
        public void StringPrototypeValueofReturnsThisStringValue4()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/S15.5.4.3_A1_T4.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.3")]
        public void TheValueofFunctionIsNotGenericItThrowsATypeerrorExceptionIfItsThisValueIsNotAStringObjectThereforeItCannotBeTransferredToOtherKindsOfObjectsForUseAsAMethod()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/S15.5.4.3_A2_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.3")]
        public void TheValueofFunctionIsNotGenericItThrowsATypeerrorExceptionIfItsThisValueIsNotAStringObjectThereforeItCannotBeTransferredToOtherKindsOfObjectsForUseAsAMethod2()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/S15.5.4.3_A2_T2.js", false);
        }


    }
}
