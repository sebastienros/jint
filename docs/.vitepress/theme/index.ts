import { h } from "vue";
import { useRoute } from "vitepress";
import DefaultTheme from "vitepress/theme";
import Jint5PreviewBanner from "./Jint5PreviewBanner.vue";
import "./custom.css";

const previewSectionPrefixes = [
  "packages/jint/web-apis",
  "packages/jint-devtools",
  "packages/jint-browser",
  "packages/jint-browser-playwright",
  "packages/jint-browser-tool",
  "packages/jint-browser-mcp"
];

const previewReferencePages = new Set([
  "guide/migrating-to-v5.md",
  "reference/web-api-features.md",
  "reference/browser-compatibility.md",
  "reference/devtools-domains.md",
  "v5-migration.md"
]);

function isJint5PreviewPage(relativePath: string): boolean {
  return previewReferencePages.has(relativePath)
    || previewSectionPrefixes.some(prefix =>
      relativePath === `${prefix}.md` || relativePath.startsWith(`${prefix}/`));
}

export default {
  extends: DefaultTheme,
  Layout: () => {
    const route = useRoute();

    return h(DefaultTheme.Layout, null, {
      "doc-before": () => isJint5PreviewPage(route.data.relativePath)
        ? h(Jint5PreviewBanner)
        : null
    });
  }
};
