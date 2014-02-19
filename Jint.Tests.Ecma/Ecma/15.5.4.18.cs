using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_15_5_4_18 : EcmaTest
    {
        [Fact]
        [Trait("Category", "15.5.4.18")]
        public void TheStringPrototypeTouppercaseLengthPropertyHasTheAttributeReadonly()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.18/S15.5.4.18_A10.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.18")]
        public void TheLengthPropertyOfTheTouppercaseMethodIs0()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.18/S15.5.4.18_A11.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.18")]
        public void StringPrototypeTouppercase()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.18/S15.5.4.18_A1_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.18")]
        public void StringPrototypeTouppercase2()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.18/S15.5.4.18_A1_T10.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.18")]
        public void StringPrototypeTouppercase3()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.18/S15.5.4.18_A1_T11.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.18")]
        public void StringPrototypeTouppercase4()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.18/S15.5.4.18_A1_T12.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.18")]
        public void StringPrototypeTouppercase5()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.18/S15.5.4.18_A1_T13.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.18")]
        public void StringPrototypeTouppercase6()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.18/S15.5.4.18_A1_T14.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.18")]
        public void StringPrototypeTouppercase7()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.18/S15.5.4.18_A1_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.18")]
        public void StringPrototypeTouppercase8()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.18/S15.5.4.18_A1_T3.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.18")]
        public void StringPrototypeTouppercase9()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.18/S15.5.4.18_A1_T4.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.18")]
        public void StringPrototypeTouppercase10()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.18/S15.5.4.18_A1_T5.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.18")]
        public void StringPrototypeTouppercase11()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.18/S15.5.4.18_A1_T6.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.18")]
        public void StringPrototypeTouppercase12()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.18/S15.5.4.18_A1_T7.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.18")]
        public void StringPrototypeTouppercase13()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.18/S15.5.4.18_A1_T8.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.18")]
        public void StringPrototypeTouppercase14()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.18/S15.5.4.18_A1_T9.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.18")]
        public void StringPrototypeTouppercaseReturnAStringButNotAStringObject()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.18/S15.5.4.18_A2_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.18")]
        public void StringPrototypeTouppercaseHasNotPrototypeProperty()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.18/S15.5.4.18_A6.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.18")]
        public void StringPrototypeTouppercaseCanTBeUsedAsConstructor()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.18/S15.5.4.18_A7.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.18")]
        public void TheStringPrototypeTouppercaseLengthPropertyHasTheAttributeDontenum()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.18/S15.5.4.18_A8.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.18")]
        public void TheStringPrototypeTouppercaseLengthPropertyHasTheAttributeDontdelete()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.18/S15.5.4.18_A9.js", false);
        }


    }
}
