using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_15_9_4_3 : EcmaTest
    {
        [Fact]
        [Trait("Category", "15.9.4.3")]
        public void TheDatePropertyUtcHasDontenumAttributes()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.4/15.9.4.3/S15.9.4.3_A1_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.4.3")]
        public void TheDatePropertyUtcHasDontenumAttributes2()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.4/15.9.4.3/S15.9.4.3_A1_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.4.3")]
        public void TheDatePropertyUtcHasDontenumAttributes3()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.4/15.9.4.3/S15.9.4.3_A1_T3.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.4.3")]
        public void TheLengthPropertyOfTheUtcIs7()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.4/15.9.4.3/S15.9.4.3_A2_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.4.3")]
        public void TheDateUtcPropertyLengthHasReadonlyDontdeleteDontenumAttributes()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.4/15.9.4.3/S15.9.4.3_A3_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.4.3")]
        public void TheDateUtcPropertyLengthHasReadonlyDontdeleteDontenumAttributes2()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.4/15.9.4.3/S15.9.4.3_A3_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.4.3")]
        public void TheDateUtcPropertyLengthHasReadonlyDontdeleteDontenumAttributes3()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.4/15.9.4.3/S15.9.4.3_A3_T3.js", false);
        }


    }
}
