using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_15_5_4_19 : EcmaTest
    {
        [Fact]
        [Trait("Category", "15.5.4.19")]
        public void TheStringPrototypeTolocaleuppercaseLengthPropertyHasTheAttributeReadonly()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.19/S15.5.4.19_A10.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.19")]
        public void TheLengthPropertyOfTheTolocaleuppercaseMethodIs0()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.19/S15.5.4.19_A11.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.19")]
        public void StringPrototypeTolocaleuppercase()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.19/S15.5.4.19_A1_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.19")]
        public void StringPrototypeTolocaleuppercase2()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.19/S15.5.4.19_A1_T10.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.19")]
        public void StringPrototypeTolocaleuppercase3()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.19/S15.5.4.19_A1_T11.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.19")]
        public void StringPrototypeTolocaleuppercase4()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.19/S15.5.4.19_A1_T12.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.19")]
        public void StringPrototypeTolocaleuppercase5()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.19/S15.5.4.19_A1_T13.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.19")]
        public void StringPrototypeTolocaleuppercase6()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.19/S15.5.4.19_A1_T14.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.19")]
        public void StringPrototypeTolocaleuppercase7()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.19/S15.5.4.19_A1_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.19")]
        public void StringPrototypeTolocaleuppercase8()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.19/S15.5.4.19_A1_T3.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.19")]
        public void StringPrototypeTolocaleuppercase9()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.19/S15.5.4.19_A1_T4.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.19")]
        public void StringPrototypeTolocaleuppercase10()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.19/S15.5.4.19_A1_T5.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.19")]
        public void StringPrototypeTolocaleuppercase11()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.19/S15.5.4.19_A1_T6.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.19")]
        public void StringPrototypeTolocaleuppercase12()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.19/S15.5.4.19_A1_T7.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.19")]
        public void StringPrototypeTolocaleuppercase13()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.19/S15.5.4.19_A1_T8.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.19")]
        public void StringPrototypeTolocaleuppercase14()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.19/S15.5.4.19_A1_T9.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.19")]
        public void StringPrototypeTolocaleuppercaseReturnAStringButNotAStringObject()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.19/S15.5.4.19_A2_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.19")]
        public void StringPrototypeTolocaleuppercaseHasNotPrototypeProperty()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.19/S15.5.4.19_A6.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.19")]
        public void StringPrototypeTolocaleuppercaseCanTBeUsedAsConstructor()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.19/S15.5.4.19_A7.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.19")]
        public void TheStringPrototypeTolocaleuppercaseLengthPropertyHasTheAttributeDontenum()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.19/S15.5.4.19_A8.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.19")]
        public void TheStringPrototypeTolocaleuppercaseLengthPropertyHasTheAttributeDontdelete()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.19/S15.5.4.19_A9.js", false);
        }


    }
}
