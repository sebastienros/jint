using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_15_2_4_7 : EcmaTest
    {
        [Fact]
        [Trait("Category", "15.2.4.7")]
        public void TheObjectPrototypePropertyisenumerableLengthPropertyHasTheAttributeReadonly()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.4/15.2.4.7/S15.2.4.7_A10.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.4.7")]
        public void TheLengthPropertyOfTheHasownpropertyMethodIs1()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.4/15.2.4.7/S15.2.4.7_A11.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.4.7")]
        public void LetOBeTheResultOfCallingToobjectPassingTheThisValueAsTheArgument()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.4/15.2.4.7/S15.2.4.7_A12.js", true);
        }

        [Fact]
        [Trait("Category", "15.2.4.7")]
        public void LetOBeTheResultOfCallingToobjectPassingTheThisValueAsTheArgument2()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.4/15.2.4.7/S15.2.4.7_A13.js", true);
        }

        [Fact]
        [Trait("Category", "15.2.4.7")]
        public void ThePropertyisenumerableMethodDoesNotConsiderObjectsInThePrototypeChain()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.4/15.2.4.7/S15.2.4.7_A1_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.4.7")]
        public void WhenThePropertyisenumerableMethodIsCalledWithArgumentVTheFollowingStepsAreTakenILetOBeThisObjectIiCallTostringVIiiIfODoesnTHaveAPropertyWithTheNameGivenByResultIiReturnFalseIvIfThePropertyHasTheDontenumAttributeReturnFalseVReturnTrue()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.4/15.2.4.7/S15.2.4.7_A2_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.4.7")]
        public void WhenThePropertyisenumerableMethodIsCalledWithArgumentVTheFollowingStepsAreTakenILetOBeThisObjectIiCallTostringVIiiIfODoesnTHaveAPropertyWithTheNameGivenByResultIiReturnFalseIvIfThePropertyHasTheDontenumAttributeReturnFalseVReturnTrue2()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.4/15.2.4.7/S15.2.4.7_A2_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.4.7")]
        public void ObjectPrototypePropertyisenumerableHasNotPrototypeProperty()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.4/15.2.4.7/S15.2.4.7_A6.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.4.7")]
        public void ObjectPrototypePropertyisenumerableCanTBeUsedAsAConstructor()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.4/15.2.4.7/S15.2.4.7_A7.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.4.7")]
        public void TheObjectPrototypePropertyisenumerableLengthPropertyHasTheAttributeDontenum()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.4/15.2.4.7/S15.2.4.7_A8.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.4.7")]
        public void TheObjectPrototypePropertyisenumerableLengthPropertyHasTheAttributeDontdelete()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.4/15.2.4.7/S15.2.4.7_A9.js", false);
        }


    }
}
