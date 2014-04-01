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

    public struct Token
    {
        public static Token Empty = new Token();

        public Tokens Type;
        public string Literal;
        public object Value;
        public int Start;
        public int End;
        public int? LineNumber;
        public int LineStart;
        public bool Octal;
        public Location Location;
        public int Precedence;

        public int StartLineNumber;
        public int StartLineStart;

        public override bool Equals(object obj)
        {
            return base.Equals(obj);
        }

        public bool Equals(Token other)
        {
            return Type == other.Type && string.Equals(Literal, other.Literal) && Equals(Value, other.Value) && Start == other.Start && End == other.End && LineNumber == other.LineNumber && LineStart == other.LineStart && Octal.Equals(other.Octal) && Equals(Location, other.Location) && Precedence == other.Precedence;
        }

        public static bool operator ==(Token lhs, Token rhs)
        {
            return lhs.Equals(rhs);
        }

        public static bool operator !=(Token lhs, Token rhs)
        {
            return !lhs.Equals(rhs);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = (int)Type;
                hashCode = (hashCode * 397) ^ (Literal != null ? Literal.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (Value != null ? Value.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ Start;
                hashCode = (hashCode * 397) ^ End;
                hashCode = (hashCode * 397) ^ LineNumber.GetHashCode();
                hashCode = (hashCode * 397) ^ LineStart;
                hashCode = (hashCode * 397) ^ Octal.GetHashCode();
                hashCode = (hashCode * 397) ^ (Location != null ? Location.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ Precedence;
                return hashCode;
            }
        }

    }
}
