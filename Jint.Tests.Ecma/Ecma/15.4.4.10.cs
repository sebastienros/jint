using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_15_4_4_10 : EcmaTest
    {
        [Fact]
        [Trait("Category", "15.4.4.10")]
        public void ArrayPrototypeSliceWillSliceAStringFromStartToEndWhenIndexPropertyReadOnlyExistsInArrayPrototypeStep10CIi()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.10/15.4.4.10-10-c-ii-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.10")]
        public void IfStartIsPositiveUseMinStartLengthIfEndIsPositiveUseMinEndLength()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.10/S15.4.4.10_A1.1_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.10")]
        public void IfStartIsPositiveUseMinStartLengthIfEndIsPositiveUseMinEndLength2()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.10/S15.4.4.10_A1.1_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.10")]
        public void IfStartIsPositiveUseMinStartLengthIfEndIsPositiveUseMinEndLength3()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.10/S15.4.4.10_A1.1_T3.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.10")]
        public void IfStartIsPositiveUseMinStartLengthIfEndIsPositiveUseMinEndLength4()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.10/S15.4.4.10_A1.1_T4.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.10")]
        public void IfStartIsPositiveUseMinStartLengthIfEndIsPositiveUseMinEndLength5()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.10/S15.4.4.10_A1.1_T5.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.10")]
        public void IfStartIsPositiveUseMinStartLengthIfEndIsPositiveUseMinEndLength6()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.10/S15.4.4.10_A1.1_T6.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.10")]
        public void IfStartIsPositiveUseMinStartLengthIfEndIsPositiveUseMinEndLength7()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.10/S15.4.4.10_A1.1_T7.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.10")]
        public void IfStartIsNegativeUseMaxStartLength0IfEndIsPositiveUseMinEndLength()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.10/S15.4.4.10_A1.2_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.10")]
        public void IfStartIsNegativeUseMaxStartLength0IfEndIsPositiveUseMinEndLength2()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.10/S15.4.4.10_A1.2_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.10")]
        public void IfStartIsNegativeUseMaxStartLength0IfEndIsPositiveUseMinEndLength3()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.10/S15.4.4.10_A1.2_T3.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.10")]
        public void IfStartIsNegativeUseMaxStartLength0IfEndIsPositiveUseMinEndLength4()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.10/S15.4.4.10_A1.2_T4.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.10")]
        public void IfStartIsPositiveUseMinStartLengthIfEndIsNegativeUseMaxEndLength0()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.10/S15.4.4.10_A1.3_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.10")]
        public void IfStartIsPositiveUseMinStartLengthIfEndIsNegativeUseMaxEndLength02()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.10/S15.4.4.10_A1.3_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.10")]
        public void IfStartIsPositiveUseMinStartLengthIfEndIsNegativeUseMaxEndLength03()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.10/S15.4.4.10_A1.3_T3.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.10")]
        public void IfStartIsPositiveUseMinStartLengthIfEndIsNegativeUseMaxEndLength04()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.10/S15.4.4.10_A1.3_T4.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.10")]
        public void IfStartIsNegativeUseMaxStartLength0IfEndIsNegativeUseMaxEndLength0()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.10/S15.4.4.10_A1.4_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.10")]
        public void IfStartIsNegativeUseMaxStartLength0IfEndIsNegativeUseMaxEndLength02()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.10/S15.4.4.10_A1.4_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.10")]
        public void IfStartIsNegativeUseMaxStartLength0IfEndIsNegativeUseMaxEndLength03()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.10/S15.4.4.10_A1.4_T3.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.10")]
        public void IfStartIsNegativeUseMaxStartLength0IfEndIsNegativeUseMaxEndLength04()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.10/S15.4.4.10_A1.4_T4.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.10")]
        public void IfEndIsUndefinedUseLength()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.10/S15.4.4.10_A1.5_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.10")]
        public void IfEndIsUndefinedUseLength2()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.10/S15.4.4.10_A1.5_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.10")]
        public void OperatorUseTointegerFromStart()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.10/S15.4.4.10_A2.1_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.10")]
        public void OperatorUseTointegerFromStart2()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.10/S15.4.4.10_A2.1_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.10")]
        public void OperatorUseTointegerFromStart3()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.10/S15.4.4.10_A2.1_T3.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.10")]
        public void OperatorUseTointegerFromStart4()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.10/S15.4.4.10_A2.1_T4.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.10")]
        public void OperatorUseTointegerFromStart5()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.10/S15.4.4.10_A2.1_T5.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.10")]
        public void OperatorUseTointegerFromEnd()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.10/S15.4.4.10_A2.2_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.10")]
        public void OperatorUseTointegerFromEnd2()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.10/S15.4.4.10_A2.2_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.10")]
        public void OperatorUseTointegerFromEnd3()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.10/S15.4.4.10_A2.2_T3.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.10")]
        public void OperatorUseTointegerFromEnd4()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.10/S15.4.4.10_A2.2_T4.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.10")]
        public void OperatorUseTointegerFromEnd5()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.10/S15.4.4.10_A2.2_T5.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.10")]
        public void TheSliceFunctionIsIntentionallyGenericItDoesNotRequireThatItsThisValueBeAnArrayObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.10/S15.4.4.10_A2_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.10")]
        public void TheSliceFunctionIsIntentionallyGenericItDoesNotRequireThatItsThisValueBeAnArrayObject2()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.10/S15.4.4.10_A2_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.10")]
        public void TheSliceFunctionIsIntentionallyGenericItDoesNotRequireThatItsThisValueBeAnArrayObject3()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.10/S15.4.4.10_A2_T3.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.10")]
        public void TheSliceFunctionIsIntentionallyGenericItDoesNotRequireThatItsThisValueBeAnArrayObject4()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.10/S15.4.4.10_A2_T4.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.10")]
        public void TheSliceFunctionIsIntentionallyGenericItDoesNotRequireThatItsThisValueBeAnArrayObject5()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.10/S15.4.4.10_A2_T5.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.10")]
        public void TheSliceFunctionIsIntentionallyGenericItDoesNotRequireThatItsThisValueBeAnArrayObject6()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.10/S15.4.4.10_A2_T6.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.10")]
        public void CheckTouint32LengthForNonArrayObjects()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.10/S15.4.4.10_A3_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.10")]
        public void CheckTouint32LengthForNonArrayObjects2()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.10/S15.4.4.10_A3_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.10")]
        public void CheckTouint32LengthForNonArrayObjects3()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.10/S15.4.4.10_A3_T3.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.10")]
        public void GetFromNotAnInheritedProperty()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.10/S15.4.4.10_A4_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.10")]
        public void TheLengthPropertyOfSliceHasTheAttributeDontenum()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.10/S15.4.4.10_A5.1.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.10")]
        public void TheLengthPropertyOfSliceHasTheAttributeDontdelete()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.10/S15.4.4.10_A5.2.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.10")]
        public void TheLengthPropertyOfSliceHasTheAttributeReadonly()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.10/S15.4.4.10_A5.3.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.10")]
        public void TheLengthPropertyOfSliceIs2()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.10/S15.4.4.10_A5.4.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.10")]
        public void TheSlicePropertyOfArrayHasTheAttributeDontenum()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.10/S15.4.4.10_A5.5.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.10")]
        public void TheSlicePropertyOfArrayHasNotPrototypeProperty()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.10/S15.4.4.10_A5.6.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.10")]
        public void TheSlicePropertyOfArrayCanTBeUsedAsConstructor()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.10/S15.4.4.10_A5.7.js", false);
        }


    }
}
