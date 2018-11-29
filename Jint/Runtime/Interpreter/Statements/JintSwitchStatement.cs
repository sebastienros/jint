using Esprima.Ast;
using Jint.Runtime.Interpreter.Expressions;

namespace Jint.Runtime.Interpreter.Statements
{
    /// <summary>
    /// http://www.ecma-international.org/ecma-262/5.1/#sec-12.11
    /// </summary>
    internal sealed class JintSwitchStatement : JintStatement<SwitchStatement>
    {
        private readonly JintSwitchBlock _switchBlock;
        private readonly JintExpression _discriminant;

        public JintSwitchStatement(Engine engine, SwitchStatement statement) : base(engine, statement)
        {
            _switchBlock = new JintSwitchBlock(engine, _statement.Cases);
            _discriminant = JintExpression.Build(engine, _statement.Discriminant);
        }

        public override Completion Execute()
        {
            var jsValue = _engine.GetValue(_discriminant.Evaluate(), true);
            var r = _switchBlock.Execute(jsValue);
            if (r.Type == CompletionType.Break && r.Identifier == _statement.LabelSet?.Name)
            {
                return new Completion(CompletionType.Normal, r.Value, null);
            }

            return r;
        }
    }
}