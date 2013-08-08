using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_15_5_4_2 : EcmaTest
    {
        [Fact]
        [Trait("Category", "15.5.4.2")]
        public void StringPrototypeTostringReturnsThisStringValue()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/S15.5.4.2_A1_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.2")]
        public void StringPrototypeTostringReturnsThisStringValue2()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/S15.5.4.2_A1_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.2")]
        public void StringPrototypeTostringReturnsThisStringValue3()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/S15.5.4.2_A1_T3.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.2")]
        public void StringPrototypeTostringReturnsThisStringValue4()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/S15.5.4.2_A1_T4.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.2")]
        public void TheTostringFunctionIsNotGenericItThrowsATypeerrorExceptionIfItsThisValueIsNotAStringObjectThereforeItCannotBeTransferredToOtherKindsOfObjectsForUseAsAMethod()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/S15.5.4.2_A2_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.2")]
        public void TheTostringFunctionIsNotGenericItThrowsATypeerrorExceptionIfItsThisValueIsNotAStringObjectThereforeItCannotBeTransferredToOtherKindsOfObjectsForUseAsAMethod2()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/S15.5.4.2_A2_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.2")]
        public void StringPrototypeTostringIsEqualStringPrototypeValueof()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/S15.5.4.2_A3_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.2")]
        public void StringPrototypeTostringHaveLengthPropertyAndItIsEqual0()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/S15.5.4.2_A4_T1.js", false);
        }


    }
}
