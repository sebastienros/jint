using Esprima;
using Esprima.Ast;
using Jint.Native;
using Jint.Runtime.CallStack;
using Jint.Runtime.Environments;

namespace Jint.Runtime.Debugger
{
    public sealed class CallFrame
    {
        private readonly CallStackExecutionContext _context;
        private readonly CallStackElement? _element;
        private readonly Lazy<DebugScopes> _scopeChain;

        internal CallFrame(
            CallStackElement? element,
            in CallStackExecutionContext context,
            Location location,
            JsValue? returnValue)
        {
            _element = element;
            _context = context;
            Location = location;
            ReturnValue = returnValue;

            _scopeChain = new Lazy<DebugScopes>(() => new DebugScopes(Environment));
        }

        private EnvironmentRecord Environment => _context.LexicalEnvironment;

        // TODO: CallFrameId
        /// <summary>
        /// Name of the function of this call frame. For global scope, this will be "(anonymous)".
        /// </summary>
        public string FunctionName => _element?.ToString() ?? "(anonymous)";

        /// <summary>
        /// Source location of function of this call frame.
        /// </summary>
        /// <remarks>For top level (global) call frames, as well as functions not defined in script, this will be null.</remarks>
        public Location? FunctionLocation => (_element?.Function._functionDefinition?.Function as Node)?.Location;

        /// <summary>
        /// Currently executing source location in this call frame.
        /// </summary>
        public Location Location { get; }

        /// <summary>
        /// The scope chain of this call frame.
        /// </summary>
        public DebugScopes ScopeChain => _scopeChain.Value;

        /// <summary>
        /// The value of <c>this</c> in this call frame.
        /// </summary>
        public JsValue This
        {
            get
            {
                var environment = _context.GetThisEnvironment();
                return environment.GetThisBinding();
            }
        }

        /// <summary>
        /// The return value of this call frame. Will be null for call frames that aren't at the top of the stack,
        /// as well as if execution is not at a return point.
        /// </summary>
        public JsValue? ReturnValue { get; }
    }
}
