using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_15_9_5_16 : EcmaTest
    {
        [Fact]
        [Trait("Category", "15.9.5.16")]
        public void TheDatePrototypePropertyGetdayHasDontenumAttributes()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.16/S15.9.5.16_A1_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5.16")]
        public void TheDatePrototypePropertyGetdayHasDontenumAttributes2()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.16/S15.9.5.16_A1_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5.16")]
        public void TheDatePrototypePropertyGetdayHasDontenumAttributes3()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.16/S15.9.5.16_A1_T3.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5.16")]
        public void TheLengthPropertyOfTheGetdayIs0()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.16/S15.9.5.16_A2_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5.16")]
        public void TheDatePrototypeGetdayPropertyLengthHasReadonlyDontdeleteDontenumAttributes()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.16/S15.9.5.16_A3_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5.16")]
        public void TheDatePrototypeGetdayPropertyLengthHasReadonlyDontdeleteDontenumAttributes2()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.16/S15.9.5.16_A3_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5.16")]
        public void TheDatePrototypeGetdayPropertyLengthHasReadonlyDontdeleteDontenumAttributes3()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.16/S15.9.5.16_A3_T3.js", false);
        }


    }
}
