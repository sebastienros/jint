namespace Jint;

/// <summary>
/// Builds a <see cref="ReferencedGlobals"/> for a program being prepared.
/// </summary>
/// <remarks>
/// Two phases. During the parse, <see cref="OnNode"/> captures the declared names of every scope the parser closes —
/// this is the only moment they are readable, as <c>Scope</c> instances are pooled and their backing arrays are
/// cleared and reused once the scope is popped. After the parse, <see cref="Build"/> walks the finished AST with a
/// stack of those name sets and resolves every reference site against it.
/// <para>
/// The walk deliberately does not consume the parser's node stream directly, even though that would avoid a second
/// traversal. Three properties of <c>OnNode</c> rule it out: arrow-function parameters are parsed as an ordinary
/// expression <em>before</em> the arrow's scope is entered, so they arrive at the enclosing scope's depth; the
/// callback also fires for nodes that are reinterpreted and never reach the final AST (the <c>get</c> of
/// <c>get g() {}</c>, the properties of a destructuring assignment); and a <c>switch</c> scope covers the cases but
/// not the discriminant. Walking the finished tree is immune to all three.
/// </para>
/// </remarks>
internal sealed class ReferencedGlobalsCollector
{
    private static readonly string[] _empty = [];

    private readonly Dictionary<Node, HashSet<string>> _scopes = new(ReferenceComparer.Instance);
    private readonly List<HashSet<string>> _scopeStack = new();
    private readonly HashSet<string> _free = new(StringComparer.Ordinal);
    private bool _hasDirectEvalCall;

    /// <summary>
    /// Records the declared names of the scope <paramref name="node"/> closes, if it closes one.
    /// </summary>
    public void OnNode(Node node, in OnNodeContext ctx)
    {
        if (!ctx.HasScope)
        {
            return;
        }

        ref readonly var scope = ref ctx.Scope;

        HashSet<string>? names = null;
        Collect(ref names, scope.VarVariables);
        Collect(ref names, scope.LexicalVariables);
        Collect(ref names, scope.Functions);

        switch (node.Type)
        {
            case NodeType.FunctionDeclaration:
            case NodeType.FunctionExpression:
                // The parser registers neither of these in the scope it hands us: `arguments` because it is an
                // implicit binding rather than a declaration, and a function expression's self-name because it is
                // not visible outside the function. Both are bound inside the function body, so both belong here.
                names ??= NewNameSet();
                names.Add("arguments");
                var functionId = ((IFunction) node).Id;
                if (functionId is not null)
                {
                    names.Add(functionId.Name);
                }
                break;

            case NodeType.ClassDeclaration:
            case NodeType.ClassExpression:
                // Same story for a class expression's self-name. A class declaration's name is additionally bound in
                // the enclosing scope, so adding it here is redundant but harmless.
                var classId = ((IClass) node).Id;
                if (classId is not null)
                {
                    (names ??= NewNameSet()).Add(classId.Name);
                }
                break;

            default:
                break;
        }

        if (names is not null)
        {
            // A scope declaring nothing cannot bind anything, so it is simply left out and the walk skips it.
            _scopes[node] = names;
        }
    }

    /// <summary>
    /// Resolves every reference site in <paramref name="program"/> against the captured scopes.
    /// </summary>
    public ReferencedGlobals Build(Program program)
    {
        Walk(program);

        string[] names;
        if (_free.Count == 0)
        {
            names = _empty;
        }
        else
        {
            names = new string[_free.Count];
            _free.CopyTo(names);
            Array.Sort(names, StringComparer.Ordinal);
        }

        return new ReferencedGlobals(names, _hasDirectEvalCall);
    }

    private static HashSet<string> NewNameSet() => new(StringComparer.Ordinal);

    private static void Collect(ref HashSet<string>? names, ReadOnlySpan<Identifier> declarations)
    {
        if (declarations.Length == 0)
        {
            return;
        }

        // The span points into the parser's pooled storage, which is cleared and reused as soon as the scope is
        // popped, so the names have to be copied out now.
        names ??= NewNameSet();
        foreach (var declaration in declarations)
        {
            names.Add(declaration.Name);
        }
    }

    private void Walk(Node node)
    {
        if (node.Type == NodeType.SwitchStatement)
        {
            // The parser attaches the switch body's scope to the SwitchStatement node, but the discriminant is not
            // part of that scope: in `switch (y) { case 1: let y; }` the discriminant `y` is free.
            var switchStatement = (SwitchStatement) node;
            Walk(switchStatement.Discriminant);

            var pushedSwitchScope = TryPushScope(node);
            foreach (var switchCase in switchStatement.Cases)
            {
                Walk(switchCase);
            }
            if (pushedSwitchScope)
            {
                PopScope();
            }
            return;
        }

        var pushed = TryPushScope(node);
        WalkCore(node);
        if (pushed)
        {
            PopScope();
        }
    }

