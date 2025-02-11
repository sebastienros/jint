using System.Runtime.CompilerServices;
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

    public static JsValue GetKey<T>(this T property, Engine engine) where T : IProperty => GetKey(property.Key, engine, property.Computed);

    public static JsValue GetKey(this Expression expression, Engine engine, bool resolveComputed = false)
    {
        var key = TryGetKey(expression, engine, resolveComputed);
        if (key is not null)
        {
            return TypeConverter.ToPropertyKey(key);
        }

        ExceptionHelper.ThrowArgumentException("Unable to extract correct key, node type: " + expression.Type);
        return JsValue.Undefined;
    }

    internal static JsValue TryGetKey<T>(this T property, Engine engine) where T : IProperty
    {
        return TryGetKey(property.Key, engine, property.Computed);
    }

    internal static JsValue TryGetKey<T>(this T expression, Engine engine, bool resolveComputed) where T : Expression
    {
        JsValue key;
        if (expression is Literal literal)
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

    internal static JsValue TryGetComputedPropertyKey<T>(T expression, Engine engine)
        where T : Expression
    {
        if (expression.Type is NodeType.Identifier
            or NodeType.CallExpression
            or NodeType.BinaryExpression
            or NodeType.UpdateExpression
            or NodeType.AssignmentExpression
            or NodeType.UnaryExpression
            or NodeType.MemberExpression
            or NodeType.LogicalExpression
            or NodeType.ConditionalExpression
            or NodeType.ArrowFunctionExpression
            or NodeType.FunctionExpression
            or NodeType.YieldExpression
            or NodeType.TemplateLiteral)
        {
            var context = engine._activeEvaluationContext ?? new EvaluationContext(engine);
            return JintExpression.Build(expression).GetValue(context);
        }

        return JsValue.Undefined;
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static string LiteralKeyToString(Literal literal)
    {
        if (literal is StringLiteral stringLiteral)
        {
            return stringLiteral.Value;
        }
        // prevent conversion to scientific notation
        else if (literal is NumericLiteral numericLiteral)
        {
            return TypeConverter.ToString(numericLiteral.Value);
        }
        else if (literal is BigIntLiteral bigIntLiteral)
        {
            return bigIntLiteral.Value.ToString(provider: null);
        }
        else
        {
            // We shouldn't ever reach this line in the case of a literal property key.
            return Convert.ToString(literal.Value, provider: null) ?? "";
        }
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
            catchEnvRecord.CreateMutableBindingAndInitialize(identifier.Name, canBeDeleted: false, value);
        }
        else if (expression is DestructuringPattern pattern)
        {
            DestructuringPatternAssignmentExpression.ProcessPatterns(context, pattern, value, env);
        }
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-runtime-semantics-definemethod
    /// </summary>
    internal static Record DefineMethod<T>(this T m, ObjectInstance obj, ObjectInstance? functionPrototype = null) where T : IProperty
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
            ExceptionHelper.ThrowSyntaxError(engine.Realm);
        }

        var definition = new JintFunctionDefinition(function);
        var closure = intrinsics.Function.OrdinaryFunctionCreate(prototype, definition, definition.ThisMode, env, privateEnv);
        closure.MakeMethod(obj);

        return new Record(propKey, closure);
    }

    internal static void GetImportEntries(this ImportDeclaration import, List<ImportEntry> importEntries, HashSet<ModuleRequest> requestedModules)
    {
        var source = import.Source.Value;
        var specifiers = import.Specifiers;
        var attributes = GetAttributes(import.Attributes);
        requestedModules.Add(new ModuleRequest(source, attributes));

        foreach (var specifier in specifiers)
        {
            switch (specifier)
            {
                case ImportNamespaceSpecifier namespaceSpecifier:
                    importEntries.Add(new ImportEntry(new ModuleRequest(source, attributes), "*", namespaceSpecifier.Local.GetModuleKey()));
                    break;
                case ImportSpecifier importSpecifier:
                    importEntries.Add(new ImportEntry(new ModuleRequest(source, attributes), importSpecifier.Imported.GetModuleKey(), importSpecifier.Local.GetModuleKey()!));
                    break;
                case ImportDefaultSpecifier defaultSpecifier:
                    importEntries.Add(new ImportEntry(new ModuleRequest(source, attributes), "default", defaultSpecifier.Local.GetModuleKey()));
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
        return new MinimalSyntaxElement(location);
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-static-semantics-allprivateidentifiersvalid
    /// </summary>
    internal static void AllPrivateIdentifiersValid(this Script script, Realm realm, HashSet<PrivateIdentifier>? privateIdentifiers)
    {
        var validator = new PrivateIdentifierValidator(realm, privateIdentifiers);
        validator.Visit(script);
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
            ExceptionHelper.ThrowSyntaxError(r, $"Private field '#{id.Name}' must be declared in an enclosing class");
        }
    }
}