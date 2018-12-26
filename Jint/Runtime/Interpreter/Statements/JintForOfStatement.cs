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
        private readonly JintExpression _identifier;
        private readonly BindingPattern _leftPattern;

        private readonly JintStatement _body;

        private readonly JintExpression _right;

        public JintForOfStatement(Engine engine, ForOfStatement statement) : base(engine, statement)
        {
            if (_statement.Left is VariableDeclaration variableDeclaration)
            {
                var element = variableDeclaration.Declarations[0].Id;
                if (element is BindingPattern bindingPattern)
                {
                    _leftPattern = bindingPattern;
                }
                else
                {
                    _identifier = JintExpression.Build(engine, (Identifier) element);
                }
            }
            else if (statement.Left is BindingPattern bindingPattern)
            {
                _leftPattern = bindingPattern;
            }
            else
            {
                _identifier = JintExpression.Build(engine, (Expression) _statement.Left);
            }

            _body = Build(engine, _statement.Body);
            _right = JintExpression.Build(engine, statement.Right);
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

            var v = JsValue.Undefined;
            bool close = false;
            try
            {
                do
                {
                    iterator.TryIteratorStep(out var item);
                    if (item.TryGetValue("done", out var done) && TypeConverter.ToBoolean(done))
                    {
                        // we can close after checks pass
                        close = true;
                        break;
                    }

                    if (!item.TryGetValue("value", out var currentValue))
                    {
                        currentValue = JsValue.Undefined;
                    }
                    
                    // we can close after checks pass
                    close = true;

                    if (_leftPattern != null)
                    {
                        BindingPatternAssignmentExpression.ProcessPatterns(
                            _engine, 
                            _leftPattern,
                            currentValue,
                            checkReference: false /* we are doing assignment */);
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

                    if (stmt.Type == CompletionType.Break)
                    {
                        return new Completion(CompletionType.Normal, v, null, Location);
                    }

                    if (stmt.Type != CompletionType.Continue)
                    {
                        if (stmt.Type != CompletionType.Normal)
                        {
                            return stmt;
                        }
                    }

                } while (true);
            }
            finally
            {
                if (close)
                {
                    iterator.Close(CompletionType.Normal);
                }
            }

            return new Completion(CompletionType.Normal, v, null, Location);
        }
    }
}