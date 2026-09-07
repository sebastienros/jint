import process from "node:process";
import {
  existsSync,
  readFileSync,
  readdirSync
} from "node:fs";
import { extname, join, relative, resolve, sep } from "node:path";
import { fileURLToPath } from "node:url";

const externalSchemes = new Set([
  "data:",
  "javascript:",
  "mailto:",
  "tel:"
]);

const normalizeBase = (value) => {
  const withLeadingSlash = value.startsWith("/") ? value : `/${value}`;
  return `${withLeadingSlash.replace(/\/+$/, "")}/`;
};

const decodeHtml = (value) => value
  .replaceAll("&amp;", "&")
  .replaceAll("&quot;", "\"")
  .replaceAll("&#39;", "'")
  .replace(/&#x([0-9a-f]+);/gi, (_, code) => String.fromCodePoint(Number.parseInt(code, 16)))
  .replace(/&#([0-9]+);/g, (_, code) => String.fromCodePoint(Number.parseInt(code, 10)));

const htmlFilesUnder = (directory) => {
  const files = [];

  const visit = (current) => {
    for (const entry of readdirSync(current, { withFileTypes: true })) {
      const path = join(current, entry.name);
      if (entry.isDirectory()) {
        visit(path);
      } else if (entry.isFile() && entry.name.endsWith(".html")) {
        files.push(path);
      }
    }
  };

  visit(directory);
  return files.sort();
};

const tagsIn = (html) => {
  const withoutScriptBodies = html
    .replace(/(<script\b[^>]*>)[\s\S]*?<\/script>/gi, "$1</script>")
    .replace(/(<style\b[^>]*>)[\s\S]*?<\/style>/gi, "$1</style>");

  return withoutScriptBodies.match(/<[^>]+>/g) ?? [];
};

const linkedValuesIn = (html) => {
  const values = [];
  for (const tag of tagsIn(html)) {
    for (const match of tag.matchAll(/\b(?:href|src)=["']([^"']+)["']/gi)) {
      values.push(decodeHtml(match[1]));
    }
  }
  return values;
};

const anchorsIn = (html) => {
  const anchors = new Set();
  for (const tag of tagsIn(html)) {
    for (const match of tag.matchAll(/\b(?:id|name)=["']([^"']+)["']/gi)) {
      anchors.add(decodeHtml(match[1]));
    }
  }
  return anchors;
};

const safeDecodePath = (pathname) => {
  try {
    return decodeURIComponent(pathname);
  } catch {
    return pathname;
  }
};

const candidatesFor = (root, base, pathname) => {
  const decoded = safeDecodePath(pathname);
  const baseWithoutTrailingSlash = base.slice(0, -1);

  if (decoded !== baseWithoutTrailingSlash && !decoded.startsWith(base)) {
    return [];
  }

  const sitePath = decoded === baseWithoutTrailingSlash
    ? ""
    : decoded.slice(base.length);

  if (sitePath.split("/").includes("..")) {
    return [];
  }

  if (sitePath === "" || sitePath.endsWith("/")) {
    return [join(root, sitePath, "index.html")];
  }

  if (extname(sitePath)) {
    return [join(root, sitePath)];
  }

  return [
    join(root, `${sitePath}.html`),
    join(root, sitePath, "index.html"),
    join(root, sitePath)
  ];
};

export const checkSite = (distDirectory, configuredBase = "/") => {
  const root = resolve(distDirectory);
  const base = normalizeBase(configuredBase);

  if (!existsSync(root)) {
    throw new Error(`no such directory: ${root}`);
  }

  const pages = htmlFilesUnder(root);
  const anchorCache = new Map();
  const problems = [];
  let checkedLinks = 0;
  let checkedFragments = 0;

  for (const page of pages) {
    const source = relative(root, page).split(sep).join("/");
    const pageUrl = new URL(`${base}${source}`, "https://docs.invalid");
    const html = readFileSync(page, "utf8");

    for (const rawLink of linkedValuesIn(html)) {
      if (rawLink === "" || rawLink.startsWith("//")) {
        continue;
      }

      let targetUrl;
      try {
        targetUrl = new URL(rawLink, pageUrl);
      } catch {
        problems.push({ source, link: rawLink, reason: "invalid URL" });
        continue;
      }

      if (targetUrl.origin !== pageUrl.origin || externalSchemes.has(targetUrl.protocol)) {
        continue;
      }

      checkedLinks++;
      const candidates = candidatesFor(root, base, targetUrl.pathname);
      if (candidates.length === 0) {
        problems.push({
          source,
          link: rawLink,
          reason: `resolves outside the configured ${base} base`
        });
        continue;
      }

      const target = candidates.find(existsSync);
      if (!target) {
        problems.push({
          source,
          link: rawLink,
          reason: `no generated file for ${targetUrl.pathname}`
        });
        continue;
      }

      if (!targetUrl.hash || !target.endsWith(".html")) {
        continue;
      }

      checkedFragments++;
      const fragment = safeDecodePath(targetUrl.hash.slice(1));
      let anchors = anchorCache.get(target);
      if (!anchors) {
        anchors = anchorsIn(readFileSync(target, "utf8"));
        anchorCache.set(target, anchors);
      }

      if (!anchors.has(fragment)) {
        problems.push({
          source,
          link: rawLink,
          reason: `${relative(root, target)} has no #${fragment}`
        });
      }
    }
  }

  return {
    base,
    files: pages.length,
    checkedLinks,
    checkedFragments,
    problems
  };
};

const run = (args) => {
  if (args.length > 2 || args.includes("--help") || args.includes("-h")) {
    console.log("Usage: node scripts/check-links.mjs [dist-directory] [base]");
    return args.length > 2 ? 2 : 0;
  }

  const directory = args[0] ?? ".vitepress/dist";
  const base = args[1] ?? process.env.DOCS_BASE ?? "/";

  let result;
  try {
    result = checkSite(directory, base);
  } catch (error) {
    console.error(`error: ${error.message}`);
    return 2;
  }

  console.log(
    `${directory}: ${result.files} pages, ${result.checkedLinks} internal links, ` +
    `${result.checkedFragments} fragments`
  );

  if (result.problems.length === 0) {
    console.log("no dead internal links, assets, or anchors");
    return 0;
  }

  for (const problem of result.problems) {
    console.error(`${problem.source}: ${problem.link}: ${problem.reason}`);
  }
  return 1;
};

const invokedPath = process.argv[1] ? resolve(process.argv[1]) : "";
if (invokedPath === fileURLToPath(import.meta.url)) {
  process.exitCode = run(process.argv.slice(2));
}
