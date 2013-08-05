using Jint.Parser.Ast;

namespace Jint
{
    public class WithStatement : Statement
    {
        public object obj;
        public Statement body;
    }
}