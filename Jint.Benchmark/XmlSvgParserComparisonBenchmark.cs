#nullable enable

using AngleSharp.Dom;
using AngleSharp.Xml.Parser;
using BenchmarkDotNet.Attributes;
using AngleNode = AngleSharp.Dom.INode;
using ParserModel = global::Jint.HtmlParser;

namespace Jint.Benchmark;

/// <summary>
/// Four XML/SVG corpus inputs, parsed into complete independent trees by both libraries. The pinned
/// AngleSharp row is the untouched control. Both parsers receive an already loaded string and reuse
/// their configuration; every invocation returns a fresh document. No browser or script engine runs.
/// </summary>
[MemoryDiagnoser]
[BenchmarkCategory("XmlSvgParserComparison")]
public class XmlSvgParserComparisonBenchmark
{
    private XmlParser _angleParser = null!;
    private ParserModel.XmlParseOptions _nativeOptions = null!;

    [ParamsSource(nameof(Cases))]
    public ParserCorpusCase Case { get; set; } = null!;

    public static IEnumerable<ParserCorpusCase> Cases => ParserCorpus.Cases.Where(corpusCase =>
        corpusCase.Name.EndsWith(".xml", StringComparison.Ordinal) ||
        corpusCase.Name.EndsWith(".svg", StringComparison.Ordinal));

    [GlobalSetup(Target = nameof(ParseAngleSharp))]
    public void SetupAngleSharp()
    {
        // These fixtures have no external entities. Keep diagnostics/source-location retention off on
        // both sides, allow internal entities, and use the default unbounded resource policy on each.
        _angleParser = new XmlParser(new XmlParserOptions
        {
            IsSuppressingErrors = false,
            IsKeepingSourceReferences = false
        });
        ParserCorpus.Validate(Case, ParseAngleSharp());
    }

    [GlobalSetup(Target = nameof(ParseNative))]
    public void SetupNative()
    {
        _angleParser = new XmlParser(new XmlParserOptions
        {
            IsSuppressingErrors = false,
            IsKeepingSourceReferences = false
        });
        _nativeOptions = new ParserModel.XmlParseOptions();
        var angle = ParseAngleSharp();
        ParserCorpus.Validate(Case, angle);
        XmlSvgCorpusComparer.Compare(Case.Name, angle, ParseNative());
    }

    /// <summary>Control row that cannot reach the native parser.</summary>
    [Benchmark(Baseline = true)]
    public IDocument ParseAngleSharp() => _angleParser.ParseDocument(Case.Source);

    /// <summary>Native XML or SVG parser plus its complete tree, with the same source and no preprocessing.</summary>
    [Benchmark]
    public ParserModel.Document ParseNative() => Case.Name.EndsWith(".svg", StringComparison.Ordinal)
        ? ParserModel.MarkupParser.ParseSvg(Case.Source, _nativeOptions)
        : ParserModel.MarkupParser.ParseXml(Case.Source, _nativeOptions);

    /// <summary>Correctness-only comparison, including corruption probes; no timing run.</summary>
    public static int ValidateAll()
    {
        foreach (var corpusCase in Cases)
        {
            var benchmark = new XmlSvgParserComparisonBenchmark { Case = corpusCase };
            benchmark.SetupAngleSharp();
            benchmark.SetupNative();
            Console.WriteLine($"{corpusCase}: equal parse workload; pinned default-xmlns projection checked");
        }

        XmlSvgCorpusComparer.ValidateNegativeProbes();
        XmlSvgCorpusComparer.ValidateAuxiliaryNodeKinds();
        Console.WriteLine("Count-preserving XML/SVG corruption was rejected.");
        return 0;
    }
}

/// <summary>Untimed XML/SVG tree comparison; checks semantics and ownership rather than aggregate counts.</summary>
internal static class XmlSvgCorpusComparer
{
    private const string XmlnsNamespace = "http://www.w3.org/2000/xmlns/";

