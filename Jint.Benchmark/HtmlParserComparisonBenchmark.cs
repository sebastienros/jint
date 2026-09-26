#nullable enable
using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using AngleSharp.Html.Parser;
using BenchmarkDotNet.Attributes;
using AngleHtmlParser = global::AngleSharp.Html.Parser.HtmlParser;
using ParserModel = global::Jint.HtmlParser;

namespace Jint.Benchmark;

/// <summary>
/// Paired complete HTML documents from the same cached source, with scripting and diagnostics off.
/// Configuration is reused, documents are fresh per invocation, and no engine or Browser runs.
/// Setup compares every node, value, attribute, namespace, template subtree and owner outside timing.
/// </summary>
[MemoryDiagnoser]
[BenchmarkCategory("HtmlParserComparison")]
public class HtmlParserComparisonBenchmark
{
    private AngleHtmlParser _angle = null!;
    private ParserModel.HtmlParseOptions _native = null!;

    [ParamsSource(nameof(Cases))]
    public ParserCorpusCase Case { get; set; } = null!;
    public static IEnumerable<ParserCorpusCase> Cases => ParserCorpus.Cases.Where(item => item.Kind == ParserCorpusKind.Html);

    [GlobalSetup(Target = nameof(ParseAngleSharp))]
    public void SetupAngleSharp()
    {
        _angle = new AngleHtmlParser(new HtmlParserOptions { IsScripting = false });
        ParserCorpus.Validate(Case, ParseAngleSharp());
    }

    [GlobalSetup(Target = nameof(ParseNative))]
    public void SetupNative()
    {
        _angle = new AngleHtmlParser(new HtmlParserOptions { IsScripting = false });
        _native = new ParserModel.HtmlParseOptions();
        var reference = ParseAngleSharp();
        ParserCorpus.Validate(Case, reference);
        HtmlCorpusComparer.Compare(Case.Name, reference, ParseNative());
    }

    /// <summary>Control path cannot reach the native parser.</summary>
    [Benchmark(Baseline = true)]
    public IDocument ParseAngleSharp() => _angle.ParseDocument(Case.Source);

    [Benchmark]
    public ParserModel.Document ParseNative() => ParserModel.MarkupParser.ParseHtml(Case.Source, _native);

    /// <summary>Correctness only; never starts BenchmarkDotNet.</summary>
    public static int ValidateAll()
    {
        foreach (var item in Cases)
        {
            var benchmark = new HtmlParserComparisonBenchmark { Case = item };
            benchmark.SetupAngleSharp();
            benchmark.SetupNative();
            Console.WriteLine($"{item}: complete HTML tree, template and ownership comparison passed");
        }
        HtmlCorpusComparer.ValidateNegativeProbes();
        HtmlControlsParserComparisonBenchmark.ValidateAll();
        Console.WriteLine("HTML corruption probes rejected; CSS comparison remains blocked on comparable CSSOM output.");
        return 0;
    }
}

internal static class HtmlCorpusComparer
{
    internal static void Compare(string name, IDocument expected, ParserModel.Document actual)
    {
        if (actual.Kind != ParserModel.DocumentKind.Html || actual.OwnerDocument is not null ||
            actual.ContentType != expected.ContentType) Fail(name, "document metadata");
        var pending = new Stack<(AngleSharp.Dom.INode Left, ParserModel.Node Right, IDocument LeftOwner, ParserModel.Document RightOwner)>();
        pending.Push((expected, actual, expected, actual));
        while (pending.TryPop(out var pair))
        {
            var (left, right, leftOwner, rightOwner) = pair;
            if ((int) left.NodeType != (int) right.NodeType) Fail(name, "node type");
            if (left is not IDocument && (!ReferenceEquals(left.Owner, leftOwner) ||
                !ReferenceEquals(right.OwnerDocument, rightOwner))) Fail(name, "owner document");
            if ((left.Parent is null) != (right.ParentNode is null)) Fail(name, "parent linkage");
            switch (left)
            {
                case IElement a when right is ParserModel.Element b:
                    Equal(name, a.LocalName, b.LocalName); Equal(name, a.Prefix, b.Prefix); Equal(name, a.NamespaceUri, b.NamespaceUri);
                    var attrs = a.Attributes.ToArray(); var copies = b.Attributes.ToArray();
                    if (attrs.Length != copies.Length) Fail(name, "attribute count");
                    for (var i = 0; i < attrs.Length; i++)
                    {
                        var x = attrs[i]; var y = copies[i];
                        Equal(name, x.Name, y.Name); Equal(name, x.LocalName, y.LocalName);
                        Equal(name, x.Prefix, y.Prefix); Equal(name, x.NamespaceUri, y.NamespaceUri); Equal(name, x.Value, y.Value);
                        if (!ReferenceEquals(x.OwnerElement, a) || !ReferenceEquals(y.OwnerElement, b) ||
                            !ReferenceEquals(y.OwnerDocument, rightOwner)) Fail(name, "attribute ownership");
                    }
                    if (a is IHtmlTemplateElement template)
                    {
                        var content = b.TemplateContent ?? throw new InvalidDataException(name + ": missing template content");
                        if (content.ParentNode is not null || !ReferenceEquals(content.Host, b) ||
                            content.OwnerDocument!.Kind != rightOwner.Kind ||
                            (rightOwner.ContentType == "text/html" ? ReferenceEquals(content.OwnerDocument, rightOwner)
                                : !ReferenceEquals(content.OwnerDocument, rightOwner)))
                            Fail(name, "template owner/parent");
                        pending.Push((template.Content, content, template.Content.Owner!, content.OwnerDocument!));
                    }
                    else if (b.TemplateContent is not null) Fail(name, "unexpected template content");
                    break;
                case IDocumentType a when right is ParserModel.DocumentType b:
                    Equal(name, a.Name, b.Name); Equal(name, a.PublicIdentifier, b.PublicId); Equal(name, a.SystemIdentifier, b.SystemId);
                    break;
                case ICharacterData a when right is ParserModel.Text b: Equal(name, a.Data, b.Data); break;
                case ICharacterData a when right is ParserModel.Comment b: Equal(name, a.Data, b.Data); break;
                case IProcessingInstruction a when right is ParserModel.ProcessingInstruction b:
                    Equal(name, a.Target, b.Target); Equal(name, a.NodeValue, b.Data); break;
            }
            var children = left.ChildNodes.ToArray(); var copiesOfChildren = right.ChildNodes.ToArray();
            if (children.Length != copiesOfChildren.Length) Fail(name, "child count");
            for (var i = children.Length - 1; i >= 0; i--)
            {
                if (!ReferenceEquals(children[i].Parent, left) || !ReferenceEquals(copiesOfChildren[i].ParentNode, right))
                    Fail(name, "child parent linkage");
                pending.Push((children[i], copiesOfChildren[i], leftOwner, rightOwner));
            }
        }
    }

