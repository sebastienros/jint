using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_15_4_4_12 : EcmaTest
    {
        [Fact]
        [Trait("Category", "15.4.4.12")]
        public void ArrayPrototypeSpliceFromIsTheResultOfTostringActualstartKInAnArray()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.12/15.4.4.12-9-a-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.12")]
        public void ArrayPrototypeSpliceWillSpliceAnArrayEvenWhenArrayPrototypeHasIndex0SetToReadOnlyAndFrompresentLessThanActualdeletecountStep9CIi()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.12/15.4.4.12-9-c-ii-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.12")]
        public void IfStartIsPositiveUseMinStartLengthIfDeletecountIsPositiveUseMinDeletecountLengthStart()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.12/S15.4.4.12_A1.1_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.12")]
        public void IfStartIsPositiveUseMinStartLengthIfDeletecountIsPositiveUseMinDeletecountLengthStart2()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.12/S15.4.4.12_A1.1_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.12")]
        public void IfStartIsPositiveUseMinStartLengthIfDeletecountIsPositiveUseMinDeletecountLengthStart3()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.12/S15.4.4.12_A1.1_T3.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.12")]
        public void IfStartIsPositiveUseMinStartLengthIfDeletecountIsPositiveUseMinDeletecountLengthStart4()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.12/S15.4.4.12_A1.1_T4.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.12")]
        public void IfStartIsPositiveUseMinStartLengthIfDeletecountIsPositiveUseMinDeletecountLengthStart5()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.12/S15.4.4.12_A1.1_T5.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.12")]
        public void IfStartIsPositiveUseMinStartLengthIfDeletecountIsPositiveUseMinDeletecountLengthStart6()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.12/S15.4.4.12_A1.1_T6.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.12")]
        public void IfStartIsNegativeUseMaxStartLength0IfDeletecountIsNegativeUse0()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.12/S15.4.4.12_A1.2_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.12")]
        public void IfStartIsNegativeUseMaxStartLength0IfDeletecountIsNegativeUse02()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.12/S15.4.4.12_A1.2_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.12")]
        public void IfStartIsNegativeUseMaxStartLength0IfDeletecountIsNegativeUse03()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.12/S15.4.4.12_A1.2_T3.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.12")]
        public void IfStartIsNegativeUseMaxStartLength0IfDeletecountIsNegativeUse04()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.12/S15.4.4.12_A1.2_T4.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.12")]
        public void IfStartIsNegativeUseMaxStartLength0IfDeletecountIsNegativeUse05()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.12/S15.4.4.12_A1.2_T5.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.12")]
        public void IfStartIsPositiveUseMinStartLengthIfDeletecountIsNegativeUse0()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.12/S15.4.4.12_A1.3_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.12")]
        public void IfStartIsPositiveUseMinStartLengthIfDeletecountIsNegativeUse02()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.12/S15.4.4.12_A1.3_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.12")]
        public void IfStartIsPositiveUseMinStartLengthIfDeletecountIsNegativeUse03()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.12/S15.4.4.12_A1.3_T3.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.12")]
        public void IfStartIsPositiveUseMinStartLengthIfDeletecountIsNegativeUse04()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.12/S15.4.4.12_A1.3_T4.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.12")]
        public void IfStartIsPositiveUseMinStartLengthIfDeletecountIsNegativeUse05()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.12/S15.4.4.12_A1.3_T5.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.12")]
        public void IfStartIsNegativeUseMaxStartLength0IfDeletecountIsPositiveUseMinDeletecountLengthStart()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.12/S15.4.4.12_A1.4_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.12")]
        public void IfStartIsNegativeUseMaxStartLength0IfDeletecountIsPositiveUseMinDeletecountLengthStart2()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.12/S15.4.4.12_A1.4_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.12")]
        public void IfStartIsNegativeUseMaxStartLength0IfDeletecountIsPositiveUseMinDeletecountLengthStart3()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.12/S15.4.4.12_A1.4_T3.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.12")]
        public void IfStartIsNegativeUseMaxStartLength0IfDeletecountIsPositiveUseMinDeletecountLengthStart4()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.12/S15.4.4.12_A1.4_T4.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.12")]
        public void IfStartIsNegativeUseMaxStartLength0IfDeletecountIsPositiveUseMinDeletecountLengthStart5()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.12/S15.4.4.12_A1.4_T5.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.12")]
        public void IfStartIsNegativeUseMaxStartLength0IfDeletecountIsPositiveUseMinDeletecountLengthStart6()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.12/S15.4.4.12_A1.4_T6.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.12")]
        public void SpliceWithUndefinedArguments()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.12/S15.4.4.12_A1.5_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.12")]
        public void SpliceWithUndefinedArguments2()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.12/S15.4.4.12_A1.5_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.12")]
        public void OperatorUseTointegerFromStart()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.12/S15.4.4.12_A2.1_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.12")]
        public void OperatorUseTointegerFromStart2()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.12/S15.4.4.12_A2.1_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.12")]
        public void OperatorUseTointegerFromStart3()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.12/S15.4.4.12_A2.1_T3.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.12")]
        public void OperatorUseTointegerFromStart4()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.12/S15.4.4.12_A2.1_T4.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.12")]
        public void OperatorUseTointegerFromStart5()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.12/S15.4.4.12_A2.1_T5.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.12")]
        public void OperatorUseTointegerFromDeletecount()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.12/S15.4.4.12_A2.2_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.12")]
        public void OperatorUseTointegerFromDeletecount2()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.12/S15.4.4.12_A2.2_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.12")]
        public void OperatorUseTointegerFromDeletecount3()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.12/S15.4.4.12_A2.2_T3.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.12")]
        public void OperatorUseTointegerFromDeletecount4()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.12/S15.4.4.12_A2.2_T4.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.12")]
        public void OperatorUseTointegerFromDeletecount5()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.12/S15.4.4.12_A2.2_T5.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.12")]
        public void TheSpliceFunctionIsIntentionallyGenericItDoesNotRequireThatItsThisValueBeAnArrayObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.12/S15.4.4.12_A2_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.12")]
        public void TheSpliceFunctionIsIntentionallyGenericItDoesNotRequireThatItsThisValueBeAnArrayObject2()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.12/S15.4.4.12_A2_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.12")]
        public void TheSpliceFunctionIsIntentionallyGenericItDoesNotRequireThatItsThisValueBeAnArrayObject3()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.12/S15.4.4.12_A2_T3.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.12")]
        public void TheSpliceFunctionIsIntentionallyGenericItDoesNotRequireThatItsThisValueBeAnArrayObject4()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.12/S15.4.4.12_A2_T4.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.12")]
        public void CheckTouint32LengthForNonArrayObjects()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.12/S15.4.4.12_A3_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.12")]
        public void CheckTouint32LengthForNonArrayObjects2()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.12/S15.4.4.12_A3_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.12")]
        public void CheckTouint32LengthForNonArrayObjects3()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.12/S15.4.4.12_A3_T3.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.12")]
        public void GetFromNotAnInheritedProperty()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.12/S15.4.4.12_A4_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.12")]
        public void GetFromNotAnInheritedProperty2()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.12/S15.4.4.12_A4_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.12")]
        public void GetFromNotAnInheritedProperty3()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.12/S15.4.4.12_A4_T3.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.12")]
        public void TheLengthPropertyOfSpliceHasTheAttributeDontenum()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.12/S15.4.4.12_A5.1.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.12")]
        public void TheLengthPropertyOfSpliceHasTheAttributeDontdelete()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.12/S15.4.4.12_A5.2.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.12")]
        public void TheLengthPropertyOfSpliceHasTheAttributeReadonly()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.12/S15.4.4.12_A5.3.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.12")]
        public void TheLengthPropertyOfSpliceIs2()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.12/S15.4.4.12_A5.4.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.12")]
        public void TheSplicePropertyOfArrayHasTheAttributeDontenum()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.12/S15.4.4.12_A5.5.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.12")]
        public void TheSplicePropertyOfArrayHasNotPrototypeProperty()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.12/S15.4.4.12_A5.6.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.12")]
        public void TheSplicePropertyOfArrayCanTBeUsedAsConstructor()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.12/S15.4.4.12_A5.7.js", false);
        }


    }
}
