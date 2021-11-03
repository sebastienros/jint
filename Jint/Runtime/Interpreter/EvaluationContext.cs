using Esprima.Ast;

namespace Jint.Runtime.Interpreter
{
    /// <summary>
    /// Per Engine.Evalute() call context.
    /// </summary>
    internal sealed class EvaluationContext
    {
        public EvaluationContext(Engine engine, in Completion? resumedCompletion = null)
        {
            Engine = engine;
            DebugMode = engine._isDebugMode;
            ResumedCompletion = resumedCompletion ?? default; // TODO later
            OperatorOverloadingAllowed = engine.Options.Interop.AllowOperatorOverloading;
        }

        public Engine Engine { get; }
        public Completion ResumedCompletion { get; }
        public bool DebugMode { get; }

        public Node LastSyntaxNode { get; set; }
        public bool OperatorOverloadingAllowed { get; }
    }
}