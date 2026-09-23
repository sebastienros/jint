#nullable enable
using Jint.HtmlParser;

namespace Jint.Tests.HtmlParser.LiveTraversal;

public class BoundaryPrimitivesTests
{
    [Test]
    public void IdentityUsesTheUnderlyingNodeOrAttributeReference()
    {
        var document = Document.CreateHtml();
        var element = document.CreateElement("div");
        var attribute = document.CreateAttribute("id");
        element.SetAttributeNode(attribute);
        var nodeIdentity = new DomNodeIdentity(element);
        var attributeIdentity = new DomNodeIdentity(attribute);

        nodeIdentity.IsValid.Should().BeTrue();
        nodeIdentity.Node.Should().BeSameAs(element);
        nodeIdentity.Attribute.Should().BeNull();
        attributeIdentity.Attribute.Should().BeSameAs(attribute);
        attributeIdentity.Node.Should().BeNull();
        nodeIdentity.Equals(new DomNodeIdentity(element)).Should().BeTrue();
        nodeIdentity.Equals((object) new DomNodeIdentity(element)).Should().BeTrue();
        nodeIdentity.Equals(new DomNodeIdentity(element.CloneNode())).Should().BeFalse();
        nodeIdentity.Equals(attributeIdentity).Should().BeFalse();
        attributeIdentity.Equals(new DomNodeIdentity(attribute)).Should().BeTrue();
        attributeIdentity.Equals(new DomNodeIdentity(attribute.Clone())).Should().BeFalse();
        nodeIdentity.GetHashCode().Should().Be(System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(element));
        attributeIdentity.GetHashCode().Should().Be(System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(attribute));
        default(DomNodeIdentity).IsValid.Should().BeFalse();
        default(DomNodeIdentity).Equals(default(DomNodeIdentity)).Should().BeTrue();
        default(DomNodeIdentity).GetHashCode().Should().Be(0);
        Assert.Throws<ArgumentNullException>(() => new DomNodeIdentity((Node) null!));
        Assert.Throws<ArgumentNullException>(() => new DomNodeIdentity((Attr) null!));
        Assert.Throws<ArgumentException>(() => BoundaryOrder.GetLength(default));
        Assert.Throws<ArgumentException>(() => BoundaryOrder.GetRoot(default, default));
        Assert.Throws<ArgumentException>(() => BoundaryOrder.Compare(new BoundaryPoint(default, 0),
            new BoundaryPoint(nodeIdentity, 0), default));
    }

    [Test]
    public void LengthsAreUnsignedUtf16OrCurrentChildCounts()
    {
        var document = Document.CreateXml();
        var container = document.CreateElement("root");
        var text = document.CreateTextNode("a\ud83d\ude00");
        var cdata = document.CreateCDataSection("\ud83d\ude00");
        var comment = document.CreateComment("abc");
        var pi = document.CreateProcessingInstruction("target", "xy");
        var attribute = document.CreateAttribute("a");
        attribute.Value = "long value";
        container.AppendChild(text);
        container.AppendChild(cdata);

        BoundaryOrder.GetLength(new DomNodeIdentity(text)).Should().Be(3);
        BoundaryOrder.GetLength(new DomNodeIdentity(cdata)).Should().Be(2);
        BoundaryOrder.GetLength(new DomNodeIdentity(comment)).Should().Be(3);
        BoundaryOrder.GetLength(new DomNodeIdentity(pi)).Should().Be(2);
        BoundaryOrder.GetLength(new DomNodeIdentity(attribute)).Should().Be(0);
        BoundaryOrder.GetLength(new DomNodeIdentity(document.CreateDocumentType("html"))).Should().Be(0);
        BoundaryOrder.GetLength(new DomNodeIdentity(container)).Should().Be(2);
        container.RemoveChild(text);
        BoundaryOrder.GetLength(new DomNodeIdentity(container)).Should().Be(1);
        pi.Data = "changed";
        BoundaryOrder.GetLength(new DomNodeIdentity(pi)).Should().Be(7);
    }

