using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_15_5_4_10 : EcmaTest
    {
        [Fact]
        [Trait("Category", "15.5.4.10")]
        public void TheStringPrototypeMatchLengthPropertyHasTheAttributeReadonly()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.10/S15.5.4.10_A10.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.10")]
        public void TheLengthPropertyOfTheMatchMethodIs1()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.10/S15.5.4.10_A11.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.10")]
        public void StringPrototypeMatchRegexp()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.10/S15.5.4.10_A1_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.10")]
        public void StringPrototypeMatchRegexp2()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.10/S15.5.4.10_A1_T10.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.10")]
        public void StringPrototypeMatchRegexp3()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.10/S15.5.4.10_A1_T11.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.10")]
        public void StringPrototypeMatchRegexp4()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.10/S15.5.4.10_A1_T12.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.10")]
        public void StringPrototypeMatchRegexp5()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.10/S15.5.4.10_A1_T13.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.10")]
        public void StringPrototypeMatchRegexp6()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.10/S15.5.4.10_A1_T14.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.10")]
        public void StringPrototypeMatchRegexp7()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.10/S15.5.4.10_A1_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.10")]
        public void StringPrototypeMatchRegexp8()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.10/S15.5.4.10_A1_T3.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.10")]
        public void StringPrototypeMatchRegexp9()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.10/S15.5.4.10_A1_T4.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.10")]
        public void StringPrototypeMatchRegexp10()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.10/S15.5.4.10_A1_T5.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.10")]
        public void StringPrototypeMatchRegexp11()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.10/S15.5.4.10_A1_T6.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.10")]
        public void StringPrototypeMatchRegexp12()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.10/S15.5.4.10_A1_T7.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.10")]
        public void StringPrototypeMatchRegexp13()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.10/S15.5.4.10_A1_T8.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.10")]
        public void StringPrototypeMatchRegexp14()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.10/S15.5.4.10_A1_T9.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.10")]
        public void MatchReturnsArrayAsSpecifiedIn151062()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.10/S15.5.4.10_A2_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.10")]
        public void MatchReturnsArrayAsSpecifiedIn1510622()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.10/S15.5.4.10_A2_T10.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.10")]
        public void MatchReturnsArrayAsSpecifiedIn1510623()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.10/S15.5.4.10_A2_T11.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.10")]
        public void MatchReturnsArrayAsSpecifiedIn1510624()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.10/S15.5.4.10_A2_T12.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.10")]
        public void MatchReturnsArrayAsSpecifiedIn1510625()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.10/S15.5.4.10_A2_T13.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.10")]
        public void MatchReturnsArrayAsSpecifiedIn1510626()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.10/S15.5.4.10_A2_T14.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.10")]
        public void MatchReturnsArrayAsSpecifiedIn1510627()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.10/S15.5.4.10_A2_T15.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.10")]
        public void MatchReturnsArrayAsSpecifiedIn1510628()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.10/S15.5.4.10_A2_T16.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.10")]
        public void MatchReturnsArrayAsSpecifiedIn1510629()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.10/S15.5.4.10_A2_T17.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.10")]
        public void MatchReturnsArrayAsSpecifiedIn15106210()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.10/S15.5.4.10_A2_T18.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.10")]
        public void MatchReturnsArrayAsSpecifiedIn15106211()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.10/S15.5.4.10_A2_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.10")]
        public void MatchReturnsArrayAsSpecifiedIn15106212()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.10/S15.5.4.10_A2_T3.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.10")]
        public void MatchReturnsArrayAsSpecifiedIn15106213()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.10/S15.5.4.10_A2_T4.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.10")]
        public void MatchReturnsArrayAsSpecifiedIn15106214()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.10/S15.5.4.10_A2_T5.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.10")]
        public void MatchReturnsArrayAsSpecifiedIn15106215()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.10/S15.5.4.10_A2_T6.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.10")]
        public void MatchReturnsArrayAsSpecifiedIn15106216()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.10/S15.5.4.10_A2_T7.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.10")]
        public void MatchReturnsArrayAsSpecifiedIn15106217()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.10/S15.5.4.10_A2_T8.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.10")]
        public void MatchReturnsArrayAsSpecifiedIn15106218()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.10/S15.5.4.10_A2_T9.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.10")]
        public void StringPrototypeMatchHasNotPrototypeProperty()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.10/S15.5.4.10_A6.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.10")]
        public void StringPrototypeMatchCanTBeUsedAsConstructor()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.10/S15.5.4.10_A7.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.10")]
        public void TheStringPrototypeMatchLengthPropertyHasTheAttributeDontenum()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.10/S15.5.4.10_A8.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.10")]
        public void TheStringPrototypeMatchLengthPropertyHasTheAttributeDontdelete()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.10/S15.5.4.10_A9.js", false);
        }


    }
}
