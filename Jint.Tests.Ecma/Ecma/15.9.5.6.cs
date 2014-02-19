using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_15_9_5_6 : EcmaTest
    {
        [Fact]
        [Trait("Category", "15.9.5.6")]
        public void TheDatePrototypePropertyTolocaledatestringHasDontenumAttributes()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.6/S15.9.5.6_A1_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5.6")]
        public void TheDatePrototypePropertyTolocaledatestringHasDontenumAttributes2()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.6/S15.9.5.6_A1_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5.6")]
        public void TheDatePrototypePropertyTolocaledatestringHasDontenumAttributes3()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.6/S15.9.5.6_A1_T3.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5.6")]
        public void TheLengthPropertyOfTheTolocaledatestringIs0()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.6/S15.9.5.6_A2_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5.6")]
        public void TheDatePrototypeTolocaledatestringPropertyLengthHasReadonlyDontdeleteDontenumAttributes()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.6/S15.9.5.6_A3_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5.6")]
        public void TheDatePrototypeTolocaledatestringPropertyLengthHasReadonlyDontdeleteDontenumAttributes2()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.6/S15.9.5.6_A3_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5.6")]
        public void TheDatePrototypeTolocaledatestringPropertyLengthHasReadonlyDontdeleteDontenumAttributes3()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.6/S15.9.5.6_A3_T3.js", false);
        }


    }
}
