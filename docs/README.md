# Jint documentation

The documentation site is built with VitePress.

## Run locally

From the repository root:

```bash
cd docs
npm ci
npm run dev
```

VitePress prints the local URL, normally `http://localhost:5173/`.

## Build the production site

```bash
cd docs
npm ci
npm run build
npm run check-links
npm run preview
```

GitHub Actions runs the same production build for pull requests. A push to
`main` that changes the site or its workflow publishes the result to GitHub
Pages.

The link checker reads the generated site under its configured base path. It
validates internal pages, assets, and heading anchors without requesting
external URLs, so links to packages or services that are not published yet do
not block the build.

## Validate documentation changes

From `docs/`:

```bash
npm run test:tools
npm run samples:check
dotnet build snippets/Jint.Documentation.Samples/Jint.Documentation.Samples.csproj -c Release
DOCS_BASE=/jint/ npm run build
DOCS_BASE=/jint/ npm run check-links
npm run lint
```

## Compiled C# samples

High-traffic examples are maintained as `#region docs:name` blocks in
`snippets/Jint.Documentation.Samples`. The sample project compiles in Release,
and the Markdown references each region with stable markers:

```markdown
<!-- snippet: name -->
<!-- endSnippet -->
```

Run `npm run samples:sync` after changing a sample. Commit the resulting
Markdown so GitHub, NuGet.org, and the documentation site all show the reviewed
code. CI runs `npm run samples:check` and fails if a block is stale or missing.
