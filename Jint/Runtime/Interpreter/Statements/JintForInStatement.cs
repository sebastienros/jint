using System.Collections.Generic;
using Esprima.Ast;
using Jint.Native;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interpreter.Expressions;
using Jint.Runtime.References;

namespace Jint.Runtime.Interpreter.Statements
{
    /// <summary>
    ///     http://www.ecma-international.org/ecma-262/5.1/#sec-12.6.4
    /// </summary>
    internal sealed class JintForInStatement : JintStatement<ForInStatement>
    {
        private readonly JintStatement _body;
        private readonly JintExpression _identifier;
        private readonly JintExpression _right;

        public JintForInStatement(Engine engine, ForInStatement statement) : base(engine, statement)
        {
            if (_statement.Left.Type == Nodes.VariableDeclaration)
            {
                _identifier = JintExpression.Build(engine, (Esprima.Ast.Identifier) ((VariableDeclaration) _statement.Left).Declarations[0].Id);
            }
            else if (_statement.Left.Type == Nodes.MemberExpression)
            {
                _identifier = JintExpression.Build(engine, ((MemberExpression) _statement.Left));
            }
            else
            {
                _identifier = JintExpression.Build(engine, (Esprima.Ast.Identifier) _statement.Left);
            }

            _body = Build(engine, _statement.Body);
            _right = JintExpression.Build(engine, statement.Right);
        }

        protected override Completion ExecuteInternal()
        {
            var varRef = _identifier.Evaluate() as Reference;
            var experValue = _right.GetValue();
            if (experValue.IsNullOrUndefined())
            {
                return new Completion(CompletionType.Normal, null, null, Location);
            }

            var obj = TypeConverter.ToObject(_engine, experValue);
            var v = Undefined.Instance;

            // keys are constructed using the prototype chain
            var cursor = obj;
            var processedKeys = new HashSet<string>();

            while (!ReferenceEquals(cursor, null))
            {
                var keys = _engine.Object.GetOwnPropertyNames(Undefined.Instance, Arguments.From(cursor)).AsArray();

                var length = keys.GetLength();
                for (uint i = 0; i < length; i++)
                {
                    var p = keys.GetOwnProperty(i).Value.AsStringWithoutTypeCheck();

                    if (processedKeys.Contains(p))
                    {
                        continue;
                    }

                    processedKeys.Add(p);

                    // collection might be modified by inner statement
                    if (cursor.GetOwnProperty(p) == PropertyDescriptor.Undefined)
                    {
                        continue;
                    }

                    var value = cursor.GetOwnProperty(p);
                    if (!value.Enumerable)
                    {
                        continue;
                    }

                    _engine.PutValue(varRef, p);

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
                }

                cursor = cursor.Prototype;
            }

            return new Completion(CompletionType.Normal, v, null, Location);
        }
    }
}