using System.Runtime.CompilerServices;
using System.Threading;
using Jint.Native;
using Jint.Native.Function;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Environments;
using Jint.Runtime.Interpreter;
using Jint.Runtime.Interpreter.Expressions;
using Jint.Runtime.Modules;
using Environment = Jint.Runtime.Environments.Environment;

namespace Jint;

public static class AstExtensions
{
#pragma warning disable CS0649 // Field is never assigned to, and will always have its default value
    internal static readonly SourceLocation DefaultLocation;
#pragma warning restore CS0649 // Field is never assigned to, and will always have its default value

    private static Tokenizer? s_cachedTokenizer;

    public static JsValue GetKey<T>(this T property, Engine engine) where T : IProperty => GetKey(property.Key, engine, property.Computed);

    public static JsValue GetKey(this Expression expression, Engine engine, bool resolveComputed = false)
    {
        var key = TryGetKey(expression, engine, resolveComputed);
        if (key is not null)
        {
            return TypeConverter.ToPropertyKey(key);
        }

        Throw.ArgumentException("Unable to extract correct key, node type: " + expression.Type);
        return JsValue.Undefined;
    }

    internal static JsValue TryGetKey<T>(this T property, Engine engine) where T : IProperty
    {
        return TryGetKey(property.Key, engine, property.Computed);
    }

    internal static JsValue TryGetKey<T>(this T expression, Engine engine, bool resolveComputed) where T : Expression
    {
        JsValue key;
        if (expression is Literal literal && (!resolveComputed || CanSkipComputedKeyEvaluation(literal)))
        {
            key = literal.Kind == TokenKind.NullLiteral ? JsValue.Null : LiteralKeyToString(literal);
        }
        else if (!resolveComputed && expression is Identifier identifier)
        {
            key = identifier.Name;
        }
        else if (expression is PrivateIdentifier privateIdentifier)
        {
            key = engine.ExecutionContext.PrivateEnvironment!.Names[privateIdentifier];
        }
        else if (resolveComputed)
        {
            return TryGetComputedPropertyKey(expression, engine);
        }
        else
        {
            key = JsValue.Undefined;
        }
        return key;
    }

    /// <summary>
    /// Whether a computed key spelled as this literal may be turned into its key string directly instead of
    /// running the full ComputedPropertyName evaluation
    /// (https://tc39.es/ecma262/#sec-runtime-semantics-propertydefinitionevaluation, which is
    /// <c>ToPropertyKey(? GetValue(? Evaluation(AssignmentExpression)))</c>).
    /// <para>
    /// The line is drawn at exactly the three literal kinds the grammar also admits in a *non-computed* key
    /// position. Each evaluates to a primitive already, so ToPropertyKey is ToPrimitive (the identity on a
    /// primitive) followed by ToString, and neither step can reach user code: the shortcut is therefore
    /// observationally equivalent, and it is what keeps the common <c>{ ["foo"]: 1 }</c> and <c>{ [0]: 1 }</c>
    /// shapes free of an expression build.
    /// </para>
    /// <para>
    /// The remaining kinds are deliberately excluded. A RegExpLiteral evaluates to an *object*, so its
    /// ToPropertyKey runs ToPrimitive and calls a user-replaceable <c>RegExp.prototype.toString</c>
    /// (<c>({ [/a/]: 0 })</c> must key on <c>"/a/"</c>, and an overridden toString must be honoured, and may
    /// throw). BooleanLiteral and NullLiteral are primitives whose conversion is pure, but there is nothing to
    /// win — such a key is vanishingly rare — and shortcutting them means hand-spelling a conversion that can
    /// drift from <see cref="TypeConverter"/>, which is exactly how <c>{ [true]: 1 }</c> came to bind
    /// <c>"True"</c>. Let the evaluator produce the value and TypeConverter convert it.
    /// </para>
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool CanSkipComputedKeyEvaluation(Literal literal)
    {
        return literal.Kind is TokenKind.StringLiteral or TokenKind.NumericLiteral or TokenKind.BigIntLiteral;
    }

