using Esprima.Ast;
using Jint.Native;
using Jint.Runtime.Interpreter.Expressions;
using Jint.Runtime.References;

namespace Jint.Runtime.Interpreter.Statements
{
    internal sealed class JintVariableDeclaration : JintStatement<VariableDeclaration>
    {
        private readonly Pair[] _declarations;

        private sealed class Pair
        {
            internal JintExpression Left;
            internal JintExpression Init;
        }

        public JintVariableDeclaration(Engine engine, VariableDeclaration statement) : base(engine, statement)
        {
            _declarations = new Pair[statement.Declarations.Count];
        }

        protected override Completion ExecuteInternal()
        {
            var declarationsCount = _statement.Declarations.Count;
            for (var i = 0; i < declarationsCount; i++)
            {
                var declaration = _declarations[i] ?? (_declarations[i] = new Pair
                {
                    Left = _statement.Declarations[i].Init != null ? JintExpression.Build(_engine, (Expression) _statement.Declarations[i].Id) : null, Init = _statement.Declarations[i].Init != null ? JintExpression.Build(_engine, _statement.Declarations[i].Init) : null
                });

                if (declaration.Init != null)
                {
                    var lhs = (Reference) declaration.Left.Evaluate();
                    lhs.AssertValid(_engine);

                    var value = _engine.GetValue(declaration.Init.Evaluate(), true);
                    _engine.PutValue(lhs, value);
                    _engine._referencePool.Return(lhs);
                }
            }

            return new Completion(CompletionType.Normal, Undefined.Instance, null);
        }
    }
}