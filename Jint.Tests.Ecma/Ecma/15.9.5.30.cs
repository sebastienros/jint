using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_15_9_5_30 : EcmaTest
    {
        [Fact]
        [Trait("Category", "15.9.5.30")]
        public void TheDatePrototypePropertySetsecondsHasDontenumAttributes()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.30/S15.9.5.30_A1_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5.30")]
        public void TheDatePrototypePropertySetsecondsHasDontenumAttributes2()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.30/S15.9.5.30_A1_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5.30")]
        public void TheDatePrototypePropertySetsecondsHasDontenumAttributes3()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.30/S15.9.5.30_A1_T3.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5.30")]
        public void TheLengthPropertyOfTheSetsecondsIs2()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.30/S15.9.5.30_A2_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5.30")]
        public void TheDatePrototypeSetsecondsPropertyLengthHasReadonlyDontdeleteDontenumAttributes()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.30/S15.9.5.30_A3_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5.30")]
        public void TheDatePrototypeSetsecondsPropertyLengthHasReadonlyDontdeleteDontenumAttributes2()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.30/S15.9.5.30_A3_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5.30")]
        public void TheDatePrototypeSetsecondsPropertyLengthHasReadonlyDontdeleteDontenumAttributes3()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.30/S15.9.5.30_A3_T3.js", false);
        }


    }
}
