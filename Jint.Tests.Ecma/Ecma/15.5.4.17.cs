using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_15_5_4_17 : EcmaTest
    {
        [Fact]
        [Trait("Category", "15.5.4.17")]
        public void TheStringPrototypeTolocalelowercaseLengthPropertyHasTheAttributeReadonly()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.17/S15.5.4.17_A10.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.17")]
        public void TheLengthPropertyOfTheTolocalelowercaseMethodIs0()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.17/S15.5.4.17_A11.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.17")]
        public void StringPrototypeTolocalelowercase()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.17/S15.5.4.17_A1_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.17")]
        public void StringPrototypeTolocalelowercase2()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.17/S15.5.4.17_A1_T10.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.17")]
        public void StringPrototypeTolocalelowercase3()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.17/S15.5.4.17_A1_T11.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.17")]
        public void StringPrototypeTolocalelowercase4()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.17/S15.5.4.17_A1_T12.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.17")]
        public void StringPrototypeTolocalelowercase5()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.17/S15.5.4.17_A1_T13.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.17")]
        public void StringPrototypeTolocalelowercase6()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.17/S15.5.4.17_A1_T14.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.17")]
        public void StringPrototypeTolocalelowercase7()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.17/S15.5.4.17_A1_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.17")]
        public void StringPrototypeTolocalelowercase8()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.17/S15.5.4.17_A1_T3.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.17")]
        public void StringPrototypeTolocalelowercase9()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.17/S15.5.4.17_A1_T4.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.17")]
        public void StringPrototypeTolocalelowercase10()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.17/S15.5.4.17_A1_T5.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.17")]
        public void StringPrototypeTolocalelowercase11()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.17/S15.5.4.17_A1_T6.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.17")]
        public void StringPrototypeTolocalelowercase12()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.17/S15.5.4.17_A1_T7.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.17")]
        public void StringPrototypeTolocalelowercase13()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.17/S15.5.4.17_A1_T8.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.17")]
        public void StringPrototypeTolocalelowercase14()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.17/S15.5.4.17_A1_T9.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.17")]
        public void StringPrototypeTolocalelowercaseReturnAStringButNotAStringObject()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.17/S15.5.4.17_A2_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.17")]
        public void StringPrototypeTolocalelowercaseHasNotPrototypeProperty()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.17/S15.5.4.17_A6.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.17")]
        public void StringPrototypeTolocalelowercaseCanTBeUsedAsConstructor()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.17/S15.5.4.17_A7.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.17")]
        public void TheStringPrototypeTolocalelowercaseLengthPropertyHasTheAttributeDontenum()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.17/S15.5.4.17_A8.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.17")]
        public void TheStringPrototypeTolocalelowercaseLengthPropertyHasTheAttributeDontdelete()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.17/S15.5.4.17_A9.js", false);
        }


    }
}
