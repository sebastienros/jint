import { defineConfig } from "vitepress";

const base = process.env.DOCS_BASE ?? "/";

export default defineConfig({
  title: "Jint",
  description: "JavaScript interpreter and runtime for .NET",
  base,
  cleanUrls: true,
  lastUpdated: true,
  srcExclude: [
    "README.md",
    "agent-instruction-files.md",
    "xml-doc-style.md",
    "releases/**"
  ],
  head: [
    ["link", { rel: "icon", href: `${base}favicon.svg`, type: "image/svg+xml" }],
    ["meta", { name: "theme-color", content: "#6f42c1" }]
  ],
  themeConfig: {
    logo: "/favicon.svg",
    siteTitle: "Jint",
    nav: [
      { text: "Guide", link: "/guide/getting-started" },
      {
        text: "Packages",
        items: [
          { text: "Jint", link: "/packages/jint/" },
          { text: "Jint.DevTools", link: "/packages/jint-devtools/" },
          { text: "Jint.Browser", link: "/packages/jint-browser/" },
          { text: "Jint.Browser.Playwright", link: "/packages/jint-browser-playwright/" },
          { text: "Jint.Browser.Tool", link: "/packages/jint-browser-tool/" },
          { text: "Jint.Browser.Mcp", link: "/packages/jint-browser-mcp/" }
        ]
      },
      { text: "Reference", link: "/reference/" },
      { text: "Architecture", link: "/architecture/" },
      { text: "Sponsors", link: "/sponsors" },
      { text: "GitHub", link: "https://github.com/sebastienros/jint" }
    ],
    sidebar: {
      "/guide/": [
        {
          text: "Guide",
          items: [
            { text: "Overview", link: "/guide/" },
            { text: "Getting Started", link: "/guide/getting-started" },
            { text: "Choosing a Package", link: "/guide/choosing-a-package" },
            { text: "Supported Platforms", link: "/guide/supported-platforms" },
            { text: "Migrating to Jint 5", link: "/guide/migrating-to-v5" }
          ]
        }
      ],
      "/packages/jint/": [
        {
          text: "Jint",
          items: [
            { text: "Overview", link: "/packages/jint/" },
            { text: "Creating an Engine", link: "/packages/jint/creating-an-engine" },
            { text: "Execute, Evaluate, and Invoke", link: "/packages/jint/execution" },
            { text: "JavaScript and .NET Values", link: "/packages/jint/values" },
            { text: "Expose Host Values", link: "/packages/jint/host-values" },
            { text: "Advanced Hosting", link: "/packages/jint/advanced-hosting" },
            { text: "CLR Interop", link: "/packages/jint/clr-interop" },
            { text: "Modules", link: "/packages/jint/modules" },
            { text: "Async and Promises", link: "/packages/jint/async" }
          ]
        },
        {
          text: "Runtime Features",
          items: [
            {
              text: "Web APIs",
              link: "/packages/jint/web-apis",
              collapsed: true,
              items: [
                { text: "Enabling Web APIs", link: "/packages/jint/web-apis/enabling" },
                { text: "Console and Timers", link: "/packages/jint/web-apis/console-and-timers" },
                { text: "Events and Messaging", link: "/packages/jint/web-apis/events-and-messaging" },
                { text: "Encoding, Files, and Streams", link: "/packages/jint/web-apis/encoding-files-and-streams" },
                { text: "Fetch and Networking", link: "/packages/jint/web-apis/fetch-and-networking" },
                { text: "Storage and Cache", link: "/packages/jint/web-apis/storage-and-cache" },
                { text: "Crypto and Performance", link: "/packages/jint/web-apis/crypto-and-performance" },
                { text: "Workers", link: "/packages/jint/web-apis/workers" }
              ]
            },
            { text: "Node Compatibility", link: "/packages/jint/node-compatibility" },
            { text: "Internationalization", link: "/packages/jint/internationalization" }
          ]
        },
        {
          text: "Hosting Safely",
          items: [
            { text: "Execution Constraints", link: "/packages/jint/constraints" },
            { text: "Untrusted Code", link: "/packages/jint/untrusted-code" },
            { text: "Errors and Diagnostics", link: "/packages/jint/errors" },
            { text: "Thread Safety", link: "/packages/jint/thread-safety" }
          ]
        },
        {
          text: "Observability and Performance",
          items: [
            { text: "Performance", link: "/packages/jint/performance" },
            { text: "Profiling", link: "/packages/jint/profiling" },
            { text: "Code Coverage", link: "/packages/jint/code-coverage" }
          ]
        }
      ],
      "/packages/jint-devtools/": [
        {
          text: "Jint.DevTools",
          items: [
            { text: "Overview", link: "/packages/jint-devtools/" },
            { text: "Getting Started", link: "/packages/jint-devtools/getting-started" },
            { text: "Hosting a Server", link: "/packages/jint-devtools/hosting" },
            { text: "Connecting Clients", link: "/packages/jint-devtools/connecting" },
            { text: "Debugging", link: "/packages/jint-devtools/debugging" },
            { text: "Profiling", link: "/packages/jint-devtools/profiling" },
            { text: "Supported Domains", link: "/packages/jint-devtools/domains" },
            { text: "Native AOT", link: "/packages/jint-devtools/native-aot" },
            { text: "Security", link: "/packages/jint-devtools/security" }
          ]
        }
      ],
      "/packages/jint-browser/": [
        {
          text: "Jint.Browser",
          items: [
            { text: "Overview", link: "/packages/jint-browser/" },
            { text: "Getting Started", link: "/packages/jint-browser/getting-started" },
            { text: "Browser and Page Lifecycle", link: "/packages/jint-browser/lifecycle" },
            { text: "Navigation", link: "/packages/jint-browser/navigation" },
            { text: "DOM and Evaluation", link: "/packages/jint-browser/dom-and-evaluation" },
            { text: "Input and Forms", link: "/packages/jint-browser/input-and-forms" },
            { text: "Network and Storage", link: "/packages/jint-browser/network-and-storage" },
            { text: "Content Extraction", link: "/packages/jint-browser/extraction" },
            { text: "Events and Workers", link: "/packages/jint-browser/events-and-workers" },
            { text: "DevTools Integration", link: "/packages/jint-browser/devtools" },
            { text: "Resource Budgets", link: "/packages/jint-browser/budgets" },
            { text: "Untrusted Content", link: "/packages/jint-browser/untrusted-content" },
            { text: "Supported Features", link: "/packages/jint-browser/supported-features" },
            { text: "Limitations", link: "/packages/jint-browser/limitations" }
          ]
        }
      ],
      "/packages/jint-browser-playwright/": [
        {
          text: "Jint.Browser.Playwright",
          items: [
            { text: "Overview", link: "/packages/jint-browser-playwright/" },
            { text: "Getting Started", link: "/packages/jint-browser-playwright/getting-started" },
            { text: "Browser API", link: "/packages/jint-browser-playwright/browser-api" },
            { text: "Locators and Actions", link: "/packages/jint-browser-playwright/locators-and-actions" },
            { text: "Waiting and Navigation", link: "/packages/jint-browser-playwright/waiting-and-navigation" },
            { text: "Supported API", link: "/packages/jint-browser-playwright/supported-api" },
            { text: "Limitations", link: "/packages/jint-browser-playwright/limitations" }
          ]
        }
      ],
      "/packages/jint-browser-tool/": [
        {
          text: "Jint.Browser.Tool",
          items: [
            { text: "Overview", link: "/packages/jint-browser-tool/" },
            { text: "Installation", link: "/packages/jint-browser-tool/installation" },
            { text: "Fetch a Page", link: "/packages/jint-browser-tool/fetch" },
            { text: "Evaluate JavaScript", link: "/packages/jint-browser-tool/evaluate" },
            { text: "Requests and State", link: "/packages/jint-browser-tool/requests-and-state" },
            { text: "Serve CDP", link: "/packages/jint-browser-tool/serve-cdp" },
            { text: "Serve MCP", link: "/packages/jint-browser-tool/serve-mcp" },
            { text: "Untrusted Content", link: "/packages/jint-browser-tool/untrusted-content" },
            { text: "Exit Codes", link: "/packages/jint-browser-tool/exit-codes" },
            { text: "Limitations", link: "/packages/jint-browser-tool/limitations" }
          ]
        }
      ],
      "/packages/jint-browser-mcp/": [
        {
          text: "Jint.Browser.Mcp",
          items: [
            { text: "Overview", link: "/packages/jint-browser-mcp/" },
            { text: "Command Line", link: "/packages/jint-browser-mcp/command-line" },
            { text: "Embedding", link: "/packages/jint-browser-mcp/embedding" },
            { text: "Tools and Resources", link: "/packages/jint-browser-mcp/tools-and-resources" },
            { text: "Sessions and Transports", link: "/packages/jint-browser-mcp/sessions-and-transports" },
            { text: "Security", link: "/packages/jint-browser-mcp/security" },
            { text: "Limitations", link: "/packages/jint-browser-mcp/limitations" }
          ]
        }
      ],
      "/reference/": [
        {
          text: "Reference",
          items: [
            { text: "Overview", link: "/reference/" },
            { text: "Package Matrix", link: "/reference/package-matrix" },
            { text: "ECMAScript Support", link: "/reference/ecmascript" },
            { text: "Web API Features", link: "/reference/web-api-features" },
            { text: "Browser Compatibility", link: "/reference/browser-compatibility" },
            { text: "DevTools Domains", link: "/reference/devtools-domains" },
            { text: "Native AOT and Trimming", link: "/reference/native-aot" },
            { text: "Security", link: "/reference/security" },
            { text: "Branches and Releases", link: "/reference/releases" }
          ]
        }
      ],
      "/architecture/": [
        {
          text: "Architecture",
          items: [
            { text: "Overview", link: "/architecture/" },
            { text: "Execution Model", link: "/architecture/execution-model" },
            { text: "Async Context", link: "/design/async-context" },
            { text: "Prepared Serialization", link: "/design/prepared-serialization" },
            { text: "DevTools Protocol", link: "/design/devtools-protocol" },
            { text: "Headless Browser", link: "/design/headless-browser" },
            { text: "Web Workers", link: "/design/web-workers" }
          ]
        }
      ],
      "/design/": [
        {
          text: "Architecture",
          items: [
            { text: "Overview", link: "/architecture/" },
            { text: "Execution Model", link: "/architecture/execution-model" },
            { text: "Async Context", link: "/design/async-context" },
            { text: "Prepared Serialization", link: "/design/prepared-serialization" },
            { text: "DevTools Protocol", link: "/design/devtools-protocol" },
            { text: "Headless Browser", link: "/design/headless-browser" },
            { text: "Web Workers", link: "/design/web-workers" }
          ]
        }
      ]
    },
    search: {
      provider: "local"
    },
    outline: [2, 3    ],
    socialLinks: [
      { icon: "github", link: "https://github.com/sebastienros/jint" }
    ],
    editLink: {
      pattern: "https://github.com/sebastienros/jint/edit/main/docs/:path"
    },
    footer: {
      message: "Released under the <a href=\"https://github.com/sebastienros/jint/blob/main/LICENSE.txt\">BSD 2-Clause License</a>.",
      copyright: "Copyright © Jint contributors"
    }
  }
});
