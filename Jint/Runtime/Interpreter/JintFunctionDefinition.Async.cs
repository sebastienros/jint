using Esprima.Ast;
using Jint.Native;
using Jint.Runtime.Interpreter.Expressions;
using System.Threading.Tasks;

namespace Jint.Runtime.Interpreter
{
    /// <summary>
    /// Works as memento for function execution. Optimization to cache things that don't change.
    /// </summary>
    internal sealed partial class JintFunctionDefinition
    {
        internal async Task<Completion> ExecuteAsync()
        {
            if (Function.Expression)
            {
                _bodyExpression ??= JintExpression.Build(_engine, (Expression)Function.Body);
                var jsValue = await _bodyExpression?.GetValueAsync() ?? Undefined.Instance;
                return new Completion(CompletionType.Return, jsValue, null, Function.Body.Location);
            }

            var blockStatement = (BlockStatement)Function.Body;
            _bodyStatementList ??= new JintStatementList(_engine, blockStatement, blockStatement.Body);
            return await _bodyStatementList.ExecuteAsync();
        }
    }
}