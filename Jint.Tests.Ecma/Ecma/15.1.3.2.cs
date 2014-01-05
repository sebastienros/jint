using Xunit;

namespace Jint.Tests.Ecma
{

#if! RELEASE
    // Ignore in DEBUG to prevent too long running test
    [Trait("Category", "Pass")]
#endif
    public class Test_15_1_3_2 : EcmaTest
    {
        [Fact]
        [Trait("Category", "15.1.3.2")]
        public void IfB110XxxxxN2AndStringCharatK4AndStringCharatK5DoNotRepresentHexadecimalDigitsThrowUrierror()
        {
			RunTest(@"TestCases/ch15/15.1/15.1.3/15.1.3.2/S15.1.3.2_A1.10_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.1.3.2")]
        public void IfB1110XxxxN3AndStringCharatK4AndStringCharatK5OrStringCharatK7AndStringCharatK8DoNotRepresentHexadecimalDigitsThrowUrierror()
        {
			RunTest(@"TestCases/ch15/15.1/15.1.3/15.1.3.2/S15.1.3.2_A1.11_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.1.3.2")]
        public void IfB1110XxxxN3AndStringCharatK4AndStringCharatK5OrStringCharatK7AndStringCharatK8DoNotRepresentHexadecimalDigitsThrowUrierror2()
        {
			RunTest(@"TestCases/ch15/15.1/15.1.3/15.1.3.2/S15.1.3.2_A1.11_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.1.3.2")]
        public void IfB11110XxxN4AndStringCharatK4AndStringCharatK5OrStringCharatK7AndStringCharatK8OrStringCharatK10AndStringCharatK11DoNotRepresentHexadecimalDigitsThrowUrierror()
        {
			RunTest(@"TestCases/ch15/15.1/15.1.3/15.1.3.2/S15.1.3.2_A1.12_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.1.3.2")]
        public void IfB11110XxxN4AndStringCharatK4AndStringCharatK5OrStringCharatK7AndStringCharatK8OrStringCharatK10AndStringCharatK11DoNotRepresentHexadecimalDigitsThrowUrierror2()
        {
			RunTest(@"TestCases/ch15/15.1/15.1.3/15.1.3.2/S15.1.3.2_A1.12_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.1.3.2")]
        public void IfB11110XxxN4AndStringCharatK4AndStringCharatK5OrStringCharatK7AndStringCharatK8OrStringCharatK10AndStringCharatK11DoNotRepresentHexadecimalDigitsThrowUrierror3()
        {
			RunTest(@"TestCases/ch15/15.1/15.1.3/15.1.3.2/S15.1.3.2_A1.12_T3.js", false);
        }

        [Fact]
        [Trait("Category", "15.1.3.2")]
        public void IfB110XxxxxN2AndC10XxxxxxCFirstOfOctetsAfterBThrowUrierror()
        {
			RunTest(@"TestCases/ch15/15.1/15.1.3/15.1.3.2/S15.1.3.2_A1.13_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.1.3.2")]
        public void IfB110XxxxxN2AndC10XxxxxxCFirstOfOctetsAfterBThrowUrierror2()
        {
			RunTest(@"TestCases/ch15/15.1/15.1.3/15.1.3.2/S15.1.3.2_A1.13_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.1.3.2")]
        public void IfB1110XxxxN3AndC10XxxxxxCFirstOfOctetsAfterBThrowUrierror()
        {
			RunTest(@"TestCases/ch15/15.1/15.1.3/15.1.3.2/S15.1.3.2_A1.14_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.1.3.2")]
        public void IfB1110XxxxN3AndC10XxxxxxCFirstOfOctetsAfterBThrowUrierror2()
        {
			RunTest(@"TestCases/ch15/15.1/15.1.3/15.1.3.2/S15.1.3.2_A1.14_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.1.3.2")]
        public void IfB1110XxxxN3AndC10XxxxxxCFirstOfOctetsAfterBThrowUrierror3()
        {
			RunTest(@"TestCases/ch15/15.1/15.1.3/15.1.3.2/S15.1.3.2_A1.14_T3.js", false);
        }

