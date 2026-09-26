using System;
using System.Collections.Generic;
using Jint.HtmlParser;

namespace HtmlParserPackageConsumer;

internal static class Program
{
    private static int Main()
    {
        try
        {
            Require(typeof(Program).Assembly.GetName().GetPublicKey()?.Length is 0,
                "The consumer assembly must be unsigned.");
            var parserAssembly = typeof(Document).Assembly;
            Require(parserAssembly.GetName().Name == "Jint.HtmlParser", "The parser package assembly was not loaded.");

            CheckHtml();
            CheckXmlAndSvg();
            CheckNotationSurface();
            CheckFragmentOwnership();
            CheckMutationSubscriptions();
            Console.WriteLine("ALL PARSER PACKAGE PROBES PASSED");
            return 0;
        }
        catch (Exception error)
        {
            Console.Error.WriteLine(error);
            return 1;
        }
    }

    private static void CheckHtml()
    {
        var options = new HtmlParseOptions();
        Require(!options.ScriptingEnabled && ReferenceEquals(options.Limits, ParseLimits.Unbounded) &&
            options.Diagnostics is null, "HTML option defaults changed.");
        var empty = MarkupParser.ParseHtml("");
        Require(empty.Kind == DocumentKind.Html && empty.ContentType == "text/html" &&
            empty.DocumentElement?.LocalName == "html" && empty.DocumentElement.ChildCount == 2,
            "Empty HTML did not create implied structure.");
        var diagnostics = new ParseDiagnosticCollector(1);
        var recovered = MarkupParser.ParseHtml("<p>one<p>two</unexpected></unexpected>", new() { Diagnostics = diagnostics });
        Require(recovered.DocumentElement?.LastChild?.ChildCount == 2 &&
            diagnostics.Items.Count == 1 && diagnostics.IsTruncated, "HTML recovery/diagnostic bound failed.");
        var owner = Document.CreateHtml();
        var context = owner.CreateElement("table");
        var old = owner.CreateComment("unchanged");
        context.AppendChild(old);
        var fragment = MarkupParser.ParseHtmlFragment("<tr><td>x", context);
        Require(ReferenceEquals(fragment.OwnerDocument, owner) && fragment.ParentNode is null &&
            fragment.FirstChild is Element { LocalName: "tbody" } &&
            ReferenceEquals(fragment.FirstChild.OwnerDocument, owner) && ReferenceEquals(context.FirstChild, old),
            "Contextual HTML fragment ownership or recovery failed.");
        var svg = owner.CreateElementNS(Namespaces.Svg, "svg");
        Require(MarkupParser.ParseHtmlFragment("<circle/>", svg).FirstChild is Element { NamespaceUri: Namespaces.Svg },
            "Foreign HTML fragment failed.");
        var template = owner.CreateElement("template");
        var templateFragment = MarkupParser.ParseHtmlFragment("<p>x", template);
        Require(ReferenceEquals(templateFragment.OwnerDocument, owner) && template.TemplateContent?.ChildCount == 0,
            "Public template context changed the result owner or existing content.");
        foreach (var scripting in new[] { false, true })
        {
            var document = MarkupParser.ParseHtml("<script>throw 1</script><div><template shadowrootmode=open>x</template></div>",
                new() { ScriptingEnabled = scripting });
            Require(document.DocumentElement?.FirstChild?.FirstChild?.FirstChild is Text { Data: "throw 1" } &&
                document.DocumentElement.LastChild?.FirstChild?.FirstChild is Element { LocalName: "template" },
                "Public HTML changed script inertness or declarative-shadow permission.");
        }
        MarkupParser.ParseHtml("<br>", new() { Limits = new() { MaxInputCharacters = 4, MaxTokenCharacters = 4, MaxNestingDepth = 3 } });
        foreach (var (limits, kind) in new[]
        {
            (new ParseLimits { MaxInputCharacters = 3 }, ParseLimitKind.InputCharacters),
            (new ParseLimits { MaxTokenCharacters = 3 }, ParseLimitKind.TokenCharacters),
            (new ParseLimits { MaxNestingDepth = 2 }, ParseLimitKind.NestingDepth)
        })
        {
            try
            {
                MarkupParser.ParseHtml("<br>", new() { Limits = limits });
                throw new InvalidOperationException("HTML exceeded its configured bound.");
            }
            catch (ParseLimitException error) { Require(error.Kind == kind, "HTML limit taxonomy changed."); }
        }
        using var cancellation = new System.Threading.CancellationTokenSource();
        cancellation.Cancel();
        try
        {
            MarkupParser.ParseHtml("", cancellationToken: cancellation.Token);
            throw new InvalidOperationException("HTML ignored pre-cancellation.");
        }
        catch (OperationCanceledException) { }
        try
        {
            MarkupParser.ParseHtmlFragment("", context, cancellationToken: cancellation.Token);
            throw new InvalidOperationException("HTML fragment ignored pre-cancellation.");
        }
        catch (OperationCanceledException) { }
    }

    private static void CheckNotationSurface()
    {
        var document = MarkupParser.ParseXml("<root/>");
        IReadOnlyList<XmlNotationDeclaration> notations = document.XmlNotations;
        Require(notations.Count == 0, "A notation-free document has notation records.");
        foreach (var notation in notations)
        {
            Require(notation.Name.Length != 0 && (notation.PublicId is not null || notation.SystemId is not null),
                "A notation declaration lacks its name or external identifier.");
        }
    }