    internal static IEnumerable<ParserModel.Node> Descendants(ParserModel.Node root)
    {
        var pending = new Stack<ParserModel.Node>(); pending.Push(root);
        while (pending.TryPop(out var node))
        {
            yield return node;
            if (node is ParserModel.Element { TemplateContent: { } content }) pending.Push(content);
            for (var child = node.LastChild; child is not null; child = child.PreviousSibling) pending.Push(child);
        }
    }

    internal static void ValidateNegativeProbes()
    {
        const string source = "<!doctype html><p id=x title=y>content<!--comment--><svg><circle/></svg><template><b>inside</b></template>";
        var reference = new AngleHtmlParser(new HtmlParserOptions { IsScripting = false }).ParseDocument(source);
        Reject(document => Descendants(document).OfType<ParserModel.Text>().First().Data = "Content");
        Reject(document => Descendants(document).OfType<ParserModel.Element>().First(e => e.LocalName == "p").SetAttribute("id", "y"));
        Reject(document => Descendants(document).OfType<ParserModel.Comment>().Single().Data = "Comment");
        Reject(document =>
        {
            var template = Descendants(document).OfType<ParserModel.Element>().Single(e => e.LocalName == "template");
            ((ParserModel.Text) template.TemplateContent!.FirstChild!.FirstChild!).Data = "Inside";
        });
        Reject(document =>
        {
            var circle = Descendants(document).OfType<ParserModel.Element>().Single(e => e.LocalName == "circle");
            circle.ParentNode!.ReplaceChild(document.CreateElementNS(ParserModel.Namespaces.Html, "circle"), circle);
        });
        Reject(document =>
        {
            var paragraph = Descendants(document).OfType<ParserModel.Element>().Single(e => e.LocalName == "p");
            paragraph.Attributes.First().Rehome(ParserModel.Document.CreateHtml());
        });
        Reject(document =>
        {
            var paragraph = Descendants(document).OfType<ParserModel.Element>().Single(e => e.LocalName == "p");
            paragraph.RemoveAttribute("id"); paragraph.SetAttribute("id", "x");
        });
        Reject(document =>
        {
            var paragraph = Descendants(document).OfType<ParserModel.Element>().Single(e => e.LocalName == "p");
            paragraph.RemoveAttribute("title"); paragraph.SetAttributeNS("urn:wrong", "title", "y");
        });
        Reject(document =>
        {
            var paragraph = Descendants(document).OfType<ParserModel.Element>().Single(e => e.LocalName == "p");
            paragraph.InsertBefore(paragraph.ChildNodes.OfType<ParserModel.Comment>().Single(), paragraph.FirstChild);
        });
        Reject(document =>
        {
            var paragraph = Descendants(document).OfType<ParserModel.Element>().Single(e => e.LocalName == "p");
            paragraph.ReplaceChild(document.CreateComment("content"), paragraph.FirstChild!);
        });
        void Reject(Action<ParserModel.Document> corrupt)
        {
            var native = ParserModel.MarkupParser.ParseHtml(source);
            var shape = Shape(native);
            corrupt(native);
            if (Shape(native) != shape) throw new InvalidDataException("Negative HTML probe changed aggregate counts.");
            try { Compare("negative HTML probe", reference, native); }
            catch (InvalidDataException) { return; }
            throw new InvalidDataException("HTML corruption escaped comparison");
        }
    }

    private static (int Nodes, int Attributes, int Characters) Shape(ParserModel.Document document)
    {
        var nodes = 0; var attributes = 0; var characters = 0;
        foreach (var node in Descendants(document))
        {
            nodes++;
            if (node is ParserModel.Element element) attributes += element.AttributeCount;
            if (node is ParserModel.Text text) characters += text.Data.Length;
            if (node is ParserModel.Comment comment) characters += comment.Data.Length;
        }
        return (nodes, attributes, characters);
    }

    private static void Equal(string name, string? left, string? right)
    {
        if (!string.Equals(left, right, StringComparison.Ordinal)) Fail(name, $"value '{left}' != '{right}'");
    }
    private static void Fail(string name, string detail) => throw new InvalidDataException($"{name}: {detail}");
}
