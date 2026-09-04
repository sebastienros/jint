import process from "node:process";
import {
  existsSync,
  readFileSync,
  readdirSync,
  writeFileSync
} from "node:fs";
import { dirname, extname, join, relative, resolve } from "node:path";
import { fileURLToPath } from "node:url";

const regionStart = /^\s*#region\s+docs:([A-Za-z0-9_.-]+)\s*$/;
const regionEnd = /^\s*#endregion(?:\s.*)?$/;
const markerStart = /^\s*<!--\s*snippet:\s*([A-Za-z0-9_.-]+)\s*-->\s*$/;
const markerEnd = /^\s*<!--\s*endSnippet\s*-->\s*$/;
const fenceStart = /^\s*(`{3,}|~{3,})/;

const filesUnder = (directory, predicate, ignoredDirectories = new Set()) => {
  const files = [];

  const visit = (current) => {
    for (const entry of readdirSync(current, { withFileTypes: true })) {
      if (entry.isDirectory() && ignoredDirectories.has(entry.name)) {
        continue;
      }

      const path = join(current, entry.name);
      if (entry.isDirectory()) {
        visit(path);
      } else if (entry.isFile() && predicate(path)) {
        files.push(path);
      }
    }
  };

  visit(directory);
  return files.sort();
};

const dedent = (lines) => {
  while (lines.length > 0 && lines[0].trim() === "") {
    lines.shift();
  }
  while (lines.length > 0 && lines.at(-1).trim() === "") {
    lines.pop();
  }

  const contentLines = lines.filter((line) => line.trim() !== "");
  const indentation = contentLines.length === 0
    ? 0
    : Math.min(...contentLines.map((line) => line.match(/^\s*/)[0].length));

  return lines.map((line) => line.slice(Math.min(indentation, line.length))).join("\n");
};

export const extractSnippets = (sources) => {
  const snippets = new Map();

  for (const source of sources) {
    const lines = source.content.split(/\r?\n/);
    let active = null;

    for (let index = 0; index < lines.length; index++) {
      const start = lines[index].match(regionStart);
      if (start) {
        if (active) {
          throw new Error(`${source.path}:${index + 1}: documentation regions cannot be nested`);
        }
        active = { name: start[1], line: index + 1, lines: [] };
        continue;
      }

      if (regionEnd.test(lines[index])) {
        if (!active) {
          continue;
        }

        const content = dedent(active.lines);
        if (content === "") {
          throw new Error(`${source.path}:${active.line}: snippet ${active.name} is empty`);
        }
        if (snippets.has(active.name)) {
          const existing = snippets.get(active.name);
          throw new Error(
            `${source.path}:${active.line}: duplicate snippet ${active.name}; ` +
            `first declared at ${existing.path}:${existing.line}`
          );
        }

        snippets.set(active.name, {
          content,
          path: source.path,
          line: active.line
        });
        active = null;
        continue;
      }

      if (active) {
        active.lines.push(lines[index]);
      }
    }

    if (active) {
      throw new Error(`${source.path}:${active.line}: snippet ${active.name} has no #endregion`);
    }
  }

  return snippets;
};

export const renderMarkdown = (source, snippets) => {
  const newline = source.includes("\r\n") ? "\r\n" : "\n";
  const lines = source.split(/\r?\n/);
  const output = [];
  const references = new Set();
  let activeFence = null;

  for (let index = 0; index < lines.length; index++) {
    const line = lines[index];
    const fence = line.match(fenceStart);
    if (fence) {
      if (!activeFence) {
        activeFence = fence[1];
      } else if (fence[1][0] === activeFence[0] && fence[1].length >= activeFence.length) {
        activeFence = null;
      }
      output.push(line);
      continue;
    }

    const start = activeFence ? null : line.match(markerStart);
    if (!start) {
      if (!activeFence && markerEnd.test(line)) {
        throw new Error("endSnippet marker has no snippet start marker");
      }
      output.push(line);
      continue;
    }

    const name = start[1];
    const snippet = snippets.get(name);
    if (!snippet) {
      throw new Error(`snippet ${name} does not exist`);
    }

    let endIndex = index + 1;
    while (endIndex < lines.length && !markerEnd.test(lines[endIndex])) {
      endIndex++;
    }
    if (endIndex === lines.length) {
      throw new Error(`snippet ${name} is missing its endSnippet marker`);
    }

    references.add(name);
    output.push(line, "```csharp", ...snippet.content.split("\n"), "```", lines[endIndex]);
    index = endIndex;
  }

  return { content: output.join(newline), references };
};

