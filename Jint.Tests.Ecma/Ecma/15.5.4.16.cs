using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_15_5_4_16 : EcmaTest
    {
        [Fact]
        [Trait("Category", "15.5.4.16")]
        public void TheStringPrototypeTolowercaseLengthPropertyHasTheAttributeReadonly()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.16/S15.5.4.16_A10.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.16")]
        public void TheLengthPropertyOfTheTolowercaseMethodIs0()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.16/S15.5.4.16_A11.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.16")]
        public void StringPrototypeTolowercase()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.16/S15.5.4.16_A1_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.16")]
        public void StringPrototypeTolowercase2()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.16/S15.5.4.16_A1_T10.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.16")]
        public void StringPrototypeTolowercase3()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.16/S15.5.4.16_A1_T11.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.16")]
        public void StringPrototypeTolowercase4()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.16/S15.5.4.16_A1_T12.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.16")]
        public void StringPrototypeTolowercase5()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.16/S15.5.4.16_A1_T13.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.16")]
        public void StringPrototypeTolowercase6()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.16/S15.5.4.16_A1_T14.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.16")]
        public void StringPrototypeTolowercase7()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.16/S15.5.4.16_A1_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.16")]
        public void StringPrototypeTolowercase8()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.16/S15.5.4.16_A1_T3.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.16")]
        public void StringPrototypeTolowercase9()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.16/S15.5.4.16_A1_T4.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.16")]
        public void StringPrototypeTolowercase10()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.16/S15.5.4.16_A1_T5.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.16")]
        public void StringPrototypeTolowercase11()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.16/S15.5.4.16_A1_T6.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.16")]
        public void StringPrototypeTolowercase12()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.16/S15.5.4.16_A1_T7.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.16")]
        public void StringPrototypeTolowercase13()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.16/S15.5.4.16_A1_T8.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.16")]
        public void StringPrototypeTolowercase14()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.16/S15.5.4.16_A1_T9.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.16")]
        public void StringPrototypeTolowercaseReturnAStringButNotAStringObject()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.16/S15.5.4.16_A2_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.16")]
        public void StringPrototypeTolowercaseHasNotPrototypeProperty()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.16/S15.5.4.16_A6.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.16")]
        public void StringPrototypeTolowercaseCanTBeUsedAsConstructor()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.16/S15.5.4.16_A7.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.16")]
        public void TheStringPrototypeTolowercaseLengthPropertyHasTheAttributeDontenum()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.16/S15.5.4.16_A8.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.16")]
        public void TheStringPrototypeTolowercaseLengthPropertyHasTheAttributeDontdelete()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.16/S15.5.4.16_A9.js", false);
        }


    }
}
