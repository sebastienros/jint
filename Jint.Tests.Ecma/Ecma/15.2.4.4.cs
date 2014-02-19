using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_15_2_4_4 : EcmaTest
    {
        [Fact]
        [Trait("Category", "15.2.4.4")]
        public void ObjectPrototypeValueofTypeofObjectPrototypeValueofCallTrueObject()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.4/15.2.4.4/15.2.4.4-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.4.4")]
        public void ObjectPrototypeValueofTypeofObjectPrototypeValueofCallFalseObject()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.4/15.2.4.4/15.2.4.4-2.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.4.4")]
        public void TheObjectPrototypeValueofLengthPropertyHasTheAttributeReadonly()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.4/15.2.4.4/S15.2.4.4_A10.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.4.4")]
        public void TheLengthPropertyOfTheValueofMethodIs0()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.4/15.2.4.4/S15.2.4.4_A11.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.4.4")]
        public void LetOBeTheResultOfCallingToobjectPassingTheThisValueAsTheArgument()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.4/15.2.4.4/S15.2.4.4_A12.js", true);
        }

        [Fact]
        [Trait("Category", "15.2.4.4")]
        public void LetOBeTheResultOfCallingToobjectPassingTheThisValueAsTheArgument2()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.4/15.2.4.4/S15.2.4.4_A13.js", true);
        }

        [Fact]
        [Trait("Category", "15.2.4.4")]
        public void LetOBeTheResultOfCallingToobjectPassingTheThisValueAsTheArgument3()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.4/15.2.4.4/S15.2.4.4_A14.js", true);
        }

        [Fact]
        [Trait("Category", "15.2.4.4")]
        public void LetOBeTheResultOfCallingToobjectPassingTheThisValueAsTheArgument4()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.4/15.2.4.4/S15.2.4.4_A15.js", true);
        }

        [Fact]
        [Trait("Category", "15.2.4.4")]
        public void TheValueofMethodReturnsItsThisValue()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.4/15.2.4.4/S15.2.4.4_A1_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.4.4")]
        public void TheValueofMethodReturnsItsThisValue2()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.4/15.2.4.4/S15.2.4.4_A1_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.4.4")]
        public void TheValueofMethodReturnsItsThisValue3()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.4/15.2.4.4/S15.2.4.4_A1_T3.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.4.4")]
        public void TheValueofMethodReturnsItsThisValue4()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.4/15.2.4.4/S15.2.4.4_A1_T4.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.4.4")]
        public void TheValueofMethodReturnsItsThisValue5()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.4/15.2.4.4/S15.2.4.4_A1_T5.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.4.4")]
        public void TheValueofMethodReturnsItsThisValue6()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.4/15.2.4.4/S15.2.4.4_A1_T6.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.4.4")]
        public void TheValueofMethodReturnsItsThisValue7()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.4/15.2.4.4/S15.2.4.4_A1_T7.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.4.4")]
        public void ObjectPrototypeValueofHasNotPrototypeProperty()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.4/15.2.4.4/S15.2.4.4_A6.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.4.4")]
        public void ObjectPrototypeValueofCanTBeUsedAsAConstructor()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.4/15.2.4.4/S15.2.4.4_A7.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.4.4")]
        public void TheObjectPrototypeValueofLengthPropertyHasTheAttributeDontenum()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.4/15.2.4.4/S15.2.4.4_A8.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.4.4")]
        public void TheObjectPrototypeValueofLengthPropertyHasTheAttributeDontdelete()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.4/15.2.4.4/S15.2.4.4_A9.js", false);
        }


    }
}
