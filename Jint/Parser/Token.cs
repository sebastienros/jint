namespace Jint.Parser
{
    public enum Tokens
    {
        BooleanLiteral = 1,
        EOF = 2,
        Identifier = 3,
        Keyword = 4,
        NullLiteral = 5,
        NumericLiteral = 6,
        Punctuator = 7,
        StringLiteral = 8,
        RegularExpression = 9
    };

    public class Token
    {
        public static Token Empty = new Token();

        public Tokens Type;
        public string Literal;
        public object Value;
        public int[] Range;
        public int? LineNumber;
        public int LineStart;
        public bool Octal;
        public Location Location;
        public int Precedence;
    }
}
