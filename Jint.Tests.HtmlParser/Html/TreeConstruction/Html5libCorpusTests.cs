#nullable enable
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Jint.HtmlParser;
using Jint.HtmlParser.Html;

namespace Jint.Tests.HtmlParser.Html.TreeConstruction;

public class Html5libCorpusTests
{
    private static readonly string Corpus = Path.Combine(AppContext.BaseDirectory, "Html", "TreeConstruction", "Corpus");
    private sealed record Case(string Id, string Source, string? Context, bool? Scripting, string Expected);

    private sealed record Exclusion(string SourceSha256, string CurrentTree, string Reason, string Spec);
    private static readonly Lazy<Case[]> Cases = new(() => ReadCases().ToArray());
    private static readonly Lazy<Dictionary<string, Exclusion>> Exclusions = new(() =>
        JsonSerializer.Deserialize<Dictionary<string, Exclusion>>(File.ReadAllText(Path.Combine(Corpus, "exclusions.json")))!);

    private static IEnumerable<TestCaseData> TreeCases()
    {
        foreach (var item in Cases.Value)
        foreach (var scripting in item.Scripting is { } flag ? new[] { flag } : new[] { false, true })
            yield return new TestCaseData(item.Id, scripting).SetName($"Html5lib/{item.Id}/script-{(scripting ? "on" : "off")}");
    }

    [Test]
    public void PinAndExclusionCensus()
    {
        var cases = Cases.Value;
        Assert.That(Directory.GetFiles(Corpus, "*.dat"), Has.Length.EqualTo(57));
        Assert.That(cases, Has.Length.EqualTo(1792));
        Assert.That(cases.Select(c => c.Id).Distinct().Count(), Is.EqualTo(cases.Length));
        Assert.That(Exclusions.Value, Has.Count.EqualTo(4));
        foreach (var (id, exclusion) in Exclusions.Value)
        {
            var item = cases.Single(c => c.Id == id); // Unknown/stale IDs fail.
            Assert.That(Hash(Encoding.UTF8.GetBytes(item.Source)), Is.EqualTo(exclusion.SourceSha256), id);
            Assert.That(exclusion.Reason, Is.Not.Empty, id);
            Assert.That(exclusion.Spec, Does.StartWith("https://html.spec.whatwg.org/"), id);
            Assert.That(exclusion.CurrentTree, Is.Not.EqualTo(item.Expected), id);
        }
        using var pin = JsonDocument.Parse(File.ReadAllText(Path.Combine(Corpus, "corpus.lock.json")));
        Assert.That(pin.RootElement.GetProperty("revision").GetString(), Is.EqualTo("9329e64694e7835d0dcff9811e22856ef6ad16f9"));
        var files = pin.RootElement.GetProperty("files");
        var paths = Directory.GetFiles(Corpus, "*.dat", SearchOption.AllDirectories);
        Assert.That(paths, Has.Length.EqualTo(60));
        Assert.That(files.EnumerateObject().Count(), Is.EqualTo(paths.Length));
        foreach (var path in paths)
        {
            var name = Path.GetRelativePath(Corpus, path).Replace('\\', '/');
            Assert.That(Hash(File.ReadAllBytes(path)), Is.EqualTo(files.GetProperty(name).GetString()), name);
        }
    }

    private static string Hash(byte[] data) => Convert.ToHexString(SHA256.HashData(data)).ToLowerInvariant();

    // Section boundaries are exact whole lines; input loses only its section's final LF.
    private static IEnumerable<Case> ReadCases()
    {
        foreach (var path in Directory.GetFiles(Corpus, "*.dat").Order(StringComparer.Ordinal))
        {
            var data = File.ReadAllText(path);
            Assert.That(data, Does.StartWith("#data\n"), path);
            var starts = data[6..].Split("\n#data\n", StringSplitOptions.None);
            for (var i = 0; i < starts.Length; i++)
            {
                var sections = new Dictionary<string, string>(StringComparer.Ordinal);
                var section = "data";
                var value = new StringBuilder();
                foreach (var line in starts[i].Split('\n'))
                {
                    if (line is "#errors" or "#new-errors" or "#document-fragment" or "#script-on" or "#script-off" or "#document")
                    {
                        sections.Add(section, value.ToString());
                        section = line[1..];
                        value.Clear();
                    }
                    else value.Append(line).Append('\n');
                }
                sections.Add(section, value.ToString());
                var source = sections["data"];
                if (source.EndsWith('\n')) source = source[..^1];
                var context = sections.TryGetValue("document-fragment", out var fragment) ? fragment.TrimEnd('\n') : null;
                bool? scripting = sections.ContainsKey("script-on") ? true : sections.ContainsKey("script-off") ? false : null;
                yield return new Case($"{Path.GetFileName(path)}#{i + 1}", source, context, scripting, sections["document"].TrimEnd('\n'));
            }
        }
    }

