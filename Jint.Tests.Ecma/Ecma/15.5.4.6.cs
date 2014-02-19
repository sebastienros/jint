using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_15_5_4_6 : EcmaTest
    {
        [Fact]
        [Trait("Category", "15.5.4.6")]
        public void TheStringPrototypeConcatLengthPropertyHasTheAttributeReadonly()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.6/S15.5.4.6_A10.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.6")]
        public void TheLengthPropertyOfTheConcatMethodIs1()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.6/S15.5.4.6_A11.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.6")]
        public void StringPrototypeConcat()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.6/S15.5.4.6_A1_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.6")]
        public void StringPrototypeConcat2()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.6/S15.5.4.6_A1_T10.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.6")]
        public void StringPrototypeConcat3()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.6/S15.5.4.6_A1_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.6")]
        public void StringPrototypeConcat4()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.6/S15.5.4.6_A1_T4.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.6")]
        public void StringPrototypeConcat5()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.6/S15.5.4.6_A1_T5.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.6")]
        public void StringPrototypeConcat6()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.6/S15.5.4.6_A1_T6.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.6")]
        public void StringPrototypeConcat7()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.6/S15.5.4.6_A1_T7.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.6")]
        public void StringPrototypeConcat8()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.6/S15.5.4.6_A1_T8.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.6")]
        public void StringPrototypeConcat9()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.6/S15.5.4.6_A1_T9.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.6")]
        public void StringPrototypeConcatCanAcceptAtLeast128()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.6/S15.5.4.6_A2.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.6")]
        public void StringPrototypeConcatCanTChangeTheInstanceToBeApplied()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.6/S15.5.4.6_A3.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.6")]
        public void WhenStringPrototypeConcatIsCalledFirstCallTostringGivingItTheThisValueAsItsArgument()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.6/S15.5.4.6_A4_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.6")]
        public void WhenStringPrototypeConcatIsCalledFirstCallTostringGivingItTheThisValueAsItsArgument2()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.6/S15.5.4.6_A4_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.6")]
        public void StringPrototypeConcatHasNotPrototypeProperty()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.6/S15.5.4.6_A6.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.6")]
        public void StringPrototypeConcatCanTBeUsedAsConstructor()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.6/S15.5.4.6_A7.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.6")]
        public void TheStringPrototypeConcatLengthPropertyHasTheAttributeDontenum()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.6/S15.5.4.6_A8.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.6")]
        public void TheStringPrototypeConcatLengthPropertyHasTheAttributeDontdelete()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.6/S15.5.4.6_A9.js", false);
        }


    }
}