    private void WalkCore(Node node)
    {
        switch (node.Type)
        {
            case NodeType.Identifier:
                AddReference(((Identifier) node).Name);
                return;

            case NodeType.MemberExpression:
                // `a.b` references only `a`; `a[b]` references both.
                var memberExpression = (MemberExpression) node;
                Walk(memberExpression.Object);
                if (memberExpression.Computed)
                {
                    Walk(memberExpression.Property);
                }
                return;

            case NodeType.Property:
                // Object literal and object pattern properties. A non-computed key is a name, not a reference.
                // For a shorthand property the key node either is the value node or is nested inside it, so walking
                // the value covers it exactly once.
                var property = (Property) node;
                if (property.Computed)
                {
                    Walk(property.Key);
                }
                Walk(property.Value);
                return;

            case NodeType.MethodDefinition:
            case NodeType.PropertyDefinition:
            case NodeType.AccessorProperty:
                // Same rule for class elements, but these also carry decorators, which are references.
                var classProperty = (ClassProperty) node;
                foreach (var child in node.ChildNodes)
                {
                    if (!classProperty.Computed && ReferenceEquals(child, classProperty.Key))
                    {
                        continue;
                    }
                    Walk(child);
                }
                return;

            case NodeType.LabeledStatement:
                // A label is not an identifier binding and cannot resolve against any environment.
                Walk(((LabeledStatement) node).Body);
                return;

            case NodeType.BreakStatement:
            case NodeType.ContinueStatement:
                return;

            case NodeType.ImportDeclaration:
            case NodeType.ImportAttribute:
                // Import bindings are declarations and imported names are raw module-export names.
                return;

            case NodeType.ExportNamedDeclaration:
                var exportDeclaration = (ExportNamedDeclaration) node;
                if (exportDeclaration.Declaration is not null)
                {
                    Walk(exportDeclaration.Declaration);
                }
                if (exportDeclaration.Source is null)
                {
                    // `export { a as b }` references the local binding `a`; `b` is a raw export name. In a re-export
                    // (`export { a } from "m"`) neither is a local reference.
                    foreach (var specifier in exportDeclaration.Specifiers)
                    {
                        if (specifier.Local is Identifier local)
                        {
                            AddReference(local.Name);
                        }
                    }
                }
                return;

            case NodeType.ExportAllDeclaration:
                return;

            case NodeType.MetaProperty:
                // `new.target` and `import.meta` are spelled with identifiers that name neither binding.
                return;

            case NodeType.CallExpression:
                if (((CallExpression) node).Callee is Identifier { Name: "eval" })
                {
                    _hasDirectEvalCall = true;
                }
                break;

            default:
                break;
        }

        foreach (var child in node.ChildNodes)
        {
            Walk(child);
        }
    }

    private bool TryPushScope(Node node)
    {
        if (!CanIntroduceScope(node.Type) || !_scopes.TryGetValue(node, out var names))
        {
            return false;
        }

        _scopeStack.Add(names);
        return true;
    }

    private void PopScope() => _scopeStack.RemoveAt(_scopeStack.Count - 1);

    private static bool CanIntroduceScope(NodeType type) => type
        is NodeType.Program
        or NodeType.FunctionDeclaration
        or NodeType.FunctionExpression
        or NodeType.ArrowFunctionExpression
        or NodeType.ClassDeclaration
        or NodeType.ClassExpression
        or NodeType.StaticBlock
        or NodeType.BlockStatement
        or NodeType.CatchClause
        or NodeType.ForStatement
        or NodeType.ForInStatement
        or NodeType.ForOfStatement
        or NodeType.SwitchStatement;

    private void AddReference(string name)
    {
        for (var i = _scopeStack.Count - 1; i >= 0; i--)
        {
            if (_scopeStack[i].Contains(name))
            {
                return;
            }
        }

        _free.Add(name);
    }

    private sealed class ReferenceComparer : IEqualityComparer<Node>
    {
        public static readonly ReferenceComparer Instance = new();

        private ReferenceComparer()
        {
        }

        public bool Equals(Node? x, Node? y) => ReferenceEquals(x, y);

        public int GetHashCode(Node obj) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
    }
}