        [Fact]
        [Trait("Category", "15.1.3.2")]
        public void IfB1110XxxxN3AndC10XxxxxxCFirstOfOctetsAfterBThrowUrierror4()
        {
			RunTest(@"TestCases/ch15/15.1/15.1.3/15.1.3.2/S15.1.3.2_A1.14_T4.js", false);
        }

        [Fact]
        [Trait("Category", "15.1.3.2")]
        public void IfB11110XxxN4AndC10XxxxxxCFirstOfOctetsAfterBThrowUrierror()
        {
			RunTest(@"TestCases/ch15/15.1/15.1.3/15.1.3.2/S15.1.3.2_A1.15_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.1.3.2")]
        public void IfB11110XxxN4AndC10XxxxxxCFirstOfOctetsAfterBThrowUrierror2()
        {
			RunTest(@"TestCases/ch15/15.1/15.1.3/15.1.3.2/S15.1.3.2_A1.15_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.1.3.2")]
        public void IfB11110XxxN4AndC10XxxxxxCFirstOfOctetsAfterBThrowUrierror3()
        {
			RunTest(@"TestCases/ch15/15.1/15.1.3/15.1.3.2/S15.1.3.2_A1.15_T3.js", false);
        }

        [Fact]
        [Trait("Category", "15.1.3.2")]
        public void IfB11110XxxN4AndC10XxxxxxCFirstOfOctetsAfterBThrowUrierror4()
        {
			RunTest(@"TestCases/ch15/15.1/15.1.3/15.1.3.2/S15.1.3.2_A1.15_T4.js", false);
        }

        [Fact]
        [Trait("Category", "15.1.3.2")]
        public void IfB11110XxxN4AndC10XxxxxxCFirstOfOctetsAfterBThrowUrierror5()
        {
			RunTest(@"TestCases/ch15/15.1/15.1.3/15.1.3.2/S15.1.3.2_A1.15_T5.js", false);
        }

        [Fact]
        [Trait("Category", "15.1.3.2")]
        public void IfB11110XxxN4AndC10XxxxxxCFirstOfOctetsAfterBThrowUrierror6()
        {
			RunTest(@"TestCases/ch15/15.1/15.1.3/15.1.3.2/S15.1.3.2_A1.15_T6.js", false);
        }

