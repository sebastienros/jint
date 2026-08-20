using Jint.Native;
using Jint.Runtime.Environments;
using Environment = Jint.Runtime.Environments.Environment;

namespace Jint.Runtime.Interpreter.Statements;

internal sealed class JintFunctionDeclarationStatement : JintStatement<FunctionDeclaration>
{
    /// <summary>
    /// True when this declaration is <em>directly</em> contained in the StatementList of a Block, a
    /// CaseClause or a DefaultClause and could therefore carry Annex B's var-scope alias. Decided at
    /// build time from the containing statement list - a top-level declaration, or one reached through
    /// a LabelledStatement, never enters <see cref="ExecuteAnnexBVarScopeAssignment"/> at all.
    /// </summary>
    private readonly bool _blockLevel;

    private readonly Key _name;

    public JintFunctionDeclarationStatement(FunctionDeclaration statement, bool blockLevel = false) : base(statement)
    {
        _name = statement.Id?.Name ?? "";

        // B.3.2 covers FunctionDeclaration only - a GeneratorDeclaration, AsyncFunctionDeclaration or
        // AsyncGeneratorDeclaration in a block stays purely block-scoped.
        _blockLevel = blockLevel && statement.Id is not null && !statement.Generator && !statement.Async;
    }

    protected override Completion ExecuteInternal(EvaluationContext context)
    {
        if (_blockLevel)
        {
            ExecuteAnnexBVarScopeAssignment(context);
        }

        return new Completion(CompletionType.Normal, JsEmpty.Instance, ((JintStatement) this)._statement);
    }

    /// <summary>
    /// The "Block-Level Function Declarations Web Legacy Compatibility Semantics" step of
    /// <see href="https://tc39.es/ecma262/#sec-functiondeclarationinstantiation">FunctionDeclarationInstantiation</see>
    /// and of its
    /// <see href="https://tc39.es/ecma262/#sec-evaldeclarationinstantiation">EvalDeclarationInstantiation</see> /
    /// <see href="https://tc39.es/ecma262/#sec-globaldeclarationinstantiation">GlobalDeclarationInstantiation</see>
    /// siblings:
    /// <code>
    /// Let funcEnv be the running execution context's VariableEnvironment.
    /// Let blockEnv be the running execution context's LexicalEnvironment.
    /// Let funcObj be ! blockEnv.GetBindingValue(funcName, false).
    /// Perform ! funcEnv.SetMutableBinding(funcName, funcObj, false).
    /// </code>
    /// This runs <em>in place of</em> the ordinary FunctionDeclaration Evaluation, i.e. when the
    /// declaration statement is reached, not when the block is entered. The distinction is observable:
    /// the var-scoped alias is <c>undefined</c> until then, and the value taken is the block binding's
    /// current one - which a later same-named declaration in the same block has already overwritten.
    /// </summary>
    private void ExecuteAnnexBVarScopeAssignment(EvaluationContext context)
    {
        var engine = context.Engine;
        var executionContext = engine.ExecutionContext;
        if (executionContext.Strict)
        {
            return;
        }

        var varEnv = executionContext.VariableEnvironment;
        var blockEnv = executionContext.LexicalEnvironment;
        if (ReferenceEquals(varEnv, blockEnv) || !IsAliasEligible(engine, in executionContext, varEnv, blockEnv))
        {
            return;
        }

        var funcObj = blockEnv.GetBindingValue(_name, strict: false);

        // this may CREATE a binding in the pre-existing var env (sloppy set-on-missing) —
        // invalidate pinned nested-global chain memos
        engine._envBindingInjectionEpoch++;
        varEnv.SetMutableBinding(_name, funcObj, strict: false);
    }

    /// <summary>
    /// Whether the instantiation that preceded this evaluation decided the alias was creatable. The
    /// three arms mirror the three amendments: function code, script/global-eval code, and a sloppy
    /// direct eval inside a function.
    /// </summary>
    private bool IsAliasEligible(Engine engine, in ExecutionContext executionContext, Environment varEnv, Environment blockEnv)
    {
        if (executionContext.Function is { _functionDefinition: { } funcDef })
        {
            // FunctionDeclarationInstantiation settled this once per declaration node. Keying on the
            // node rather than the name matters: same-named declarations at different block levels
            // are not interchangeable.
            return funcDef.Initialize().AnnexBFunctionDeclarations?.Contains(_statement) == true;
        }

        if (varEnv is GlobalEnvironment globalEnv)
        {
            // Script or global-scope eval: Global/EvalDeclarationInstantiation created the global var
            // binding exactly when the name was definable and not lexically declared.
            return !globalEnv.HasLexicalDeclaration(_name) && globalEnv.HasOwnBinding(_name);
        }

        // Sloppy direct eval inside a function. Two conditions, and only the engine has both.
        //
        // The static one - "replacing the FunctionDeclaration with a VariableStatement ... would not
        // produce any Early Errors for body" - is what excludes, say, a declaration under a
        // DESTRUCTURED catch parameter of the same name, which B.3.4's exemption does not cover. It is
        // decided once for the eval body by HoistingScope, and reaches here through the engine because
        // a block statement has no other route back to the eval that is running it.
        var candidates = engine._evalAnnexBFunctions;
        if (candidates is null)
        {
            return false;
        }

        var eligible = false;
        for (var i = 0; i < candidates.Count; i++)
        {
            if (ReferenceEquals(candidates[i], _statement))
            {
                eligible = true;
                break;
            }
        }

        if (!eligible)
        {
            return false;
        }

        // The dynamic one is EvalDeclarationInstantiation's bindingExists walk: an environment between
        // the eval's own and the variable environment that already binds the name blocks the alias.
        // Object environment records (a `with` scope) and catch environments are skipped, per the same
        // amendment and B.3.4. Note this is deliberately NOT a test for the binding EDI may itself have
        // created in varEnv: eval-introduced var bindings are deletable, and `delete f` before the
        // block runs must not turn the alias off.
        for (var env = blockEnv._outerEnv; env is not null && !ReferenceEquals(env, varEnv); env = env._outerEnv)
        {
            if (env is ObjectEnvironment or DeclarativeEnvironment { _catchEnvironment: true })
            {
                continue;
            }

            if (env.HasBinding(_name))
            {
                return false;
            }
        }

        return true;
    }
}
