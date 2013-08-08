using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_7_6 : EcmaTest
    {
        [Fact]
        [Trait("Category", "7.6")]
        public void SyntaxerrorExpectedReservedWordsUsedAsIdentifierNamesInUtf8NullNull()
        {
			RunTest(@"TestCases/ch07/7.6/7.6-1.js", false);
        }

        [Fact]
        [Trait("Category", "7.6")]
        public void SyntaxerrorExpectedReservedWordsUsedAsIdentifierNamesInUtf8NewNew()
        {
			RunTest(@"TestCases/ch07/7.6/7.6-10.js", false);
        }

        [Fact]
        [Trait("Category", "7.6")]
        public void SyntaxerrorExpectedReservedWordsUsedAsIdentifierNamesInUtf8VarVar()
        {
			RunTest(@"TestCases/ch07/7.6/7.6-11.js", false);
        }

        [Fact]
        [Trait("Category", "7.6")]
        public void SyntaxerrorExpectedReservedWordsUsedAsIdentifierNamesInUtf8TryTry()
        {
			RunTest(@"TestCases/ch07/7.6/7.6-12.js", false);
        }

        [Fact]
        [Trait("Category", "7.6")]
        public void SyntaxerrorExpectedReservedWordsUsedAsIdentifierNamesInUtf8CatchCatch()
        {
			RunTest(@"TestCases/ch07/7.6/7.6-13.js", false);
        }

        [Fact]
        [Trait("Category", "7.6")]
        public void SyntaxerrorExpectedReservedWordsUsedAsIdentifierNamesInUtf8FinallyFinally()
        {
			RunTest(@"TestCases/ch07/7.6/7.6-14.js", false);
        }

        [Fact]
        [Trait("Category", "7.6")]
        public void SyntaxerrorExpectedReservedWordsUsedAsIdentifierNamesInUtf8ReturnReturn()
        {
			RunTest(@"TestCases/ch07/7.6/7.6-15.js", false);
        }

        [Fact]
        [Trait("Category", "7.6")]
        public void SyntaxerrorExpectedReservedWordsUsedAsIdentifierNamesInUtf8VoidVoid()
        {
			RunTest(@"TestCases/ch07/7.6/7.6-16.js", false);
        }

        [Fact]
        [Trait("Category", "7.6")]
        public void SyntaxerrorExpectedReservedWordsUsedAsIdentifierNamesInUtf8ContinueContinue()
        {
			RunTest(@"TestCases/ch07/7.6/7.6-17.js", false);
        }

        [Fact]
        [Trait("Category", "7.6")]
        public void SyntaxerrorExpectedReservedWordsUsedAsIdentifierNamesInUtf8ForFor()
        {
			RunTest(@"TestCases/ch07/7.6/7.6-18.js", false);
        }

        [Fact]
        [Trait("Category", "7.6")]
        public void SyntaxerrorExpectedReservedWordsUsedAsIdentifierNamesInUtf8SwitchSwitch()
        {
			RunTest(@"TestCases/ch07/7.6/7.6-19.js", false);
        }

        [Fact]
        [Trait("Category", "7.6")]
        public void SyntaxerrorExpectedReservedWordsUsedAsIdentifierNamesInUtf8TrueTrue()
        {
			RunTest(@"TestCases/ch07/7.6/7.6-2.js", false);
        }

        [Fact]
        [Trait("Category", "7.6")]
        public void SyntaxerrorExpectedReservedWordsUsedAsIdentifierNamesInUtf8WhileWhile()
        {
			RunTest(@"TestCases/ch07/7.6/7.6-20.js", false);
        }

        [Fact]
        [Trait("Category", "7.6")]
        public void SyntaxerrorExpectedReservedWordsUsedAsIdentifierNamesInUtf8DebuggerDebugger()
        {
			RunTest(@"TestCases/ch07/7.6/7.6-21.js", false);
        }

        [Fact]
        [Trait("Category", "7.6")]
        public void SyntaxerrorExpectedReservedWordsUsedAsIdentifierNamesInUtf8FunctionFunction()
        {
			RunTest(@"TestCases/ch07/7.6/7.6-22.js", false);
        }

        [Fact]
        [Trait("Category", "7.6")]
        public void SyntaxerrorExpectedReservedWordsUsedAsIdentifierNamesInUtf8ThisThis()
        {
			RunTest(@"TestCases/ch07/7.6/7.6-23.js", false);
        }

        [Fact]
        [Trait("Category", "7.6")]
        public void SyntaxerrorExpectedReservedWordsUsedAsIdentifierNamesInUtf8IfIf()
        {
			RunTest(@"TestCases/ch07/7.6/7.6-24.js", false);
        }

        [Fact]
        [Trait("Category", "7.6")]
        public void SyntaxerrorExpectedReservedWordsUsedAsIdentifierNamesInUtf8WithWith()
        {
			RunTest(@"TestCases/ch07/7.6/7.6-25.js", false);
        }

        [Fact]
        [Trait("Category", "7.6")]
        public void SyntaxerrorExpectedReservedWordsUsedAsIdentifierNamesInUtf8DefaultDefault()
        {
			RunTest(@"TestCases/ch07/7.6/7.6-26.js", false);
        }

        [Fact]
        [Trait("Category", "7.6")]
        public void SyntaxerrorExpectedReservedWordsUsedAsIdentifierNamesInUtf8ThrowThrow()
        {
			RunTest(@"TestCases/ch07/7.6/7.6-27.js", false);
        }

        [Fact]
        [Trait("Category", "7.6")]
        public void SyntaxerrorExpectedReservedWordsUsedAsIdentifierNamesInUtf8InIn()
        {
			RunTest(@"TestCases/ch07/7.6/7.6-28.js", false);
        }

        [Fact]
        [Trait("Category", "7.6")]
        public void SyntaxerrorExpectedReservedWordsUsedAsIdentifierNamesInUtf8DeleteDelete()
        {
			RunTest(@"TestCases/ch07/7.6/7.6-29.js", false);
        }

        [Fact]
        [Trait("Category", "7.6")]
        public void SyntaxerrorExpectedReservedWordsUsedAsIdentifierNamesInUtf8FalseFalse()
        {
			RunTest(@"TestCases/ch07/7.6/7.6-3.js", false);
        }

        [Fact]
        [Trait("Category", "7.6")]
        public void SyntaxerrorExpectedReservedWordsUsedAsIdentifierNamesInUtf8ClassClass()
        {
			RunTest(@"TestCases/ch07/7.6/7.6-30.js", false);
        }

        [Fact]
        [Trait("Category", "7.6")]
        public void SyntaxerrorExpectedReservedWordsUsedAsIdentifierNamesInUtf8ExtendsExtends()
        {
			RunTest(@"TestCases/ch07/7.6/7.6-31.js", false);
        }

        [Fact]
        [Trait("Category", "7.6")]
        public void SyntaxerrorExpectedReservedWordsUsedAsIdentifierNamesInUtf8EnumEnum()
        {
			RunTest(@"TestCases/ch07/7.6/7.6-32.js", false);
        }

        [Fact]
        [Trait("Category", "7.6")]
        public void SyntaxerrorExpectedReservedWordsUsedAsIdentifierNamesInUtf8SuperSuper()
        {
			RunTest(@"TestCases/ch07/7.6/7.6-33.js", false);
        }

        [Fact]
        [Trait("Category", "7.6")]
        public void SyntaxerrorExpectedReservedWordsUsedAsIdentifierNamesInUtf8ConstConst()
        {
			RunTest(@"TestCases/ch07/7.6/7.6-34.js", false);
        }

        [Fact]
        [Trait("Category", "7.6")]
        public void SyntaxerrorExpectedReservedWordsUsedAsIdentifierNamesInUtf8ExportExport()
        {
			RunTest(@"TestCases/ch07/7.6/7.6-35.js", false);
        }

        [Fact]
        [Trait("Category", "7.6")]
        public void SyntaxerrorExpectedReservedWordsUsedAsIdentifierNamesInUtf8ImportImport()
        {
			RunTest(@"TestCases/ch07/7.6/7.6-36.js", false);
        }

        [Fact]
        [Trait("Category", "7.6")]
        public void SyntaxerrorExpectedReservedWordsUsedAsIdentifierNamesInUtf8BreakBreak()
        {
			RunTest(@"TestCases/ch07/7.6/7.6-4.js", false);
        }

        [Fact]
        [Trait("Category", "7.6")]
        public void SyntaxerrorExpectedReservedWordsUsedAsIdentifierNamesInUtf8CaseCase()
        {
			RunTest(@"TestCases/ch07/7.6/7.6-5.js", false);
        }

        [Fact]
        [Trait("Category", "7.6")]
        public void SyntaxerrorExpectedReservedWordsUsedAsIdentifierNamesInUtf8InstanceofInstanceof()
        {
			RunTest(@"TestCases/ch07/7.6/7.6-6.js", false);
        }

        [Fact]
        [Trait("Category", "7.6")]
        public void SyntaxerrorExpectedReservedWordsUsedAsIdentifierNamesInUtf8TypeofTypeof()
        {
			RunTest(@"TestCases/ch07/7.6/7.6-7.js", false);
        }

        [Fact]
        [Trait("Category", "7.6")]
        public void SyntaxerrorExpectedReservedWordsUsedAsIdentifierNamesInUtf8DoDo()
        {
			RunTest(@"TestCases/ch07/7.6/7.6-8.js", false);
        }

        [Fact]
        [Trait("Category", "7.6")]
        public void SyntaxerrorExpectedReservedWordsUsedAsIdentifierNamesInUtf8ElseElseNull()
        {
			RunTest(@"TestCases/ch07/7.6/7.6-9.js", false);
        }

        [Fact]
        [Trait("Category", "7.6")]
        public void Identifierstart()
        {
			RunTest(@"TestCases/ch07/7.6/S7.6_A1.2_T1.js", false);
        }

        [Fact]
        [Trait("Category", "7.6")]
        public void Identifierstart2()
        {
			RunTest(@"TestCases/ch07/7.6/S7.6_A1.2_T2.js", false);
        }

        [Fact]
        [Trait("Category", "7.6")]
        public void Identifierstart3()
        {
			RunTest(@"TestCases/ch07/7.6/S7.6_A1.2_T3.js", false);
        }

        [Fact]
        [Trait("Category", "7.6")]
        public void Identifierstart4()
        {
			RunTest(@"TestCases/ch07/7.6/S7.6_A1.3_T1.js", false);
        }

        [Fact]
        [Trait("Category", "7.6")]
        public void Identifierstart5()
        {
			RunTest(@"TestCases/ch07/7.6/S7.6_A1.3_T2.js", false);
        }

        [Fact]
        [Trait("Category", "7.6")]
        public void Identifierstart6()
        {
			RunTest(@"TestCases/ch07/7.6/S7.6_A1.3_T3.js", false);
        }

        [Fact]
        [Trait("Category", "7.6")]
        public void IdentifierpartIdentifierstart()
        {
			RunTest(@"TestCases/ch07/7.6/S7.6_A2.1_T1.js", false);
        }

        [Fact]
        [Trait("Category", "7.6")]
        public void IdentifierpartIdentifierstart2()
        {
			RunTest(@"TestCases/ch07/7.6/S7.6_A2.1_T2.js", false);
        }

        [Fact]
        [Trait("Category", "7.6")]
        public void IdentifierpartIdentifierstart3()
        {
			RunTest(@"TestCases/ch07/7.6/S7.6_A2.1_T3.js", false);
        }

        [Fact]
        [Trait("Category", "7.6")]
        public void IdentifierpartIdentifierstart4()
        {
			RunTest(@"TestCases/ch07/7.6/S7.6_A2.1_T4.js", false);
        }

        [Fact]
        [Trait("Category", "7.6")]
        public void CorrectInterpretationOfEnglishAlphabet()
        {
			RunTest(@"TestCases/ch07/7.6/S7.6_A4.1_T1.js", false);
        }

        [Fact]
        [Trait("Category", "7.6")]
        public void CorrectInterpretationOfEnglishAlphabet2()
        {
			RunTest(@"TestCases/ch07/7.6/S7.6_A4.1_T2.js", false);
        }

        [Fact]
        [Trait("Category", "7.6")]
        public void CorrectInterpretationOfRussianAlphabet()
        {
			RunTest(@"TestCases/ch07/7.6/S7.6_A4.2_T1.js", false);
        }

        [Fact]
        [Trait("Category", "7.6")]
        public void CorrectInterpretationOfRussianAlphabet2()
        {
			RunTest(@"TestCases/ch07/7.6/S7.6_A4.2_T2.js", false);
        }

        [Fact]
        [Trait("Category", "7.6")]
        public void CorrectInterpretationOfDigits()
        {
			RunTest(@"TestCases/ch07/7.6/S7.6_A4.3_T1.js", false);
        }


    }
}