    [Test]
    public void ComparesCrossBranchesAncestorsEqualPointsAndUnsignedExtremes()
    {
        var document = Document.CreateHtml();
        var root = document.CreateElement("root");
        var a = document.CreateElement("a");
        var b = document.CreateElement("b");
        var aText = document.CreateTextNode("abc");
        var bText = document.CreateTextNode("xyz");
        document.AppendChild(root);
        root.AppendChild(a);
        root.AppendChild(b);
        a.AppendChild(aText);
        b.AppendChild(bText);

        static BoundaryPoint Point(Node node, uint offset) => new(new DomNodeIdentity(node), offset);
        BoundaryOrder.Compare(Point(aText, 3), Point(bText, 0), default).Should().Be(-1);
        BoundaryOrder.Compare(Point(bText, 0), Point(aText, 3), default).Should().Be(1);
        BoundaryOrder.Compare(Point(root, 0), Point(aText, 0), default).Should().Be(-1);
        BoundaryOrder.Compare(Point(aText, 0), Point(root, 0), default).Should().Be(1);
        BoundaryOrder.Compare(Point(root, 1), Point(aText, 3), default).Should().Be(1);
        BoundaryOrder.Compare(Point(aText, 3), Point(root, 1), default).Should().Be(-1);
        BoundaryOrder.Compare(Point(root, 1), Point(bText, 0), default).Should().Be(-1);
        BoundaryOrder.Compare(Point(root, 2), Point(bText, 3), default).Should().Be(1);
        BoundaryOrder.Compare(Point(aText, 1), Point(aText, 1), default).Should().Be(0);
        BoundaryOrder.Compare(Point(aText, 1), Point(aText, 2), default).Should().Be(-1);
        BoundaryOrder.Compare(Point(aText, 2), Point(aText, 1), default).Should().Be(1);

        var overflow = Point(aText, uint.MaxValue);
        overflow.Offset.Should().Be(uint.MaxValue);
        ErrorName(() => BoundaryOrder.Compare(overflow, Point(bText, 0), default))
            .Should().Be("IndexSizeError");
        ErrorName(() => BoundaryOrder.Compare(Point(aText, 0), overflow, default))
            .Should().Be("IndexSizeError");
    }

    [Test]
    public void AttributesAreSingletonRootsAndDocumentTypesAreRejectedBeforeRootComparison()
    {
        var document = Document.CreateHtml();
        var element = document.CreateElement("div");
        var attribute = document.CreateAttribute("id");
        var otherAttribute = document.CreateAttribute("class");
        element.SetAttributeNode(attribute);
        var attr = new DomNodeIdentity(attribute);
        var elementId = new DomNodeIdentity(element);

        BoundaryOrder.GetRoot(attr, default).Should().Be(attr);
        BoundaryOrder.GetLength(attr).Should().Be(0);
        BoundaryOrder.Compare(new BoundaryPoint(attr, 0), new BoundaryPoint(attr, 0), default).Should().Be(0);
        ErrorName(() => BoundaryOrder.Compare(new BoundaryPoint(attr, 1),
            new BoundaryPoint(elementId, 0), default)).Should().Be("IndexSizeError");
        ErrorName(() => BoundaryOrder.Compare(new BoundaryPoint(attr, 0),
            new BoundaryPoint(elementId, 0), default)).Should().Be("WrongDocumentError");
        ErrorName(() => BoundaryOrder.Compare(new BoundaryPoint(attr, 0),
            new BoundaryPoint(new DomNodeIdentity(otherAttribute), 0), default)).Should().Be("WrongDocumentError");

        var doctype = new DomNodeIdentity(document.CreateDocumentType("html"));
        ErrorName(() => BoundaryOrder.Compare(new BoundaryPoint(doctype, uint.MaxValue),
            new BoundaryPoint(attr, 1), default)).Should().Be("InvalidNodeTypeError");
        ErrorName(() => BoundaryOrder.Compare(new BoundaryPoint(attr, 0),
            new BoundaryPoint(doctype, 0), default)).Should().Be("InvalidNodeTypeError");
    }

