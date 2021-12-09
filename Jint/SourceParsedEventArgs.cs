using Esprima.Ast;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Jint
{
    public sealed class SourceParsedEventArgs
    {
        public SourceParsedEventArgs(string source, Script ast)
        {
            SourceId = ast.Location.Source ?? Guid.NewGuid().ToString();
            Source = source;
            Ast = ast;
        }

        public string SourceId { get; }
        public string Source { get; }
        public Script Ast { get; }
    }
}
