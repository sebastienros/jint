using Esprima.Ast;

namespace Jint.Runtime.Interpreter.Statements
{
    internal sealed class JintLabeledStatement : JintStatement<LabeledStatement>
    {
        private readonly JintStatement _body;
        private readonly string _labelName;

        public JintLabeledStatement(Engine engine, LabeledStatement statement) : base(engine, statement)
        {
            _body = Build(engine, statement.Body);
            _labelName = statement.Label.Name;
        }

        protected override Completion ExecuteInternal()
        {
            // TODO: Esprima added Statement.Label, maybe not necessary as this line is finding the
            // containing label and could keep a table per program with all the labels
            // labeledStatement.Body.LabelSet = labeledStatement.Label;
            var result = _body.Execute();
            if (result.Type == CompletionType.Break && result.Identifier == _labelName)
            {
                var value = result.Value;
                return new Completion(CompletionType.Normal, value, null, Location);
            }

            return result;
        }
    }
}