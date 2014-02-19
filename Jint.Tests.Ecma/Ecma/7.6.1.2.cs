using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_7_6_1_2 : EcmaTest
    {
        [Fact]
        [Trait("Category", "7.6.1.2")]
        public void StrictModeSyntaxerrorIsThrownWhenFuturereservedwordImplementsOccursInStrictModeCode()
        {
			RunTest(@"TestCases/ch07/7.6/7.6.1/7.6.1.2-1gs.js", true);
        }

        [Fact]
        [Trait("Category", "7.6.1.2")]
        public void StrictModeSyntaxerrorIsThrownWhenFuturereservedwordImplementsOccursInStrictModeCode2()
        {
			RunTest(@"TestCases/ch07/7.6/7.6.1/7.6.1.2/7.6.1.2-1-s.js", false);
        }

        [Fact]
        [Trait("Category", "7.6.1.2")]
        public void StrictModeSyntaxerrorIsnTThrownWhenImplementsOccursInStrictModeCode()
        {
			RunTest(@"TestCases/ch07/7.6/7.6.1/7.6.1.2/7.6.1.2-10-s.js", false);
        }

        [Fact]
        [Trait("Category", "7.6.1.2")]
        public void StrictModeSyntaxerrorIsnTThrownWhenImplementsOccursInStrictModeCode2()
        {
			RunTest(@"TestCases/ch07/7.6/7.6.1/7.6.1.2/7.6.1.2-11-s.js", false);
        }

        [Fact]
        [Trait("Category", "7.6.1.2")]
        public void StrictModeSyntaxerrorIsnTThrownWhenImplementOccursInStrictModeCode()
        {
			RunTest(@"TestCases/ch07/7.6/7.6.1/7.6.1.2/7.6.1.2-12-s.js", false);
        }

        [Fact]
        [Trait("Category", "7.6.1.2")]
        public void StrictModeSyntaxerrorIsnTThrownWhenImplementssOccursInStrictModeCode()
        {
			RunTest(@"TestCases/ch07/7.6/7.6.1/7.6.1.2/7.6.1.2-13-s.js", false);
        }

        [Fact]
        [Trait("Category", "7.6.1.2")]
        public void StrictModeSyntaxerrorIsnTThrownWhenImplements0OccursInStrictModeCode()
        {
			RunTest(@"TestCases/ch07/7.6/7.6.1/7.6.1.2/7.6.1.2-14-s.js", false);
        }

        [Fact]
        [Trait("Category", "7.6.1.2")]
        public void StrictModeSyntaxerrorIsnTThrownWhenImplementsOccursInStrictModeCode3()
        {
			RunTest(@"TestCases/ch07/7.6/7.6.1/7.6.1.2/7.6.1.2-16-s.js", false);
        }

        [Fact]
        [Trait("Category", "7.6.1.2")]
        public void StrictModeSyntaxerrorIsThrownWhenFuturereservedwordLetOccursInStrictModeCode()
        {
			RunTest(@"TestCases/ch07/7.6/7.6.1/7.6.1.2/7.6.1.2-2-s.js", false);
        }

        [Fact]
        [Trait("Category", "7.6.1.2")]
        public void StrictModeSyntaxerrorIsThrownWhenFuturereservedwordPrivateOccursInStrictModeCode()
        {
			RunTest(@"TestCases/ch07/7.6/7.6.1/7.6.1.2/7.6.1.2-3-s.js", false);
        }

        [Fact]
        [Trait("Category", "7.6.1.2")]
        public void StrictModeSyntaxerrorIsThrownWhenFuturereservedwordPublicOccursInStrictModeCode()
        {
			RunTest(@"TestCases/ch07/7.6/7.6.1/7.6.1.2/7.6.1.2-4-s.js", false);
        }

        [Fact]
        [Trait("Category", "7.6.1.2")]
        public void StrictModeSyntaxerrorIsThrownWhenFuturereservedwordYieldOccursInStrictModeCode()
        {
			RunTest(@"TestCases/ch07/7.6/7.6.1/7.6.1.2/7.6.1.2-5-s.js", false);
        }

        [Fact]
        [Trait("Category", "7.6.1.2")]
        public void StrictModeSyntaxerrorIsThrownWhenFuturereservedwordInterfaceOccursInStrictModeCode()
        {
			RunTest(@"TestCases/ch07/7.6/7.6.1/7.6.1.2/7.6.1.2-6-s.js", false);
        }

        [Fact]
        [Trait("Category", "7.6.1.2")]
        public void StrictModeSyntaxerrorIsThrownWhenFuturereservedwordPackageOccursInStrictModeCode()
        {
			RunTest(@"TestCases/ch07/7.6/7.6.1/7.6.1.2/7.6.1.2-7-s.js", false);
        }

        [Fact]
        [Trait("Category", "7.6.1.2")]
        public void StrictModeSyntaxerrorIsThrownWhenFuturereservedwordProtectedOccursInStrictModeCode()
        {
			RunTest(@"TestCases/ch07/7.6/7.6.1/7.6.1.2/7.6.1.2-8-s.js", false);
        }

        [Fact]
        [Trait("Category", "7.6.1.2")]
        public void StrictModeSyntaxerrorIsThrownWhenFuturereservedwordStaticOccursInStrictModeCode()
        {
			RunTest(@"TestCases/ch07/7.6/7.6.1/7.6.1.2/7.6.1.2-9-s.js", false);
        }

        [Fact]
        [Trait("Category", "7.6.1.2")]
        public void TheAbstractTokenCanBeUsedAsIdentifier()
        {
			RunTest(@"TestCases/ch07/7.6/7.6.1/7.6.1.2/S7.6.1.2_A1.1.js", false);
        }

        [Fact]
        [Trait("Category", "7.6.1.2")]
        public void TheExportTokenCanNotBeUsedAsIdentifier()
        {
			RunTest(@"TestCases/ch07/7.6/7.6.1/7.6.1.2/S7.6.1.2_A1.10.js", true);
        }

        [Fact]
        [Trait("Category", "7.6.1.2")]
        public void TheExtendsTokenCanNotBeUsedAsIdentifier()
        {
			RunTest(@"TestCases/ch07/7.6/7.6.1/7.6.1.2/S7.6.1.2_A1.11.js", true);
        }

        [Fact]
        [Trait("Category", "7.6.1.2")]
        public void TheFinalTokenCanBeUsedAsIdentifier()
        {
			RunTest(@"TestCases/ch07/7.6/7.6.1/7.6.1.2/S7.6.1.2_A1.12.js", false);
        }

        [Fact]
        [Trait("Category", "7.6.1.2")]
        public void TheFloatTokenCanBeUsedAsIdentifier()
        {
			RunTest(@"TestCases/ch07/7.6/7.6.1/7.6.1.2/S7.6.1.2_A1.13.js", false);
        }

        [Fact]
        [Trait("Category", "7.6.1.2")]
        public void TheGotoTokenCanBeUsedAsIdentifier()
        {
			RunTest(@"TestCases/ch07/7.6/7.6.1/7.6.1.2/S7.6.1.2_A1.14.js", false);
        }

        [Fact]
        [Trait("Category", "7.6.1.2")]
        public void TheImplementsTokenCanNotBeUsedAsIdentifierInStrictCode()
        {
			RunTest(@"TestCases/ch07/7.6/7.6.1/7.6.1.2/S7.6.1.2_A1.15.js", true);
        }

        [Fact]
        [Trait("Category", "7.6.1.2")]
        public void TheImplementsTokenCanBeUsedAsIdentifierInNonStrictCode()
        {
			RunTest(@"TestCases/ch07/7.6/7.6.1/7.6.1.2/S7.6.1.2_A1.15ns.js", false);
        }

        [Fact]
        [Trait("Category", "7.6.1.2")]
        public void TheImportTokenCanNotBeUsedAsIdentifier()
        {
			RunTest(@"TestCases/ch07/7.6/7.6.1/7.6.1.2/S7.6.1.2_A1.16.js", true);
        }

        [Fact]
        [Trait("Category", "7.6.1.2")]
        public void TheIntTokenCanBeUsedAsIdentifier()
        {
			RunTest(@"TestCases/ch07/7.6/7.6.1/7.6.1.2/S7.6.1.2_A1.17.js", false);
        }

        [Fact]
        [Trait("Category", "7.6.1.2")]
        public void TheInterfaceTokenCanNotBeUsedAsIdentifierInStrictCode()
        {
			RunTest(@"TestCases/ch07/7.6/7.6.1/7.6.1.2/S7.6.1.2_A1.18.js", true);
        }

        [Fact]
        [Trait("Category", "7.6.1.2")]
        public void TheInterfaceTokenCanBeUsedAsIdentifierInNonStrictCode()
        {
			RunTest(@"TestCases/ch07/7.6/7.6.1/7.6.1.2/S7.6.1.2_A1.18ns.js", false);
        }

        [Fact]
        [Trait("Category", "7.6.1.2")]
        public void TheLongTokenCanBeUsedAsIdentifier()
        {
			RunTest(@"TestCases/ch07/7.6/7.6.1/7.6.1.2/S7.6.1.2_A1.19.js", false);
        }

        [Fact]
        [Trait("Category", "7.6.1.2")]
        public void TheBooleanTokenCanBeUsedAsIdentifier()
        {
			RunTest(@"TestCases/ch07/7.6/7.6.1/7.6.1.2/S7.6.1.2_A1.2.js", false);
        }

        [Fact]
        [Trait("Category", "7.6.1.2")]
        public void TheNativeTokenCanBeUsedAsIdentifier()
        {
			RunTest(@"TestCases/ch07/7.6/7.6.1/7.6.1.2/S7.6.1.2_A1.20.js", false);
        }

        [Fact]
        [Trait("Category", "7.6.1.2")]
        public void ThePackageTokenCanNotBeUsedAsIdentifierInStrictCode()
        {
			RunTest(@"TestCases/ch07/7.6/7.6.1/7.6.1.2/S7.6.1.2_A1.21.js", true);
        }

        [Fact]
        [Trait("Category", "7.6.1.2")]
        public void ThePackageTokenCanBeUsedAsIdentifierInNonStrictCode()
        {
			RunTest(@"TestCases/ch07/7.6/7.6.1/7.6.1.2/S7.6.1.2_A1.21ns.js", false);
        }

        [Fact]
        [Trait("Category", "7.6.1.2")]
        public void ThePrivateTokenCanNotBeUsedAsIdentifierInStrictCode()
        {
			RunTest(@"TestCases/ch07/7.6/7.6.1/7.6.1.2/S7.6.1.2_A1.22.js", true);
        }

        [Fact]
        [Trait("Category", "7.6.1.2")]
        public void ThePrivateTokenCanBeUsedAsIdentifierInNonStrictCode()
        {
			RunTest(@"TestCases/ch07/7.6/7.6.1/7.6.1.2/S7.6.1.2_A1.22ns.js", false);
        }

        [Fact]
        [Trait("Category", "7.6.1.2")]
        public void TheProtectedTokenCanNotBeUsedAsIdentifierInStrictCode()
        {
			RunTest(@"TestCases/ch07/7.6/7.6.1/7.6.1.2/S7.6.1.2_A1.23.js", true);
        }

        [Fact]
        [Trait("Category", "7.6.1.2")]
        public void TheProtectedTokenCanBeUsedAsIdentifierInNonStrictCode()
        {
			RunTest(@"TestCases/ch07/7.6/7.6.1/7.6.1.2/S7.6.1.2_A1.23ns.js", false);
        }

        [Fact]
        [Trait("Category", "7.6.1.2")]
        public void ThePublicTokenCanNotBeUsedAsIdentifierInStrictCode()
        {
			RunTest(@"TestCases/ch07/7.6/7.6.1/7.6.1.2/S7.6.1.2_A1.24.js", true);
        }

        [Fact]
        [Trait("Category", "7.6.1.2")]
        public void ThePublicTokenCanBeUsedAsIdentifierInNonStrictCode()
        {
			RunTest(@"TestCases/ch07/7.6/7.6.1/7.6.1.2/S7.6.1.2_A1.24ns.js", false);
        }

        [Fact]
        [Trait("Category", "7.6.1.2")]
        public void TheShortTokenCanBeUsedAsIdentifier()
        {
			RunTest(@"TestCases/ch07/7.6/7.6.1/7.6.1.2/S7.6.1.2_A1.25.js", false);
        }

        [Fact]
        [Trait("Category", "7.6.1.2")]
        public void TheStaticTokenCanNotBeUsedAsIdentifierInStrictCode()
        {
			RunTest(@"TestCases/ch07/7.6/7.6.1/7.6.1.2/S7.6.1.2_A1.26.js", true);
        }

        [Fact]
        [Trait("Category", "7.6.1.2")]
        public void TheStaticTokenCanBeUsedAsIdentifierInNonStrictCode()
        {
			RunTest(@"TestCases/ch07/7.6/7.6.1/7.6.1.2/S7.6.1.2_A1.26ns.js", false);
        }

        [Fact]
        [Trait("Category", "7.6.1.2")]
        public void TheSuperTokenCanNotBeUsedAsIdentifier()
        {
			RunTest(@"TestCases/ch07/7.6/7.6.1/7.6.1.2/S7.6.1.2_A1.27.js", true);
        }

        [Fact]
        [Trait("Category", "7.6.1.2")]
        public void TheSynchronizedTokenCanBeUsedAsIdentifier()
        {
			RunTest(@"TestCases/ch07/7.6/7.6.1/7.6.1.2/S7.6.1.2_A1.28.js", false);
        }

        [Fact]
        [Trait("Category", "7.6.1.2")]
        public void TheThrowsTokenCanBeUsedAsIdentifier()
        {
			RunTest(@"TestCases/ch07/7.6/7.6.1/7.6.1.2/S7.6.1.2_A1.29.js", false);
        }

        [Fact]
        [Trait("Category", "7.6.1.2")]
        public void TheByteTokenCanBeUsedAsIdentifier()
        {
			RunTest(@"TestCases/ch07/7.6/7.6.1/7.6.1.2/S7.6.1.2_A1.3.js", false);
        }

        [Fact]
        [Trait("Category", "7.6.1.2")]
        public void TheTransientTokenCanBeUsedAsIdentifier()
        {
			RunTest(@"TestCases/ch07/7.6/7.6.1/7.6.1.2/S7.6.1.2_A1.30.js", false);
        }

        [Fact]
        [Trait("Category", "7.6.1.2")]
        public void TheVolatileTokenCanBeUsedAsIdentifier()
        {
			RunTest(@"TestCases/ch07/7.6/7.6.1/7.6.1.2/S7.6.1.2_A1.31.js", false);
        }

        [Fact]
        [Trait("Category", "7.6.1.2")]
        public void TheCharTokenCanBeUsedAsIdentifier()
        {
			RunTest(@"TestCases/ch07/7.6/7.6.1/7.6.1.2/S7.6.1.2_A1.4.js", false);
        }

        [Fact]
        [Trait("Category", "7.6.1.2")]
        public void TheClassTokenCanNotBeUsedAsIdentifier()
        {
			RunTest(@"TestCases/ch07/7.6/7.6.1/7.6.1.2/S7.6.1.2_A1.5.js", true);
        }

        [Fact]
        [Trait("Category", "7.6.1.2")]
        public void TheConstTokenCanNotBeUsedAsIdentifier()
        {
			RunTest(@"TestCases/ch07/7.6/7.6.1/7.6.1.2/S7.6.1.2_A1.6.js", true);
        }

        [Fact]
        [Trait("Category", "7.6.1.2")]
        public void TheDebuggerTokenCanNotBeUsedAsIdentifier()
        {
			RunTest(@"TestCases/ch07/7.6/7.6.1/7.6.1.2/S7.6.1.2_A1.7.js", true);
        }

        [Fact]
        [Trait("Category", "7.6.1.2")]
        public void TheDoubleTokenCanBeUsedAsIdentifier()
        {
			RunTest(@"TestCases/ch07/7.6/7.6.1/7.6.1.2/S7.6.1.2_A1.8.js", false);
        }

        [Fact]
        [Trait("Category", "7.6.1.2")]
        public void TheEnumTokenCanNotBeUsedAsIdentifier()
        {
			RunTest(@"TestCases/ch07/7.6/7.6.1/7.6.1.2/S7.6.1.2_A1.9.js", true);
        }


    }
}