    internal static JsValue TryGetComputedPropertyKey<T>(T expression, Engine engine)
        where T : Expression
    {
        // Evaluate whatever expression appears in the computed-key position. This used to be an
        // allowlist of node types with a silent JsValue.Undefined fallback, which made keys like
        // `{ [(sideEffect(), "k")]: v }` (SequenceExpression) or `{ [new Key()]: v }` bind the
        // property "undefined" without running the key expression at all.
        var context = engine._evaluationContext;
        var result = JintExpression.Build(expression).GetValue(context);

        // If the expression suspended the generator (e.g., yield in computed property name),
        // return the value. The caller should check ExecutionContext.Suspended.
        return result;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool IsFunctionDefinition<T>(this T node) where T : Node
    {
        var type = node.Type;
        return type
            is NodeType.FunctionExpression
            or NodeType.ArrowFunctionExpression
            or NodeType.ClassExpression;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool IsStrict(this IFunction function)
    {
        return function.Body is FunctionBody { Strict: true };
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-static-semantics-isconstantdeclaration
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool IsConstantDeclaration(this Declaration d)
    {
        return d is VariableDeclaration { Kind: VariableDeclarationKind.Const };
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool HasName<T>(this T node) where T : Node
    {
        if (!node.IsFunctionDefinition())
        {
            return false;
        }

        if (node is IFunction { Id: not null })
        {
            return true;
        }

        if (node is ClassExpression { Id: not null })
        {
            return true;
        }

        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool IsAnonymousFunctionDefinition<T>(this T node) where T : Node
    {
        if (!node.IsFunctionDefinition())
        {
            return false;
        }

        if (node.HasName())
        {
            return false;
        }

        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool IsOptional<T>(this T node) where T : Expression
    {
        return node is IChainElement { Optional: true };
    }

    /// <summary>
    /// Renders a literal the way JavaScript spells it. The first three branches are the only kinds that can
    /// reach here as a property key (see <see cref="CanSkipComputedKeyEvaluation"/>); the rest exist for the
    /// diagnostic callers that render an arbitrary literal — <see cref="JintExpression"/>'s source text for
    /// error messages, and the call-stack argument rendering.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static string LiteralKeyToString(Literal literal)
    {
        if (literal is StringLiteral stringLiteral)
        {
            return stringLiteral.Value;
        }

        // prevent conversion to scientific notation
        if (literal is NumericLiteral numericLiteral)
        {
            return TypeConverter.ToString(numericLiteral.Value);
        }

        if (literal is BigIntLiteral bigIntLiteral)
        {
            return bigIntLiteral.Value.ToString(provider: null);
        }

        return NonKeyLiteralToString(literal);
    }

    /// <summary>
    /// The literal kinds that are never a property key: a non-computed key is a string/numeric/bigint literal
    /// by grammar, and a computed one is evaluated and converted by <see cref="TypeConverter.ToPropertyKey"/>.
    /// Only the diagnostic callers land here, and they get the JavaScript spelling rather than a CLR-formatted
    /// one — <c>Convert.ToString</c> renders a boolean as <c>"True"</c>, and for a regex either the bare
    /// pattern held by the <see cref="System.Text.RegularExpressions.Regex"/> or, when Acornima did not build
    /// one, the empty string.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static string NonKeyLiteralToString(Literal literal)
    {
        // Raw is the literal's own source text, which for a regex is exactly what
        // RegExp.prototype.toString produces ("/a/gi").
        return literal switch
        {
            BooleanLiteral booleanLiteral => booleanLiteral.Value ? "true" : "false",
            NullLiteral => "null",
            _ => literal.Raw ?? "",
        };
    }

    internal static void GetBoundNames(this VariableDeclaration variableDeclaration, List<Key> target)
    {
        ref readonly var declarations = ref variableDeclaration.Declarations;
        for (var i = 0; i < declarations.Count; i++)
        {
            var declaration = declarations[i];
            GetBoundNames(declaration.Id, target);
        }
    }

    internal static void GetBoundNames(this Node? parameter, List<Key> target)
    {
        if (parameter is null || parameter.Type == NodeType.Literal)
        {
            return;
        }

        // try to get away without a loop
        if (parameter is Identifier id)
        {
            target.Add(id.Name);
            return;
        }

        if (parameter is VariableDeclaration variableDeclaration)
        {
            variableDeclaration.GetBoundNames(target);
            return;
        }

        while (true)
        {
            if (parameter is Identifier identifier)
            {
                target.Add(identifier.Name);
                return;
            }

            if (parameter is RestElement restElement)
            {
                parameter = restElement.Argument;
                continue;
            }

            if (parameter is ArrayPattern arrayPattern)
            {
                ref readonly var arrayPatternElements = ref arrayPattern.Elements;
                for (var i = 0; i < arrayPatternElements.Count; i++)
                {
                    var expression = arrayPatternElements[i];
                    GetBoundNames(expression, target);
                }
            }
            else if (parameter is ObjectPattern objectPattern)
            {
                ref readonly var objectPatternProperties = ref objectPattern.Properties;
                for (var i = 0; i < objectPatternProperties.Count; i++)
                {
                    var property = objectPatternProperties[i];
                    if (property is AssignmentProperty p)
                    {
                        GetBoundNames(p.Value, target);
                    }
                    else
                    {
                        GetBoundNames((RestElement) property, target);
                    }
                }
            }
            else if (parameter is AssignmentPattern assignmentPattern)
            {
                parameter = assignmentPattern.Left;
                continue;
            }
            else if (parameter is ClassDeclaration classDeclaration)
            {
                var name = classDeclaration.Id?.Name;
                if (name != null)
                {
                    target.Add(name);
                }
            }
            else if (parameter is FunctionDeclaration functionDeclaration)
            {
                var name = functionDeclaration.Id?.Name;
                if (name != null)
                {
                    target.Add(name);
                }
            }
            break;
        }
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-static-semantics-privateboundidentifiers
    /// </summary>
    internal static void PrivateBoundIdentifiers(this Node parameter, HashSet<PrivateIdentifier> target)
    {
        if (parameter.Type == NodeType.PrivateIdentifier)
        {
            target.Add((PrivateIdentifier) parameter);
        }
        else if (parameter.Type is NodeType.AccessorProperty or NodeType.MethodDefinition or NodeType.PropertyDefinition)
        {
            if (((ClassProperty) parameter).Key is PrivateIdentifier privateKeyIdentifier)
            {
                target.Add(privateKeyIdentifier);
            }
        }
        else if (parameter.Type == NodeType.ClassBody)
        {
            ref readonly var elements = ref ((ClassBody) parameter).Body;
            for (var i = 0; i < elements.Count; i++)
            {
                var element = elements[i];
                PrivateBoundIdentifiers(element, target);
            }
        }
    }

    internal static void BindingInitialization(
        this Node? expression,
        EvaluationContext context,
        JsValue value,
        Environment env)
    {
        if (expression is Identifier identifier)
        {
            var catchEnvRecord = (DeclarativeEnvironment) env;
            catchEnvRecord.CreateMutableBindingAndInitialize(identifier.Name, canBeDeleted: false, value, DisposeHint.Normal);
        }
        else if (expression is DestructuringPattern pattern)
        {
            DestructuringPatternAssignmentExpression.ProcessPatterns(context, pattern, value, env);
        }
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-runtime-semantics-definemethod
    /// </summary>
    internal static Record DefineMethod<T>(this T m, ObjectInstance obj, ObjectInstance? functionPrototype, INode sourceTextNode) where T : IProperty
    {
        var engine = obj.Engine;
        var propKey = TypeConverter.ToPropertyKey(m.GetKey(engine));
        var intrinsics = engine.Realm.Intrinsics;

        var runningExecutionContext = engine.ExecutionContext;
        var env = runningExecutionContext.LexicalEnvironment;
        var privateEnv = runningExecutionContext.PrivateEnvironment;

        var prototype = functionPrototype ?? intrinsics.Function.PrototypeObject;
        var function = m.Value as IFunction;
        if (function is null)
        {
            Throw.SyntaxError(engine.Realm);
        }

        // re-evaluating the same class (factory functions, re-run prepared scripts) reuses the
        // method's interpreter definition and its warm body handler tree; the closure, home object
        // and environments stay per-evaluation
        if (!engine.TryGetFunctionDefinition((Node) function, out var definition))
        {
            engine.CacheFunctionDefinition((Node) function, definition = new JintFunctionDefinition(function, sourceTextNode));
        }

        var closure = intrinsics.Function.OrdinaryFunctionCreate(prototype, definition, definition.ThisMode, env, privateEnv);
        closure.MakeMethod(obj);

        return new Record(propKey, closure);
    }

    internal static void GetImportEntries(this ImportDeclaration import, List<ImportEntry> importEntries, HashSet<ModuleRequest> requestedModules)
    {
        var source = import.Source.Value;
        var specifiers = import.Specifiers;
        var attributes = GetAttributes(import.Attributes);

        var phase = import.Phase switch
        {
            ImportPhase.Defer => ModuleImportPhase.Defer,
            ImportPhase.Source => ModuleImportPhase.Source,
            _ => ModuleImportPhase.Evaluation,
        };

        var moduleRequest = new ModuleRequest(source, attributes) { Phase = phase };
        requestedModules.Add(moduleRequest);

        foreach (var specifier in specifiers)
        {
            switch (specifier)
            {
                case ImportNamespaceSpecifier namespaceSpecifier:
                    importEntries.Add(new ImportEntry(moduleRequest, "*", namespaceSpecifier.Local.GetModuleKey(), phase));
                    break;
                case ImportSpecifier importSpecifier:
                    importEntries.Add(new ImportEntry(moduleRequest, importSpecifier.Imported.GetModuleKey(), importSpecifier.Local.GetModuleKey()!, phase));
                    break;
                case ImportDefaultSpecifier defaultSpecifier:
                    importEntries.Add(new ImportEntry(moduleRequest, "default", defaultSpecifier.Local.GetModuleKey(), phase));
                    break;
            }
        }
    }

    private static ModuleImportAttribute[] GetAttributes(in NodeList<ImportAttribute> importAttributes)
    {
        if (importAttributes.Count == 0)
        {
            return [];
        }

        var attributes = new ModuleImportAttribute[importAttributes.Count];
        for (var i = 0; i < importAttributes.Count; i++)
        {
            var attribute = importAttributes[i];
            var key = attribute.Key is Identifier identifier ? identifier.Name : ((StringLiteral) attribute.Key).Value;
            attributes[i] = new ModuleImportAttribute(key, attribute.Value.Value);
        }
        return attributes;
    }

    internal static void GetExportEntries(this ExportDeclaration export, List<ExportEntry> exportEntries, HashSet<ModuleRequest> requestedModules)
    {
        switch (export)
        {
            case ExportDefaultDeclaration defaultDeclaration:
                GetExportEntries(true, defaultDeclaration.Declaration, exportEntries);
                break;
            case ExportAllDeclaration allDeclaration:
                //Note: there is a pending PR for Esprima to support exporting an imported modules content as a namespace i.e. 'export * as ns from "mod"'
                requestedModules.Add(new ModuleRequest(allDeclaration.Source.Value, []));
                exportEntries.Add(new(allDeclaration.Exported?.GetModuleKey(), new ModuleRequest(allDeclaration.Source.Value, []), "*", null));
                break;
            case ExportNamedDeclaration namedDeclaration:
                ref readonly var specifiers = ref namedDeclaration.Specifiers;
                if (specifiers.Count == 0)
                {
                    ModuleRequest? moduleRequest = namedDeclaration.Source != null
                        ? new ModuleRequest(namedDeclaration.Source.Value, [])
                        : null;

                    GetExportEntries(false, namedDeclaration.Declaration!, exportEntries, moduleRequest);
                }
                else
                {
                    for (var i = 0; i < specifiers.Count; i++)
                    {
                        var specifier = specifiers[i];
                        if (namedDeclaration.Source != null)
                        {
                            exportEntries.Add(new(specifier.Exported.GetModuleKey(), new ModuleRequest(namedDeclaration.Source.Value, []), specifier.Local.GetModuleKey(), null));
                        }
                        else
                        {
                            exportEntries.Add(new(specifier.Exported.GetModuleKey(), null, null, specifier.Local.GetModuleKey()));
                        }
                    }
                }

                if (namedDeclaration.Source is not null)
                {
                    requestedModules.Add(new ModuleRequest(namedDeclaration.Source.Value, []));
                }

                break;
        }
    }

    private static void GetExportEntries(bool defaultExport, StatementOrExpression declaration, List<ExportEntry> exportEntries, ModuleRequest? moduleRequest = null)
    {
        var names = GetExportNames(declaration);

        if (names.Count == 0)
        {
            if (defaultExport)
            {
                exportEntries.Add(new("default", null, null, "*default*"));
            }
        }
        else
        {
            for (var i = 0; i < names.Count; i++)
            {
                var name = names[i];
                var exportName = defaultExport ? "default" : name.Name;
                exportEntries.Add(new(exportName, moduleRequest, null, name));
            }
        }
    }

    private static List<Key> GetExportNames(StatementOrExpression declaration)
    {
        var result = new List<Key>();

        switch (declaration)
        {
            case FunctionDeclaration functionDeclaration:
                var funcName = functionDeclaration.Id?.Name;
                if (funcName is not null)
                {
                    result.Add(funcName);
                }

                break;
            case ClassDeclaration classDeclaration:
                var className = classDeclaration.Id?.Name;
                if (className is not null)
                {
                    result.Add(className);
                }

                break;
            case VariableDeclaration variableDeclaration:
                variableDeclaration.GetBoundNames(result);
                break;
        }

        return result;
    }

    private static string GetModuleKey(this Expression expression)
    {
        return (expression as Identifier)?.Name ?? ((StringLiteral) expression).Value;
    }

    internal readonly record struct Record(JsValue Key, ScriptFunction Closure);

    /// <summary>
    /// Creates a dummy node that can be used when only location available and node is required.
    /// </summary>
    internal static Node CreateLocationNode(in SourceLocation location)
    {
        return new MinimalSyntaxElement(in location);
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-static-semantics-allprivateidentifiersvalid
    /// </summary>
    internal static void AllPrivateIdentifiersValid(this Script script, Realm realm, HashSet<PrivateIdentifier>? privateIdentifiers)
    {
        var validator = new PrivateIdentifierValidator(realm, privateIdentifiers);
        validator.Visit(script);
    }

    internal static DisposeHint GetDisposeHint(this VariableDeclarationKind statement)
    {
        return statement switch
        {
            VariableDeclarationKind.AwaitUsing => DisposeHint.Async,
            VariableDeclarationKind.Using => DisposeHint.Sync,
            _ => DisposeHint.Normal,
        };
    }

    internal static int GetSecondTokenStartIndex(string sourceText, int start, int end)
    {
        var tokenizer = Interlocked.Exchange(ref s_cachedTokenizer, value: null) ?? new Tokenizer(string.Empty);
        try
        {
            tokenizer.Reset(sourceText, start, end - start, SourceType.Script);
            tokenizer.Next();
            tokenizer.Next(); // skip first token + potential whitespace and/or comments
            return tokenizer.Current.Start;
        }
        finally
        {
            tokenizer.Reset(string.Empty);
            Volatile.Write(ref s_cachedTokenizer, tokenizer);
        }
    }

    private sealed class MinimalSyntaxElement : Node
    {
        public MinimalSyntaxElement(in SourceLocation location) : base(NodeType.Unknown)
        {
            Location = location;
        }

        protected override IEnumerator<Node>? GetChildNodes() => throw new NotImplementedException();
        protected override object? Accept(AstVisitor visitor) => throw new NotImplementedException();
    }

    private sealed class PrivateIdentifierValidator : AstVisitor
    {
        private readonly Realm _realm;
        private HashSet<PrivateIdentifier>? _privateNames;

        public PrivateIdentifierValidator(Realm realm, HashSet<PrivateIdentifier>? privateNames)
        {
            _realm = realm;
            _privateNames = privateNames;
        }

        protected override object VisitPrivateIdentifier(PrivateIdentifier privateIdentifier)
        {
            if (_privateNames is null || !_privateNames.Contains(privateIdentifier))
            {
                Throw(_realm, privateIdentifier);
            }
            return privateIdentifier;
        }

        protected override object VisitClassBody(ClassBody classBody)
        {
            var oldList = _privateNames;
            _privateNames = new HashSet<PrivateIdentifier>(PrivateIdentifierNameComparer._instance);
            classBody.PrivateBoundIdentifiers(_privateNames);
            base.VisitClassBody(classBody);
            _privateNames = oldList;
            return classBody;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void Throw(Realm r, PrivateIdentifier id)
        {
            Runtime.Throw.SyntaxError(r, $"Private field '#{id.Name}' must be declared in an enclosing class");
        }
    }
}
