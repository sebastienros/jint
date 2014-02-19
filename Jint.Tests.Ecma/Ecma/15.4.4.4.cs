using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_15_4_4_4 : EcmaTest
    {
        [Fact]
        [Trait("Category", "15.4.4.4")]
        public void ArrayPrototypeConcatWillConcatAnArrayWhenIndexPropertyReadOnlyExistsInArrayPrototypeStep5BIii3B()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.4/15.4.4.4-5-b-iii-3-b-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.4")]
        public void ArrayPrototypeConcatWillConcatAnArrayWhenIndexPropertyReadOnlyExistsInArrayPrototypeStep5CI()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.4/15.4.4.4-5-c-i-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.4")]
        public void WhenTheConcatMethodIsCalledWithZeroOrMoreArgumentsItem1Item2EtcItReturnsAnArrayContainingTheArrayElementsOfTheObjectFollowedByTheArrayElementsOfEachArgumentInOrder()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.4/S15.4.4.4_A1_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.4")]
        public void WhenTheConcatMethodIsCalledWithZeroOrMoreArgumentsItem1Item2EtcItReturnsAnArrayContainingTheArrayElementsOfTheObjectFollowedByTheArrayElementsOfEachArgumentInOrder2()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.4/S15.4.4.4_A1_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.4")]
        public void WhenTheConcatMethodIsCalledWithZeroOrMoreArgumentsItem1Item2EtcItReturnsAnArrayContainingTheArrayElementsOfTheObjectFollowedByTheArrayElementsOfEachArgumentInOrder3()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.4/S15.4.4.4_A1_T3.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.4")]
        public void WhenTheConcatMethodIsCalledWithZeroOrMoreArgumentsItem1Item2EtcItReturnsAnArrayContainingTheArrayElementsOfTheObjectFollowedByTheArrayElementsOfEachArgumentInOrder4()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.4/S15.4.4.4_A1_T4.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.4")]
        public void TheConcatFunctionIsIntentionallyGenericItDoesNotRequireThatItsThisValueBeAnArrayObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.4/S15.4.4.4_A2_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.4")]
        public void TheConcatFunctionIsIntentionallyGenericItDoesNotRequireThatItsThisValueBeAnArrayObject2()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.4/S15.4.4.4_A2_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.4")]
        public void GetFromNotAnInheritedProperty()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.4/S15.4.4.4_A3_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.4")]
        public void TheLengthPropertyOfConcatHasTheAttributeDontenum()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.4/S15.4.4.4_A4.1.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.4")]
        public void TheLengthPropertyOfConcatHasTheAttributeDontdelete()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.4/S15.4.4.4_A4.2.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.4")]
        public void TheLengthPropertyOfConcatHasTheAttributeReadonly()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.4/S15.4.4.4_A4.3.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.4")]
        public void TheLengthPropertyOfConcatIs1()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.4/S15.4.4.4_A4.4.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.4")]
        public void TheConcatPropertyOfArrayHasTheAttributeDontenum()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.4/S15.4.4.4_A4.5.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.4")]
        public void TheConcatPropertyOfArrayHasNotPrototypeProperty()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.4/S15.4.4.4_A4.6.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.4")]
        public void TheConcatPropertyOfArrayCanTBeUsedAsConstructor()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.4/S15.4.4.4_A4.7.js", false);
        }


    }
}