    public static void Compare(string name, IDocument expected, ParserModel.Document actual)
    {
        if (actual.Kind != ParserModel.DocumentKind.Xml || !ReferenceEquals(actual.OwnerDocument, null))
        {
            Fail(name, "native document kind or owner");
        }

        var pending = new Stack<(AngleNode Expected, ParserModel.Node Actual, string Path)>();
        pending.Push((expected, actual, "document"));
        while (pending.TryPop(out var pair))
        {
            var (left, right, path) = pair;
            var kind = MapKind(left.NodeType);
            if (right.NodeType != kind)
            {
                Fail(name, $"{path}: node kind {left.NodeType} != {right.NodeType}");
            }

            if (left is not IDocument && (!ReferenceEquals(left.Owner, expected) ||
                !ReferenceEquals(right.OwnerDocument, actual)))
            {
                Fail(name, $"{path}: owner document");
            }

            if ((left.Parent is null) != (right.ParentNode is null))
            {
                Fail(name, $"{path}: parent linkage");
            }

            switch (left)
            {
                case IElement leftElement when right is ParserModel.Element rightElement:
                    CompareElement(name, path, leftElement, rightElement, actual);
                    break;
                case IDocumentType leftType when right is ParserModel.DocumentType rightType:
                    Equal(name, path, "doctype name", leftType.Name, rightType.Name);
                    Equal(name, path, "doctype public id", leftType.PublicIdentifier, rightType.PublicId);
                    Equal(name, path, "doctype system id", leftType.SystemIdentifier, rightType.SystemId);
                    break;
                case IProcessingInstruction leftInstruction when right is ParserModel.ProcessingInstruction rightInstruction:
                    Equal(name, path, "processing-instruction target", leftInstruction.Target, rightInstruction.Target);
                    Equal(name, path, "processing-instruction data", leftInstruction.NodeValue, rightInstruction.Data);
                    break;
                case ICharacterData leftData when right is ParserModel.Text rightText:
                    Equal(name, path, "text", leftData.Data, rightText.Data);
                    break;
                case ICharacterData leftData when right is ParserModel.CDataSection rightCData:
                    Equal(name, path, "CDATA", leftData.Data, rightCData.Data);
                    break;
                case ICharacterData leftData when right is ParserModel.Comment rightComment:
                    Equal(name, path, "comment", leftData.Data, rightComment.Data);
                    break;
            }

            var leftChildren = left.ChildNodes.ToArray();
            var rightChildren = right.ChildNodes.ToArray();
            if (leftChildren.Length != rightChildren.Length)
            {
                Fail(name, $"{path}: child count {leftChildren.Length} != {rightChildren.Length}");
            }

            for (var i = leftChildren.Length - 1; i >= 0; i--)
            {
                if (!ReferenceEquals(leftChildren[i].Parent, left) ||
                    !ReferenceEquals(rightChildren[i].ParentNode, right))
                {
                    Fail(name, $"{path}/{i}: child parent linkage");
                }

                pending.Push((leftChildren[i], rightChildren[i], $"{path}/{i}"));
            }
        }
    }

    private static void CompareElement(string name, string path, IElement left, ParserModel.Element right,
        ParserModel.Document document)
    {
        Equal(name, path, "local name", left.LocalName, right.LocalName);
        Equal(name, path, "prefix", left.Prefix, right.Prefix);
        Equal(name, path, "namespace", left.NamespaceUri, right.NamespaceUri);

        var leftAttributes = left.Attributes.ToArray();
        var rightAttributes = right.Attributes.ToArray();
        if (leftAttributes.Length != rightAttributes.Length)
        {
            Fail(name, $"{path}: attribute count {leftAttributes.Length} != {rightAttributes.Length}");
        }

        for (var i = 0; i < leftAttributes.Length; i++)
        {
            var a = leftAttributes[i];
            var b = rightAttributes[i];
            var attributePath = $"{path}/@{i}";
            CompareAttributeViews(name, attributePath,
                new AttributeView(a.Name, a.LocalName, a.Prefix, a.NamespaceUri, a.Value),
                new AttributeView(b.Name, b.LocalName, b.Prefix, b.NamespaceUri, b.Value));
            if (!ReferenceEquals(a.OwnerElement, left) || !ReferenceEquals(b.OwnerElement, right) ||
                !ReferenceEquals(b.OwnerDocument, document))
            {
                Fail(name, $"{attributePath}: attribute ownership");
            }
        }
    }

    private static ParserModel.NodeType MapKind(AngleSharp.Dom.NodeType kind) => kind switch
    {
        AngleSharp.Dom.NodeType.Document => ParserModel.NodeType.Document,
        AngleSharp.Dom.NodeType.Element => ParserModel.NodeType.Element,
        AngleSharp.Dom.NodeType.Text => ParserModel.NodeType.Text,
        AngleSharp.Dom.NodeType.CharacterData => ParserModel.NodeType.CDataSection,
        AngleSharp.Dom.NodeType.Comment => ParserModel.NodeType.Comment,
        AngleSharp.Dom.NodeType.ProcessingInstruction => ParserModel.NodeType.ProcessingInstruction,
        AngleSharp.Dom.NodeType.DocumentType => ParserModel.NodeType.DocumentType,
        AngleSharp.Dom.NodeType.DocumentFragment => ParserModel.NodeType.DocumentFragment,
        _ => throw new InvalidDataException($"Unsupported AngleSharp node kind {kind}"),
    };

    private readonly record struct AttributeView(string Name, string LocalName, string? Prefix,
        string? NamespaceUri, string Value);

