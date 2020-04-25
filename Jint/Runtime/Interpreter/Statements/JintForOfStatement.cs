using Esprima.Ast;
using Jint.Native;
using Jint.Native.Iterator;
using Jint.Runtime.Interpreter.Expressions;
using Jint.Runtime.References;

namespace Jint.Runtime.Interpreter.Statements
{
    /// <summary>
    ///     https://www.ecma-international.org/ecma-262/6.0/#sec-for-in-and-for-of-statements
    /// </summary>
    internal sealed class JintForOfStatement : JintStatement<ForOfStatement>
    {
        private JintStatement _body;
        private JintExpression _identifier;
        private BindingPattern _leftPattern;
        private VariableDeclarationKind _variableDeclarationKind = VariableDeclarationKind.Var;

        private JintExpression _right;

        public JintForOfStatement(Engine engine, ForOfStatement statement) : base(engine, statement)
        {
            _initialized = false;
        }

        protected override void Initialize()
        {
            if (_statement.Left is VariableDeclaration variableDeclaration)
            {
                _variableDeclarationKind = variableDeclaration.Kind;
                var element = variableDeclaration.Declarations[0].Id;
                if (element is BindingPattern bindingPattern)
                {
                    _leftPattern = bindingPattern;
                }
                else
                {
                    _identifier = JintExpression.Build(_engine, (Identifier) element);
                }
            }
            else if (_statement.Left is BindingPattern bindingPattern)
            {
                _leftPattern = bindingPattern;
            }
            else
            {
                _identifier = JintExpression.Build(_engine, (Expression) _statement.Left);
            }

            _body = Build(_engine, _statement.Body);
            _right = JintExpression.Build(_engine, _statement.Right);
        }

        protected override Completion ExecuteInternal()
        {
            var experValue = _right.GetValue();

            if (!(experValue is IIterator iterator))
            {
                var obj = TypeConverter.ToObject(_engine, experValue);
                obj.TryGetIterator(_engine, out iterator);
            }

            if (iterator is null)
            {
                return ExceptionHelper.ThrowTypeError<Completion>(_engine, _identifier + " is not iterable");
            }

            var completionType = CompletionType.Normal;
            var v = JsValue.Undefined;
            var close = false;
            try
            {
                do
                {
                    iterator.TryIteratorStep(out var item);
                    var done = item.Get(CommonProperties.Done);
                    if (TypeConverter.ToBoolean(done))
                    {
                        // we can close after checks pass
                        close = true;
                        break;
                    }

                    var currentValue = item.Get(CommonProperties.Value);

                    // we can close after checks pass
                    close = true;

                    if (_leftPattern != null)
                    {
                        BindingPatternAssignmentExpression.ProcessPatterns(
                            _engine,
                            _leftPattern,
                            currentValue,
                            checkReference: !(_statement.Left is VariableDeclaration));
                    }
                    else if (_variableDeclarationKind != VariableDeclarationKind.Var)
                    {
                        var lhsRef = (Reference) _identifier.Evaluate();
                        lhsRef.InitializeReferencedBinding(currentValue);
                    }
                    else
                    {
                        var varRef = (Reference) _identifier.Evaluate();
                        _engine.PutValue(varRef, currentValue);
                    }

                    var stmt = _body.Execute();

                    if (!ReferenceEquals(stmt.Value, null))
                    {
                        v = stmt.Value;
                    }

                    if (stmt.Type == CompletionType.Break && (stmt.Identifier == null || stmt.Identifier == _statement?.LabelSet?.Name))
                    {
                        return new Completion(CompletionType.Normal, stmt.Value ?? JsValue.Undefined, null, Location);
                    }

                    if (stmt.Type != CompletionType.Continue || ((stmt.Identifier != null) && stmt.Identifier != _statement?.LabelSet?.Name))
                    {
                        if (stmt.Type != CompletionType.Normal)
                        {
                            return stmt;
                        }
                    }
                    
                } while (true);
            }
            catch
            {
                completionType = CompletionType.Throw;
                throw;
            }   
            finally
            {
                if (close)
                {
                    iterator.Close(completionType);
                }
            }

            return new Completion(CompletionType.Normal, v, null, Location);
        }
    }
}