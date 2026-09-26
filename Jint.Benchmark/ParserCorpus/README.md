# Parser comparison corpus

These twelve UTF-8 inputs were written for this repository and are distributed under its
[BSD 2-Clause license](../../LICENSE.txt). They use fixed counts and names; none downloads content,
samples a live site or depends on a random seed. The files are copied to the benchmark output directory.

| Input | Bytes | Coverage |
| --- | ---: | --- |
| `html-small.html` | 535 | Doctype, attributes, entities, script rawtext, textarea, template, inline SVG and foreign HTML |
| `html-recovery.html` | 247 | Optional end tags, duplicate attributes, table cells and misnested formatting |
| `html-large.html` | 55,095 | 256 repeated articles with attributes, text, entities and links |
| `html-recovery-large.html` | 11,285 | 128 repeated broken paragraphs, tables and formatting elements |
| `svg-small.svg` | 374 | XML prolog, namespaces, gradient, text and XLink attribute |
| `svg-large.svg` | 23,205 | 192 groups with paths, transforms and text |
| `xml-small.xml` | 235 | Default and prefixed namespaces, entities, CDATA and comment |
| `xml-large.xml` | 26,229 | 256 namespaced catalog items and attributes |
| `css-rules-small.css` | 277 | Layer, media, supports, selectors, custom property and keyframes |
| `css-rules-large.css` | 20,347 | 192 nested selector rules and declarations |
| `css-declarations-small.css` | 120 | Shorthands, custom property, color and URL token |
| `css-declarations-large.css` | 16,195 | 256 custom properties and repeated standard declarations |

`HtmlParserCorpusBenchmark` parses each source into a complete AngleSharp document, stylesheet or
declaration block. HTML and the two error-recovery cases use the HTML parser. Standalone SVG and XML
use the XML parser; inline SVG in `html-small.html` exercises the HTML foreign-content rules. The
source string and parser instance are prepared in `GlobalSetup`; each measured operation creates a
fresh result. The case label includes UTF-8 input bytes, and `[MemoryDiagnoser]` tracks managed
allocation per operation.

Run `dotnet run -c Release --project Jint.Benchmark/Jint.Benchmark.csproj --
--validate-html-parser-corpus` from the repository root to parse all inputs without timing them.
It compares the resulting tree/rule counts with pinned AngleSharp shapes and checks HTML entities,
rawtext, template content, namespaces, table repair, XML CDATA and values, and CSS property values
inside conditional rules and keyframes. The shape includes a template's content fragment and XML
CDATA text. Count-preserving value mutations in HTML, XML and CSS must fail validation. These
pinned counts detect accidental fixture or baseline changes; they are an
AngleSharp reference, not a standards oracle.

`XmlSvgParserComparisonBenchmark` pairs the four standalone XML/SVG inputs with the public
`MarkupParser.ParseXml` and `ParseSvg` APIs. Its AngleSharp row is the control. Both rows receive
the same loaded string, keep parser configuration outside the timed method, and return a fresh,
complete document. `dotnet run -c Release --project Jint.Benchmark/Jint.Benchmark.csproj --
--validate-xml-svg-parser-comparison` compares the trees without timing. The comparer checks node
kinds, element and attribute names, prefixes, namespaces, values and order, text/CDATA, comments,
processing instructions, doctypes, parent links and owner documents. It also rejects deliberate
count-preserving changes to content, node kind, attribute order, regular attribute namespace and
SVG XLink value. A separate untimed XML input exercises an internal entity, PI and doctype because
the four throughput fixtures do not all carry those constructs.

The XML comparison uses default, unbounded parsing with no external entities in the inputs.
This is an **equal parse workload with a documented default-`xmlns` representation difference**,
not a claim of identical DOMs. Pinned AngleSharp.Xml 1.2.0 reports the unprefixed `xmlns`
declaration with a null/empty namespace; the native DOM reports the XMLNS namespace. Outside
timing, the comparer requires precisely those two tuples, including the same name, value, position
and owner. A prefixed `xmlns:m` or `xmlns:xlink` declaration must use XMLNS on both sides; ordinary
attributes and elements require exact namespace equality. Deliberately wrong declaration tuples,
missing or reordered declarations, and corrupt descendant bindings are rejected. The rows compare
parsed trees; SVG document MIME branding differs between the generic AngleSharp XML entry and
native `ParseSvg`.

`HtmlParserComparisonBenchmark` pairs the four HTML inputs with public `MarkupParser.ParseHtml`.
Both rows receive identical cached strings, reuse scripting-disabled configuration, retain no
source references or diagnostics, and produce fresh complete documents. Setup checks all node
kinds, values, attribute order/namespaces, child order, template subtrees and owner/parent links.
Untimed corruption probes exercise content, attributes, comments, namespaces and ownership.
Run `--validate-html-parser-comparison` for these checks without starting a timing run.

`HtmlControlsParserComparisonBenchmark` adds three separate pairs over 256 text/url/email/password
inputs: parsing alone, parsing plus first value access, and warm value access. First access includes
a fresh parse and control-array construction on both sides; warm access has row-owned parsed
documents and control arrays outside measurement. Setup verifies identical values and that native
parsing leaves input value sidecars absent. These rows deliberately expose deferred sanitizer cost.
URL and email values are already canonical: the pinned AngleSharp getter preserves surrounding URL
spaces while native HTML normalization removes them, so such inputs cannot establish equal observable
value workloads. Setup compares each control value, not only a checksum.
The native semantic row invokes the production native input-state accessor through benchmark
internal access; it does not claim a public parser-package semantic API.

CSS remains AngleSharp-only: native public CSS Syntax results are not comparable to AngleSharp
CSSOM output. A future candidate must return a fully materialized result of the same kind and validate it during
setup. A tokenizer-only row would not be comparable to document-producing rows. Use the
repository's paired benchmark procedure for reported timing numbers; no timing results are
recorded with this corpus.
