using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_15_9_5_37 : EcmaTest
    {
        [Fact]
        [Trait("Category", "15.9.5.37")]
        public void TheDatePrototypePropertySetutcdateHasDontenumAttributes()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.37/S15.9.5.37_A1_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5.37")]
        public void TheDatePrototypePropertySetutcdateHasDontenumAttributes2()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.37/S15.9.5.37_A1_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5.37")]
        public void TheDatePrototypePropertySetutcdateHasDontenumAttributes3()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.37/S15.9.5.37_A1_T3.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5.37")]
        public void TheLengthPropertyOfTheSetutcdateIs1()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.37/S15.9.5.37_A2_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5.37")]
        public void TheDatePrototypeSetutcdatePropertyLengthHasReadonlyDontdeleteDontenumAttributes()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.37/S15.9.5.37_A3_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5.37")]
        public void TheDatePrototypeSetutcdatePropertyLengthHasReadonlyDontdeleteDontenumAttributes2()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.37/S15.9.5.37_A3_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5.37")]
        public void TheDatePrototypeSetutcdatePropertyLengthHasReadonlyDontdeleteDontenumAttributes3()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.37/S15.9.5.37_A3_T3.js", false);
        }


    }
}
