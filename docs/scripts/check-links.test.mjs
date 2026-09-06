import assert from "node:assert/strict";
import { mkdtempSync, mkdirSync, rmSync, writeFileSync } from "node:fs";
import { tmpdir } from "node:os";
import { join } from "node:path";
import { afterEach, test } from "node:test";

import { checkSite } from "./check-links.mjs";

const temporaryDirectories = [];

const fixture = () => {
  const root = mkdtempSync(join(tmpdir(), "jint-docs-links-"));
  temporaryDirectories.push(root);
  mkdirSync(join(root, "guide"), { recursive: true });
  mkdirSync(join(root, "assets"), { recursive: true });
  writeFileSync(join(root, "assets", "logo.svg"), "<svg />");
  return root;
};

afterEach(() => {
  for (const directory of temporaryDirectories.splice(0)) {
    rmSync(directory, { recursive: true, force: true });
  }
});

test("accepts generated pages, assets, fragments, and unpublished external links", () => {
  const root = fixture();
  writeFileSync(join(root, "index.html"), `
    <main id="home">
      <a href="/jint/guide/start#install">Start</a>
      <a href="#home">Home</a>
      <a href="https://www.nuget.org/packages/Future.Package">Future package</a>
      <img src="/jint/assets/logo.svg" alt="">
    </main>`);
  writeFileSync(join(root, "guide", "start.html"), `
    <h1 id="install">Install</h1>
    <a href="/jint/">Home</a>`);

  const result = checkSite(root, "/jint/");

  assert.deepEqual(result.problems, []);
  assert.equal(result.files, 2);
  assert.equal(result.checkedLinks, 4);
  assert.equal(result.checkedFragments, 2);
});

test("rejects missing generated targets, dead fragments, and links outside the base", () => {
  const root = fixture();
  writeFileSync(join(root, "index.html"), `
    <a href="/jint/missing">Missing</a>
    <a href="/jint/guide/start#missing">Dead fragment</a>
    <a href="/favicon.svg">Wrong base</a>`);
  writeFileSync(join(root, "guide", "start.html"), "<h1 id=\"install\">Install</h1>");

  const result = checkSite(root, "/jint/");

  assert.deepEqual(
    result.problems.map(({ reason }) => reason),
    [
      "no generated file for /jint/missing",
      "guide/start.html has no #missing",
      "resolves outside the configured /jint/ base"
    ]
  );
});
