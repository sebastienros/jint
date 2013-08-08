using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_15_5_4_8 : EcmaTest
    {
        [Fact]
        [Trait("Category", "15.5.4.8")]
        public void TheStringPrototypeLastindexofLengthPropertyHasTheAttributeReadonly()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.8/S15.5.4.8_A10.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.8")]
        public void TheLengthPropertyOfTheLastindexofMethodIs1()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.8/S15.5.4.8_A11.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.8")]
        public void StringPrototypeLastindexofSearchstringPosition()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.8/S15.5.4.8_A1_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.8")]
        public void StringPrototypeLastindexofSearchstringPosition2()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.8/S15.5.4.8_A1_T10.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.8")]
        public void StringPrototypeLastindexofSearchstringPosition3()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.8/S15.5.4.8_A1_T12.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.8")]
        public void StringPrototypeLastindexofSearchstringPosition4()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.8/S15.5.4.8_A1_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.8")]
        public void StringPrototypeLastindexofSearchstringPosition5()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.8/S15.5.4.8_A1_T4.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.8")]
        public void StringPrototypeLastindexofSearchstringPosition6()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.8/S15.5.4.8_A1_T5.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.8")]
        public void StringPrototypeLastindexofSearchstringPosition7()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.8/S15.5.4.8_A1_T6.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.8")]
        public void StringPrototypeLastindexofSearchstringPosition8()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.8/S15.5.4.8_A1_T7.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.8")]
        public void StringPrototypeLastindexofSearchstringPosition9()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.8/S15.5.4.8_A1_T8.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.8")]
        public void StringPrototypeLastindexofSearchstringPosition10()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.8/S15.5.4.8_A1_T9.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.8")]
        public void WhenStringPrototypeLastindexofSearchstringPositionIsCalledFirstCallTostringGivingItTheThisValueAsItsArgumentThenCallTostringSearchstringAndCallTonumberPosition()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.8/S15.5.4.8_A4_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.8")]
        public void WhenStringPrototypeLastindexofSearchstringPositionIsCalledFirstCallTostringGivingItTheThisValueAsItsArgumentThenCallTostringSearchstringAndCallTonumberPosition2()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.8/S15.5.4.8_A4_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.8")]
        public void WhenStringPrototypeLastindexofSearchstringPositionIsCalledFirstCallTostringGivingItTheThisValueAsItsArgumentThenCallTostringSearchstringAndCallTonumberPosition3()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.8/S15.5.4.8_A4_T3.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.8")]
        public void WhenStringPrototypeLastindexofSearchstringPositionIsCalledFirstCallTostringGivingItTheThisValueAsItsArgumentThenCallTostringSearchstringAndCallTonumberPosition4()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.8/S15.5.4.8_A4_T4.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.8")]
        public void WhenStringPrototypeLastindexofSearchstringPositionIsCalledFirstCallTostringGivingItTheThisValueAsItsArgumentThenCallTostringSearchstringAndCallTonumberPosition5()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.8/S15.5.4.8_A4_T5.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.8")]
        public void StringPrototypeLastindexofHasNotPrototypeProperty()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.8/S15.5.4.8_A6.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.8")]
        public void StringPrototypeLastindexofCanTBeUsedAsConstructor()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.8/S15.5.4.8_A7.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.8")]
        public void TheStringPrototypeLastindexofLengthPropertyHasTheAttributeDontenum()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.8/S15.5.4.8_A8.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.8")]
        public void TheStringPrototypeLastindexofLengthPropertyHasTheAttributeDontdelete()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.8/S15.5.4.8_A9.js", false);
        }


    }
}
