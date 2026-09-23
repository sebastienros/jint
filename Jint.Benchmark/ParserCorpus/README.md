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

When Jint.HtmlParser has a stable API, add a candidate row to the same benchmark class. Reuse
`Case.Source`, return a fully materialized result of the same kind, and keep parser construction
outside the timed method. Validate the candidate's structure and representative values during
setup before measuring. Compare HTML recovery behavior against the applicable standard when it
differs from AngleSharp, and record any intentional differences. A tokenizer-only row would not
be comparable to these document-producing rows. Use the repository's paired benchmark procedure
for reported timing numbers; no timing results are recorded with this corpus.
