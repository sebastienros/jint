using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using Jint.Parser;
using Jint.Parser.Ast;
using Xunit;
using Xunit.Extensions;

namespace Jint.Tests.Parser
{
    public class JavascriptParserTests
    {
        private readonly JavaScriptParser _parser = new JavaScriptParser();

        [Theory]
        [InlineData("jQuery.js", "1.9.1")]
        [InlineData("underscore.js", "1.5.2")]
        [InlineData("backbone.js", "1.1.0")]
        [InlineData("mootools.js", "1.4.5")]
        [InlineData("angular.js", "1.2.5")]
        [InlineData("JSXTransformer.js", "0.10.0")]
        [InlineData("handlebars.js", "2.0.0")]
        public void ShouldParseScriptFile(string file, string version)
        {
            const string prefix = "Jint.Tests.Parser.Scripts.";

            var assembly = Assembly.GetExecutingAssembly();
            var scriptPath = prefix + file;
            var sw = new Stopwatch();

            using (var stream = assembly.GetManifestResourceStream(scriptPath))
            {
                if (stream != null)
                {
                    using (var sr = new StreamReader(stream))
                    {
                        var source = sr.ReadToEnd();
                        sw.Restart();
                        var parser = new JavaScriptParser();
                        var program = parser.Parse(source);
                        Console.WriteLine("Parsed {0} {1} ({3} KB) in {2} ms", file, version, sw.ElapsedMilliseconds, (int)source.Length/1024);
                        Assert.NotNull(program);
                    }
                }
            }
        }
        
        [Fact]
        public void ShouldParseThis()
        {
            var program = _parser.Parse("this");
            var body = program.Body;

            Assert.NotNull(body);
            Assert.Equal(1, body.Count());
            Assert.Equal(SyntaxNodes.ThisExpression, body.First().As<ExpressionStatement>().Expression.Type);
        }

        [Fact]
        public void ShouldParseNull()
        {
            var program = _parser.Parse("null");
            var body = program.Body;

            Assert.NotNull(body);
            Assert.Equal(1, body.Count());
            Assert.Equal(SyntaxNodes.Literal, body.First().As<ExpressionStatement>().Expression.Type);
            Assert.Equal(null, body.First().As<ExpressionStatement>().Expression.As<Literal>().Value);
            Assert.Equal("null", body.First().As<ExpressionStatement>().Expression.As<Literal>().Raw);
        }

        [Fact]
        public void ShouldParseNumeric()
        {
            var program = _parser.Parse(
                @"
                42
            ");
            var body = program.Body;

            Assert.NotNull(body);
            Assert.Equal(1, body.Count());
            Assert.Equal(SyntaxNodes.Literal, body.First().As<ExpressionStatement>().Expression.Type);
            Assert.Equal(42d, body.First().As<ExpressionStatement>().Expression.As<Literal>().Value);
            Assert.Equal("42", body.First().As<ExpressionStatement>().Expression.As<Literal>().Raw);
        }

        [Fact]
        public void ShouldParseBinaryExpression()
        {
            BinaryExpression binary;

            var program = _parser.Parse("(1 + 2 ) * 3");
            var body = program.Body;

            Assert.NotNull(body);
            Assert.Equal(1, body.Count());
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
        public void ShouldParseNumericLiterals(object expected, string source)
        {
            Literal literal;

            var program = _parser.Parse(source);
            var body = program.Body;

            Assert.NotNull(body);
            Assert.Equal(1, body.Count());
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

            var program = _parser.Parse(source);
            var body = program.Body;

            Assert.NotNull(body);
            Assert.Equal(1, body.Count());
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
            var program = _parser.Parse(source);
            var body = program.Body;

            Assert.NotNull(body);
        }

        [Fact]
        public void ShouldProvideLocationForMultiLinesStringLiterals()
        {
            var source = @"'\
\
'
";
            var program = _parser.Parse(source);
            var expr = program.Body.First().As<ExpressionStatement>().Expression;
            Assert.Equal(1, expr.Location.Start.Line);
            Assert.Equal(0, expr.Location.Start.Column);
            Assert.Equal(3, expr.Location.End.Line);
            Assert.Equal(1, expr.Location.End.Column);
        }

    }
}
