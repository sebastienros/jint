using System.Runtime.InteropServices;
using Jint.Runtime;

namespace Jint;

[StructLayout(LayoutKind.Auto)]
internal readonly record struct ParsingConstraints(int? MaxSourceLength, int? MaxNodeCount)
{
    public static ParsingConstraints From(Options.ParsingOptions options)
        => Create(options.MaxSourceLength, options.MaxNodeCount);

    public static ParsingConstraints From(IParsingOptions options)
        => options is IParsingLimitOptions limits
            ? Create(limits.MaxSourceLength, limits.MaxNodeCount)
            : default;

    public ParsingConstraints Combine(IParsingOptions options)
    {
        var additional = From(options);
        return new ParsingConstraints(
            Minimum(MaxSourceLength, additional.MaxSourceLength),
            Minimum(MaxNodeCount, additional.MaxNodeCount));
    }

    public ParsingConstraints Combine(in ParsingConstraints additional)
    {
        return new ParsingConstraints(
            Minimum(MaxSourceLength, additional.MaxSourceLength),
            Minimum(MaxNodeCount, additional.MaxNodeCount));
    }

    public void CheckSourceLength(long length)
    {
        if (MaxSourceLength is int limit && length > limit)
        {
            throw new ParsingLimitException(ParsingLimitKind.SourceLength, limit, length);
        }
    }

    private static ParsingConstraints Create(int? maxSourceLength, int? maxNodeCount)
    {
        Validate(maxSourceLength, nameof(Options.ParsingOptions.MaxSourceLength));
        Validate(maxNodeCount, nameof(Options.ParsingOptions.MaxNodeCount));
        return new ParsingConstraints(maxSourceLength, maxNodeCount);
    }

    private static int? Minimum(int? first, int? second)
        => first is null ? second : second is null ? first : Math.Min(first.Value, second.Value);

    private static void Validate(int? value, string name)
    {
        if (value < 0)
        {
            Throw.ArgumentOutOfRangeException(name, "Parsing limits cannot be negative.");
        }
    }
}

internal sealed class JintParser
{
    private readonly Parser _parser;
    private readonly ParserOptions _options;
    private readonly ParsingConstraints _constraints;
    private readonly NodeCounter? _nodeCounter;
    private readonly bool _retainsSourceText;

    private JintParser(
        ParserOptions options,
        ParserOptions effectiveParserOptions,
        in ParsingConstraints constraints,
        NodeCounter? nodeCounter,
        bool retainsSourceText)
    {
        _parser = new Parser(effectiveParserOptions);
        _options = options;
        _constraints = constraints;
        _nodeCounter = nodeCounter;
        _retainsSourceText = retainsSourceText;
    }

    public static JintParser Create(ParserOptions options, in ParsingConstraints constraints)
        => Create(options, in constraints, RetainsSourceText(options));

    /// <summary>
    /// Creates a parser whose retention is stated rather than derived, for the one caller that composes its
    /// own node visitor: a preparation's static-analysis pass reads the retained text out of the parse context
    /// itself, so <see cref="RetainsSourceText"/> cannot see it in <see cref="ParserOptions.OnNode"/>.
    /// </summary>
    public static JintParser Create(ParserOptions options, in ParsingConstraints constraints, bool retainsSourceText)
    {
        NodeCounter? counter = null;
        var effectiveOptions = options;
        if (constraints.MaxNodeCount is int maxNodeCount)
        {
            counter = new NodeCounter(maxNodeCount, options.OnNode);
            effectiveOptions = options with { OnNode = counter.Visit };
        }

        return new JintParser(options, effectiveOptions, in constraints, counter, retainsSourceText);
    }

    /// <summary>
    /// Whether a parse under <paramref name="options"/> keeps the source text it is handed. Reading the
    /// installed handler is what keeps the two retentions one decision: the same
    /// <see cref="Engine.DefaultNodeHandler"/> that stamps the input onto every function node is what
    /// <c>IParsingOptions.RetainFunctionSourceText</c> installs, so a program's text is remembered exactly
    /// when its functions' text is.
    /// </summary>
    private static bool RetainsSourceText(ParserOptions options)
        => ReferenceEquals(options.OnNode, Engine.DefaultNodeHandler);

    internal ParserOptions Options => _options;

    public Script ParseScript(string input, int start, int length, string? sourceFile = null, bool strict = false)
    {
        BeginParse(length);
        var script = _parser.ParseScript(input, start, length, sourceFile, strict);
        RecordSourceText(script, input);
        return script;
    }

    public Script ParseScript(string input, string? sourceFile = null, bool strict = false)
        => ParseScript(input, 0, input.Length, sourceFile, strict);

    public Module ParseModule(string input, int start, int length, string? sourceFile = null)
    {
        BeginParse(length);
        var module = _parser.ParseModule(input, start, length, sourceFile);
        RecordSourceText(module, input);
        return module;
    }

    public Module ParseModule(string input, string? sourceFile = null)
        => ParseModule(input, 0, input.Length, sourceFile);

    public Expression ParseExpression(string input, int start, int length, string? sourceFile = null, bool strict = false)
    {
        BeginParse(length);
        return _parser.ParseExpression(input, start, length, sourceFile, strict);
    }

    public Expression ParseExpression(string input, string? sourceFile = null, bool strict = false)
        => ParseExpression(input, 0, input.Length, sourceFile, strict);

    private void BeginParse(int length)
    {
        _constraints.CheckSourceLength(length);
        _nodeCounter?.Reset();
    }

    /// <summary>
    /// Publishes the whole input beside the program it produced, for
    /// <see cref="Engine.AdvancedOperations.TryGetSourceText"/>. The string is the one the caller handed in
    /// — <see cref="ScriptParsingOptions.SourceOffset"/> padding included, since that is what every location
    /// in the parsed tree indexes into.
    /// </summary>
    private void RecordSourceText(Program program, string input)
    {
        if (_retainsSourceText)
        {
            Engine.RecordSourceText(program, input);
        }
    }

    internal void CheckSourceLength(long length) => _constraints.CheckSourceLength(length);

    internal ParsingConstraints Constraints => _constraints;

    private sealed class NodeCounter(int limit, OnNodeHandler? next)
    {
        private int _count;

        public void Reset() => _count = 0;

        public void Visit(Node node, in OnNodeContext context)
        {
            var count = ++_count;
            if (count > limit)
            {
                throw new ParsingLimitException(ParsingLimitKind.NodeCount, limit, count);
            }

            next?.Invoke(node, in context);
        }
    }
}
