using System;
using System.Linq;
using Esprima;
using Esprima.Ast;
using Jint.Runtime;
using Xunit;

namespace Jint.Tests.Parser
{
    public class JavascriptParserTests
    {
        [Fact]
        public void ShouldParseThis()
        {
            var program = new JavaScriptParser("this").ParseProgram();
            var body = program.Body;

            Assert.Single(body);
            Assert.Equal(Nodes.ThisExpression, body.First().As<ExpressionStatement>().Expression.Type);
        }

        [Fact]
        public void ShouldParseNull()
        {
            var program = new JavaScriptParser("null").ParseProgram();
            var body = program.Body;

            Assert.Single(body);
            Assert.Equal(Nodes.Literal, body.First().As<ExpressionStatement>().Expression.Type);
            Assert.Equal(null, body.First().As<ExpressionStatement>().Expression.As<Literal>().Value);
            Assert.Equal("null", body.First().As<ExpressionStatement>().Expression.As<Literal>().Raw);
        }

        [Fact]
        public void ShouldParseNumeric()
        {
            var program = new JavaScriptParser(
                @"
                42
            ").ParseProgram();
            var body = program.Body;

            Assert.Single(body);
            Assert.Equal(Nodes.Literal, body.First().As<ExpressionStatement>().Expression.Type);
            Assert.Equal(42d, body.First().As<ExpressionStatement>().Expression.As<Literal>().Value);
            Assert.Equal("42", body.First().As<ExpressionStatement>().Expression.As<Literal>().Raw);
        }

        [Fact]
        public void ShouldParseBinaryExpression()
        {
            BinaryExpression binary;

            var program = new JavaScriptParser("(1 + 2 ) * 3").ParseProgram();
            var body = program.Body;

            Assert.Single(body);
            Assert.NotNull(binary = body.First().As<ExpressionStatement>().Expression.As<BinaryExpression>());
            Assert.Equal(3d, binary.Right.As<Literal>().Value);
            Assert.Equal(BinaryOperator.Times, binary.Operator);
            Assert.Equal(1d, binary.Left.As<BinaryExpression>().Left.As<Literal>().Value);
            Assert.Equal(2d, binary.Left.As<BinaryExpression>().Right.As<Literal>().Value);
            Assert.Equal(BinaryOperator.Plus, binary.Left.As<BinaryExpression>().Operator);
        }

        [Theory]
        [InlineData(0, "0")]
        [InlineData(42, "42")]
        [InlineData(0.14, "0.14")]
        [InlineData(3.14159, "3.14159")]
        [InlineData(6.02214179e+23, "6.02214179e+23")]
        [InlineData(1.492417830e-10, "1.492417830e-10")]
        [InlineData(0, "0x0")]
        [InlineData(0, "0x0;")]
        [InlineData(0xabc, "0xabc")]
        [InlineData(0xdef, "0xdef")]
        [InlineData(0X1A, "0X1A")]
        [InlineData(0x10, "0x10")]
        [InlineData(0x100, "0x100")]
        [InlineData(0X04, "0X04")]
        [InlineData(02, "02")]
        [InlineData(10, "012")]
        [InlineData(10, "0012")]
        [InlineData(1.189008226412092e+38, "0x5973772948c653ac1971f1576e03c4d4")]
        [InlineData(18446744073709552000d, "0xffffffffffffffff")]
        public void ShouldParseNumericLiterals(object expected, string source)
        {
            Literal literal;

            var program = new JavaScriptParser(source).ParseProgram();
            var body = program.Body;

            Assert.Single(body);
            Assert.NotNull(literal = body.First().As<ExpressionStatement>().Expression.As<Literal>());
            Assert.Equal(Convert.ToDouble(expected), Convert.ToDouble(literal.Value));
        }

        [Theory]
        [InlineData("Hello", @"'Hello'")]
        [InlineData("\n\r\t\v\b\f\\\'\"\0", @"'\n\r\t\v\b\f\\\'\""\0'")]
        [InlineData("\u0061", @"'\u0061'")]
        [InlineData("\x61", @"'\x61'")]
        [InlineData("Hello\nworld", @"'Hello\nworld'")]
        [InlineData("Hello\\\nworld", @"'Hello\\\nworld'")]
        public void ShouldParseStringLiterals(string expected, string source)
        {
            Literal literal;

            var program = new JavaScriptParser(source).ParseProgram();
            var body = program.Body;

            Assert.Single(body);
            Assert.NotNull(literal = body.First().As<ExpressionStatement>().Expression.As<Literal>());
            Assert.Equal(expected, literal.Value);
        }

        [Theory]
        [InlineData(@"{ x
                      ++y }")]
        [InlineData(@"{ x
                      --y }")]
        [InlineData(@"var x /* comment */;
                      { var x = 14, y = 3
                      z; }")]
        [InlineData(@"while (true) { continue
                      there; }")]
        [InlineData(@"while (true) { continue // Comment
                      there; }")]
        [InlineData(@"while (true) { continue /* Multiline
                      Comment */there; }")]
        [InlineData(@"while (true) { break
                      there; }")]
        [InlineData(@"while (true) { break // Comment
                      there; }")]
        [InlineData(@"while (true) { break /* Multiline
                      Comment */there; }")]
        [InlineData(@"(function(){ return
                      x; })")]
        [InlineData(@"(function(){ return // Comment
                      x; })")]
        [InlineData(@"(function(){ return/* Multiline
                      Comment */x; })")]
        [InlineData(@"{ throw error
                      error; }")]
        [InlineData(@"{ throw error// Comment
                      error; }")]
        [InlineData(@"{ throw error/* Multiline
                      Comment */error; }")]

        public void ShouldInsertSemicolons(string source)
        {
            new JavaScriptParser(source).ParseProgram();
        }

        [Fact]
        public void ShouldProvideLocationForMultiLinesStringLiterals()
        {
            var source = @"'\
\
'
";
            var program = new JavaScriptParser(source, new ParserOptions { Loc = true }).ParseProgram();
            var expr = program.Body.First().As<ExpressionStatement>().Expression;
            Assert.Equal(1, expr.Location.Start.Line);
            Assert.Equal(0, expr.Location.Start.Column);
            Assert.Equal(3, expr.Location.End.Line);
            Assert.Equal(1, expr.Location.End.Column);
        }

        [Fact]
        public void ShouldThrowErrorForInvalidLeftHandOperation()
        {
            Assert.Throws<JavaScriptException>(() => new Engine().Execute("~ (WE0=1)--- l('1');"));
        }
    }
}
