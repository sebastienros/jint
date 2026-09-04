import assert from "node:assert/strict";
import { test } from "node:test";

import { extractSnippets, renderMarkdown } from "./sync-snippets.mjs";

test("extracts and dedents named documentation regions", () => {
  const snippets = extractSnippets([{
    path: "Sample.cs",
    content: `
public static void Sample()
{
    #region docs:hello

    var engine = new Engine();
    engine.Execute("hello()");

    #endregion
}`
  }]);

  assert.equal(
    snippets.get("hello").content,
    "var engine = new Engine();\nengine.Execute(\"hello()\");"
  );
});

test("rejects duplicate documentation region names", () => {
  assert.throws(
    () => extractSnippets([
      { path: "One.cs", content: "#region docs:same\n1\n#endregion" },
      { path: "Two.cs", content: "#region docs:same\n2\n#endregion" }
    ]),
    /duplicate snippet same/
  );
});

test("renders source-backed fenced blocks between stable markers", () => {
  const snippets = new Map([
    ["hello", { content: "var answer = 6 * 7;", path: "Sample.cs", line: 1 }]
  ]);
  const source = "<!-- snippet: hello -->\nold\n<!-- endSnippet -->";

  const result = renderMarkdown(source, snippets);

  assert.equal(
    result.content,
    "<!-- snippet: hello -->\n```csharp\nvar answer = 6 * 7;\n```\n<!-- endSnippet -->"
  );
  assert.deepEqual([...result.references], ["hello"]);
});

test("rejects markers that do not name a compiled snippet", () => {
  assert.throws(
    () => renderMarkdown(
      "<!-- snippet: missing -->\nold\n<!-- endSnippet -->",
      new Map()
    ),
    /snippet missing does not exist/
  );
});

test("ignores marker examples inside fenced code", () => {
  const source = "```markdown\n<!-- snippet: example -->\n<!-- endSnippet -->\n```";

  const result = renderMarkdown(source, new Map());

  assert.equal(result.content, source);
  assert.deepEqual([...result.references], []);
});
