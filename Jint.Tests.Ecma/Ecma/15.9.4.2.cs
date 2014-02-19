using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_15_9_4_2 : EcmaTest
    {
        [Fact]
        [Trait("Category", "15.9.4.2")]
        public void TheDatePropertyParseHasDontenumAttributes()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.4/15.9.4.2/S15.9.4.2_A1_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.4.2")]
        public void TheDatePropertyParseHasDontenumAttributes2()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.4/15.9.4.2/S15.9.4.2_A1_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.4.2")]
        public void TheDatePropertyParseHasDontenumAttributes3()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.4/15.9.4.2/S15.9.4.2_A1_T3.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.4.2")]
        public void TheLengthPropertyOfTheParseIs1()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.4/15.9.4.2/S15.9.4.2_A2_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.4.2")]
        public void TheDateParsePropertyLengthHasReadonlyDontdeleteDontenumAttributes()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.4/15.9.4.2/S15.9.4.2_A3_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.4.2")]
        public void TheDateParsePropertyLengthHasReadonlyDontdeleteDontenumAttributes2()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.4/15.9.4.2/S15.9.4.2_A3_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.4.2")]
        public void TheDateParsePropertyLengthHasReadonlyDontdeleteDontenumAttributes3()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.4/15.9.4.2/S15.9.4.2_A3_T3.js", false);
        }


    }
}
