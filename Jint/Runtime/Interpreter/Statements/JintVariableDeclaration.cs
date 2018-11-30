using Esprima.Ast;
using Jint.Native;
using Jint.Runtime.Interpreter.Expressions;
using Jint.Runtime.References;

namespace Jint.Runtime.Interpreter.Statements
{
    internal sealed class JintVariableDeclaration : JintStatement<VariableDeclaration>
    {
        private Pair[] _declarations;

        private sealed class Pair
        {
            internal JintExpression Left;
            internal JintExpression Init;
            internal JsValue Value;
        }

        public JintVariableDeclaration(Engine engine, VariableDeclaration statement) : base(engine, statement)
        {
        }

        protected override void Initialize()
        {
            _declarations = new Pair[_statement.Declarations.Count];
            for (var i = 0; i < _declarations.Length; i++)
            {
                var declaration = _statement.Declarations[i];
                var left = declaration.Init != null
                    ? JintExpression.Build(_engine, (Expression) declaration.Id)
                    : null;
                var init = declaration.Init != null
                    ? JintExpression.Build(_engine, declaration.Init)
                    : null;

                _declarations[i] = new Pair
                {
                    Left = left,
                    Init = init,
                    Value = JintExpression.FastResolve(init)
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
                    var lhs = (Reference) declaration.Left.Evaluate();
                    lhs.AssertValid(_engine);

                    var value = declaration.Value ?? _engine.GetValue(declaration.Init.Evaluate(), true);
                    _engine.PutValue(lhs, value);
                    _engine._referencePool.Return(lhs);
                }
            }

            return new Completion(CompletionType.Normal, Undefined.Instance, null);
        }
    }
}