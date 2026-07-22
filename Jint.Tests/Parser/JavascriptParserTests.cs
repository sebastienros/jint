using Jint.Runtime;

namespace Jint.Tests.Parsing;

public class JavascriptParserTests
{
    [Fact]
    public void ShouldParseThis()
    {
        var program = new Parser().ParseScript("this");
        var body = program.Body;

        body.Should().ContainSingle();
        body.First().As<ExpressionStatement>().Expression.Type.Should().Be(NodeType.ThisExpression);
    }

    [Fact]
    public void ShouldParseNull()
    {
        var program = new Parser().ParseScript("null");
        var body = program.Body;

        body.Should().ContainSingle();
        body.First().As<ExpressionStatement>().Expression.Type.Should().Be(NodeType.Literal);
        body.First().As<ExpressionStatement>().Expression.As<Literal>().Value.Should().BeNull();
        body.First().As<ExpressionStatement>().Expression.As<Literal>().Raw.Should().Be("null");
    }

    [Fact]
    public void ShouldParseNumeric()
    {
        var code = @"
                42
            ";
        var program = new Parser().ParseScript(code);
        var body = program.Body;

        body.Should().ContainSingle();
        body.First().As<ExpressionStatement>().Expression.Type.Should().Be(NodeType.Literal);
        body.First().As<ExpressionStatement>().Expression.As<Literal>().Value.Should().Be(42d);
        body.First().As<ExpressionStatement>().Expression.As<Literal>().Raw.Should().Be("42");
    }

    [Fact]
    public void ShouldParseBinaryExpression()
    {
        BinaryExpression binary;

        var program = new Parser().ParseScript("(1 + 2 ) * 3");
        var body = program.Body;

        body.Should().ContainSingle();
        (binary = body.First().As<ExpressionStatement>().Expression.As<BinaryExpression>()).Should().NotBeNull();
        binary.Right.As<Literal>().Value.Should().Be(3d);
        binary.Operator.Should().Be(Operator.Multiplication);
        binary.Left.As<BinaryExpression>().Left.As<Literal>().Value.Should().Be(1d);
        binary.Left.As<BinaryExpression>().Right.As<Literal>().Value.Should().Be(2d);
        binary.Left.As<BinaryExpression>().Operator.Should().Be(Operator.Addition);
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
    public void ShouldParseNumericLiterals(object expected, string code)
    {
        Literal literal;

        var program = new Parser().ParseScript(code);
        var body = program.Body;

        body.Should().ContainSingle();
        (literal = body.First().As<ExpressionStatement>().Expression.As<Literal>()).Should().NotBeNull();
        Convert.ToDouble(literal.Value).Should().Be(Convert.ToDouble(expected));
    }

    [Theory]
    [InlineData("Hello", @"'Hello'")]
    [InlineData("\n\r\t\v\b\f\\\'\"\0", @"'\n\r\t\v\b\f\\\'\""\0'")]
    [InlineData("\u0061", @"'\u0061'")]
    [InlineData("\x61", @"'\x61'")]
    [InlineData("Hello\nworld", @"'Hello\nworld'")]
    [InlineData("Hello\\\nworld", @"'Hello\\\nworld'")]
    public void ShouldParseStringLiterals(string expected, string code)
    {
        Literal literal;

        var program = new Parser().ParseScript(code);
        var body = program.Body;

        body.Should().ContainSingle();
        (literal = body.First().As<ExpressionStatement>().Expression.As<Literal>()).Should().NotBeNull();
        literal.Value.Should().Be(expected);
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

    public void ShouldInsertSemicolons(string code)
    {
        new Parser().ParseScript(code);
    }

    [Fact]
    public void ShouldProvideLocationForMultiLinesStringLiterals()
    {
        const string Code = @"'\
\
'
";
        var program = new Parser().ParseScript(Code);
        var expr = program.Body.First().As<ExpressionStatement>().Expression;
        expr.Location.Start.Line.Should().Be(1);
        expr.Location.Start.Column.Should().Be(0);
        expr.Location.End.Line.Should().Be(3);
        expr.Location.End.Column.Should().Be(1);
    }

    [Fact]
    public void ShouldThrowErrorForInvalidLeftHandOperation()
    {
        var ex = Invoking(() => new Engine().Execute("~ (WE0=1)--- l('1');")).Should().ThrowExactly<JavaScriptException>().Which;
        ex.Message.Should().Be("Invalid left-hand side expression in postfix operation (<anonymous>:1:4)");
    }


    [Theory]
    [InlineData("....")]
    [InlineData("while")]
    [InlineData("var")]
    [InlineData("-.-")]
    public void ShouldThrowParseErrorExceptionForInvalidCode(string code)
    {
        Invoking(() => new Parser().ParseScript(code)).Should().ThrowExactly<Acornima.SyntaxErrorException>();
    }
}
