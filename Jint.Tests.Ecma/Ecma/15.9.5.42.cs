using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_15_9_5_42 : EcmaTest
    {
        [Fact]
        [Trait("Category", "15.9.5.42")]
        public void TheDatePrototypePropertyToutcstringHasDontenumAttributes()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.42/S15.9.5.42_A1_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5.42")]
        public void TheDatePrototypePropertyToutcstringHasDontenumAttributes2()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.42/S15.9.5.42_A1_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5.42")]
        public void TheDatePrototypePropertyToutcstringHasDontenumAttributes3()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.42/S15.9.5.42_A1_T3.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5.42")]
        public void TheLengthPropertyOfTheToutcstringIs0()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.42/S15.9.5.42_A2_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5.42")]
        public void TheDatePrototypeToutcstringPropertyLengthHasReadonlyDontdeleteDontenumAttributes()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.42/S15.9.5.42_A3_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5.42")]
        public void TheDatePrototypeToutcstringPropertyLengthHasReadonlyDontdeleteDontenumAttributes2()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.42/S15.9.5.42_A3_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5.42")]
        public void TheDatePrototypeToutcstringPropertyLengthHasReadonlyDontdeleteDontenumAttributes3()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.42/S15.9.5.42_A3_T3.js", false);
        }


    }
}
