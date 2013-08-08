using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_15_2_4_3 : EcmaTest
    {
        [Fact]
        [Trait("Category", "15.2.4.3")]
        public void TolocalestringFunctionReturnsTheResultOfCallingTostring()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.4/15.2.4.3/S15.2.4.3_A1.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.4.3")]
        public void TheObjectPrototypeTolocalestringLengthPropertyHasTheAttributeReadonly()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.4/15.2.4.3/S15.2.4.3_A10.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.4.3")]
        public void TheLengthPropertyOfTheTolocalestringMethodIs0()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.4/15.2.4.3/S15.2.4.3_A11.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.4.3")]
        public void LetOBeTheResultOfCallingToobjectPassingTheThisValueAsTheArgument()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.4/15.2.4.3/S15.2.4.3_A12.js", true);
        }

        [Fact]
        [Trait("Category", "15.2.4.3")]
        public void LetOBeTheResultOfCallingToobjectPassingTheThisValueAsTheArgument2()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.4/15.2.4.3/S15.2.4.3_A13.js", true);
        }

        [Fact]
        [Trait("Category", "15.2.4.3")]
        public void ObjectPrototypeTolocalestringHasNotPrototypeProperty()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.4/15.2.4.3/S15.2.4.3_A6.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.4.3")]
        public void ObjectPrototypeTolocalestringCanTBeUsedAsAConstructor()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.4/15.2.4.3/S15.2.4.3_A7.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.4.3")]
        public void TheObjectPrototypeTolocalestringLengthPropertyHasTheAttributeDontenum()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.4/15.2.4.3/S15.2.4.3_A8.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.4.3")]
        public void TheObjectPrototypeTolocalestringLengthPropertyHasTheAttributeDontdelete()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.4/15.2.4.3/S15.2.4.3_A9.js", false);
        }


    }
}
