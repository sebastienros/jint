using Esprima.Ast;
using Jint.Native;
using Jint.Native.Function;
using Jint.Runtime.Interpreter.Expressions;
using Jint.Runtime.References;
using System.Threading.Tasks;

namespace Jint.Runtime.Interpreter.Statements
{
    internal sealed partial class JintVariableDeclaration : JintStatement<VariableDeclaration>
    {
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