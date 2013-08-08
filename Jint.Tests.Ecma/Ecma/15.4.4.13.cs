using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_15_4_4_13 : EcmaTest
    {
        [Fact]
        [Trait("Category", "15.4.4.13")]
        public void TheArgumentsArePrependedToTheStartOfTheArraySuchThatTheirOrderWithinTheArrayIsTheSameAsTheOrderInWhichTheyAppearInTheArgumentList()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.13/S15.4.4.13_A1_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.13")]
        public void TheArgumentsArePrependedToTheStartOfTheArraySuchThatTheirOrderWithinTheArrayIsTheSameAsTheOrderInWhichTheyAppearInTheArgumentList2()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.13/S15.4.4.13_A1_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.13")]
        public void TheUnshiftFunctionIsIntentionallyGenericItDoesNotRequireThatItsThisValueBeAnArrayObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.13/S15.4.4.13_A2_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.13")]
        public void TheUnshiftFunctionIsIntentionallyGenericItDoesNotRequireThatItsThisValueBeAnArrayObject2()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.13/S15.4.4.13_A2_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.13")]
        public void TheUnshiftFunctionIsIntentionallyGenericItDoesNotRequireThatItsThisValueBeAnArrayObject3()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.13/S15.4.4.13_A2_T3.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.13")]
        public void CheckTouint32LengthForNonArrayObjects()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.13/S15.4.4.13_A3_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.13")]
        public void CheckTouint32LengthForNonArrayObjects2()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.13/S15.4.4.13_A3_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.13")]
        public void CheckTouint32LengthForNonArrayObjects3()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.13/S15.4.4.13_A3_T3.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.13")]
        public void GetDeleteFromNotAnInheritedProperty()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.13/S15.4.4.13_A4_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.13")]
        public void GetDeleteFromNotAnInheritedProperty2()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.13/S15.4.4.13_A4_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.13")]
        public void TheLengthPropertyOfUnshiftHasTheAttributeDontenum()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.13/S15.4.4.13_A5.1.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.13")]
        public void TheLengthPropertyOfUnshiftHasTheAttributeDontdelete()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.13/S15.4.4.13_A5.2.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.13")]
        public void TheLengthPropertyOfUnshiftHasTheAttributeReadonly()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.13/S15.4.4.13_A5.3.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.13")]
        public void TheLengthPropertyOfUnshiftIs1()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.13/S15.4.4.13_A5.4.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.13")]
        public void TheUnshiftPropertyOfArrayHasTheAttributeDontenum()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.13/S15.4.4.13_A5.5.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.13")]
        public void TheUnshiftPropertyOfArrayHasNotPrototypeProperty()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.13/S15.4.4.13_A5.6.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.13")]
        public void TheUnshiftPropertyOfArrayCanTBeUsedAsConstructor()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.13/S15.4.4.13_A5.7.js", false);
        }


    }
}
