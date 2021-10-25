using Esprima.Ast;
using Jint.Native;
using Jint.Native.Function;
using Jint.Runtime.Interpreter.Expressions;
using Jint.Runtime.References;

namespace Jint.Runtime.Interpreter.Statements
{
    internal sealed class JintVariableDeclaration : JintStatement<VariableDeclaration>
    {
        private static readonly Completion VoidCompletion = new(CompletionType.Normal, null!, default);

        private ResolvedDeclaration[] _declarations;

        private sealed class ResolvedDeclaration
        {
            internal JintExpression Left;
            internal BindingPattern LeftPattern;
            internal JintExpression Init;
            internal JintIdentifierExpression LeftIdentifierExpression;
            internal bool EvalOrArguments;
        }

        public JintVariableDeclaration(VariableDeclaration statement) : base(statement)
        {
        }

        protected override void Initialize(EvaluationContext context)
        {
            var engine = context.Engine;
            _declarations = new ResolvedDeclaration[_statement.Declarations.Count];
            for (var i = 0; i < _declarations.Length; i++)
            {
                var declaration = _statement.Declarations[i];

                JintExpression left = null;
                JintExpression init = null;
                BindingPattern bindingPattern = null;

                if (declaration.Id is BindingPattern bp)
                {
                    bindingPattern = bp;
                }
                else
                {
                    left = JintExpression.Build(engine, declaration.Id);
                }

                if (declaration.Init != null)
                {
                    init = JintExpression.Build(engine, declaration.Init);
                }

                var leftIdentifier = left as JintIdentifierExpression;
                _declarations[i] = new ResolvedDeclaration
                {
                    Left = left,
                    LeftPattern = bindingPattern,
                    LeftIdentifierExpression = leftIdentifier,
                    EvalOrArguments = leftIdentifier?.HasEvalOrArguments == true,
                    Init = init
                };
            }
        }

        protected override Completion ExecuteInternal(EvaluationContext context)
        {
            var engine = context.Engine;
            foreach (var declaration in _declarations)
            {
                if (_statement.Kind != VariableDeclarationKind.Var && declaration.Left != null)
                {
                    var lhs = (Reference) declaration.Left.Evaluate(context).Value;
                    var value = JsValue.Undefined;
                    if (declaration.Init != null)
                    {
                        var completion = declaration.Init.GetValue(context);
                        value = completion.Value.Clone();
                        if (declaration.Init._expression.IsFunctionDefinition())
                        {
                            ((FunctionInstance) value).SetFunctionName(lhs.GetReferencedName());
                        }
                    }

                    lhs.InitializeReferencedBinding(value);
                    engine._referencePool.Return(lhs);
                }
                else if (declaration.Init != null)
                {
                    if (declaration.LeftPattern != null)
                    {
                        var environment = _statement.Kind != VariableDeclarationKind.Var
                            ? engine.ExecutionContext.LexicalEnvironment
                            : null;

                        var completion = declaration.Init.GetValue(context);

                        BindingPatternAssignmentExpression.ProcessPatterns(
                            context,
                            declaration.LeftPattern,
                            completion.Value,
                            environment,
                            checkObjectPatternPropertyReference: _statement.Kind != VariableDeclarationKind.Var);
                    }
                    else if (declaration.LeftIdentifierExpression == null
                             || JintAssignmentExpression.SimpleAssignmentExpression.AssignToIdentifier(
                                 context,
                                 declaration.LeftIdentifierExpression,
                                 declaration.Init,
                                 declaration.EvalOrArguments) is null)
                    {
                        // slow path
                        var lhs = (Reference) declaration.Left.Evaluate(context).Value;
                        lhs.AssertValid(engine.Realm);

                        var completion = declaration.Init.GetValue(context);
                        var value = completion.Value.Clone();

                        if (declaration.Init._expression.IsFunctionDefinition())
                        {
                            ((FunctionInstance) value).SetFunctionName(lhs.GetReferencedName());
                        }

                        engine.PutValue(lhs, value);
                        engine._referencePool.Return(lhs);
                    }
                }
            }

            return VoidCompletion;
        }
    }
}