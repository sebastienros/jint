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
        private readonly string _labelSetName;
        private readonly JintStatement _body;
        private readonly JintExpression _test;

        public JintWhileStatement(Engine engine, WhileStatement statement) : base(engine, statement)
        {
            _labelSetName = _statement?.LabelSet?.Name;
            _body = Build(engine, statement.Body);
            _test = JintExpression.Build(engine, statement.Test);
        }

        protected override Completion ExecuteInternal()
        {
            var v = Undefined.Instance;
            while (true)
            {
                var jsValue = _test.GetValue();
                if (!TypeConverter.ToBoolean(jsValue))
                {
                    return new Completion(CompletionType.Normal, v, null, Location);
                }

                var completion = _body.Execute();

                if (!ReferenceEquals(completion.Value, null))
                {
                    v = completion.Value;
                }

                if (completion.Type != CompletionType.Continue || completion.Identifier != _labelSetName)
                {
                    if (completion.Type == CompletionType.Break && (completion.Identifier == null || completion.Identifier == _labelSetName))
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