using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_15_5_4_7 : EcmaTest
    {
        [Fact]
        [Trait("Category", "15.5.4.7")]
        public void TheStringPrototypeIndexofLengthPropertyHasTheAttributeReadonly()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.7/S15.5.4.7_A10.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.7")]
        public void TheLengthPropertyOfTheIndexofMethodIs1()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.7/S15.5.4.7_A11.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.7")]
        public void StringPrototypeIndexofSearchstringPosition()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.7/S15.5.4.7_A1_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.7")]
        public void StringPrototypeIndexofSearchstringPosition2()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.7/S15.5.4.7_A1_T10.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.7")]
        public void StringPrototypeIndexofSearchstringPosition3()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.7/S15.5.4.7_A1_T11.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.7")]
        public void StringPrototypeIndexofSearchstringPosition4()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.7/S15.5.4.7_A1_T12.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.7")]
        public void StringPrototypeIndexofSearchstringPosition5()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.7/S15.5.4.7_A1_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.7")]
        public void StringPrototypeIndexofSearchstringPosition6()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.7/S15.5.4.7_A1_T4.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.7")]
        public void StringPrototypeIndexofSearchstringPosition7()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.7/S15.5.4.7_A1_T5.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.7")]
        public void StringPrototypeIndexofSearchstringPosition8()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.7/S15.5.4.7_A1_T6.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.7")]
        public void StringPrototypeIndexofSearchstringPosition9()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.7/S15.5.4.7_A1_T7.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.7")]
        public void StringPrototypeIndexofSearchstringPosition10()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.7/S15.5.4.7_A1_T8.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.7")]
        public void StringPrototypeIndexofSearchstringPosition11()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.7/S15.5.4.7_A1_T9.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.7")]
        public void WhenLengthOfSearchstringLessThanLengthOfTostringThis1Returns()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.7/S15.5.4.7_A2_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.7")]
        public void WhenLengthOfSearchstringLessThanLengthOfTostringThis1Returns2()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.7/S15.5.4.7_A2_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.7")]
        public void WhenLengthOfSearchstringLessThanLengthOfTostringThis1Returns3()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.7/S15.5.4.7_A2_T3.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.7")]
        public void WhenLengthOfSearchstringLessThanLengthOfTostringThis1Returns4()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.7/S15.5.4.7_A2_T4.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.7")]
        public void SinceWeDealWithMaxTointegerPos0IfTointegerPosLessThan0IndexofSearchstring0Returns()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.7/S15.5.4.7_A3_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.7")]
        public void SinceWeDealWithMaxTointegerPos0IfTointegerPosLessThan0IndexofSearchstring0Returns2()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.7/S15.5.4.7_A3_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.7")]
        public void SinceWeDealWithMaxTointegerPos0IfTointegerPosLessThan0IndexofSearchstring0Returns3()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.7/S15.5.4.7_A3_T3.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.7")]
        public void WhenStringPrototypeIndexofSearchstringPositionIsCalledFirstCallTostringGivingItTheThisValueAsItsArgumentThenCallTostringSearchstringAndCallTonumberPosition()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.7/S15.5.4.7_A4_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.7")]
        public void WhenStringPrototypeIndexofSearchstringPositionIsCalledFirstCallTostringGivingItTheThisValueAsItsArgumentThenCallTostringSearchstringAndCallTonumberPosition2()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.7/S15.5.4.7_A4_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.7")]
        public void WhenStringPrototypeIndexofSearchstringPositionIsCalledFirstCallTostringGivingItTheThisValueAsItsArgumentThenCallTostringSearchstringAndCallTonumberPosition3()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.7/S15.5.4.7_A4_T3.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.7")]
        public void WhenStringPrototypeIndexofSearchstringPositionIsCalledFirstCallTostringGivingItTheThisValueAsItsArgumentThenCallTostringSearchstringAndCallTonumberPosition4()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.7/S15.5.4.7_A4_T4.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.7")]
        public void WhenStringPrototypeIndexofSearchstringPositionIsCalledFirstCallTostringGivingItTheThisValueAsItsArgumentThenCallTostringSearchstringAndCallTonumberPosition5()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.7/S15.5.4.7_A4_T5.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.7")]
        public void StringPrototypeIndexofWorksProperly()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.7/S15.5.4.7_A5_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.7")]
        public void StringPrototypeIndexofWorksProperly2()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.7/S15.5.4.7_A5_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.7")]
        public void StringPrototypeIndexofWorksProperly3()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.7/S15.5.4.7_A5_T3.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.7")]
        public void StringPrototypeIndexofWorksProperly4()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.7/S15.5.4.7_A5_T4.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.7")]
        public void StringPrototypeIndexofWorksProperly5()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.7/S15.5.4.7_A5_T5.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.7")]
        public void StringPrototypeIndexofWorksProperly6()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.7/S15.5.4.7_A5_T6.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.7")]
        public void StringPrototypeIndexofHasNotPrototypeProperty()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.7/S15.5.4.7_A6.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.7")]
        public void StringPrototypeIndexofCanTBeUsedAsConstructor()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.7/S15.5.4.7_A7.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.7")]
        public void TheStringPrototypeIndexofLengthPropertyHasTheAttributeDontenum()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.7/S15.5.4.7_A8.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.7")]
        public void TheStringPrototypeIndexofLengthPropertyHasTheAttributeDontdelete()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.7/S15.5.4.7_A9.js", false);
        }


    }
}
