using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_15_9_5_7 : EcmaTest
    {
        [Fact]
        [Trait("Category", "15.9.5.7")]
        public void TheDatePrototypePropertyTolocaletimestringHasDontenumAttributes()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.7/S15.9.5.7_A1_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5.7")]
        public void TheDatePrototypePropertyTolocaletimestringHasDontenumAttributes2()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.7/S15.9.5.7_A1_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5.7")]
        public void TheDatePrototypePropertyTolocaletimestringHasDontenumAttributes3()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.7/S15.9.5.7_A1_T3.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5.7")]
        public void TheLengthPropertyOfTheTolocaletimestringIs0()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.7/S15.9.5.7_A2_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5.7")]
        public void TheDatePrototypeTolocaletimestringPropertyLengthHasReadonlyDontdeleteDontenumAttributes()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.7/S15.9.5.7_A3_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5.7")]
        public void TheDatePrototypeTolocaletimestringPropertyLengthHasReadonlyDontdeleteDontenumAttributes2()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.7/S15.9.5.7_A3_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5.7")]
        public void TheDatePrototypeTolocaletimestringPropertyLengthHasReadonlyDontdeleteDontenumAttributes3()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.7/S15.9.5.7_A3_T3.js", false);
        }


    }
}
