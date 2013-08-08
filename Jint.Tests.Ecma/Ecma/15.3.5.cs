using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_15_3_5 : EcmaTest
    {
        [Fact]
        [Trait("Category", "15.3.5")]
        public void StrictmodeErrorIsThrownWhenReadingTheCallerPropertyOfAFunctionObject()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.5/15.3.5-1gs.js", true);
        }

        [Fact]
        [Trait("Category", "15.3.5")]
        public void StrictmodeErrorIsThrownWhenReadingTheCallerPropertyOfAFunctionObject2()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.5/15.3.5-2gs.js", true);
        }

        [Fact]
        [Trait("Category", "15.3.5")]
        public void TheValueOfTheClassPropertyIsFunction()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.5/S15.3.5_A1_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.3.5")]
        public void TheValueOfTheClassPropertyIsFunction2()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.5/S15.3.5_A1_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.3.5")]
        public void EveryFunctionInstanceHasACallProperty()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.5/S15.3.5_A2_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.3.5")]
        public void EveryFunctionInstanceHasACallProperty2()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.5/S15.3.5_A2_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.3.5")]
        public void EveryFunctionInstanceHasAConstructProperty()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.5/S15.3.5_A3_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.3.5")]
        public void EveryFunctionInstanceHasAConstructProperty2()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.5/S15.3.5_A3_T2.js", false);
        }


    }
}
