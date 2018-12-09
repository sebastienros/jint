using Esprima.Ast;
using Jint.Native;
using Jint.Runtime.Interpreter.Expressions;
using Jint.Runtime.References;

namespace Jint.Runtime.Interpreter.Statements
{
    internal sealed class JintVariableDeclaration : JintStatement<VariableDeclaration>
    {
        private static readonly Completion VoidCompletion = new Completion(CompletionType.Normal, Undefined.Instance, null);

        private ResolvedDeclaration[] _declarations;

        private sealed class ResolvedDeclaration
        {
            internal JintExpression Left;
            internal JintExpression Init;
            internal JintIdentifierExpression LeftIdentifier;
            internal bool EvalOrArguments;
        }

        public JintVariableDeclaration(Engine engine, VariableDeclaration statement) : base(engine, statement)
        {
            _initialized = false;
        }

        protected override void Initialize()
        {
            _declarations = new ResolvedDeclaration[_statement.Declarations.Count];
            for (var i = 0; i < _declarations.Length; i++)
            {
                var declaration = _statement.Declarations[i];
                var left = declaration.Init != null
                    ? JintExpression.Build(_engine, (Expression) declaration.Id)
                    : null;
                var init = declaration.Init != null
                    ? JintExpression.Build(_engine, declaration.Init)
                    : null;

                var leftIdentifier = left as JintIdentifierExpression;
                _declarations[i] = new ResolvedDeclaration
                {
                    Left = left,
                    LeftIdentifier = leftIdentifier,
                    EvalOrArguments = leftIdentifier?._expressionName == "eval" || leftIdentifier?._expressionName == "arguments",
                    Init = init
                };
            }
        }

        protected override Completion ExecuteInternal()
        {
            var declarations = _declarations;
            for (var i = 0; i < (uint) declarations.Length; i++)
            {
                var declaration = declarations[i];
                if (declaration.Init != null)
                {
                    if (declaration.LeftIdentifier == null
                        || JintAssignmentExpression.Assignment.AssignToIdentifier(
                            _engine,
                            declaration.LeftIdentifier,
                            declaration.Init,
                            declaration.EvalOrArguments) is null)
                    {
                        // slow path
                        var lhs = (Reference) declaration.Left.Evaluate();
                        lhs.AssertValid(_engine);

                        var value = declaration.Init.GetValue();
                        _engine.PutValue(lhs, value);
                        _engine._referencePool.Return(lhs);
                    }
                }
            }

            return VoidCompletion;
        }
    }
}