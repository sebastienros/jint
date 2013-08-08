using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_15_9_5_2 : EcmaTest
    {
        [Fact]
        [Trait("Category", "15.9.5.2")]
        public void TheDatePrototypePropertyTostringHasDontenumAttributes()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.2/S15.9.5.2_A1_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5.2")]
        public void TheDatePrototypePropertyTostringHasDontenumAttributes2()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.2/S15.9.5.2_A1_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5.2")]
        public void TheDatePrototypePropertyTostringHasDontenumAttributes3()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.2/S15.9.5.2_A1_T3.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5.2")]
        public void TheLengthPropertyOfTheTostringIs0()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.2/S15.9.5.2_A2_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5.2")]
        public void TheDatePrototypeTostringPropertyLengthHasReadonlyDontdeleteDontenumAttributes()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.2/S15.9.5.2_A3_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5.2")]
        public void TheDatePrototypeTostringPropertyLengthHasReadonlyDontdeleteDontenumAttributes2()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.2/S15.9.5.2_A3_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5.2")]
        public void TheDatePrototypeTostringPropertyLengthHasReadonlyDontdeleteDontenumAttributes3()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.2/S15.9.5.2_A3_T3.js", false);
        }


    }
}
