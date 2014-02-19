using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_15_7_4_2 : EcmaTest
    {
        [Fact]
        [Trait("Category", "15.7.4.2")]
        public void TostringIfRadixIsTheNumber10OrUndefinedThenThisNumberValueIsGivenAsAnArgumentToTheTostringOperatorTheResultingStringValueIsReturned()
        {
			RunTest(@"TestCases/ch15/15.7/15.7.4/15.7.4.2/S15.7.4.2_A1_T01.js", false);
        }

        [Fact]
        [Trait("Category", "15.7.4.2")]
        public void TostringIfRadixIsTheNumber10OrUndefinedThenThisNumberValueIsGivenAsAnArgumentToTheTostringOperatorTheResultingStringValueIsReturned2()
        {
			RunTest(@"TestCases/ch15/15.7/15.7.4/15.7.4.2/S15.7.4.2_A1_T02.js", false);
        }

        [Fact]
        [Trait("Category", "15.7.4.2")]
        public void TostringIfRadixIsTheNumber10OrUndefinedThenThisNumberValueIsGivenAsAnArgumentToTheTostringOperatorTheResultingStringValueIsReturned3()
        {
			RunTest(@"TestCases/ch15/15.7/15.7.4/15.7.4.2/S15.7.4.2_A1_T03.js", false);
        }

        [Fact]
        [Trait("Category", "15.7.4.2")]
        public void TostringIfRadixIsAnIntegerFrom2To36ButNot10TheResultIsAStringTheChoiceOfWhichIsImplementationDependent()
        {
			RunTest(@"TestCases/ch15/15.7/15.7.4/15.7.4.2/S15.7.4.2_A2_T01.js", false);
        }

        [Fact]
        [Trait("Category", "15.7.4.2")]
        public void TostringIfRadixIsAnIntegerFrom2To36ButNot10TheResultIsAStringTheChoiceOfWhichIsImplementationDependent2()
        {
			RunTest(@"TestCases/ch15/15.7/15.7.4/15.7.4.2/S15.7.4.2_A2_T02.js", false);
        }

        [Fact]
        [Trait("Category", "15.7.4.2")]
        public void TostringIfRadixIsAnIntegerFrom2To36ButNot10TheResultIsAStringTheChoiceOfWhichIsImplementationDependent3()
        {
			RunTest(@"TestCases/ch15/15.7/15.7.4/15.7.4.2/S15.7.4.2_A2_T03.js", false);
        }

        [Fact]
        [Trait("Category", "15.7.4.2")]
        public void TostringIfRadixIsAnIntegerFrom2To36ButNot10TheResultIsAStringTheChoiceOfWhichIsImplementationDependent4()
        {
			RunTest(@"TestCases/ch15/15.7/15.7.4/15.7.4.2/S15.7.4.2_A2_T04.js", false);
        }

        [Fact]
        [Trait("Category", "15.7.4.2")]
        public void TostringIfRadixIsAnIntegerFrom2To36ButNot10TheResultIsAStringTheChoiceOfWhichIsImplementationDependent5()
        {
			RunTest(@"TestCases/ch15/15.7/15.7.4/15.7.4.2/S15.7.4.2_A2_T05.js", false);
        }

        [Fact]
        [Trait("Category", "15.7.4.2")]
        public void TostringIfRadixIsAnIntegerFrom2To36ButNot10TheResultIsAStringTheChoiceOfWhichIsImplementationDependent6()
        {
			RunTest(@"TestCases/ch15/15.7/15.7.4/15.7.4.2/S15.7.4.2_A2_T06.js", false);
        }

        [Fact]
        [Trait("Category", "15.7.4.2")]
        public void TostringIfRadixIsAnIntegerFrom2To36ButNot10TheResultIsAStringTheChoiceOfWhichIsImplementationDependent7()
        {
			RunTest(@"TestCases/ch15/15.7/15.7.4/15.7.4.2/S15.7.4.2_A2_T07.js", false);
        }

        [Fact]
        [Trait("Category", "15.7.4.2")]
        public void TostringIfRadixIsAnIntegerFrom2To36ButNot10TheResultIsAStringTheChoiceOfWhichIsImplementationDependent8()
        {
			RunTest(@"TestCases/ch15/15.7/15.7.4/15.7.4.2/S15.7.4.2_A2_T08.js", false);
        }

        [Fact]
        [Trait("Category", "15.7.4.2")]
        public void TostringIfRadixIsAnIntegerFrom2To36ButNot10TheResultIsAStringTheChoiceOfWhichIsImplementationDependent9()
        {
			RunTest(@"TestCases/ch15/15.7/15.7.4/15.7.4.2/S15.7.4.2_A2_T09.js", false);
        }

        [Fact]
        [Trait("Category", "15.7.4.2")]
        public void TostringIfRadixIsAnIntegerFrom2To36ButNot10TheResultIsAStringTheChoiceOfWhichIsImplementationDependent10()
        {
			RunTest(@"TestCases/ch15/15.7/15.7.4/15.7.4.2/S15.7.4.2_A2_T10.js", false);
        }

        [Fact]
        [Trait("Category", "15.7.4.2")]
        public void TostringIfRadixIsAnIntegerFrom2To36ButNot10TheResultIsAStringTheChoiceOfWhichIsImplementationDependent11()
        {
			RunTest(@"TestCases/ch15/15.7/15.7.4/15.7.4.2/S15.7.4.2_A2_T11.js", false);
        }

        [Fact]
        [Trait("Category", "15.7.4.2")]
        public void TostringIfRadixIsAnIntegerFrom2To36ButNot10TheResultIsAStringTheChoiceOfWhichIsImplementationDependent12()
        {
			RunTest(@"TestCases/ch15/15.7/15.7.4/15.7.4.2/S15.7.4.2_A2_T12.js", false);
        }

        [Fact]
        [Trait("Category", "15.7.4.2")]
        public void TostringIfRadixIsAnIntegerFrom2To36ButNot10TheResultIsAStringTheChoiceOfWhichIsImplementationDependent13()
        {
			RunTest(@"TestCases/ch15/15.7/15.7.4/15.7.4.2/S15.7.4.2_A2_T13.js", false);
        }

        [Fact]
        [Trait("Category", "15.7.4.2")]
        public void TostringIfRadixIsAnIntegerFrom2To36ButNot10TheResultIsAStringTheChoiceOfWhichIsImplementationDependent14()
        {
			RunTest(@"TestCases/ch15/15.7/15.7.4/15.7.4.2/S15.7.4.2_A2_T14.js", false);
        }

        [Fact]
        [Trait("Category", "15.7.4.2")]
        public void TostringIfRadixIsAnIntegerFrom2To36ButNot10TheResultIsAStringTheChoiceOfWhichIsImplementationDependent15()
        {
			RunTest(@"TestCases/ch15/15.7/15.7.4/15.7.4.2/S15.7.4.2_A2_T15.js", false);
        }

        [Fact]
        [Trait("Category", "15.7.4.2")]
        public void TostringIfRadixIsAnIntegerFrom2To36ButNot10TheResultIsAStringTheChoiceOfWhichIsImplementationDependent16()
        {
			RunTest(@"TestCases/ch15/15.7/15.7.4/15.7.4.2/S15.7.4.2_A2_T16.js", false);
        }

        [Fact]
        [Trait("Category", "15.7.4.2")]
        public void TostringIfRadixIsAnIntegerFrom2To36ButNot10TheResultIsAStringTheChoiceOfWhichIsImplementationDependent17()
        {
			RunTest(@"TestCases/ch15/15.7/15.7.4/15.7.4.2/S15.7.4.2_A2_T17.js", false);
        }

        [Fact]
        [Trait("Category", "15.7.4.2")]
        public void TostringIfRadixIsAnIntegerFrom2To36ButNot10TheResultIsAStringTheChoiceOfWhichIsImplementationDependent18()
        {
			RunTest(@"TestCases/ch15/15.7/15.7.4/15.7.4.2/S15.7.4.2_A2_T18.js", false);
        }

        [Fact]
        [Trait("Category", "15.7.4.2")]
        public void TostringIfRadixIsAnIntegerFrom2To36ButNot10TheResultIsAStringTheChoiceOfWhichIsImplementationDependent19()
        {
			RunTest(@"TestCases/ch15/15.7/15.7.4/15.7.4.2/S15.7.4.2_A2_T19.js", false);
        }

        [Fact]
        [Trait("Category", "15.7.4.2")]
        public void TostringIfRadixIsAnIntegerFrom2To36ButNot10TheResultIsAStringTheChoiceOfWhichIsImplementationDependent20()
        {
			RunTest(@"TestCases/ch15/15.7/15.7.4/15.7.4.2/S15.7.4.2_A2_T20.js", false);
        }

        [Fact]
        [Trait("Category", "15.7.4.2")]
        public void TostringIfRadixIsAnIntegerFrom2To36ButNot10TheResultIsAStringTheChoiceOfWhichIsImplementationDependent21()
        {
			RunTest(@"TestCases/ch15/15.7/15.7.4/15.7.4.2/S15.7.4.2_A2_T21.js", false);
        }

        [Fact]
        [Trait("Category", "15.7.4.2")]
        public void TostringIfRadixIsAnIntegerFrom2To36ButNot10TheResultIsAStringTheChoiceOfWhichIsImplementationDependent22()
        {
			RunTest(@"TestCases/ch15/15.7/15.7.4/15.7.4.2/S15.7.4.2_A2_T22.js", false);
        }

        [Fact]
        [Trait("Category", "15.7.4.2")]
        public void TostringIfRadixIsAnIntegerFrom2To36ButNot10TheResultIsAStringTheChoiceOfWhichIsImplementationDependent23()
        {
			RunTest(@"TestCases/ch15/15.7/15.7.4/15.7.4.2/S15.7.4.2_A2_T23.js", false);
        }

        [Fact]
        [Trait("Category", "15.7.4.2")]
        public void TostringIfRadixIsAnIntegerFrom2To36ButNot10TheResultIsAStringTheChoiceOfWhichIsImplementationDependent24()
        {
			RunTest(@"TestCases/ch15/15.7/15.7.4/15.7.4.2/S15.7.4.2_A2_T24.js", false);
        }

        [Fact]
        [Trait("Category", "15.7.4.2")]
        public void TostringIfRadixIsAnIntegerFrom2To36ButNot10TheResultIsAStringTheChoiceOfWhichIsImplementationDependent25()
        {
			RunTest(@"TestCases/ch15/15.7/15.7.4/15.7.4.2/S15.7.4.2_A2_T25.js", false);
        }

        [Fact]
        [Trait("Category", "15.7.4.2")]
        public void TostringIfRadixIsAnIntegerFrom2To36ButNot10TheResultIsAStringTheChoiceOfWhichIsImplementationDependent26()
        {
			RunTest(@"TestCases/ch15/15.7/15.7.4/15.7.4.2/S15.7.4.2_A2_T26.js", false);
        }

        [Fact]
        [Trait("Category", "15.7.4.2")]
        public void TostringIfRadixIsAnIntegerFrom2To36ButNot10TheResultIsAStringTheChoiceOfWhichIsImplementationDependent27()
        {
			RunTest(@"TestCases/ch15/15.7/15.7.4/15.7.4.2/S15.7.4.2_A2_T27.js", false);
        }

        [Fact]
        [Trait("Category", "15.7.4.2")]
        public void TostringIfRadixIsAnIntegerFrom2To36ButNot10TheResultIsAStringTheChoiceOfWhichIsImplementationDependent28()
        {
			RunTest(@"TestCases/ch15/15.7/15.7.4/15.7.4.2/S15.7.4.2_A2_T28.js", false);
        }

        [Fact]
        [Trait("Category", "15.7.4.2")]
        public void TostringIfRadixIsAnIntegerFrom2To36ButNot10TheResultIsAStringTheChoiceOfWhichIsImplementationDependent29()
        {
			RunTest(@"TestCases/ch15/15.7/15.7.4/15.7.4.2/S15.7.4.2_A2_T29.js", false);
        }

        [Fact]
        [Trait("Category", "15.7.4.2")]
        public void TostringIfRadixIsAnIntegerFrom2To36ButNot10TheResultIsAStringTheChoiceOfWhichIsImplementationDependent30()
        {
			RunTest(@"TestCases/ch15/15.7/15.7.4/15.7.4.2/S15.7.4.2_A2_T30.js", false);
        }

        [Fact]
        [Trait("Category", "15.7.4.2")]
        public void TostringIfRadixIsAnIntegerFrom2To36ButNot10TheResultIsAStringTheChoiceOfWhichIsImplementationDependent31()
        {
			RunTest(@"TestCases/ch15/15.7/15.7.4/15.7.4.2/S15.7.4.2_A2_T31.js", false);
        }

        [Fact]
        [Trait("Category", "15.7.4.2")]
        public void TostringIfRadixIsAnIntegerFrom2To36ButNot10TheResultIsAStringTheChoiceOfWhichIsImplementationDependent32()
        {
			RunTest(@"TestCases/ch15/15.7/15.7.4/15.7.4.2/S15.7.4.2_A2_T32.js", false);
        }

        [Fact]
        [Trait("Category", "15.7.4.2")]
        public void TostringIfRadixIsAnIntegerFrom2To36ButNot10TheResultIsAStringTheChoiceOfWhichIsImplementationDependent33()
        {
			RunTest(@"TestCases/ch15/15.7/15.7.4/15.7.4.2/S15.7.4.2_A2_T33.js", false);
        }

        [Fact]
        [Trait("Category", "15.7.4.2")]
        public void TostringIfRadixIsAnIntegerFrom2To36ButNot10TheResultIsAStringTheChoiceOfWhichIsImplementationDependent34()
        {
			RunTest(@"TestCases/ch15/15.7/15.7.4/15.7.4.2/S15.7.4.2_A2_T34.js", false);
        }

        [Fact]
        [Trait("Category", "15.7.4.2")]
        public void TostringRadixShouldBeAnIntegerBetween2And36()
        {
			RunTest(@"TestCases/ch15/15.7/15.7.4/15.7.4.2/S15.7.4.2_A3_T01.js", false);
        }

        [Fact]
        [Trait("Category", "15.7.4.2")]
        public void TostringRadixShouldBeAnIntegerBetween2And362()
        {
			RunTest(@"TestCases/ch15/15.7/15.7.4/15.7.4.2/S15.7.4.2_A3_T02.js", false);
        }

        [Fact]
        [Trait("Category", "15.7.4.2")]
        public void TostringRadixShouldBeAnIntegerBetween2And363()
        {
			RunTest(@"TestCases/ch15/15.7/15.7.4/15.7.4.2/S15.7.4.2_A3_T03.js", false);
        }

        [Fact]
        [Trait("Category", "15.7.4.2")]
        public void TostringRadixShouldBeAnIntegerBetween2And364()
        {
			RunTest(@"TestCases/ch15/15.7/15.7.4/15.7.4.2/S15.7.4.2_A3_T04.js", false);
        }

        [Fact]
        [Trait("Category", "15.7.4.2")]
        public void TheTostringFunctionIsNotGenericItCannotBeTransferredToOtherKindsOfObjectsForUseAsAMethodAndThereIsShouldBeATypeerrorExceptionIfItsThisValueIsNotANumberObject()
        {
			RunTest(@"TestCases/ch15/15.7/15.7.4/15.7.4.2/S15.7.4.2_A4_T01.js", false);
        }

        [Fact]
        [Trait("Category", "15.7.4.2")]
        public void TheTostringFunctionIsNotGenericItCannotBeTransferredToOtherKindsOfObjectsForUseAsAMethodAndThereIsShouldBeATypeerrorExceptionIfItsThisValueIsNotANumberObject2()
        {
			RunTest(@"TestCases/ch15/15.7/15.7.4/15.7.4.2/S15.7.4.2_A4_T02.js", false);
        }

        [Fact]
        [Trait("Category", "15.7.4.2")]
        public void TheTostringFunctionIsNotGenericItCannotBeTransferredToOtherKindsOfObjectsForUseAsAMethodAndThereIsShouldBeATypeerrorExceptionIfItsThisValueIsNotANumberObject3()
        {
			RunTest(@"TestCases/ch15/15.7/15.7.4/15.7.4.2/S15.7.4.2_A4_T03.js", false);
        }

        [Fact]
        [Trait("Category", "15.7.4.2")]
        public void TheTostringFunctionIsNotGenericItCannotBeTransferredToOtherKindsOfObjectsForUseAsAMethodAndThereIsShouldBeATypeerrorExceptionIfItsThisValueIsNotANumberObject4()
        {
			RunTest(@"TestCases/ch15/15.7/15.7.4/15.7.4.2/S15.7.4.2_A4_T04.js", false);
        }

        [Fact]
        [Trait("Category", "15.7.4.2")]
        public void TheTostringFunctionIsNotGenericItCannotBeTransferredToOtherKindsOfObjectsForUseAsAMethodAndThereIsShouldBeATypeerrorExceptionIfItsThisValueIsNotANumberObject5()
        {
			RunTest(@"TestCases/ch15/15.7/15.7.4/15.7.4.2/S15.7.4.2_A4_T05.js", false);
        }


    }
}
