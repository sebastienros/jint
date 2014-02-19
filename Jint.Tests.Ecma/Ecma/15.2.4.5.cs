using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_15_2_4_5 : EcmaTest
    {
        [Fact]
        [Trait("Category", "15.2.4.5")]
        public void TheObjectPrototypeHasownpropertyLengthPropertyHasTheAttributeReadonly()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.4/15.2.4.5/S15.2.4.5_A10.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.4.5")]
        public void TheLengthPropertyOfTheHasownpropertyMethodIs1()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.4/15.2.4.5/S15.2.4.5_A11.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.4.5")]
        public void LetOBeTheResultOfCallingToobjectPassingTheThisValueAsTheArgument()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.4/15.2.4.5/S15.2.4.5_A12.js", true);
        }

        [Fact]
        [Trait("Category", "15.2.4.5")]
        public void LetOBeTheResultOfCallingToobjectPassingTheThisValueAsTheArgument2()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.4/15.2.4.5/S15.2.4.5_A13.js", true);
        }

        [Fact]
        [Trait("Category", "15.2.4.5")]
        public void WhenTheHasownpropertyMethodIsCalledWithArgumentVTheFollowingStepsAreTakenILetOBeThisObjectIiCallTostringVIiiIfODoesnTHaveAPropertyWithTheNameGivenByResultIiReturnFalseIvReturnTrue()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.4/15.2.4.5/S15.2.4.5_A1_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.4.5")]
        public void WhenTheHasownpropertyMethodIsCalledWithArgumentVTheFollowingStepsAreTakenILetOBeThisObjectIiCallTostringVIiiIfODoesnTHaveAPropertyWithTheNameGivenByResultIiReturnFalseIvReturnTrue2()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.4/15.2.4.5/S15.2.4.5_A1_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.4.5")]
        public void WhenTheHasownpropertyMethodIsCalledWithArgumentVTheFollowingStepsAreTakenILetOBeThisObjectIiCallTostringVIiiIfODoesnTHaveAPropertyWithTheNameGivenByResultIiReturnFalseIvReturnTrue3()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.4/15.2.4.5/S15.2.4.5_A1_T3.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.4.5")]
        public void ObjectPrototypeHasownpropertyHasNotPrototypeProperty()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.4/15.2.4.5/S15.2.4.5_A6.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.4.5")]
        public void ObjectPrototypeHasownpropertyCanTBeUsedAsAConstructor()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.4/15.2.4.5/S15.2.4.5_A7.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.4.5")]
        public void TheObjectPrototypeHasownpropertyLengthPropertyHasTheAttributeDontenum()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.4/15.2.4.5/S15.2.4.5_A8.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.4.5")]
        public void TheObjectPrototypeHasownpropertyLengthPropertyHasTheAttributeDontdelete()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.4/15.2.4.5/S15.2.4.5_A9.js", false);
        }


    }
}
