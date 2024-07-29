namespace Jint.Runtime.Interpreter.Statements;

internal sealed class JintScript
{
    private readonly JintStatementList _list;

    public JintScript(Script script)
    {
        _list = new JintStatementList(script);
    }

    public Completion Execute(EvaluationContext context)
    {
        return _list.Execute(context);
    }
}