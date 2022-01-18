using System;
using Esprima.Ast;
using Jint.Native;
using Jint.Runtime.Interpreter.Expressions;

#nullable enable

namespace Jint.Runtime.Interpreter.Statements;

internal abstract class JintExportDeclarationStatement<T> : JintStatement<T> where T : ExportDeclaration
{
    protected JintExpression? _declarationExpression;
    protected JintStatement? _declarationStatement;

    protected JintExportDeclarationStatement(T statement) : base(statement)
    {
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-exports-runtime-semantics-evaluation
    /// </summary>
    protected override Completion ExecuteInternal(EvaluationContext context)
    {
        return NormalCompletion(Undefined.Instance);
    }
}