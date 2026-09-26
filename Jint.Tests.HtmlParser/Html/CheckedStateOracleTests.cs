#nullable enable
using Jint.HtmlParser;

namespace Jint.Tests.HtmlParser.Html;

public class CheckedStateOracleTests
{
    [Test]
    public void DeterministicMutationsAgreeWithIndependentTreeDerivedMembership()
    {
        var document = Document.CreateHtml();
        var root = document.CreateElement("main");
        document.AppendChild(root);
        var detached = document.CreateElement("div");
        var form = document.CreateElement("form");
        form.SetAttribute("id", "f");
        root.AppendChild(form);
        var inputs = Enumerable.Range(0, 24).Select(i =>
        {
            var input = document.CreateElement("input");
            input.SetAttribute("type", "radio");
            input.SetAttribute("name", i % 3 == 0 ? "G" : "g");
            (i % 2 == 0 ? root : detached).AppendChild(input);
            return input;
        }).ToArray();
        var random = new Random(6371);
        for (var step = 0; step < 400; step++)
        {
            var input = inputs[random.Next(inputs.Length)];
            switch (step % 10)
            {
                case 0: HtmlCheckableState.Get(input)!.SetChecked(random.Next(2) == 0, default); break;
                case 1: input.SetAttribute("name", new[] { "g", "G", " ", "é", "" }[random.Next(5)]); break;
                case 2: input.SetAttribute("type", random.Next(3) == 0 ? "checkbox" : "radio"); break;
                case 3: (random.Next(2) == 0 ? root : detached).AppendChild(input); break;
                case 4: input.SetAttribute("form", random.Next(2) == 0 ? "f" : ""); break;
                case 5: input.RemoveAttribute("form"); break;
                case 6: input.SetAttribute("required", ""); break;
                case 7: input.RemoveAttribute("required"); break;
                case 8: form.SetAttribute("id", step % 20 == 8 ? "f" : "other"); break;
                case 9: HtmlCheckednessAlgorithms.ResetCheckedness(input, default); break;
            }
            foreach (var candidate in inputs) Check(candidate);
        }
    }

    private static void Check(Element input)
    {
        var before = HtmlCheckableState.Get(input)!.Checked;
        var group = SlowGroup(input);
        var facts = HtmlCheckableState.GetRadioGroupFacts(input, default);
        facts.Applies.Should().Be(group.Count > 0);
        facts.MemberCount.Should().Be(group.Count);
        facts.CheckedCount.Should().Be(group.Count(element => HtmlCheckableState.Get(element)!.Checked));
        facts.RequiredCount.Should().Be(group.Count(element => element.GetAttributeNodeNS(null, "required") is not null));
        HtmlCheckableState.SnapshotRadioGroup(input, default).Should().Equal(group);
        HtmlCheckableState.FirstCheckedRadio(input, default).Should().BeSameAs(group.FirstOrDefault(element => HtmlCheckableState.Get(element)!.Checked));
        HtmlCheckableState.Get(input)!.Checked.Should().Be(before, "queries must not normalize flags");
    }

    // Deliberately independent: no production root, group, classifier or index helpers.
    private static List<Element> SlowGroup(Element input)
    {
        if (!string.Equals(input.GetAttribute("type"), "radio", StringComparison.OrdinalIgnoreCase)) return [];
        var name = input.GetAttributeNodeNS(null, "name")?.Value;
        if (string.IsNullOrEmpty(name)) return [input];
        Node root = input;
        while (root.ParentNode is { } parent) root = parent;
        var result = new List<Element>();
        var pending = new Stack<Node>();
        pending.Push(root);
        while (pending.TryPop(out var node))
        {
            if (node is Element { NamespaceUri: Namespaces.Html, LocalName: "input" } candidate &&
                string.Equals(candidate.GetAttribute("type"), "radio", StringComparison.OrdinalIgnoreCase) &&
                candidate.GetAttributeNodeNS(null, "name")?.Value == name &&
                ReferenceEquals(candidate.FormAssociationState?.Owner, input.FormAssociationState?.Owner)) result.Add(candidate);
            for (var child = node.LastChild; child is not null; child = child.PreviousSibling) pending.Push(child);
        }
        return result;
    }
}
