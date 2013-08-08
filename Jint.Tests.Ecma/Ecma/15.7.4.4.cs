using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_15_7_4_4 : EcmaTest
    {
        [Fact]
        [Trait("Category", "15.7.4.4")]
        public void NumberPrototypeValueofReturnsThisNumberValue()
        {
			RunTest(@"TestCases/ch15/15.7/15.7.4/15.7.4.4/S15.7.4.4_A1_T01.js", false);
        }

        [Fact]
        [Trait("Category", "15.7.4.4")]
        public void NumberPrototypeValueofReturnsThisNumberValue2()
        {
			RunTest(@"TestCases/ch15/15.7/15.7.4/15.7.4.4/S15.7.4.4_A1_T02.js", false);
        }

        [Fact]
        [Trait("Category", "15.7.4.4")]
        public void TheValueofFunctionIsNotGenericItCannotBeTransferredToOtherKindsOfObjectsForUseAsAMethodAndThereIsShouldBeATypeerrorExceptionIfItsThisValueIsNotANumberObject()
        {
			RunTest(@"TestCases/ch15/15.7/15.7.4/15.7.4.4/S15.7.4.4_A2_T01.js", false);
        }

        [Fact]
        [Trait("Category", "15.7.4.4")]
        public void TheValueofFunctionIsNotGenericItCannotBeTransferredToOtherKindsOfObjectsForUseAsAMethodAndThereIsShouldBeATypeerrorExceptionIfItsThisValueIsNotANumberObject2()
        {
			RunTest(@"TestCases/ch15/15.7/15.7.4/15.7.4.4/S15.7.4.4_A2_T02.js", false);
        }

        [Fact]
        [Trait("Category", "15.7.4.4")]
        public void TheValueofFunctionIsNotGenericItCannotBeTransferredToOtherKindsOfObjectsForUseAsAMethodAndThereIsShouldBeATypeerrorExceptionIfItsThisValueIsNotANumberObject3()
        {
			RunTest(@"TestCases/ch15/15.7/15.7.4/15.7.4.4/S15.7.4.4_A2_T03.js", false);
        }

        [Fact]
        [Trait("Category", "15.7.4.4")]
        public void TheValueofFunctionIsNotGenericItCannotBeTransferredToOtherKindsOfObjectsForUseAsAMethodAndThereIsShouldBeATypeerrorExceptionIfItsThisValueIsNotANumberObject4()
        {
			RunTest(@"TestCases/ch15/15.7/15.7.4/15.7.4.4/S15.7.4.4_A2_T04.js", false);
        }

        [Fact]
        [Trait("Category", "15.7.4.4")]
        public void TheValueofFunctionIsNotGenericItCannotBeTransferredToOtherKindsOfObjectsForUseAsAMethodAndThereIsShouldBeATypeerrorExceptionIfItsThisValueIsNotANumberObject5()
        {
			RunTest(@"TestCases/ch15/15.7/15.7.4/15.7.4.4/S15.7.4.4_A2_T05.js", false);
        }


    }
}
