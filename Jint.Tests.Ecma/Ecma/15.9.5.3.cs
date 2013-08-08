using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_15_9_5_3 : EcmaTest
    {
        [Fact]
        [Trait("Category", "15.9.5.3")]
        public void TheDatePrototypePropertyTodatestringHasDontenumAttributes()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.3/S15.9.5.3_A1_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5.3")]
        public void TheDatePrototypePropertyTodatestringHasDontenumAttributes2()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.3/S15.9.5.3_A1_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5.3")]
        public void TheDatePrototypePropertyTodatestringHasDontenumAttributes3()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.3/S15.9.5.3_A1_T3.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5.3")]
        public void TheLengthPropertyOfTheTodatestringIs0()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.3/S15.9.5.3_A2_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5.3")]
        public void TheDatePrototypeTodatestringPropertyLengthHasReadonlyDontdeleteDontenumAttributes()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.3/S15.9.5.3_A3_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5.3")]
        public void TheDatePrototypeTodatestringPropertyLengthHasReadonlyDontdeleteDontenumAttributes2()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.3/S15.9.5.3_A3_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5.3")]
        public void TheDatePrototypeTodatestringPropertyLengthHasReadonlyDontdeleteDontenumAttributes3()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.3/S15.9.5.3_A3_T3.js", false);
        }


    }
}
