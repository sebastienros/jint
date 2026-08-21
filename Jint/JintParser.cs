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

    private JintParser(
        ParserOptions options,
        ParserOptions effectiveParserOptions,
        in ParsingConstraints constraints,
        NodeCounter? nodeCounter)
    {
        _parser = new Parser(effectiveParserOptions);
        _options = options;
        _constraints = constraints;
        _nodeCounter = nodeCounter;
    }

    public static JintParser Create(ParserOptions options, in ParsingConstraints constraints)
    {
        NodeCounter? counter = null;
        var effectiveOptions = options;
        if (constraints.MaxNodeCount is int maxNodeCount)
        {
            counter = new NodeCounter(maxNodeCount, options.OnNode);
            effectiveOptions = options with { OnNode = counter.Visit };
        }

        return new JintParser(options, effectiveOptions, in constraints, counter);
    }

    internal ParserOptions Options => _options;

    public Script ParseScript(string input, int start, int length, string? sourceFile = null, bool strict = false)
    {
        BeginParse(length);
        return _parser.ParseScript(input, start, length, sourceFile, strict);
    }

    public Script ParseScript(string input, string? sourceFile = null, bool strict = false)
        => ParseScript(input, 0, input.Length, sourceFile, strict);

    public Module ParseModule(string input, int start, int length, string? sourceFile = null)
    {
        BeginParse(length);
        return _parser.ParseModule(input, start, length, sourceFile);
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
