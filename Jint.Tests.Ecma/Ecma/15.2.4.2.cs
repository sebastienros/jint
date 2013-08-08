using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_15_2_4_2 : EcmaTest
    {
        [Fact]
        [Trait("Category", "15.2.4.2")]
        public void ObjectPrototypeTostringObjectUndefinedWillBeReturnedWhenThisValueIsUndefined()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.4/15.2.4.2/15.2.4.2-1-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.4.2")]
        public void ObjectPrototypeTostringObjectUndefinedWillBeReturnedWhenThisValueIsUndefined2()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.4/15.2.4.2/15.2.4.2-1-2.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.4.2")]
        public void ObjectPrototypeTostringObjectNullWillBeReturnedWhenThisValueIsNull()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.4/15.2.4.2/15.2.4.2-2-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.4.2")]
        public void ObjectPrototypeTostringObjectNullWillBeReturnedWhenThisValueIsNull2()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.4/15.2.4.2/15.2.4.2-2-2.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.4.2")]
        public void WhenTheTostringMethodIsCalledTheFollowingStepsAreTakenIGetTheClassPropertyOfThisObjectIiComputeAStringValueByConcatenatingTheThreeStringsObjectResult1AndIiiReturnResult2()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.4/15.2.4.2/S15.2.4.2_A1.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.4.2")]
        public void TheObjectPrototypeTostringLengthPropertyHasTheAttributeReadonly()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.4/15.2.4.2/S15.2.4.2_A10.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.4.2")]
        public void TheLengthPropertyOfTheTostringMethodIs0()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.4/15.2.4.2/S15.2.4.2_A11.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.4.2")]
        public void IfTheThisValueIsUndefinedReturnObjectUndefined()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.4/15.2.4.2/S15.2.4.2_A12.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.4.2")]
        public void IfTheThisValueIsNullReturnObjectNull()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.4/15.2.4.2/S15.2.4.2_A13.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.4.2")]
        public void LetOBeTheResultOfCallingToobjectPassingTheThisValueAsTheArgument()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.4/15.2.4.2/S15.2.4.2_A14.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.4.2")]
        public void LetOBeTheResultOfCallingToobjectPassingTheThisValueAsTheArgument2()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.4/15.2.4.2/S15.2.4.2_A15.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.4.2")]
        public void LetOBeTheResultOfCallingToobjectPassingTheThisValueAsTheArgument3()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.4/15.2.4.2/S15.2.4.2_A16.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.4.2")]
        public void ObjectPrototypeTostringHasNotPrototypeProperty()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.4/15.2.4.2/S15.2.4.2_A6.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.4.2")]
        public void ObjectPrototypeTostringCanTBeUsedAsAConstructor()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.4/15.2.4.2/S15.2.4.2_A7.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.4.2")]
        public void TheObjectPrototypeTostringLengthPropertyHasTheAttributeDontenum()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.4/15.2.4.2/S15.2.4.2_A8.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.4.2")]
        public void TheObjectPrototypeTostringLengthPropertyHasTheAttributeDontdelete()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.4/15.2.4.2/S15.2.4.2_A9.js", false);
        }


    }
}
