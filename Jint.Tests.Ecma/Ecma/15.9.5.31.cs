using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_15_9_5_31 : EcmaTest
    {
        [Fact]
        [Trait("Category", "15.9.5.31")]
        public void TheDatePrototypePropertySetutcsecondsHasDontenumAttributes()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.31/S15.9.5.31_A1_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5.31")]
        public void TheDatePrototypePropertySetutcsecondsHasDontenumAttributes2()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.31/S15.9.5.31_A1_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5.31")]
        public void TheDatePrototypePropertySetutcsecondsHasDontenumAttributes3()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.31/S15.9.5.31_A1_T3.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5.31")]
        public void TheLengthPropertyOfTheSetutcsecondsIs2()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.31/S15.9.5.31_A2_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5.31")]
        public void TheDatePrototypeSetutcsecondsPropertyLengthHasReadonlyDontdeleteDontenumAttributes()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.31/S15.9.5.31_A3_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5.31")]
        public void TheDatePrototypeSetutcsecondsPropertyLengthHasReadonlyDontdeleteDontenumAttributes2()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.31/S15.9.5.31_A3_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5.31")]
        public void TheDatePrototypeSetutcsecondsPropertyLengthHasReadonlyDontdeleteDontenumAttributes3()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.31/S15.9.5.31_A3_T3.js", false);
        }


    }
}