    [Test]
    public void OrdinaryRootsIgnoreOwnershipTemplateAndShadowHosts()
    {
        var document = Document.CreateHtml();
        var host = document.CreateElement("div");
        var template = document.CreateElement("template");
        document.AppendChild(host);
        host.AppendChild(template);
        var content = template.TemplateContent!;
        var inertChild = content.OwnerDocument!.CreateElement("inside");
        content.AppendChild(inertChild);
        var shadow = ShadowTree.Attach(host, new ShadowRootInit(ShadowRootMode.Open), default);
        var shadowChild = document.CreateElement("shadow-child");
        shadow.AppendChild(shadowChild);
        var detached = document.CreateElement("detached");

        BoundaryOrder.GetRoot(new DomNodeIdentity(template), default).Node.Should().BeSameAs(document);
        BoundaryOrder.GetRoot(new DomNodeIdentity(inertChild), default).Node.Should().BeSameAs(content);
        BoundaryOrder.GetRoot(new DomNodeIdentity(shadowChild), default).Node.Should().BeSameAs(shadow);
        BoundaryOrder.GetRoot(new DomNodeIdentity(detached), default).Node.Should().BeSameAs(detached);
        ErrorName(() => BoundaryOrder.Compare(new BoundaryPoint(new DomNodeIdentity(template), 0),
            new BoundaryPoint(new DomNodeIdentity(inertChild), 0), default)).Should().Be("WrongDocumentError");
        ErrorName(() => BoundaryOrder.Compare(new BoundaryPoint(new DomNodeIdentity(host), 0),
            new BoundaryPoint(new DomNodeIdentity(shadowChild), 0), default)).Should().Be("WrongDocumentError");
        ErrorName(() => BoundaryOrder.Compare(new BoundaryPoint(new DomNodeIdentity(document), 0),
            new BoundaryPoint(new DomNodeIdentity(detached), 0), default)).Should().Be("WrongDocumentError");
    }

    [Test]
    public void StaticRangeStoresMalformedValuesAndChecksCurrentTree()
    {
        var document = Document.CreateHtml();
        var root = document.CreateElement("root");
        var first = document.CreateElement("first");
        var second = document.CreateElement("second");
        document.AppendChild(root);
        root.AppendChild(first);
        root.AppendChild(second);
        static BoundaryPoint Point(Node node, uint offset) => new(new DomNodeIdentity(node), offset);

        var reversed = new DomStaticRange(Point(root, 2), Point(root, 0));
        reversed.Collapsed.Should().BeFalse();
        reversed.IsValid(default).Should().BeFalse();
        var huge = new DomStaticRange(Point(root, uint.MaxValue), Point(root, uint.MaxValue));
        huge.Collapsed.Should().BeTrue();
        huge.IsValid(default).Should().BeFalse();
        var differentRoots = new DomStaticRange(Point(root, 0), Point(document.CreateElement("detached"), 0));
        differentRoots.IsValid(default).Should().BeFalse();

        var range = new DomStaticRange(Point(root, 0), Point(root, 2));
        range.IsValid(default).Should().BeTrue();
        root.RemoveChild(second);
        range.End.Offset.Should().Be(2);
        range.IsValid(default).Should().BeFalse();
        root.AppendChild(second);
        range.IsValid(default).Should().BeTrue();
        var subtreeRange = new DomStaticRange(Point(first, 0), Point(second, 0));
        subtreeRange.IsValid(default).Should().BeTrue();
        root.RemoveChild(second);
        subtreeRange.IsValid(default).Should().BeFalse();
        root.AppendChild(second);
        subtreeRange.IsValid(default).Should().BeTrue();

        var doctype = new DomNodeIdentity(document.CreateDocumentType("html"));
        var attribute = new DomNodeIdentity(document.CreateAttribute("id"));
        ErrorName(() => new DomStaticRange(new BoundaryPoint(doctype, 0), Point(root, 0)))
            .Should().Be("InvalidNodeTypeError");
        ErrorName(() => new DomStaticRange(Point(root, 0), new BoundaryPoint(attribute, 0)))
            .Should().Be("InvalidNodeTypeError");
        Assert.Throws<ArgumentException>(() => new DomStaticRange(new BoundaryPoint(default, 0), Point(root, 0)));
    }

