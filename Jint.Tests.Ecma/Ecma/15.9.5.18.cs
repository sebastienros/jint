using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_15_9_5_18 : EcmaTest
    {
        [Fact]
        [Trait("Category", "15.9.5.18")]
        public void TheDatePrototypePropertyGethoursHasDontenumAttributes()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.18/S15.9.5.18_A1_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5.18")]
        public void TheDatePrototypePropertyGethoursHasDontenumAttributes2()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.18/S15.9.5.18_A1_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5.18")]
        public void TheDatePrototypePropertyGethoursHasDontenumAttributes3()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.18/S15.9.5.18_A1_T3.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5.18")]
        public void TheLengthPropertyOfTheGethoursIs0()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.18/S15.9.5.18_A2_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5.18")]
        public void TheDatePrototypeGethoursPropertyLengthHasReadonlyDontdeleteDontenumAttributes()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.18/S15.9.5.18_A3_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5.18")]
        public void TheDatePrototypeGethoursPropertyLengthHasReadonlyDontdeleteDontenumAttributes2()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.18/S15.9.5.18_A3_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5.18")]
        public void TheDatePrototypeGethoursPropertyLengthHasReadonlyDontdeleteDontenumAttributes3()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.18/S15.9.5.18_A3_T3.js", false);
        }


    }
}
