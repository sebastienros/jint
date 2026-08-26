using Jint.Runtime;

namespace Jint.Tests.Parsing;

public class JavascriptParserTests
{
    [Test]
    public void ShouldParseThis()
    {
        var program = new Parser().ParseScript("this");
        var body = program.Body;

        body.Should().ContainSingle();
        body.First().As<ExpressionStatement>().Expression.Type.Should().Be(NodeType.ThisExpression);
    }

    [Test]
    public void ShouldParseNull()
    {
        var program = new Parser().ParseScript("null");
        var body = program.Body;

        body.Should().ContainSingle();
        body.First().As<ExpressionStatement>().Expression.Type.Should().Be(NodeType.Literal);
        body.First().As<ExpressionStatement>().Expression.As<Literal>().Value.Should().BeNull();
        body.First().As<ExpressionStatement>().Expression.As<Literal>().Raw.Should().Be("null");
    }

    [Test]
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

    [Test]
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

    [TestCase(0, "0")]
    [TestCase(42, "42")]
    [TestCase(0.14, "0.14")]
    [TestCase(3.14159, "3.14159")]
    [TestCase(6.02214179e+23, "6.02214179e+23")]
    [TestCase(1.492417830e-10, "1.492417830e-10")]
    [TestCase(0, "0x0")]
    [TestCase(0, "0x0;")]
    [TestCase(0xabc, "0xabc")]
    [TestCase(0xdef, "0xdef")]
    [TestCase(0X1A, "0X1A")]
    [TestCase(0x10, "0x10")]
    [TestCase(0x100, "0x100")]
    [TestCase(0X04, "0X04")]
    [TestCase(02, "02")]
    [TestCase(10, "012")]
    [TestCase(10, "0012")]
    [TestCase(1.189008226412092e+38, "0x5973772948c653ac1971f1576e03c4d4")]
    [TestCase(18446744073709552000d, "0xffffffffffffffff")]
    public void ShouldParseNumericLiterals(object expected, string code)
    {
        Literal literal;

        var program = new Parser().ParseScript(code);
        var body = program.Body;

        body.Should().ContainSingle();
        (literal = body.First().As<ExpressionStatement>().Expression.As<Literal>()).Should().NotBeNull();
        Convert.ToDouble(literal.Value).Should().Be(Convert.ToDouble(expected));
    }

    [TestCase("Hello", @"'Hello'")]
    [TestCase("\n\r\t\v\b\f\\\'\"\0", @"'\n\r\t\v\b\f\\\'\""\0'")]
    [TestCase("\u0061", @"'\u0061'")]
    [TestCase("\x61", @"'\x61'")]
    [TestCase("Hello\nworld", @"'Hello\nworld'")]
    [TestCase("Hello\\\nworld", @"'Hello\\\nworld'")]
    public void ShouldParseStringLiterals(string expected, string code)
    {
        Literal literal;

        var program = new Parser().ParseScript(code);
        var body = program.Body;

        body.Should().ContainSingle();
        (literal = body.First().As<ExpressionStatement>().Expression.As<Literal>()).Should().NotBeNull();
        literal.Value.Should().Be(expected);
    }

    [TestCase(@"{ x
                      ++y }")]
    [TestCase(@"{ x
                      --y }")]
    [TestCase(@"var x /* comment */;
                      { var x = 14, y = 3
                      z; }")]
    [TestCase(@"while (true) { continue
                      there; }")]
    [TestCase(@"while (true) { continue // Comment
                      there; }")]
    [TestCase(@"while (true) { continue /* Multiline
                      Comment */there; }")]
    [TestCase(@"while (true) { break
                      there; }")]
    [TestCase(@"while (true) { break // Comment
                      there; }")]
    [TestCase(@"while (true) { break /* Multiline
                      Comment */there; }")]
    [TestCase(@"(function(){ return
                      x; })")]
    [TestCase(@"(function(){ return // Comment
                      x; })")]
    [TestCase(@"(function(){ return/* Multiline
                      Comment */x; })")]
    [TestCase(@"{ throw error
                      error; }")]
    [TestCase(@"{ throw error// Comment
                      error; }")]
    [TestCase(@"{ throw error/* Multiline
                      Comment */error; }")]

    public void ShouldInsertSemicolons(string code)
    {
        new Parser().ParseScript(code);
    }

    [Test]
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

    [Test]
    public void ShouldThrowErrorForInvalidLeftHandOperation()
    {
        var ex = Invoking(() => new Engine().Execute("~ (WE0=1)--- l('1');")).Should().ThrowExactly<JavaScriptException>().Which;
        ex.Message.Should().Be("Invalid left-hand side expression in postfix operation (<anonymous>:1:4)");
    }


    [TestCase("....")]
    [TestCase("while")]
    [TestCase("var")]
    [TestCase("-.-")]
    public void ShouldThrowParseErrorExceptionForInvalidCode(string code)
    {
        Invoking(() => new Parser().ParseScript(code)).Should().ThrowExactly<Acornima.SyntaxErrorException>();
    }
}
