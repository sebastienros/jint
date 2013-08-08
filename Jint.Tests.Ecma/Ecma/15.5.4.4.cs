using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_15_5_4_4 : EcmaTest
    {
        [Fact]
        [Trait("Category", "15.5.4.4")]
        public void StringPrototypeCharatCanAcceptManyArguments()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.4/S15.5.4.4_A1.1.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.4")]
        public void TheStringPrototypeCharatLengthPropertyHasTheAttributeReadonly()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.4/S15.5.4.4_A10.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.4")]
        public void TheLengthPropertyOfTheCharatMethodIs1()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.4/S15.5.4.4_A11.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.4")]
        public void StringPrototypeCharatPos()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.4/S15.5.4.4_A1_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.4")]
        public void StringPrototypeCharatPos2()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.4/S15.5.4.4_A1_T10.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.4")]
        public void StringPrototypeCharatPos3()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.4/S15.5.4.4_A1_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.4")]
        public void StringPrototypeCharatPos4()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.4/S15.5.4.4_A1_T4.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.4")]
        public void StringPrototypeCharatPos5()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.4/S15.5.4.4_A1_T5.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.4")]
        public void StringPrototypeCharatPos6()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.4/S15.5.4.4_A1_T6.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.4")]
        public void StringPrototypeCharatPos7()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.4/S15.5.4.4_A1_T7.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.4")]
        public void StringPrototypeCharatPos8()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.4/S15.5.4.4_A1_T8.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.4")]
        public void StringPrototypeCharatPos9()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.4/S15.5.4.4_A1_T9.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.4")]
        public void WhenStringPrototypeCharatPosCallsIfTointegerPosLessThan0TheEmptyStringReturns()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.4/S15.5.4.4_A2.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.4")]
        public void WhenStringPrototypeCharatPosCallsIfTointegerPosNotLessThanTostringThisValueTheEmptyStringReturns()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.4/S15.5.4.4_A3.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.4")]
        public void IfPosIsAValueOfNumberTypeThatIsAnIntegerThenTheResultOfXCharatPosIsEqualToTheResultOfXSubstringPosPos1()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.4/S15.5.4.4_A4_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.4")]
        public void IfPosIsAValueOfNumberTypeThatIsAnIntegerThenTheResultOfXCharatPosIsEqualToTheResultOfXSubstringPosPos12()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.4/S15.5.4.4_A4_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.4")]
        public void IfPosIsAValueOfNumberTypeThatIsAnIntegerThenTheResultOfXCharatPosIsEqualToTheResultOfXSubstringPosPos13()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.4/S15.5.4.4_A4_T3.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.4")]
        public void WhenStringPrototypeCharatPosCallsFirstCallsTostringGivingItTheThisValueAsItsArgument()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.4/S15.5.4.4_A5.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.4")]
        public void StringPrototypeCharatHasNotPrototypeProperty()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.4/S15.5.4.4_A6.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.4")]
        public void StringPrototypeCharatCanTBeUsedAsConstructor()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.4/S15.5.4.4_A7.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.4")]
        public void TheStringPrototypeCharatLengthPropertyHasTheAttributeDontenum()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.4/S15.5.4.4_A8.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.4")]
        public void TheStringPrototypeCharatLengthPropertyHasTheAttributeDontdelete()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.4/S15.5.4.4_A9.js", false);
        }


    }
}
