using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_15_6_4_2 : EcmaTest
    {
        [Fact]
        [Trait("Category", "15.6.4.2")]
        public void TostringIfThisBooleanValueIsTrueThenTheStringTrueIsReturnedOtherwiseThisBooleanValueMustBeFalseAndTheStringFalseIsReturned()
        {
			RunTest(@"TestCases/ch15/15.6/15.6.4/S15.6.4.2_A1_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.6.4.2")]
        public void TostringIfThisBooleanValueIsTrueThenTheStringTrueIsReturnedOtherwiseThisBooleanValueMustBeFalseAndTheStringFalseIsReturned2()
        {
			RunTest(@"TestCases/ch15/15.6/15.6.4/S15.6.4.2_A1_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.6.4.2")]
        public void TheTostringFunctionIsNotGenericItCannotBeTransferredToOtherKindsOfObjectsForUseAsAMethodAndThereIsShouldBeATypeerrorExceptionIfItsThisValueIsNotABooleanObject()
        {
			RunTest(@"TestCases/ch15/15.6/15.6.4/S15.6.4.2_A2_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.6.4.2")]
        public void TheTostringFunctionIsNotGenericItCannotBeTransferredToOtherKindsOfObjectsForUseAsAMethodAndThereIsShouldBeATypeerrorExceptionIfItsThisValueIsNotABooleanObject2()
        {
			RunTest(@"TestCases/ch15/15.6/15.6.4/S15.6.4.2_A2_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.6.4.2")]
        public void TheTostringFunctionIsNotGenericItCannotBeTransferredToOtherKindsOfObjectsForUseAsAMethodAndThereIsShouldBeATypeerrorExceptionIfItsThisValueIsNotABooleanObject3()
        {
			RunTest(@"TestCases/ch15/15.6/15.6.4/S15.6.4.2_A2_T3.js", false);
        }

        [Fact]
        [Trait("Category", "15.6.4.2")]
        public void TheTostringFunctionIsNotGenericItCannotBeTransferredToOtherKindsOfObjectsForUseAsAMethodAndThereIsShouldBeATypeerrorExceptionIfItsThisValueIsNotABooleanObject4()
        {
			RunTest(@"TestCases/ch15/15.6/15.6.4/S15.6.4.2_A2_T4.js", false);
        }

        [Fact]
        [Trait("Category", "15.6.4.2")]
        public void TheTostringFunctionIsNotGenericItCannotBeTransferredToOtherKindsOfObjectsForUseAsAMethodAndThereIsShouldBeATypeerrorExceptionIfItsThisValueIsNotABooleanObject5()
        {
			RunTest(@"TestCases/ch15/15.6/15.6.4/S15.6.4.2_A2_T5.js", false);
        }


    }
}
