namespace Jint.Runtime.Interpreter.Statements;

internal sealed class JintLabeledStatement : JintStatement<LabeledStatement>
{
    private readonly JintStatement _body;
    private readonly string _labelName;

    public JintLabeledStatement(LabeledStatement statement, bool blockLevel = false) : base(statement)
    {
        // A label chain sitting in a Block / CaseClause StatementList keeps its item block-level, so a
        // labelled function declaration still writes Annex B's var-scope alias when it is evaluated.
        _body = blockLevel ? BuildBlockLevel(statement.Body) : Build(statement.Body);
        _labelName = statement.Label.Name;

        // LabelledEvaluation evaluates the item under a stack of labels with all of them, but Acornima records only the
        // innermost on that item (Statement.LabelSet), and nothing on a label says what is stacked above it. So the
        // outermost label of a stack - the one no label sits directly over - passes the rest down, once.
        if (statement.LabelSet is null && _body is JintLabeledStatement)
        {
            PassStackedLabelsDown();
        }
    }

    /// <summary>
    /// Hands the item under this label stack every label above its own, the rest of LabelledEvaluation's newLabelSet.
    /// https://tc39.es/ecma262/#sec-runtime-semantics-labelledevaluation
    /// </summary>
    private void PassStackedLabelsDown()
    {
        var labels = new List<string>();
        JintStatement item = this;
        while (item is JintLabeledStatement labelled)
        {
            labels.Add(labelled._labelName);
            item = labelled._body;
        }

        // The last label is the one Acornima recorded on the item itself.
        labels.RemoveAt(labels.Count - 1);
        item.SetEnclosingLabels(labels.ToArray());
    }

    protected override Completion ExecuteInternal(EvaluationContext context)
    {
        // TODO: Esprima added Statement.Label, maybe not necessary as this line is finding the
        // containing label and could keep a table per program with all the labels
        // labeledStatement.Body.LabelSet = labeledStatement.Label;
        var result = _body.Execute(context);
        if (result.Type == CompletionType.Break && string.Equals(result.Target, _labelName, StringComparison.Ordinal))
        {
            var value = result.Value;
            return new Completion(CompletionType.Normal, value, _statement);
        }

        return result;
    }
}