    private static void CheckXmlAndSvg()
    {
        var xml = MarkupParser.ParseXml("<?xml version='1.0'?><anything xmlns='urn:test'><child/></anything>");
        Require(xml.ContentType == "application/xml" && xml.CharacterSet == "UTF-8",
            "Generic XML document metadata changed.");
        Require(xml.DocumentElement?.LocalName == "anything" &&
            xml.DocumentElement.NamespaceUri == "urn:test", "Generic XML root was not preserved.");

        var svg = MarkupParser.ParseSvg("<s:svg xmlns:s='http://www.w3.org/2000/svg'><s:path/></s:svg>");
        var svgRoot = svg.DocumentElement ?? throw new InvalidOperationException("Strict SVG has no root.");
        Require(svg.ContentType == "image/svg+xml" && svgRoot.LocalName == "svg" &&
            svgRoot.NamespaceUri == Namespaces.Svg, "Strict SVG root was not recognized.");
        Require(svgRoot.FirstChild is Element path && path.LocalName == "path" &&
            path.NamespaceUri == Namespaces.Svg, "SVG child namespace was not preserved.");

        var generic = MarkupParser.ParseXml("<svg/>");
        Require(generic.DocumentElement?.NamespaceUri is null,
            "Generic XML must not infer the SVG namespace from a root spelling.");
        try
        {
            MarkupParser.ParseSvg("<svg/>");
            throw new InvalidOperationException("Strict SVG accepted an unnamespaced root.");
        }
        catch (MarkupParseException error) when (error.Code == "xml/svg-root-required" && error.Offset == 0)
        {
        }
    }

    private static void CheckFragmentOwnership()
    {
        var owner = MarkupParser.ParseXml("<root xmlns='urn:default' xmlns:p='urn:prefix'><old/></root>");
        var context = owner.DocumentElement!;
        var before = context.ChildCount;
        var fragment = MarkupParser.ParseXmlFragment("<p:item/><local/>text", context);
        Require(ReferenceEquals(fragment.OwnerDocument, owner) && context.ChildCount == before,
            "Fragment parsing changed context ownership or children.");
        Require(fragment.ChildCount == 3 && fragment.FirstChild is Element first &&
            first.NamespaceUri == "urn:prefix" && first.LocalName == "item",
            "Fragment prefix binding was not inherited.");
        Require(fragment.FirstChild?.NextSibling is Element second &&
            second.NamespaceUri == "urn:default" && second.LocalName == "local",
            "Fragment default namespace was not inherited.");
        Require(fragment.LastChild is Text text && text.Data == "text",
            "Fragment text was not retained.");
    }

    private static void CheckMutationSubscriptions()
    {
        var document = Document.CreateXml();
        var root = document.CreateElement("root");
        document.AppendChild(root);
        using var subscription = document.ObserveMutations(root, new MutationObserverOptions
        {
            ChildList = true,
            Subtree = true,
            Attributes = true,
            AttributeOldValue = true,
            CharacterData = true,
            CharacterDataOldValue = true
        });

        var child = document.CreateElement("child");
        root.AppendChild(child);
        child.SetAttribute("id", "one");
        child.SetAttribute("id", "two");
        var text = document.CreateTextNode("before");
        child.AppendChild(text);
        text.Data = "after";

        var records = subscription.TakeRecords();
        Require(records.Count == 5, "Unexpected mutation record count.");
        Require(records[0].Kind == MutationRecordKind.ChildList &&
            ReferenceEquals(records[0].Target, root) && records[0].AddedNodes.Count == 1 &&
            ReferenceEquals(records[0].AddedNodes[0], child), "Child insertion record is incorrect.");
        Require(records[1].Kind == MutationRecordKind.Attributes && records[1].AttributeName == "id" &&
            records[1].OldValue is null, "Initial attribute record is incorrect.");
        Require(records[2].Kind == MutationRecordKind.Attributes && records[2].OldValue == "one",
            "Attribute old value was not captured.");
        Require(records[3].Kind == MutationRecordKind.ChildList &&
            ReferenceEquals(records[3].AddedNodes[0], text), "Text insertion record is incorrect.");
        Require(records[4].Kind == MutationRecordKind.CharacterData &&
            records[4].OldValue == "before", "Character-data old value was not captured.");
        Require(subscription.TakeRecords().Count == 0 && records.Count == 5,
            "Draining mutated a prior record snapshot.");
        Require(records is IList<MutationRecord> readOnly && readOnly.IsReadOnly,
            "Drained records expose a mutable collection.");

        child.SetAttribute("id", "three");
        var delivery = subscription.TakeRecordsForDelivery();
        Require(delivery.Count == 1 && delivery[0].OldValue == "two" &&
            subscription.TakeRecords().Count == 0, "Delivery drain did not return one independent record.");
        subscription.Disconnect();
        child.SetAttribute("id", "four");
        Require(subscription.TakeRecords().Count == 0, "Disconnect left an active registration.");

        try
        {
            document.ObserveMutations(root, new MutationObserverOptions());
            throw new InvalidOperationException("An empty mutation option set was accepted.");
        }
        catch (ArgumentException)
        {
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
