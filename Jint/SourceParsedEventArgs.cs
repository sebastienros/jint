using Esprima.Ast;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Jint
{
    /// <summary>
    /// Event arguments for the <see cref="Engine.Parsed" /> event, containing the results of the engine's parsing
    /// of the script (AST etc.)
    /// </summary>
    public sealed class SourceParsedEventArgs
    {
        public SourceParsedEventArgs(Script ast)
        {
            SourceId = ast.Location.Source ?? Guid.NewGuid().ToString();
            Ast = ast;
        }

        /// <summary>
        /// The source ID of the parsed script.
        /// </summary>
        /// <remarks>
        /// This corresponds to the source passed to Esprima through ParserOptions - which is stored on each AST
        /// node's Location.Source property. I.e, it's user defined, and is a string describing where the code is
        /// coming from.
        /// </remarks>
        public string SourceId { get; }

        /// <summary>
        /// The AST result from the parser.
        /// </summary>
        public Script Ast { get; }
    }
}
