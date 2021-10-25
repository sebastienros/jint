using Esprima.Ast;
using Jint.Runtime.Interpreter.Expressions;

namespace Jint.Runtime.Interpreter.Statements
{
    /// <summary>
    /// http://www.ecma-international.org/ecma-262/5.1/#sec-12.11
    /// </summary>
    internal sealed class JintSwitchStatement : JintStatement<SwitchStatement>
    {
        private JintSwitchBlock _switchBlock;
        private JintExpression _discriminant;

        public JintSwitchStatement(SwitchStatement statement) : base(statement)
        {
        }

        protected override void Initialize(EvaluationContext context)
        {
            var engine = context.Engine;
            _switchBlock = new JintSwitchBlock(_statement.Cases);
            _discriminant = JintExpression.Build(engine, _statement.Discriminant);
        }

        protected override Completion ExecuteInternal(EvaluationContext context)
        {
            var value = _discriminant.GetValue(context).Value;
            var r = _switchBlock.Execute(context, value);
            if (r.Type == CompletionType.Break && r.Target == _statement.LabelSet?.Name)
            {
                return NormalCompletion(r.Value);
            }

            return r;
        }
    }
}