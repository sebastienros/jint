using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_15_2_4_6 : EcmaTest
    {
        [Fact]
        [Trait("Category", "15.2.4.6")]
        public void WhenTheIsprototypeofMethodIsCalledWithArgumentVAndWhenOAndVReferToTheSameObjectOrToObjectsJoinedToEachOtherReturnTrue()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.4/15.2.4.6/S15.2.4.6_A1.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.4.6")]
        public void TheObjectPrototypeIsprototypeofLengthPropertyHasTheAttributeReadonly()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.4/15.2.4.6/S15.2.4.6_A10.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.4.6")]
        public void TheLengthPropertyOfTheHasownpropertyMethodIs1()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.4/15.2.4.6/S15.2.4.6_A11.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.4.6")]
        public void LetOBeTheResultOfCallingToobjectPassingTheThisValueAsTheArgument()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.4/15.2.4.6/S15.2.4.6_A12.js", true);
        }

        [Fact]
        [Trait("Category", "15.2.4.6")]
        public void LetOBeTheResultOfCallingToobjectPassingTheThisValueAsTheArgument2()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.4/15.2.4.6/S15.2.4.6_A13.js", true);
        }

        [Fact]
        [Trait("Category", "15.2.4.6")]
        public void ObjectPrototypeIsprototypeofHasNotPrototypeProperty()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.4/15.2.4.6/S15.2.4.6_A6.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.4.6")]
        public void ObjectPrototypeIsprototypeofCanTBeUsedAsAConstructor()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.4/15.2.4.6/S15.2.4.6_A7.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.4.6")]
        public void TheObjectPrototypeIsprototypeofLengthPropertyHasTheAttributeDontenum()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.4/15.2.4.6/S15.2.4.6_A8.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.4.6")]
        public void TheObjectPrototypeIsprototypeofLengthPropertyHasTheAttributeDontdelete()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.4/15.2.4.6/S15.2.4.6_A9.js", false);
        }


    }
}
