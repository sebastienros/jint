using Esprima.Ast;

namespace Jint.Runtime.Interpreter
{
    /// <summary>
    /// Per Engine.Evalute() call context.
    /// </summary>
    internal sealed class EvaluationContext
    {
        private readonly bool _shouldRunBeforeExecuteStatementChecks;

        public EvaluationContext(Engine engine, in Completion? resumedCompletion = null)
        {
            Engine = engine;
            ResumedCompletion = resumedCompletion ?? default; // TODO later
            OperatorOverloadingAllowed = engine.Options.Interop.AllowOperatorOverloading;
            _shouldRunBeforeExecuteStatementChecks = engine._constraints.Length > 0 || engine._isDebugMode;
        }

        public Engine Engine { get; }
        public Completion ResumedCompletion { get; }
        public bool DebugMode => Engine._isDebugMode;

        public SyntaxElement LastSyntaxElement { get; set; } = null!;
        public bool OperatorOverloadingAllowed { get; }

        public void RunBeforeExecuteStatementChecks(Statement statement)
        {
            if (_shouldRunBeforeExecuteStatementChecks)
            {
                Engine.RunBeforeExecuteStatementChecks(statement);
            }
        }
    }
}
