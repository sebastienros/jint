using System.Collections.Generic;
using Jint.Parser.Ast;

namespace Jint.Parser
{
    public interface IFunctionDeclaration : IFunctionScope
    {
        Identifier Id { get; }
        IEnumerable<Identifier> Parameters { get; }
        Statement Body { get; }
        bool Strict { get; }
    }
}