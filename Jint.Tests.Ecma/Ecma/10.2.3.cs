using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_10_2_3 : EcmaTest
    {
        [Fact]
        [Trait("Category", "10.2.3")]
        public void GlobalObjectHasPropertiesSuchAsBuiltInObjectsSuchAsMathStringDateParseintEtc()
        {
			RunTest(@"TestCases/ch10/10.2/10.2.3/S10.2.3_A1.1_T1.js", false);
        }

        [Fact]
        [Trait("Category", "10.2.3")]
        public void GlobalObjectHasPropertiesSuchAsBuiltInObjectsSuchAsMathStringDateParseintEtc2()
        {
			RunTest(@"TestCases/ch10/10.2/10.2.3/S10.2.3_A1.1_T2.js", false);
        }

        [Fact]
        [Trait("Category", "10.2.3")]
        public void GlobalObjectHasPropertiesSuchAsBuiltInObjectsSuchAsMathStringDateParseintEtc3()
        {
			RunTest(@"TestCases/ch10/10.2/10.2.3/S10.2.3_A1.1_T3.js", false);
        }

        [Fact]
        [Trait("Category", "10.2.3")]
        public void GlobalObjectHasPropertiesSuchAsBuiltInObjectsSuchAsMathStringDateParseintEtc4()
        {
			RunTest(@"TestCases/ch10/10.2/10.2.3/S10.2.3_A1.1_T4.js", false);
        }

        [Fact]
        [Trait("Category", "10.2.3")]
        public void GlobalObjectHasPropertiesSuchAsBuiltInObjectsSuchAsMathStringDateParseintEtc5()
        {
			RunTest(@"TestCases/ch10/10.2/10.2.3/S10.2.3_A1.2_T1.js", false);
        }

        [Fact]
        [Trait("Category", "10.2.3")]
        public void GlobalObjectHasPropertiesSuchAsBuiltInObjectsSuchAsMathStringDateParseintEtc6()
        {
			RunTest(@"TestCases/ch10/10.2/10.2.3/S10.2.3_A1.2_T2.js", false);
        }

        [Fact]
        [Trait("Category", "10.2.3")]
        public void GlobalObjectHasPropertiesSuchAsBuiltInObjectsSuchAsMathStringDateParseintEtc7()
        {
			RunTest(@"TestCases/ch10/10.2/10.2.3/S10.2.3_A1.2_T3.js", false);
        }

        [Fact]
        [Trait("Category", "10.2.3")]
        public void GlobalObjectHasPropertiesSuchAsBuiltInObjectsSuchAsMathStringDateParseintEtc8()
        {
			RunTest(@"TestCases/ch10/10.2/10.2.3/S10.2.3_A1.2_T4.js", false);
        }

        [Fact]
        [Trait("Category", "10.2.3")]
        public void GlobalObjectHasPropertiesSuchAsBuiltInObjectsSuchAsMathStringDateParseintEtc9()
        {
			RunTest(@"TestCases/ch10/10.2/10.2.3/S10.2.3_A1.3_T1.js", false);
        }

        [Fact]
        [Trait("Category", "10.2.3")]
        public void GlobalObjectHasPropertiesSuchAsBuiltInObjectsSuchAsMathStringDateParseintEtc10()
        {
			RunTest(@"TestCases/ch10/10.2/10.2.3/S10.2.3_A1.3_T2.js", false);
        }

        [Fact]
        [Trait("Category", "10.2.3")]
        public void GlobalObjectHasPropertiesSuchAsBuiltInObjectsSuchAsMathStringDateParseintEtc11()
        {
			RunTest(@"TestCases/ch10/10.2/10.2.3/S10.2.3_A1.3_T3.js", false);
        }

        [Fact]
        [Trait("Category", "10.2.3")]
        public void GlobalObjectHasPropertiesSuchAsBuiltInObjectsSuchAsMathStringDateParseintEtc12()
        {
			RunTest(@"TestCases/ch10/10.2/10.2.3/S10.2.3_A1.3_T4.js", false);
        }

        [Fact]
        [Trait("Category", "10.2.3")]
        public void GlobalObjectPropertiesHaveAttributesDontenum()
        {
			RunTest(@"TestCases/ch10/10.2/10.2.3/S10.2.3_A2.1_T1.js", false);
        }

        [Fact]
        [Trait("Category", "10.2.3")]
        public void GlobalObjectPropertiesHaveAttributesDontenum2()
        {
			RunTest(@"TestCases/ch10/10.2/10.2.3/S10.2.3_A2.1_T2.js", false);
        }

        [Fact]
        [Trait("Category", "10.2.3")]
        public void GlobalObjectPropertiesHaveAttributesDontenum3()
        {
			RunTest(@"TestCases/ch10/10.2/10.2.3/S10.2.3_A2.1_T3.js", false);
        }

        [Fact]
        [Trait("Category", "10.2.3")]
        public void GlobalObjectPropertiesHaveAttributesDontenum4()
        {
			RunTest(@"TestCases/ch10/10.2/10.2.3/S10.2.3_A2.1_T4.js", false);
        }

        [Fact]
        [Trait("Category", "10.2.3")]
        public void GlobalObjectPropertiesHaveAttributesDontenum5()
        {
			RunTest(@"TestCases/ch10/10.2/10.2.3/S10.2.3_A2.2_T1.js", false);
        }

        [Fact]
        [Trait("Category", "10.2.3")]
        public void GlobalObjectPropertiesHaveAttributesDontenum6()
        {
			RunTest(@"TestCases/ch10/10.2/10.2.3/S10.2.3_A2.2_T2.js", false);
        }

        [Fact]
        [Trait("Category", "10.2.3")]
        public void GlobalObjectPropertiesHaveAttributesDontenum7()
        {
			RunTest(@"TestCases/ch10/10.2/10.2.3/S10.2.3_A2.2_T3.js", false);
        }

        [Fact]
        [Trait("Category", "10.2.3")]
        public void GlobalObjectPropertiesHaveAttributesDontenum8()
        {
			RunTest(@"TestCases/ch10/10.2/10.2.3/S10.2.3_A2.2_T4.js", false);
        }

        [Fact]
        [Trait("Category", "10.2.3")]
        public void GlobalObjectPropertiesHaveAttributesDontenum9()
        {
			RunTest(@"TestCases/ch10/10.2/10.2.3/S10.2.3_A2.3_T1.js", false);
        }

        [Fact]
        [Trait("Category", "10.2.3")]
        public void GlobalObjectPropertiesHaveAttributesDontenum10()
        {
			RunTest(@"TestCases/ch10/10.2/10.2.3/S10.2.3_A2.3_T2.js", false);
        }

        [Fact]
        [Trait("Category", "10.2.3")]
        public void GlobalObjectPropertiesHaveAttributesDontenum11()
        {
			RunTest(@"TestCases/ch10/10.2/10.2.3/S10.2.3_A2.3_T3.js", false);
        }

        [Fact]
        [Trait("Category", "10.2.3")]
        public void GlobalObjectPropertiesHaveAttributesDontenum12()
        {
			RunTest(@"TestCases/ch10/10.2/10.2.3/S10.2.3_A2.3_T4.js", false);
        }


    }
}
