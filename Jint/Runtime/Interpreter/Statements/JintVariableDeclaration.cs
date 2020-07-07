using Esprima.Ast;
using Jint.Native;
using Jint.Native.Function;
using Jint.Runtime.Interpreter.Expressions;
using Jint.Runtime.References;
using System.Threading.Tasks;

namespace Jint.Runtime.Interpreter.Statements
{
    internal sealed class JintVariableDeclaration : JintStatement<VariableDeclaration>
    {
        private static readonly Completion VoidCompletion = new Completion(CompletionType.Normal, null, null, default);

        private ResolvedDeclaration[] _declarations;

        private sealed class ResolvedDeclaration
        {
            internal JintExpression Left;
            internal BindingPattern LeftPattern;
            internal JintExpression Init;
            internal JintIdentifierExpression LeftIdentifierExpression;
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

                JintExpression left = null;
                JintExpression init = null;
                BindingPattern bindingPattern = null;

                if (declaration.Id is BindingPattern bp)
                {
                    bindingPattern = bp;
                }
                else
                {
                    left = JintExpression.Build(_engine, declaration.Id);
                }
                
                if (declaration.Init != null)
                {
                    init = JintExpression.Build(_engine, declaration.Init);
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

        protected override Completion ExecuteInternal()
        {
            if (!_initialized)
            {
                _initialized = true;
                Initialize();
            }
            
            foreach (var declaration in _declarations)
            {
                if (_statement.Kind != VariableDeclarationKind.Var && declaration.Left != null)
                {
                    var lhs = (Reference) declaration.Left.Evaluate();
                    var value = JsValue.Undefined;
                    if (declaration.Init != null)
                    {
                        value = declaration.Init.GetValue().Clone();
                        if (declaration.Init._expression.IsFunctionWithName())
                        {
                            ((FunctionInstance) value).SetFunctionName(lhs.GetReferencedName());
                        }
                    }

                    lhs.InitializeReferencedBinding(value);
                    _engine._referencePool.Return(lhs);
                }
                else if (declaration.Init != null)
                {
                    if (declaration.LeftPattern != null)
                    {
                        var environment = _statement.Kind != VariableDeclarationKind.Var
                            ? _engine.ExecutionContext.LexicalEnvironment
                            : null;

                        BindingPatternAssignmentExpression.ProcessPatterns(
                            _engine,
                            declaration.LeftPattern,
                            declaration.Init.GetValue(),
                            environment,
                            checkObjectPatternPropertyReference: _statement.Kind != VariableDeclarationKind.Var);
                    }
                    else if (declaration.LeftIdentifierExpression == null
                             || JintAssignmentExpression.SimpleAssignmentExpression.AssignToIdentifier(
                                 _engine,
                                 declaration.LeftIdentifierExpression,
                                 declaration.Init,
                                 declaration.EvalOrArguments) is null)
                    {
                        // slow path
                        var lhs = (Reference)declaration.Left.Evaluate();
                        lhs.AssertValid(_engine);

                        var value = declaration.Init.GetValue().Clone();

                        if (declaration.Init._expression.IsFunctionWithName())
                        {
                            ((FunctionInstance)value).SetFunctionName(lhs.GetReferencedName());
                        }

                        _engine.PutValue(lhs, value);
                        _engine._referencePool.Return(lhs);
                    }
                }
            }

            return VoidCompletion;
        }

        protected async override Task<Completion> ExecuteInternalAsync()
        {
            if (!_initialized)
            {
                _initialized = true;
                Initialize();
            }

            foreach (var declaration in _declarations)
            {
                if (_statement.Kind != VariableDeclarationKind.Var && declaration.Left != null)
                {
                    var lhs = (Reference) await declaration.Left.EvaluateAsync();
                    var value = JsValue.Undefined;
                    if (declaration.Init != null)
                    {
                        value = declaration.Init.GetValue().Clone();
                        if (declaration.Init._expression.IsFunctionWithName())
                        {
                            ((FunctionInstance)value).SetFunctionName(lhs.GetReferencedName());
                        }
                    }

                    lhs.InitializeReferencedBinding(value);
                    _engine._referencePool.Return(lhs);
                }
                else if (declaration.Init != null)
                {
                    if (declaration.LeftPattern != null)
                    {
                        var environment = _statement.Kind != VariableDeclarationKind.Var
                            ? _engine.ExecutionContext.LexicalEnvironment
                            : null;

                        BindingPatternAssignmentExpression.ProcessPatterns(
                            _engine,
                            declaration.LeftPattern,
                            declaration.Init.GetValue(),
                            environment,
                            checkObjectPatternPropertyReference: _statement.Kind != VariableDeclarationKind.Var);
                    }
                    else if (declaration.LeftIdentifierExpression == null
                             || await JintAssignmentExpression.SimpleAssignmentExpression.AssignToIdentifierAsync(
                                 _engine,
                                 declaration.LeftIdentifierExpression,
                                 declaration.Init,
                                 declaration.EvalOrArguments) is null)
                    {
                        // slow path
                        var lhs = (Reference) await declaration.Left.EvaluateAsync();
                        lhs.AssertValid(_engine);

                        var value = declaration.Init.GetValue().Clone();

                        if (declaration.Init._expression.IsFunctionWithName())
                        {
                            ((FunctionInstance)value).SetFunctionName(lhs.GetReferencedName());
                        }

                        _engine.PutValue(lhs, value);
                        _engine._referencePool.Return(lhs);
                    }
                }
            }

            return VoidCompletion;
        }
    }
}