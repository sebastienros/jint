using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_15_9_5 : EcmaTest
    {
        [Fact]
        [Trait("Category", "15.9.5")]
        public void TheDatePrototypeHasThePropertyConstructor()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/S15.9.5_A01_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5")]
        public void TheDatePrototypeHasThePropertyTostring()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/S15.9.5_A02_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5")]
        public void TheDatePrototypeHasThePropertyTodatestring()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/S15.9.5_A03_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5")]
        public void TheDatePrototypeHasThePropertyTotimestring()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/S15.9.5_A04_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5")]
        public void TheDatePrototypeHasThePropertyTolocalestring()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/S15.9.5_A05_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5")]
        public void TheDatePrototypeHasThePropertyTolocaledatestring()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/S15.9.5_A06_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5")]
        public void TheDatePrototypeHasThePropertyTolocaletimestring()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/S15.9.5_A07_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5")]
        public void TheDatePrototypeHasThePropertyValueof()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/S15.9.5_A08_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5")]
        public void TheDatePrototypeHasThePropertyGettime()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/S15.9.5_A09_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5")]
        public void TheDatePrototypeHasThePropertyGetfullyear()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/S15.9.5_A10_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5")]
        public void TheDatePrototypeHasThePropertyGetutcfullyear()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/S15.9.5_A11_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5")]
        public void TheDatePrototypeHasThePropertyGetmonth()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/S15.9.5_A12_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5")]
        public void TheDatePrototypeHasThePropertyGetutcmonth()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/S15.9.5_A13_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5")]
        public void TheDatePrototypeHasThePropertyGetdate()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/S15.9.5_A14_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5")]
        public void TheDatePrototypeHasThePropertyGetutcdate()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/S15.9.5_A15_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5")]
        public void TheDatePrototypeHasThePropertyGetday()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/S15.9.5_A16_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5")]
        public void TheDatePrototypeHasThePropertyGetutcday()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/S15.9.5_A17_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5")]
        public void TheDatePrototypeHasThePropertyGethours()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/S15.9.5_A18_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5")]
        public void TheDatePrototypeHasThePropertyGetutchours()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/S15.9.5_A19_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5")]
        public void TheDatePrototypeHasThePropertyGetminutes()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/S15.9.5_A20_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5")]
        public void TheDatePrototypeHasThePropertyGetutcminutes()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/S15.9.5_A21_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5")]
        public void TheDatePrototypeHasThePropertyGetseconds()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/S15.9.5_A22_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5")]
        public void TheDatePrototypeHasThePropertyGetutcseconds()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/S15.9.5_A23_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5")]
        public void TheDatePrototypeHasThePropertyGetmilliseconds()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/S15.9.5_A24_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5")]
        public void TheDatePrototypeHasThePropertyGetutcmilliseconds()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/S15.9.5_A25_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5")]
        public void TheDatePrototypeHasThePropertyGettimezoneoffset()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/S15.9.5_A26_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5")]
        public void TheDatePrototypeHasThePropertySettime()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/S15.9.5_A27_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5")]
        public void TheDatePrototypeHasThePropertySetmilliseconds()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/S15.9.5_A28_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5")]
        public void TheDatePrototypeHasThePropertySetutcmilliseconds()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/S15.9.5_A29_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5")]
        public void TheDatePrototypeHasThePropertySetseconds()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/S15.9.5_A30_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5")]
        public void TheDatePrototypeHasThePropertySetutcseconds()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/S15.9.5_A31_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5")]
        public void TheDatePrototypeHasThePropertySetminutes()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/S15.9.5_A32_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5")]
        public void TheDatePrototypeHasThePropertySetutcminutes()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/S15.9.5_A33_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5")]
        public void TheDatePrototypeHasThePropertySethours()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/S15.9.5_A34_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5")]
        public void TheDatePrototypeHasThePropertySetutchours()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/S15.9.5_A35_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5")]
        public void TheDatePrototypeHasThePropertySetdate()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/S15.9.5_A36_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5")]
        public void TheDatePrototypeHasThePropertySetutcdate()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/S15.9.5_A37_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5")]
        public void TheDatePrototypeHasThePropertySetmonth()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/S15.9.5_A38_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5")]
        public void TheDatePrototypeHasThePropertySetutcmonth()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/S15.9.5_A39_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5")]
        public void TheDatePrototypeHasThePropertySetfullyear()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/S15.9.5_A40_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5")]
        public void TheDatePrototypeHasThePropertySetutcfullyear()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/S15.9.5_A41_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5")]
        public void TheDatePrototypeHasThePropertyToutcstring()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/S15.9.5_A42_T1.js", false);
        }


    }
}