    [Test]
    public void CurrentOrderFollowsMovesAndAdoptionWithoutChangingStoredEndpoints()
    {
        var source = Document.CreateHtml();
        var destination = Document.CreateHtml();
        var root = source.CreateElement("root");
        var first = source.CreateElement("first");
        var second = source.CreateElement("second");
        source.AppendChild(root);
        root.AppendChild(first);
        root.AppendChild(second);
        var firstPoint = new BoundaryPoint(new DomNodeIdentity(first), 0);
        var secondPoint = new BoundaryPoint(new DomNodeIdentity(second), 0);
        var snapshot = new DomStaticRange(firstPoint, secondPoint);

        snapshot.IsValid(default).Should().BeTrue();
        root.InsertBefore(second, first);
        snapshot.IsValid(default).Should().BeFalse();
        BoundaryOrder.Compare(firstPoint, secondPoint, default).Should().Be(1);
        root.InsertBefore(first, second);
        snapshot.IsValid(default).Should().BeTrue();
        destination.AdoptNode(second);
        snapshot.IsValid(default).Should().BeFalse();
        ErrorName(() => BoundaryOrder.Compare(firstPoint, secondPoint, default))
            .Should().Be("WrongDocumentError");
        root.AppendChild(second); // Insertion adopts it back to the source document.
        snapshot.Start.Should().Be(firstPoint);
        snapshot.End.Should().Be(secondPoint);
        snapshot.IsValid(default).Should().BeTrue();
    }

    [Test]
    public void DeepAndWideComparisonPollsAndCanCancelDuringActualWork()
    {
        var document = Document.CreateHtml();
        var parent = document.CreateElement("root");
        var left = document.CreateElement("left");
        var right = document.CreateElement("right");
        parent.AppendChild(left);
        Element? nearEnd = null;
        for (var i = 0; i < 1024; i++)
        {
            nearEnd = document.CreateElement("middle");
            parent.AppendChild(nearEnd);
        }

        parent.AppendChild(right);
        var deep = left;
        for (var i = 0; i < 1024; i++)
        {
            var child = document.CreateElement("deep");
            deep.AppendChild(child);
            deep = child;
        }

        var observed = 0;
        BoundaryOrder.GetRoot(new DomNodeIdentity(deep), steps => observed = steps, default)
            .Node.Should().BeSameAs(parent);
        observed.Should().BeGreaterThan(1024);
        observed = 0;
        BoundaryOrder.Compare(new BoundaryPoint(new DomNodeIdentity(deep), 0),
            new BoundaryPoint(new DomNodeIdentity(right), 0), steps => observed = steps, default)
            .Should().Be(-1);
        observed.Should().BeGreaterThan(1024);
        observed = 0;
        BoundaryOrder.Compare(new BoundaryPoint(new DomNodeIdentity(right), 0),
            new BoundaryPoint(new DomNodeIdentity(nearEnd!), 0), steps => observed = steps, default)
            .Should().Be(1);
        observed.Should().BeGreaterThan(1024);

        using var deepCancellation = new CancellationTokenSource();
        Assert.Throws<OperationCanceledException>(() => BoundaryOrder.GetRoot(new DomNodeIdentity(deep),
            steps => { if (steps >= 512) deepCancellation.Cancel(); }, deepCancellation.Token));
        using var wideCancellation = new CancellationTokenSource();
        Assert.Throws<OperationCanceledException>(() => BoundaryOrder.Compare(
            new BoundaryPoint(new DomNodeIdentity(right), 0), new BoundaryPoint(new DomNodeIdentity(nearEnd!), 0),
            steps => { if (steps >= 512) wideCancellation.Cancel(); }, wideCancellation.Token));
    }

    [Test]
    public void EmptyAndFinalAscentPathsCheckCancellation()
    {
        var node = new DomNodeIdentity(Document.CreateHtml().CreateElement("single"));
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();
        Assert.Throws<OperationCanceledException>(() => BoundaryOrder.GetRoot(node, canceled.Token));
        Assert.Throws<OperationCanceledException>(() => BoundaryOrder.Compare(new BoundaryPoint(node, 0),
            new BoundaryPoint(node, 0), canceled.Token));
        Assert.Throws<OperationCanceledException>(() => new DomStaticRange(new BoundaryPoint(node, 0),
            new BoundaryPoint(node, 0)).IsValid(canceled.Token));

        using var finalCancellation = new CancellationTokenSource();
        Assert.Throws<OperationCanceledException>(() => BoundaryOrder.GetRoot(node,
            steps => { if (steps == 1) finalCancellation.Cancel(); }, finalCancellation.Token));
    }

    private static string ErrorName(Action action) => Assert.Throws<DomException>(action)!.Name;
}