        [Fact]
        [Trait("Category", "15.1.3.2")]
        public void IfStringCharatKEqualAndK2StringLengthThrowUrierror()
        {
			RunTest(@"TestCases/ch15/15.1/15.1.3/15.1.3.2/S15.1.3.2_A1.1_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.1.3.2")]
        public void IfBStringCharatK1StringCharatK2DoNotRepresentHexadecimalDigitsThrowUrierror()
        {
			RunTest(@"TestCases/ch15/15.1/15.1.3/15.1.3.2/S15.1.3.2_A1.2_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.1.3.2")]
        public void IfBStringCharatK1StringCharatK2DoNotRepresentHexadecimalDigitsThrowUrierror2()
        {
			RunTest(@"TestCases/ch15/15.1/15.1.3/15.1.3.2/S15.1.3.2_A1.2_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.1.3.2")]
        public void IfB10XxxxxxOrB11111XxxThrowUrierror()
        {
			RunTest(@"TestCases/ch15/15.1/15.1.3/15.1.3.2/S15.1.3.2_A1.3_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.1.3.2")]
        public void IfB10XxxxxxOrB11111XxxThrowUrierror2()
        {
			RunTest(@"TestCases/ch15/15.1/15.1.3/15.1.3.2/S15.1.3.2_A1.3_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.1.3.2")]
        public void IfB110XxxxxN2AndK23LengthThrowUrierror()
        {
			RunTest(@"TestCases/ch15/15.1/15.1.3/15.1.3.2/S15.1.3.2_A1.4_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.1.3.2")]
        public void IfB1110XxxxN3AndK26LengthThrowUrierror()
        {
			RunTest(@"TestCases/ch15/15.1/15.1.3/15.1.3.2/S15.1.3.2_A1.5_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.1.3.2")]
        public void IfB11110XxxN4AndK29LengthThrowUrierror()
        {
			RunTest(@"TestCases/ch15/15.1/15.1.3/15.1.3.2/S15.1.3.2_A1.6_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.1.3.2")]
        public void IfB110XxxxxN2AndStringCharatK3NotEqualThrowUrierror()
        {
			RunTest(@"TestCases/ch15/15.1/15.1.3/15.1.3.2/S15.1.3.2_A1.7_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.1.3.2")]
        public void IfB1110XxxxN3AndStringCharatK3StringCharatK6NotEqualThrowUrierror()
        {
			RunTest(@"TestCases/ch15/15.1/15.1.3/15.1.3.2/S15.1.3.2_A1.8_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.1.3.2")]
        public void IfB1110XxxxN3AndStringCharatK3StringCharatK6NotEqualThrowUrierror2()
        {
			RunTest(@"TestCases/ch15/15.1/15.1.3/15.1.3.2/S15.1.3.2_A1.8_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.1.3.2")]
        public void IfB11110XxxN4AndStringCharatK3StringCharatK6StringCharatK9NotEqualThrowUrierror()
        {
			RunTest(@"TestCases/ch15/15.1/15.1.3/15.1.3.2/S15.1.3.2_A1.9_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.1.3.2")]
        public void IfB11110XxxN4AndStringCharatK3StringCharatK6StringCharatK9NotEqualThrowUrierror2()
        {
			RunTest(@"TestCases/ch15/15.1/15.1.3/15.1.3.2/S15.1.3.2_A1.9_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.1.3.2")]
        public void IfB11110XxxN4AndStringCharatK3StringCharatK6StringCharatK9NotEqualThrowUrierror3()
        {
			RunTest(@"TestCases/ch15/15.1/15.1.3/15.1.3.2/S15.1.3.2_A1.9_T3.js", false);
        }

        [Fact]
        [Trait("Category", "15.1.3.2")]
        public void IfStringCharatKNotEqualReturnThisChar()
        {
			RunTest(@"TestCases/ch15/15.1/15.1.3/15.1.3.2/S15.1.3.2_A2.1_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.1.3.2")]
        public void IfB10Xxxxxxxx0X000X7FReturnB1()
        {
			RunTest(@"TestCases/ch15/15.1/15.1.3/15.1.3.2/S15.1.3.2_A2.2_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.1.3.2")]
        public void IfB1110Xxxxx0Xc00XdfB210Xxxxxx0X800XbfWithoutB10Xc00Xc1ReturnUtf8B1B2()
        {
			RunTest(@"TestCases/ch15/15.1/15.1.3/15.1.3.2/S15.1.3.2_A2.3_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.1.3.2")]
        public void IfB11110Xxxx0Xe00XefB2B310Xxxxxxx0X800XbfWithoutB1B20Xe00X800X9F0Xed0Xa00Xbf0Xd8000XdfffReturnUtf8B1B2B3()
        {
			RunTest(@"TestCases/ch15/15.1/15.1.3/15.1.3.2/S15.1.3.2_A2.4_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.1.3.2")]
        public void IfB111110Xxx0Xf00X0F4B2B3B410Xxxxxxx0X800XbfWithoutB1B20Xf00X800X9F0Xf40X900XbfReturnUtf8B1B2B3B4()
        {
			RunTest(@"TestCases/ch15/15.1/15.1.3/15.1.3.2/S15.1.3.2_A2.5_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.1.3.2")]
        public void LetReserveduricomponentsetBeTheEmptyString()
        {
			RunTest(@"TestCases/ch15/15.1/15.1.3/15.1.3.2/S15.1.3.2_A3_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.1.3.2")]
        public void LetReserveduricomponentsetBeTheEmptyString2()
        {
			RunTest(@"TestCases/ch15/15.1/15.1.3/15.1.3.2/S15.1.3.2_A3_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.1.3.2")]
        public void LetReserveduricomponentsetBeTheEmptyString3()
        {
			RunTest(@"TestCases/ch15/15.1/15.1.3/15.1.3.2/S15.1.3.2_A3_T3.js", false);
        }

