using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_15_6_4_3 : EcmaTest
    {
        [Fact]
        [Trait("Category", "15.6.4.3")]
        public void BooleanPrototypeValueofReturnsThisBooleanValue()
        {
			RunTest(@"TestCases/ch15/15.6/15.6.4/S15.6.4.3_A1_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.6.4.3")]
        public void BooleanPrototypeValueofReturnsThisBooleanValue2()
        {
			RunTest(@"TestCases/ch15/15.6/15.6.4/S15.6.4.3_A1_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.6.4.3")]
        public void TheValueofFunctionIsNotGenericItCannotBeTransferredToOtherKindsOfObjectsForUseAsAMethodAndThereIsShouldBeATypeerrorExceptionIfItsThisValueIsNotABooleanObject()
        {
			RunTest(@"TestCases/ch15/15.6/15.6.4/S15.6.4.3_A2_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.6.4.3")]
        public void TheValueofFunctionIsNotGenericItCannotBeTransferredToOtherKindsOfObjectsForUseAsAMethodAndThereIsShouldBeATypeerrorExceptionIfItsThisValueIsNotABooleanObject2()
        {
			RunTest(@"TestCases/ch15/15.6/15.6.4/S15.6.4.3_A2_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.6.4.3")]
        public void TheValueofFunctionIsNotGenericItCannotBeTransferredToOtherKindsOfObjectsForUseAsAMethodAndThereIsShouldBeATypeerrorExceptionIfItsThisValueIsNotABooleanObject3()
        {
			RunTest(@"TestCases/ch15/15.6/15.6.4/S15.6.4.3_A2_T3.js", false);
        }

        [Fact]
        [Trait("Category", "15.6.4.3")]
        public void TheValueofFunctionIsNotGenericItCannotBeTransferredToOtherKindsOfObjectsForUseAsAMethodAndThereIsShouldBeATypeerrorExceptionIfItsThisValueIsNotABooleanObject4()
        {
			RunTest(@"TestCases/ch15/15.6/15.6.4/S15.6.4.3_A2_T4.js", false);
        }

        [Fact]
        [Trait("Category", "15.6.4.3")]
        public void TheValueofFunctionIsNotGenericItCannotBeTransferredToOtherKindsOfObjectsForUseAsAMethodAndThereIsShouldBeATypeerrorExceptionIfItsThisValueIsNotABooleanObject5()
        {
			RunTest(@"TestCases/ch15/15.6/15.6.4/S15.6.4.3_A2_T5.js", false);
        }


    }
}