const repositoryRoot = resolve(dirname(fileURLToPath(import.meta.url)), "../..");
const sampleDirectory = join(repositoryRoot, "docs", "snippets", "Jint.Documentation.Samples");
const packageReadmes = [
  "README.md",
  "Jint.DevTools/README.md",
  "Jint.Browser/README.md",
  "Jint.Browser.Playwright/README.md",
  "Jint.Browser.Tool/README.md",
  "Jint.Browser.Mcp/README.md"
];

export const synchronize = ({ checkOnly = false } = {}) => {
  const sampleFiles = filesUnder(sampleDirectory, (path) => extname(path) === ".cs");
  const snippets = extractSnippets(sampleFiles.map((path) => ({
    path: relative(repositoryRoot, path),
    content: readFileSync(path, "utf8")
  })));

  const documentationFiles = filesUnder(
    join(repositoryRoot, "docs"),
    (path) => extname(path) === ".md",
    new Set([".vitepress", "node_modules", "snippets"])
  );
  const markdownFiles = [
    ...documentationFiles,
    ...packageReadmes
      .map((path) => join(repositoryRoot, path))
      .filter(existsSync)
  ].sort();

  const changed = [];
  const referenced = new Set();

  for (const path of markdownFiles) {
    const original = readFileSync(path, "utf8");
    let rendered;
    try {
      rendered = renderMarkdown(original, snippets);
    } catch (error) {
      throw new Error(`${relative(repositoryRoot, path)}: ${error.message}`, { cause: error });
    }

    for (const name of rendered.references) {
      referenced.add(name);
    }

    if (rendered.content === original) {
      continue;
    }

    changed.push(relative(repositoryRoot, path));
    if (!checkOnly) {
      writeFileSync(path, rendered.content);
    }
  }

  const unreferenced = [...snippets.keys()].filter((name) => !referenced.has(name)).sort();
  return { changed, snippets: snippets.size, markdownFiles: markdownFiles.length, unreferenced };
};

const run = (args) => {
  if (args.some((arg) => arg !== "--check")) {
    console.error("Usage: node scripts/sync-snippets.mjs [--check]");
    return 2;
  }

  const checkOnly = args.includes("--check");
  let result;
  try {
    result = synchronize({ checkOnly });
  } catch (error) {
    console.error(`error: ${error.message}`);
    return 2;
  }

  if (result.unreferenced.length > 0) {
    console.warn(`unreferenced documentation snippets: ${result.unreferenced.join(", ")}`);
  }

  if (result.changed.length === 0) {
    console.log(
      `${result.snippets} compiled snippets are current across ` +
      `${result.markdownFiles} Markdown files`
    );
    return 0;
  }

  if (checkOnly) {
    console.error("documentation snippets are stale:");
    for (const path of result.changed) {
      console.error(`  ${path}`);
    }
    console.error("run 'npm run samples:sync' from docs and commit the result");
    return 1;
  }

  for (const path of result.changed) {
    console.log(`updated ${path}`);
  }
  return 0;
};

const invokedPath = process.argv[1] ? resolve(process.argv[1]) : "";
if (invokedPath === fileURLToPath(import.meta.url)) {
  process.exitCode = run(process.argv.slice(2));
}
