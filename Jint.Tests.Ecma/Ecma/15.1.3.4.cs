using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_15_1_3_4 : EcmaTest
    {
        [Fact]
        [Trait("Category", "15.1.3.4")]
        public void IfStringCharatKIn0Xdc000XdfffThrowUrierror()
        {
			RunTest(@"TestCases/ch15/15.1/15.1.3/15.1.3.4/S15.1.3.4_A1.1_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.1.3.4")]
        public void IfStringCharatKIn0Xdc000XdfffThrowUrierror2()
        {
			RunTest(@"TestCases/ch15/15.1/15.1.3/15.1.3.4/S15.1.3.4_A1.1_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.1.3.4")]
        public void IfStringCharatKIn0Xd8000XdbffAndStringLengthK1ThrowUrierror()
        {
			RunTest(@"TestCases/ch15/15.1/15.1.3/15.1.3.4/S15.1.3.4_A1.2_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.1.3.4")]
        public void IfStringCharatKIn0Xd8000XdbffAndStringLengthK1ThrowUrierror2()
        {
			RunTest(@"TestCases/ch15/15.1/15.1.3/15.1.3.4/S15.1.3.4_A1.2_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.1.3.4")]
        public void IfStringCharatKIn0Xd8000XdbffAndStringCharatK1NotIn0Xdc000XdfffThrowUrierror()
        {
			RunTest(@"TestCases/ch15/15.1/15.1.3/15.1.3.4/S15.1.3.4_A1.3_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.1.3.4")]
        public void IfStringCharatKIn0X00000X007FUriunescapedReturn1Octet000000000Zzzzzzz0Zzzzzzz()
        {
			RunTest(@"TestCases/ch15/15.1/15.1.3/15.1.3.4/S15.1.3.4_A2.1_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.1.3.4")]
        public void IfStringCharatKIn0X00800X07FfReturn2Octets00000YyyYyzzzzzz110Yyyyy10Zzzzzz()
        {
			RunTest(@"TestCases/ch15/15.1/15.1.3/15.1.3.4/S15.1.3.4_A2.2_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.1.3.4")]
        public void IfStringCharatKIn0X08000Xd7FfReturn3OctetsXxxxyyyyYyzzzzzz1110Xxxx10Yyyyyy10Zzzzzz()
        {
			RunTest(@"TestCases/ch15/15.1/15.1.3/15.1.3.4/S15.1.3.4_A2.3_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.1.3.4")]
        public void IfStringCharatKIn0Xd8000XdbffAndStringCharatK1In0Xdc000XdfffReturn4Octets000WwwxxXxxxyyyyYyzzzzzz11110Www10Xxxxxx10Yyyyyy10Zzzzzz()
        {
			RunTest(@"TestCases/ch15/15.1/15.1.3/15.1.3.4/S15.1.3.4_A2.4_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.1.3.4")]
        public void IfStringCharatKIn0Xd8000XdbffAndStringCharatK1In0Xdc000XdfffReturn4Octets000WwwxxXxxxyyyyYyzzzzzz11110Www10Xxxxxx10Yyyyyy10Zzzzzz2()
        {
			RunTest(@"TestCases/ch15/15.1/15.1.3/15.1.3.4/S15.1.3.4_A2.4_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.1.3.4")]
        public void IfStringCharatKIn0Xe0000XffffReturn3OctetsXxxxyyyyYyzzzzzz1110Xxxx10Yyyyyy10Zzzzzz()
        {
			RunTest(@"TestCases/ch15/15.1/15.1.3/15.1.3.4/S15.1.3.4_A2.5_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.1.3.4")]
        public void UnescapeduricomponentsetNotContainingUrireserved()
        {
			RunTest(@"TestCases/ch15/15.1/15.1.3/15.1.3.4/S15.1.3.4_A3.1_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.1.3.4")]
        public void UnescapeduricomponentsetContainingOneInstanceOfEachCharacterValidInUriunescaped()
        {
			RunTest(@"TestCases/ch15/15.1/15.1.3/15.1.3.4/S15.1.3.4_A3.2_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.1.3.4")]
        public void UnescapeduricomponentsetContainingOneInstanceOfEachCharacterValidInUriunescaped2()
        {
			RunTest(@"TestCases/ch15/15.1/15.1.3/15.1.3.4/S15.1.3.4_A3.2_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.1.3.4")]
        public void UnescapeduricomponentsetContainingOneInstanceOfEachCharacterValidInUriunescaped3()
        {
			RunTest(@"TestCases/ch15/15.1/15.1.3/15.1.3.4/S15.1.3.4_A3.2_T3.js", false);
        }

        [Fact]
        [Trait("Category", "15.1.3.4")]
        public void UnescapeduricomponentsetNotContaining()
        {
			RunTest(@"TestCases/ch15/15.1/15.1.3/15.1.3.4/S15.1.3.4_A3.3_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.1.3.4")]
        public void UriTests()
        {
			RunTest(@"TestCases/ch15/15.1/15.1.3/15.1.3.4/S15.1.3.4_A4_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.1.3.4")]
        public void UriTests2()
        {
			RunTest(@"TestCases/ch15/15.1/15.1.3/15.1.3.4/S15.1.3.4_A4_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.1.3.4")]
        public void UriTests3()
        {
			RunTest(@"TestCases/ch15/15.1/15.1.3/15.1.3.4/S15.1.3.4_A4_T3.js", false);
        }

        [Fact]
        [Trait("Category", "15.1.3.4")]
        public void UriTests4()
        {
			RunTest(@"TestCases/ch15/15.1/15.1.3/15.1.3.4/S15.1.3.4_A4_T4.js", false);
        }

        [Fact]
        [Trait("Category", "15.1.3.4")]
        public void TheLengthPropertyOfEncodeuricomponentHasTheAttributeDontenum()
        {
			RunTest(@"TestCases/ch15/15.1/15.1.3/15.1.3.4/S15.1.3.4_A5.1.js", false);
        }

        [Fact]
        [Trait("Category", "15.1.3.4")]
        public void TheLengthPropertyOfEncodeuricomponentHasTheAttributeDontdelete()
        {
			RunTest(@"TestCases/ch15/15.1/15.1.3/15.1.3.4/S15.1.3.4_A5.2.js", false);
        }

        [Fact]
        [Trait("Category", "15.1.3.4")]
        public void TheLengthPropertyOfEncodeuricomponentHasTheAttributeReadonly()
        {
			RunTest(@"TestCases/ch15/15.1/15.1.3/15.1.3.4/S15.1.3.4_A5.3.js", false);
        }

        [Fact]
        [Trait("Category", "15.1.3.4")]
        public void TheLengthPropertyOfEncodeuricomponentIs1()
        {
			RunTest(@"TestCases/ch15/15.1/15.1.3/15.1.3.4/S15.1.3.4_A5.4.js", false);
        }

        [Fact]
        [Trait("Category", "15.1.3.4")]
        public void TheEncodeuricomponentPropertyHasTheAttributeDontenum()
        {
			RunTest(@"TestCases/ch15/15.1/15.1.3/15.1.3.4/S15.1.3.4_A5.5.js", false);
        }

        [Fact]
        [Trait("Category", "15.1.3.4")]
        public void TheEncodeuricomponentPropertyHasNotPrototypeProperty()
        {
			RunTest(@"TestCases/ch15/15.1/15.1.3/15.1.3.4/S15.1.3.4_A5.6.js", false);
        }

        [Fact]
        [Trait("Category", "15.1.3.4")]
        public void TheEncodeuricomponentPropertyCanTBeUsedAsConstructor()
        {
			RunTest(@"TestCases/ch15/15.1/15.1.3/15.1.3.4/S15.1.3.4_A5.7.js", false);
        }

        [Fact]
        [Trait("Category", "15.1.3.4")]
        public void OperatorUseTostring()
        {
			RunTest(@"TestCases/ch15/15.1/15.1.3/15.1.3.4/S15.1.3.4_A6_T1.js", false);
        }


    }
}
