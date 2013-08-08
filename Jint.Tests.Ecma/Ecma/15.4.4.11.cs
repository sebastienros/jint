using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_15_4_4_11 : EcmaTest
    {
        [Fact]
        [Trait("Category", "15.4.4.11")]
        public void IfThisObjectDoesNotHaveAPropertyNamedByTostringJAndThisObjectDoesNotHaveAPropertyNamedByTostringKReturn0()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.11/S15.4.4.11_A1.1_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.11")]
        public void IfThisObjectDoesNotHaveAPropertyNamedByTostringJReturn1IfThisObjectDoesNotHaveAPropertyNamedByTostringKReturn1()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.11/S15.4.4.11_A1.2_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.11")]
        public void IfThisObjectDoesNotHaveAPropertyNamedByTostringJReturn1IfThisObjectDoesNotHaveAPropertyNamedByTostringKReturn12()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.11/S15.4.4.11_A1.2_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.11")]
        public void IfGetTostringJAndGetTostringKAreBothUndefinedReturn0()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.11/S15.4.4.11_A1.3_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.11")]
        public void IfGetTostringJIsUndefinedReturn1IfGetTostringKIsUndefinedReturn1()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.11/S15.4.4.11_A1.4_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.11")]
        public void IfGetTostringJIsUndefinedReturn1IfGetTostringKIsUndefinedReturn12()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.11/S15.4.4.11_A1.4_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.11")]
        public void IfComparefnIsUndefinedUseSortcompareOperator()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.11/S15.4.4.11_A1.5_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.11")]
        public void IfTostringGetTostringJTostringGetTostringKReturn1IfTostringGetTostringJTostringGetTostringKReturn1Return1()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.11/S15.4.4.11_A2.1_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.11")]
        public void IfTostringGetTostringJTostringGetTostringKReturn1IfTostringGetTostringJTostringGetTostringKReturn1Return12()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.11/S15.4.4.11_A2.1_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.11")]
        public void IfTostringGetTostringJTostringGetTostringKReturn1IfTostringGetTostringJTostringGetTostringKReturn1Return13()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.11/S15.4.4.11_A2.1_T3.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.11")]
        public void MyComparefnIsInverseImplementationComparefn()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.11/S15.4.4.11_A2.2_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.11")]
        public void MyComparefnIsInverseImplementationComparefn2()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.11/S15.4.4.11_A2.2_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.11")]
        public void MyComparefnIsInverseImplementationComparefn3()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.11/S15.4.4.11_A2.2_T3.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.11")]
        public void TheSortFunctionIsIntentionallyGenericItDoesNotRequireThatItsThisValueBeAnArrayObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.11/S15.4.4.11_A3_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.11")]
        public void TheSortFunctionIsIntentionallyGenericItDoesNotRequireThatItsThisValueBeAnArrayObject2()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.11/S15.4.4.11_A3_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.11")]
        public void CheckTouint32LengthForNonArrayObjects()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.11/S15.4.4.11_A4_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.11")]
        public void CheckTouint32LengthForNonArrayObjects2()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.11/S15.4.4.11_A4_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.11")]
        public void CheckTouint32LengthForNonArrayObjects3()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.11/S15.4.4.11_A4_T3.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.11")]
        public void ArraySortShouldNotEatExceptions()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.11/S15.4.4.11_A5_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.11")]
        public void GetDeleteFromNotAnInheritedProperty()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.11/S15.4.4.11_A6_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.11")]
        public void TheLengthPropertyOfSortHasTheAttributeDontenum()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.11/S15.4.4.11_A7.1.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.11")]
        public void TheLengthPropertyOfSortHasTheAttributeDontdelete()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.11/S15.4.4.11_A7.2.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.11")]
        public void TheLengthPropertyOfSortHasTheAttributeReadonly()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.11/S15.4.4.11_A7.3.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.11")]
        public void TheLengthPropertyOfSortIs1()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.11/S15.4.4.11_A7.4.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.11")]
        public void TheSortPropertyOfArrayHasTheAttributeDontenum()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.11/S15.4.4.11_A7.5.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.11")]
        public void TheSortPropertyOfArrayHasNotPrototypeProperty()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.11/S15.4.4.11_A7.6.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.11")]
        public void TheSortPropertyOfArrayCanTBeUsedAsConstructor()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.11/S15.4.4.11_A7.7.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.11")]
        public void CallTheComparefnPassingUndefinedAsTheThisValueStep13B()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.11/S15.4.4.11_A8.js", false);
        }


    }
}