        [Fact]
        [Trait("Category", "15.1.3.2")]
        public void UriTests()
        {
			RunTest(@"TestCases/ch15/15.1/15.1.3/15.1.3.2/S15.1.3.2_A4_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.1.3.2")]
        public void UriTests2()
        {
			RunTest(@"TestCases/ch15/15.1/15.1.3/15.1.3.2/S15.1.3.2_A4_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.1.3.2")]
        public void UriTests3()
        {
			RunTest(@"TestCases/ch15/15.1/15.1.3/15.1.3.2/S15.1.3.2_A4_T3.js", false);
        }

        [Fact]
        [Trait("Category", "15.1.3.2")]
        public void UriTests4()
        {
			RunTest(@"TestCases/ch15/15.1/15.1.3/15.1.3.2/S15.1.3.2_A4_T4.js", false);
        }

        [Fact]
        [Trait("Category", "15.1.3.2")]
        public void TheLengthPropertyOfDecodeuricomponentHasTheAttributeDontenum()
        {
			RunTest(@"TestCases/ch15/15.1/15.1.3/15.1.3.2/S15.1.3.2_A5.1.js", false);
        }

        [Fact]
        [Trait("Category", "15.1.3.2")]
        public void TheLengthPropertyOfDecodeuricomponentHasTheAttributeDontdelete()
        {
			RunTest(@"TestCases/ch15/15.1/15.1.3/15.1.3.2/S15.1.3.2_A5.2.js", false);
        }

        [Fact]
        [Trait("Category", "15.1.3.2")]
        public void TheLengthPropertyOfDecodeuricomponentHasTheAttributeReadonly()
        {
			RunTest(@"TestCases/ch15/15.1/15.1.3/15.1.3.2/S15.1.3.2_A5.3.js", false);
        }

        [Fact]
        [Trait("Category", "15.1.3.2")]
        public void TheLengthPropertyOfDecodeuricomponentIs1()
        {
			RunTest(@"TestCases/ch15/15.1/15.1.3/15.1.3.2/S15.1.3.2_A5.4.js", false);
        }

        [Fact]
        [Trait("Category", "15.1.3.2")]
        public void TheDecodeuricomponentPropertyHasTheAttributeDontenum()
        {
			RunTest(@"TestCases/ch15/15.1/15.1.3/15.1.3.2/S15.1.3.2_A5.5.js", false);
        }

        [Fact]
        [Trait("Category", "15.1.3.2")]
        public void TheDecodeuricomponentPropertyHasNotPrototypeProperty()
        {
			RunTest(@"TestCases/ch15/15.1/15.1.3/15.1.3.2/S15.1.3.2_A5.6.js", false);
        }

        [Fact]
        [Trait("Category", "15.1.3.2")]
        public void TheDecodeuricomponentPropertyCanTBeUsedAsConstructor()
        {
			RunTest(@"TestCases/ch15/15.1/15.1.3/15.1.3.2/S15.1.3.2_A5.7.js", false);
        }

        [Fact]
        [Trait("Category", "15.1.3.2")]
        public void OperatorUseTostring()
        {
			RunTest(@"TestCases/ch15/15.1/15.1.3/15.1.3.2/S15.1.3.2_A6_T1.js", false);
        }


    }
}
