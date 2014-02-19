using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_15_9_5_5 : EcmaTest
    {
        [Fact]
        [Trait("Category", "15.9.5.5")]
        public void TheDatePrototypePropertyTolocalestringHasDontenumAttributes()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.5/S15.9.5.5_A1_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5.5")]
        public void TheDatePrototypePropertyTolocalestringHasDontenumAttributes2()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.5/S15.9.5.5_A1_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5.5")]
        public void TheDatePrototypePropertyTolocalestringHasDontenumAttributes3()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.5/S15.9.5.5_A1_T3.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5.5")]
        public void TheLengthPropertyOfTheTolocalestringIs0()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.5/S15.9.5.5_A2_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5.5")]
        public void TheDatePrototypeTolocalestringPropertyLengthHasReadonlyDontdeleteDontenumAttributes()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.5/S15.9.5.5_A3_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5.5")]
        public void TheDatePrototypeTolocalestringPropertyLengthHasReadonlyDontdeleteDontenumAttributes2()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.5/S15.9.5.5_A3_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5.5")]
        public void TheDatePrototypeTolocalestringPropertyLengthHasReadonlyDontdeleteDontenumAttributes3()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.5/S15.9.5.5_A3_T3.js", false);
        }


    }
}
