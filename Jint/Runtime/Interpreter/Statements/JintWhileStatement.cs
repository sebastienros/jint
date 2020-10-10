using Esprima.Ast;
using Jint.Native;
using Jint.Runtime.Interpreter.Expressions;

namespace Jint.Runtime.Interpreter.Statements
{
    /// <summary>
    /// http://www.ecma-international.org/ecma-262/5.1/#sec-12.6.2
    /// </summary>
    internal sealed class JintWhileStatement : JintStatement<WhileStatement>
    {
        private string _labelSetName;
        private JintStatement _body;
        private JintExpression _test;

        public JintWhileStatement(WhileStatement statement) : base(statement)
        {
        }

        protected override void Initialize(EvaluationContext context)
        {
            _labelSetName = _statement.LabelSet?.Name;
            _body = Build(_statement.Body);
            _test = JintExpression.Build(context.Engine, _statement.Test);
        }

        protected override Completion ExecuteInternal(EvaluationContext context)
        {
            var v = Undefined.Instance;
            while (true)
            {
                var jsValue = _test.GetValue(context).Value;
                if (!TypeConverter.ToBoolean(jsValue))
                {
                    return new Completion(CompletionType.Normal, v, null, Location);
                }

                var completion = _body.Execute(context);

                if (!ReferenceEquals(completion.Value, null))
                {
                    v = completion.Value;
                }

                if (completion.Type != CompletionType.Continue || completion.Target != _labelSetName)
                {
                    if (completion.Type == CompletionType.Break && (completion.Target == null || completion.Target == _labelSetName))
                    {
                        return new Completion(CompletionType.Normal, v, null, Location);
                    }

                    if (completion.Type != CompletionType.Normal)
                    {
                        return completion;
                    }
                }
            }
        }
    }
}