    [TestCaseSource(nameof(TreeCases))]
    public void PinnedTree(string id, bool scripting)
    {
        var item = Cases.Value.Single(c => c.Id == id);
        var options = new HtmlParseOptions { ScriptingEnabled = scripting };
        var document = Document.CreateHtml();
        HtmlParserSession session;
        if (item.Context is { } context)
        {
            var space = context.IndexOf(' ');
            var ns = space < 0 ? Namespaces.Html : context[..space] switch
            {
                "svg" => Namespaces.Svg,
                "math" => Namespaces.MathMl,
                _ => throw new InvalidDataException(context)
            };
            session = HtmlParserSession.CreateFragment(document.CreateElementNS(ns, space < 0 ? context : context[(space + 1)..]), options);
        }
        else session = new HtmlParserSession(document, options);
        session.AppendInput(item.Source, isFinal: true);
        HtmlParseStep step;
        var turns = 0;
        do
        {
            step = session.Drive(4096, CancellationToken.None);
            Assert.That(++turns, Is.LessThan(10000), item.Id);
        } while (step.Kind == HtmlParseStepKind.Yielded);
        Assert.That(step.Kind, Is.EqualTo(HtmlParseStepKind.Complete), item.Id);
        var actual = Dump(session.Fragment ?? (Node) document);
        if (Exclusions.Value.TryGetValue(id, out var exclusion))
        {
            Assert.That(actual, Is.Not.EqualTo(item.Expected), $"Stale exclusion {id}: historical tree now passes");
            Assert.That(actual, Is.EqualTo(exclusion.CurrentTree), $"{id}: {exclusion.Reason} ({exclusion.Spec})");
        }
        else Assert.That(actual, Is.EqualTo(item.Expected), $"{id}, scripting={scripting}, source={item.Source}");
    }

    private static string Dump(Node root)
    {
        var output = new StringBuilder();
        void Line(int depth, string value) => output.Append("| ").Append(' ', depth * 2).Append(value).Append('\n');
        static string Name(string? ns, string local) => (ns switch
        {
            Namespaces.Svg => "svg ", Namespaces.MathMl => "math ",
            "http://www.w3.org/1999/xlink" => "xlink ", Namespaces.Xml => "xml ", Namespaces.Xmlns => "xmlns ", _ => ""
        }) + local;
        void Visit(Node node, int depth)
        {
            switch (node)
            {
                case Element element:
                    Line(depth, "<" + Name(element.NamespaceUri, element.LocalName) + ">");
                    foreach (var attr in element.Attributes.OrderBy(a => Name(a.NamespaceUri, a.LocalName), StringComparer.Ordinal))
                        Line(depth + 1, Name(attr.NamespaceUri, attr.LocalName) + "=\"" + attr.Value + "\"");
                    foreach (var child in element.ChildNodes) Visit(child, depth + 1);
                    if (element.TemplateContent is { } content)
                    {
                        Line(depth + 1, "content");
                        foreach (var child in content.ChildNodes) Visit(child, depth + 2);
                    }
                    break;
                case Text text: Line(depth, "\"" + text.Data + "\""); break;
                case Comment comment: Line(depth, "<!-- " + comment.Data + " -->"); break;
                case ProcessingInstruction pi: Line(depth, "<?" + pi.Target + " " + pi.Data + ">"); break;
                case DocumentType type:
                    Line(depth, "<!DOCTYPE " + type.Name + (type.PublicId.Length != 0 || type.SystemId.Length != 0
                        ? " \"" + type.PublicId + "\" \"" + type.SystemId + "\"" : "") + ">");
                    break;
                default: throw new InvalidDataException(node.GetType().Name);
            }
        }
        foreach (var child in root.ChildNodes) Visit(child, 0);
        return output.ToString().TrimEnd('\n');
    }
}