    private static void CompareAttributeViews(string name, string path, AttributeView left, AttributeView right)
    {
        Equal(name, path, "name", left.Name, right.Name);
        Equal(name, path, "local name", left.LocalName, right.LocalName);
        Equal(name, path, "prefix", left.Prefix, right.Prefix);
        Equal(name, path, "value", left.Value, right.Value);

        if (left.Name == "xmlns")
        {
            // AngleSharp.Xml 1.2.0 constructs only the unprefixed declaration as a null-namespace
            // Attr; its xmlns-prefixed declarations use XMLNS. Require that exact pinned defect here.
            if (left.LocalName != "xmlns" || left.Prefix is not null ||
                left.NamespaceUri is not null and not "" ||
                right.Name != "xmlns" || right.LocalName != "xmlns" || right.Prefix is not null ||
                right.NamespaceUri != XmlnsNamespace)
            {
                Fail(name, $"{path}: default xmlns projection is not the pinned AngleSharp/native pair");
            }

            return;
        }

        if (left.Prefix == "xmlns")
        {
            if (left.NamespaceUri != XmlnsNamespace || right.NamespaceUri != XmlnsNamespace)
            {
                Fail(name, $"{path}: prefixed xmlns must use the XMLNS namespace on both sides");
            }

            return;
        }

        Equal(name, path, "namespace", left.NamespaceUri, right.NamespaceUri);
    }

    private static void Equal(string name, string path, string field, string? expected, string? actual)
    {
        if (!string.Equals(expected, actual, StringComparison.Ordinal))
        {
            Fail(name, $"{path}: {field} '{expected}' != '{actual}'");
        }
    }

    private static void Fail(string name, string detail) => throw new InvalidDataException($"{name}: {detail}");

    public static void ValidateNegativeProbes()
    {
        var corpusCase = ParserCorpus.Cases.Single(item => item.Name == "xml-small.xml");
        var expected = new XmlParser().ParseDocument(corpusCase.Source);
        var native = ParserModel.MarkupParser.ParseXml(corpusCase.Source);
        var cdata = Descendants(native).OfType<ParserModel.CDataSection>().Single();
        cdata.Data = "<Literal>";
        AssertRejected(corpusCase.Name, expected, native);

        native = ParserModel.MarkupParser.ParseXml(corpusCase.Source);
        cdata = Descendants(native).OfType<ParserModel.CDataSection>().Single();
        cdata.ParentNode!.ReplaceChild(native.CreateTextNode(cdata.Data), cdata);
        AssertRejected(corpusCase.Name, expected, native);

        native = ParserModel.MarkupParser.ParseXml(corpusCase.Source);
        var ranked = Descendants(native).OfType<ParserModel.Element>().Single(element => element.GetAttribute("id") == "a");
        var firstAttribute = ranked.Attributes.First();
        ranked.RemoveAttributeNode(firstAttribute);
        ranked.SetAttributeNode(firstAttribute);
        AssertRejected(corpusCase.Name, expected, native);

        native = ParserModel.MarkupParser.ParseXml(corpusCase.Source);
        ranked = Descendants(native).OfType<ParserModel.Element>().Single(element => element.GetAttribute("id") == "a");
        var rank = ranked.Attributes.Single(attribute => attribute.Name == "m:rank");
        ranked.RemoveAttributeNode(rank);
        var wrongNamespace = native.CreateAttributeNS("urn:fake", "m:rank");
        wrongNamespace.Value = rank.Value;
        ranked.SetAttributeNode(wrongNamespace);
        AssertRejected(corpusCase.Name, expected, native);

        var svgCase = ParserCorpus.Cases.Single(item => item.Name == "svg-small.svg");
        var expectedSvg = new XmlParser().ParseDocument(svgCase.Source);
        var nativeSvg = ParserModel.MarkupParser.ParseSvg(svgCase.Source);
        var use = Descendants(nativeSvg).OfType<ParserModel.Element>().Single(element => element.LocalName == "use");
        use.GetAttributeNodeNS("http://www.w3.org/1999/xlink", "href")!.Value = "#x";
        AssertRejected(svgCase.Name, expectedSvg, nativeSvg);

        nativeSvg = ParserModel.MarkupParser.ParseSvg(svgCase.Source);
        var svgRoot = nativeSvg.DocumentElement!;
        var defaultDeclaration = svgRoot.Attributes.First(attribute => attribute.Name == "xmlns");
        svgRoot.RemoveAttributeNode(defaultDeclaration);
        AssertRejected(svgCase.Name, expectedSvg, nativeSvg);

        nativeSvg = ParserModel.MarkupParser.ParseSvg(svgCase.Source);
        svgRoot = nativeSvg.DocumentElement!;
        defaultDeclaration = svgRoot.Attributes.First(attribute => attribute.Name == "xmlns");
        svgRoot.RemoveAttributeNode(defaultDeclaration);
        svgRoot.SetAttributeNode(defaultDeclaration);
        AssertRejected(svgCase.Name, expectedSvg, nativeSvg);

        nativeSvg = ParserModel.MarkupParser.ParseSvg(svgCase.Source);
        nativeSvg.DocumentElement!.GetAttributeNode("xmlns")!.Value = "urn:another";
        AssertRejected(svgCase.Name, expectedSvg, nativeSvg);

        native = ParserModel.MarkupParser.ParseXml(corpusCase.Source);
        var originalName = Descendants(native).OfType<ParserModel.Element>()
            .Single(element => element.LocalName == "name" && element.ParentNode is ParserModel.Element parent &&
                parent.GetAttribute("id") == "a");
        var wrongElementNamespace = native.CreateElementNS("urn:fake", "name");
        while (originalName.FirstChild is { } child)
        {
            wrongElementNamespace.AppendChild(child);
        }
        originalName.ParentNode!.ReplaceChild(wrongElementNamespace, originalName);
        AssertRejected(corpusCase.Name, expected, native);

        ValidateNamespaceProjectionNegatives();
    }

