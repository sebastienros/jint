using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_15_9_5_4 : EcmaTest
    {
        [Fact]
        [Trait("Category", "15.9.5.4")]
        public void TheDatePrototypePropertyTotimestringHasDontenumAttributes()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.4/S15.9.5.4_A1_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5.4")]
        public void TheDatePrototypePropertyTotimestringHasDontenumAttributes2()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.4/S15.9.5.4_A1_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5.4")]
        public void TheDatePrototypePropertyTotimestringHasDontenumAttributes3()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.4/S15.9.5.4_A1_T3.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5.4")]
        public void TheLengthPropertyOfTheTotimestringIs0()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.4/S15.9.5.4_A2_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5.4")]
        public void TheDatePrototypeTotimestringPropertyLengthHasReadonlyDontdeleteDontenumAttributes()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.4/S15.9.5.4_A3_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5.4")]
        public void TheDatePrototypeTotimestringPropertyLengthHasReadonlyDontdeleteDontenumAttributes2()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.4/S15.9.5.4_A3_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5.4")]
        public void TheDatePrototypeTotimestringPropertyLengthHasReadonlyDontdeleteDontenumAttributes3()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.4/S15.9.5.4_A3_T3.js", false);
        }


    }
}
