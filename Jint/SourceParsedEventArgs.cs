using Esprima.Ast;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Jint
{
    public class SourceParsedEventArgs
    {
        public string SourceId { get; }
        public string Source { get; }
        public Script Ast { get; }

        public SourceParsedEventArgs(string source, Script ast)
        {
            SourceId = ast.Location.Source ?? Guid.NewGuid().ToString();
            Source = source;
            Ast = ast;
        }
    }
}