    private static void ValidateNamespaceProjectionNegatives()
    {
        var defaultLeft = new AttributeView("xmlns", "xmlns", null, null, "urn:catalog");
        var defaultRight = new AttributeView("xmlns", "xmlns", null, XmlnsNamespace, "urn:catalog");
        CompareAttributeViews("default xmlns control", "@0", defaultLeft, defaultRight);
        AssertViewRejected(defaultLeft, defaultRight with { Value = "urn:fake" });
        AssertViewRejected(defaultLeft, defaultRight with { NamespaceUri = null });
        AssertViewRejected(defaultLeft, defaultRight with { NamespaceUri = "urn:fake" });
        AssertViewRejected(defaultLeft with { NamespaceUri = "urn:fake" }, defaultRight);

        var prefixed = new AttributeView("xmlns:m", "m", "xmlns", XmlnsNamespace, "urn:meta");
        CompareAttributeViews("prefixed xmlns control", "@1", prefixed, prefixed);
        AssertViewRejected(prefixed with { NamespaceUri = null }, prefixed);
        AssertViewRejected(prefixed, prefixed with { NamespaceUri = null });
        AssertViewRejected(prefixed, prefixed with { NamespaceUri = "urn:fake" });

        var ordinary = new AttributeView("m:rank", "rank", "m", "urn:meta", "1");
        CompareAttributeViews("ordinary attribute control", "@2", ordinary, ordinary);
        AssertViewRejected(ordinary, ordinary with { NamespaceUri = "urn:fake" });
    }

    private static void AssertViewRejected(AttributeView left, AttributeView right)
    {
        try
        {
            CompareAttributeViews("attribute projection negative", "@0", left, right);
        }
        catch (InvalidDataException)
        {
            return;
        }

        Fail("attribute projection negative", "invalid namespace declaration tuple passed");
    }

    public static void ValidateAuxiliaryNodeKinds()
    {
        const string source = "<!DOCTYPE root [<!ENTITY word 'value'>]><?note ready?><root>&word;<![CDATA[part]]></root>";
        var expected = new XmlParser(new XmlParserOptions { IsSuppressingErrors = false }).ParseDocument(source);
        var actual = ParserModel.MarkupParser.ParseXml(source);
        Compare("XML auxiliary node kinds", expected, actual);

        var instruction = Descendants(actual).OfType<ParserModel.ProcessingInstruction>().Single();
        instruction.Data = "other";
        AssertRejected("XML processing instruction", expected, actual);

        actual = ParserModel.MarkupParser.ParseXml(source);
        var doctype = Descendants(actual).OfType<ParserModel.DocumentType>().Single();
        actual.ReplaceChild(actual.CreateDocumentType("different"), doctype);
        AssertRejected("XML doctype", expected, actual);
    }

    private static IEnumerable<ParserModel.Node> Descendants(ParserModel.Node root)
    {
        var pending = new Stack<ParserModel.Node>();
        pending.Push(root);
        while (pending.TryPop(out var node))
        {
            yield return node;
            foreach (var child in node.ChildNodes)
            {
                pending.Push(child);
            }
        }
    }

    private static void AssertRejected(string name, IDocument expected, ParserModel.Document actual)
    {
        try
        {
            Compare(name, expected, actual);
        }
        catch (InvalidDataException)
        {
            return;
        }

        Fail(name, "count-preserving corruption passed the semantic comparer");
    }
}
