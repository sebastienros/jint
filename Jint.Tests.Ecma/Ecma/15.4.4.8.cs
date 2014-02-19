using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_15_4_4_8 : EcmaTest
    {
        [Fact]
        [Trait("Category", "15.4.4.8")]
        public void TheElementsOfTheArrayAreRearrangedSoAsToReverseTheirOrderTheObjectIsReturnedAsTheResultOfTheCall()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.8/S15.4.4.8_A1_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.8")]
        public void TheElementsOfTheArrayAreRearrangedSoAsToReverseTheirOrderTheObjectIsReturnedAsTheResultOfTheCall2()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.8/S15.4.4.8_A1_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.8")]
        public void TheReverseFunctionIsIntentionallyGenericItDoesNotRequireThatItsThisValueBeAnArrayObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.8/S15.4.4.8_A2_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.8")]
        public void TheReverseFunctionIsIntentionallyGenericItDoesNotRequireThatItsThisValueBeAnArrayObject2()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.8/S15.4.4.8_A2_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.8")]
        public void TheReverseFunctionIsIntentionallyGenericItDoesNotRequireThatItsThisValueBeAnArrayObject3()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.8/S15.4.4.8_A2_T3.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.8")]
        public void CheckTouint32LengthForNonArrayObjects()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.8/S15.4.4.8_A3_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.8")]
        public void CheckTouint32LengthForNonArrayObjects2()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.8/S15.4.4.8_A3_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.8")]
        public void CheckTouint32LengthForNonArrayObjects3()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.8/S15.4.4.8_A3_T3.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.8")]
        public void GetDeleteFromNotAnInheritedProperty()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.8/S15.4.4.8_A4_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.8")]
        public void GetDeleteFromNotAnInheritedProperty2()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.8/S15.4.4.8_A4_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.8")]
        public void TheLengthPropertyOfReverseHasTheAttributeDontenum()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.8/S15.4.4.8_A5.1.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.8")]
        public void TheLengthPropertyOfReverseHasTheAttributeDontdelete()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.8/S15.4.4.8_A5.2.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.8")]
        public void TheLengthPropertyOfReverseHasTheAttributeReadonly()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.8/S15.4.4.8_A5.3.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.8")]
        public void TheLengthPropertyOfReverseIs0()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.8/S15.4.4.8_A5.4.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.8")]
        public void TheReversePropertyOfArrayHasTheAttributeDontenum()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.8/S15.4.4.8_A5.5.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.8")]
        public void TheReversePropertyOfArrayHasNotPrototypeProperty()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.8/S15.4.4.8_A5.6.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.8")]
        public void TheReversePropertyOfArrayCanTBeUsedAsConstructor()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.8/S15.4.4.8_A5.7.js", false);
        }


    }
}
